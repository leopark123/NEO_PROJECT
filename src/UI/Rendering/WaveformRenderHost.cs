// WaveformRenderHost.cs
// Sprint 3.1/3.2: Bridge between D3DImageRenderer and Neo.Rendering engine
//
// Source: NEO_UI_Development_Plan_WPF.md §8, ARCHITECTURE.md §5
// CHARTER: R-01 render callback O(1), R-03 no per-frame resource creation
//
// Pipeline:
//   D3DImageRenderer (D3D11 → D2D1 → WriteableBitmap)
//   └→ LayeredRenderer (Grid/Content/Overlay layers)
//      └→ ResourceCache (cached brushes/fonts)
//
// This class coordinates the rendering lifecycle and exposes WPF bindings.

using System.Numerics;
using System.Windows;
using System.Windows.Media;
using Neo.Core.Interfaces;
using Neo.Core.Models;
using Neo.Rendering.AEEG;
using Neo.Rendering.Core;
using Neo.Rendering.EEG;
using Neo.Rendering.Layers;
using Neo.Rendering.Mapping;
using Neo.Rendering.Resources;
using Neo.UI.Services;
using Vortice.Direct2D1;
using Vortice.Mathematics;
using Neo.Playback;

namespace Neo.UI.Rendering;

/// <summary>
/// Hosts the D3D11 → D2D1 → WPF rendering pipeline.
/// Coordinates D3DImageRenderer, LayeredRenderer, and ResourceCache.
/// </summary>
/// <remarks>
/// Sprint 3.1: Created for WaveformPanel integration.
///
/// Lifecycle:
/// 1. Construction: Creates D3DImageRenderer, LayeredRenderer, ResourceCache
/// 2. Start(): Hooks CompositionTarget.Rendering for 60fps loop
/// 3. Resize(): Updates D3DImageRenderer and reinitializes ResourceCache
/// 4. Stop(): Unhooks render callback
/// 5. Dispose(): Releases all resources
/// </remarks>
public sealed class WaveformRenderHost : IDisposable
{
    private readonly D3DImageRenderer _renderer;
    private readonly LayeredRenderer _layeredRenderer;
    private readonly ResourceCache _resourceCache;
    private readonly EegDataBridge _dataBridge;
    private readonly SweepModeRenderer _sweepRenderer;
    private readonly QualityIndicatorRenderer _qualityIndicatorRenderer;
    private readonly AeegSeriesBuilder _aeegBuilder;
    private readonly UiAeegTrendRenderer _aeegRenderer;
    private readonly UiAeegGridAndAxisRenderer _aeegGridRenderer;
    private readonly EegPreviewRenderer _eegPreviewRenderer;
    private readonly TimeAxisRenderer _timeAxisRenderer;
    private readonly AeegSemiLogVisualizer _semiLogVisualizer;
    private readonly PlaybackClock _playbackClock;
    private readonly IAuditService? _audit;
    private readonly IThemeService? _themeService;

    private RenderContext _renderContext;
    private long _frameNumber;
    private bool _isRunning;
    private bool _disposed;
    private WaveformLayout _layout;
    private float _seekPosition;
    private long _aeegVisibleDurationUs = 3L * 60 * 60 * 1_000_000; // 3 hours default
    private int _lastAeegVersion = -1;
    private AeegTrendRenderData _aeegCh1RenderData;
    private AeegTrendRenderData _aeegCh2RenderData;
    private bool _aeegCh1Ready;
    private bool _aeegCh2Ready;
    private long _playbackStartUs = 0;
    private long _playbackEndUs = 24L * 60 * 60 * 1_000_000; // 24 hours mock range

    // Per-lane gain settings (μV/cm) — default 100 per WaveformViewModel
    private int _lane0GainMicrovoltsPerCm = 100;  // EEG-1 (top lane)
    private int _lane1GainMicrovoltsPerCm = 100;  // EEG-2 (bottom lane)

    // Per-lane Y-axis range (±μV) — default 100 per spec
    private int _lane0YAxisRangeUv = 100;  // EEG-1 (top lane)
    private int _lane1YAxisRangeUv = 100;  // EEG-2 (bottom lane)

    // Legacy global properties (deprecated, kept for backward compatibility)
    [Obsolete("Use Lane0GainMicrovoltsPerCm and Lane1GainMicrovoltsPerCm instead")]
    private int _gainMicrovoltsPerCm = 100;

    [Obsolete("Use Lane0YAxisRangeUv and Lane1YAxisRangeUv instead")]
    private int _yAxisRangeUv = 100;

    // Time tracking (microseconds)
    private long _currentTimestampUs;
    private long _visibleDurationUs = 15_000_000; // 15 seconds default (EEG view)
    private readonly System.Diagnostics.Stopwatch _stopwatch = new();

    // GS histogram display control
    private bool _showGsHistogram = false; // Default: hidden

    /// <summary>WPF ImageSource for binding to Image.Source.</summary>
    public ImageSource? ImageSource => _renderer.ImageSource;

    /// <summary>Current viewport width.</summary>
    public int Width => _renderer.Width;

    /// <summary>Current viewport height.</summary>
    public int Height => _renderer.Height;

    /// <summary>True if rendering is active.</summary>
    public bool IsRunning => _isRunning;

    /// <summary>Current frame number (for diagnostics).</summary>
    public long FrameNumber => _frameNumber;

    /// <summary>Layered renderer for adding custom layers.</summary>
    public LayeredRenderer LayeredRenderer => _layeredRenderer;

    /// <summary>Resource cache for brushes and fonts.</summary>
    public ResourceCache ResourceCache => _resourceCache;

    /// <summary>Visible time duration in microseconds.</summary>
    public long VisibleDurationUs
    {
        get => _visibleDurationUs;
        set => _visibleDurationUs = Math.Max(1_000_000, value); // Minimum 1 second
    }

    /// <summary>Current seek position (0-1).</summary>
    public float SeekPosition
    {
        get => _seekPosition;
        set => _seekPosition = Math.Clamp(value, 0f, 1f);
    }

    /// <summary>EEG data bridge for connecting data sources.</summary>
    public EegDataBridge DataBridge => _dataBridge;

    /// <summary>Lane 0 (EEG-1, top) gain setting in μV/cm (10-1000).</summary>
    public int Lane0GainMicrovoltsPerCm
    {
        get => _lane0GainMicrovoltsPerCm;
        set => _lane0GainMicrovoltsPerCm = Math.Clamp(value, 10, 1000);
    }

    /// <summary>Lane 1 (EEG-2, bottom) gain setting in μV/cm (10-1000).</summary>
    public int Lane1GainMicrovoltsPerCm
    {
        get => _lane1GainMicrovoltsPerCm;
        set => _lane1GainMicrovoltsPerCm = Math.Clamp(value, 10, 1000);
    }

    /// <summary>Lane 0 (EEG-1, top) Y-axis display range in ±μV (25-200).</summary>
    public int Lane0YAxisRangeUv
    {
        get => _lane0YAxisRangeUv;
        set => _lane0YAxisRangeUv = Math.Clamp(value, 25, 200);
    }

    /// <summary>Lane 1 (EEG-2, bottom) Y-axis display range in ±μV (25-200).</summary>
    public int Lane1YAxisRangeUv
    {
        get => _lane1YAxisRangeUv;
        set => _lane1YAxisRangeUv = Math.Clamp(value, 25, 200);
    }

    /// <summary>
    /// [DEPRECATED] Global gain setting in μV/cm (10-1000).
    /// Use Lane0GainMicrovoltsPerCm and Lane1GainMicrovoltsPerCm instead.
    /// Setting this property updates both lanes for backward compatibility.
    /// </summary>
    [Obsolete("Use Lane0GainMicrovoltsPerCm and Lane1GainMicrovoltsPerCm instead")]
    public int GainMicrovoltsPerCm
    {
        get => _gainMicrovoltsPerCm;
        set
        {
            _gainMicrovoltsPerCm = Math.Clamp(value, 10, 1000);
            _lane0GainMicrovoltsPerCm = _gainMicrovoltsPerCm;
            _lane1GainMicrovoltsPerCm = _gainMicrovoltsPerCm;
        }
    }

    /// <summary>
    /// [DEPRECATED] Global Y-axis display range in ±μV (25-200).
    /// Use Lane0YAxisRangeUv and Lane1YAxisRangeUv instead.
    /// Setting this property updates both lanes for backward compatibility.
    /// </summary>
    [Obsolete("Use Lane0YAxisRangeUv and Lane1YAxisRangeUv instead")]
    public int YAxisRangeUv
    {
        get => _yAxisRangeUv;
        set
        {
            _yAxisRangeUv = Math.Clamp(value, 25, 200);
            _lane0YAxisRangeUv = _yAxisRangeUv;
            _lane1YAxisRangeUv = _yAxisRangeUv;
        }
    }

    /// <summary>aEEG visible duration in hours (1-24).</summary>
    public int AeegVisibleHours
    {
        get => (int)(_aeegVisibleDurationUs / (3600L * 1_000_000));
        set
        {
            _aeegVisibleDurationUs = Math.Clamp(value, 1, 24) * 3600L * 1_000_000;
            _lastAeegVersion = -1; // Force aEEG re-render
        }
    }

    /// <summary>Controls whether GS histogram is displayed in aEEG regions.</summary>
    public bool ShowGsHistogram
    {
        get => _showGsHistogram;
        set => _showGsHistogram = value;
    }

    /// <summary>PlaybackClock for external pause/resume control.</summary>
    public PlaybackClock PlaybackClock => _playbackClock;

    /// <summary>
    /// Creates a new render host with default 3-layer configuration.
    /// </summary>
    /// <param name="audit">Optional audit service for logging seek events.</param>
    /// <param name="themeService">Optional theme service for color scheme switching.</param>
    public WaveformRenderHost(IAuditService? audit = null, IThemeService? themeService = null)
    {
        _audit = audit;
        _themeService = themeService;
        _renderer = new D3DImageRenderer();
        _layeredRenderer = LayeredRenderer.CreateDefault();
        // Disable GridLayer - per-region renderers handle their own backgrounds
        var gridLayer = _layeredRenderer.GetLayer("Grid");
        if (gridLayer != null) gridLayer.IsEnabled = false;
        _resourceCache = new ResourceCache();
        _dataBridge = new EegDataBridge();
        _sweepRenderer = new SweepModeRenderer();
        _qualityIndicatorRenderer = new QualityIndicatorRenderer();
        _aeegBuilder = new AeegSeriesBuilder();
        _aeegRenderer = new UiAeegTrendRenderer();
        _aeegGridRenderer = new UiAeegGridAndAxisRenderer();
        _eegPreviewRenderer = new EegPreviewRenderer();
        _timeAxisRenderer = new TimeAxisRenderer();
        _semiLogVisualizer = new AeegSemiLogVisualizer();
        _playbackClock = new PlaybackClock();
        _playbackClock.SeekTo(_playbackStartUs);

        // Subscribe to theme changes
        if (_themeService != null)
        {
            UiAeegPalette.SetTheme(_themeService.CurrentTheme == ThemeType.Apple);
            UiEegPalette.SetTheme(_themeService.CurrentTheme == ThemeType.Apple);
            _themeService.ThemeChanged += OnThemeChanged;
        }
        else
        {
            UiAeegPalette.SetTheme(isApple: false);
            UiEegPalette.SetTheme(isApple: false);
        }
        // Clock starts paused; WaveformPanel bridge will call Start() when IsPlaying becomes true
        _renderContext = CreateRenderContext();
        _layout = WaveformLayout.Create(1, 1);

        _stopwatch.Start();
    }

    /// <summary>
    /// Attaches an EEG data source and starts receiving samples.
    /// </summary>
    /// <param name="source">The EEG data source (e.g., MockEegSource).</param>
    public void AttachDataSource(ITimeSeriesSource<EegSample> source)
    {
        _dataBridge.AttachSource(source);
    }

    /// <summary>
    /// Detaches the current EEG data source.
    /// </summary>
    public void DetachDataSource()
    {
        _dataBridge.DetachSource();
    }

    /// <summary>
    /// Resizes the render target and reinitializes resources.
    /// Safe to call multiple times; only resizes if dimensions change.
    /// </summary>
    public void Resize(int width, int height, double dpiX = 96.0, double dpiY = 96.0)
    {
        if (_disposed) return;
        if (width <= 0 || height <= 0) return;
        if (_renderer.Width == width && _renderer.Height == height &&
            Math.Abs(_renderer.DpiX - (float)dpiX) < 0.01f &&
            Math.Abs(_renderer.DpiY - (float)dpiY) < 0.01f) return;

        // Resize D3D resources
        _renderer.Resize(width, height, dpiX, dpiY);

        // Reinitialize ResourceCache with new DeviceContext
        if (_renderer.DeviceContext != null)
        {
            _resourceCache.Clear();
            _resourceCache.Initialize(_renderer.DeviceContext);
        }

        // Invalidate all layers
        _layeredRenderer.InvalidateAll();

        // Update layout
        _layout = WaveformLayout.Create(width, height);
    }

    /// <summary>
    /// Starts the 60fps render loop via CompositionTarget.Rendering.
    /// </summary>
    public void Start()
    {
        if (_disposed || _isRunning) return;

        _isRunning = true;
        CompositionTarget.Rendering += OnRendering;
    }

    /// <summary>
    /// Stops the render loop.
    /// </summary>
    public void Stop()
    {
        if (!_isRunning) return;

        _isRunning = false;
        CompositionTarget.Rendering -= OnRendering;
    }

    /// <summary>
    /// CompositionTarget.Rendering callback (60fps on most displays).
    /// </summary>
    private void OnRendering(object? sender, EventArgs e)
    {
        if (_disposed || !_isRunning) return;
        if (!_renderer.IsRenderReady || !_resourceCache.IsInitialized) return;

        // Update time
        _currentTimestampUs = _playbackClock.GetCurrentUs();
        if (_currentTimestampUs < _playbackStartUs) _currentTimestampUs = _playbackStartUs;
        if (_currentTimestampUs > _playbackEndUs) _currentTimestampUs = _playbackEndUs;
        _seekPosition = (float)((_currentTimestampUs - _playbackStartUs) / Math.Max(1.0, (_playbackEndUs - _playbackStartUs)));

        // Begin render frame
        if (!_renderer.BeginRender()) return;

        try
        {
            // Phase 3: 5-region layout rendering
            if (_renderer.DeviceContext != null)
            {
                // Update render context for LayeredRenderer
                _renderContext = CreateRenderContext();

                // Render grid/overlay layers via LayeredRenderer (Sprint 3.2 requirement)
                _layeredRenderer.RenderFrame(_renderer.DeviceContext, _resourceCache, _renderContext);

                // Get EEG sweep data for both channels
                var sweepData = _dataBridge.GetSweepData();

                // Update aEEG render data for both channels
                UpdateAeegRenderDataIfNeeded();

                // aEEG Ch1 (25%)
                RenderAeeg(_renderer.DeviceContext, _layout.Aeeg1,
                    _aeegCh1Ready ? _aeegCh1RenderData : null);

                // EEG Preview Ch1 (5%) - narrow strip with waveform (Lane 0 / EEG-1)
                if (sweepData.Length >= 1)
                {
                    _sweepRenderer.RenderChannel(_renderer.DeviceContext, _resourceCache,
                        sweepData[0], _layout.EegPreview1, _lane0YAxisRangeUv, _lane0GainMicrovoltsPerCm);
                }

                // aEEG Ch2 (25%)
                RenderAeeg(_renderer.DeviceContext, _layout.Aeeg2,
                    _aeegCh2Ready ? _aeegCh2RenderData : null);

                // EEG Preview Ch2 (5%) - narrow strip with waveform (Lane 1 / EEG-2)
                if (sweepData.Length >= 2)
                {
                    _sweepRenderer.RenderChannel(_renderer.DeviceContext, _resourceCache,
                        sweepData[1], _layout.EegPreview2, _lane1YAxisRangeUv, _lane1GainMicrovoltsPerCm);
                }

                // NIRS UI placeholder (layout only, no data-logic changes)
                RenderNirsPlaceholder(_renderer.DeviceContext, _layout.Nirs);

                RenderSeparators(_renderer.DeviceContext);
            }
        }
        finally
        {
            // End render frame (copies to WriteableBitmap)
            _renderer.EndRender();
            _frameNumber++;
        }
    }

    /// <summary>
    /// Creates a new RenderContext with current state and channel data.
    /// </summary>
    private RenderContext CreateRenderContext()
    {
        long startUs = Math.Max(0, _currentTimestampUs - _visibleDurationUs);
        long endUs = _currentTimestampUs;
        var visibleRange = new TimeRange(startUs, endUs);

        // Get channel data from bridge (Sprint 3.2)
        var channels = _dataBridge.GetChannelData(visibleRange);

        return new RenderContext
        {
            CurrentTimestampUs = _currentTimestampUs,
            VisibleRange = visibleRange,
            Zoom = new ZoomLevel(_visibleDurationUs / 1_000_000.0, 0),
            FrameNumber = _frameNumber,
            ViewportWidth = _renderer.Width,
            ViewportHeight = _renderer.Height,
            Dpi = _renderer.DpiX,
            Channels = channels
        };
    }

    /// <summary>
    /// SeekBar interaction is disabled in current UI baseline.
    /// </summary>
    public bool TrySetSeekFromPoint(double x, double y)
    {
        _ = x;
        _ = y;
        _ = _audit;
        return false;
    }

    private void RenderPlaceholder(ID2D1DeviceContext context, string label, in Vortice.Mathematics.Rect area)
    {
        if (area.Width <= 0 || area.Height <= 0)
            return;

        var background = _resourceCache.DarkGrayBrush;
        var textColor = GetThemeTextColorOrDefault(_themeService?.CurrentTheme == ThemeType.Apple
            ? new Color4(0.07f, 0.07f, 0.07f, 0.96f)
            : new Color4(0.80f, 0.80f, 0.80f, 1.00f));
        var textBrush = _resourceCache.GetSolidBrush(textColor);
        var textFormat = _resourceCache.SmallTextFormat;

        context.FillRectangle(area, background);
        context.DrawText(label, textFormat, area, textBrush);
    }

    private void RenderNirsPlaceholder(ID2D1DeviceContext context, in Vortice.Mathematics.Rect area)
    {
        if (area.Width <= 0 || area.Height <= 0)
            return;

        bool isApple = _themeService?.CurrentTheme != ThemeType.Medical;
        float accentR = isApple ? 0.35f : 0.15f;
        float accentG = isApple ? 0.78f : 0.78f;
        float accentB = isApple ? 0.98f : 0.85f;
        float panelR = isApple ? 0.11f : 0.10f;
        float panelG = isApple ? 0.12f : 0.13f;
        float panelB = isApple ? 0.14f : 0.16f;
        float cardR = isApple ? 0.15f : 0.14f;
        float cardG = isApple ? 0.17f : 0.18f;
        float cardB = isApple ? 0.20f : 0.22f;

        var panelBackground = _resourceCache.GetSolidBrush(panelR, panelG, panelB, 1f);
        var cardBackground = _resourceCache.GetSolidBrush(cardR, cardG, cardB, 1f);
        var borderBrush = _resourceCache.GetSolidBrush(accentR, accentG, accentB, 0.52f);
        var gridBrush = _resourceCache.GetSolidBrush(accentR, accentG, accentB, 0.28f);
        var trendBrush = _resourceCache.GetSolidBrush(accentR, accentG, accentB, 0.96f);
        var textColor = isApple
            ? GetThemeTextColorOrDefault(new Color4(0.07f, 0.07f, 0.07f, 0.96f))
            : new Color4(accentR, accentG, accentB, 0.92f);
        var textBrush = _resourceCache.GetSolidBrush(textColor);
        var titleFormat = _resourceCache.GetTextFormat("Segoe UI", 10f);

        context.FillRectangle(area, panelBackground);

        var titleArea = new Vortice.Mathematics.Rect(
            area.Left + 10f,
            area.Top + 6f,
            area.Right - 10f,
            area.Top + 24f);
        context.DrawText("NIRS 趋势占位（4通道）", titleFormat, titleArea, textBrush);

        const int channelCount = 4;
        const float gap = 6f;
        float top = area.Top + 28f;
        float bottom = area.Bottom - 8f;
        float height = Math.Max(0f, bottom - top);
        float laneHeight = (height - (channelCount - 1) * gap) / channelCount;
        if (laneHeight <= 0f)
            return;

        for (int i = 0; i < channelCount; i++)
        {
            float laneTop = top + i * (laneHeight + gap);
            var lane = new Vortice.Mathematics.Rect(
                area.Left + 8f,
                laneTop,
                area.Right - 8f,
                laneTop + laneHeight);

            context.FillRectangle(lane, cardBackground);
            context.DrawRectangle(lane, borderBrush, 1f);

            float midY = (lane.Top + lane.Bottom) * 0.5f;
            float quarterY1 = lane.Top + lane.Height * 0.30f;
            float quarterY2 = lane.Top + lane.Height * 0.70f;
            context.DrawLine(new Vector2(lane.Left + 2f, quarterY1), new Vector2(lane.Right - 2f, quarterY1), gridBrush, 1f);
            context.DrawLine(new Vector2(lane.Left + 2f, midY), new Vector2(lane.Right - 2f, midY), gridBrush, 1f);
            context.DrawLine(new Vector2(lane.Left + 2f, quarterY2), new Vector2(lane.Right - 2f, quarterY2), gridBrush, 1f);

            float accentX = lane.Left + 2f;
            context.DrawLine(
                new Vector2(accentX, lane.Top + 1f),
                new Vector2(accentX, lane.Bottom - 1f),
                trendBrush,
                1.4f);

            var labelRect = new Vortice.Mathematics.Rect(lane.Left + 8f, lane.Top + 2f, lane.Left + 60f, lane.Bottom);
            context.DrawText($"CH{i + 1}", titleFormat, labelRect, textBrush);

            // Draw waveform placeholder (no percentage text at lower-left NIRS region).
            float xStart = lane.Left + 54f;
            float xEnd = lane.Right - 8f;
            int steps = 54;
            float amplitude = Math.Max(2f, lane.Height * 0.20f);
            Vector2 prev = new(xStart, midY);
            for (int step = 1; step <= steps; step++)
            {
                float t = step / (float)steps;
                float x = xStart + (xEnd - xStart) * t;
                float phase = (i * 0.7f) + t * 7.2f;
                float y = midY + MathF.Sin(phase * 2.3f) * amplitude * 0.55f + MathF.Sin(phase * 0.7f) * amplitude * 0.30f;
                Vector2 current = new(x, y);
                context.DrawLine(prev, current, trendBrush, 1.4f);
                prev = current;
            }
        }
    }

    private static Color4 GetThemeTextColorOrDefault(Color4 fallback)
    {
        try
        {
            if (Application.Current?.Resources["TextOnDarkBrush"] is SolidColorBrush brush)
            {
                var c = brush.Color;
                return new Color4(c.ScR, c.ScG, c.ScB, c.ScA);
            }
        }
        catch
        {
            // Keep fallback if resource lookup fails in startup/teardown timing windows.
        }

        return fallback;
    }

    private void UpdateAeegRenderDataIfNeeded()
    {
        int version = _dataBridge.AeegVersion;
        if (version == _lastAeegVersion)
            return;

        _lastAeegVersion = version;

        // Fixed 3-hour time window - new data appears from right
        long endUs = _currentTimestampUs;
        long startUs = endUs - _aeegVisibleDurationUs;  // Allow negative startUs for proper right-to-left accumulation

        // Process two separate aEEG regions (not split from one)
        var ch1TrendArea = GetAeegTrendArea(_layout.Aeeg1);
        var ch2TrendArea = GetAeegTrendArea(_layout.Aeeg2);

        _aeegCh1Ready = BuildAeegRenderData(0, ch1TrendArea, startUs, endUs, out _aeegCh1RenderData);
        _aeegCh2Ready = BuildAeegRenderData(1, ch2TrendArea, startUs, endUs, out _aeegCh2RenderData);

    }

    private bool BuildAeegRenderData(
        int channelIndex,
        in Vortice.Mathematics.Rect area,
        long startUs,
        long endUs,
        out AeegTrendRenderData renderData)
    {
        var snapshot = _dataBridge.GetAeegSeriesSnapshot(channelIndex);
        if (snapshot.Count == 0)
        {
            renderData = default;
            return false;
        }

        double areaHeight = Math.Max(1.0, area.Height);
        var mapper = new AeegSemiLogMapper(areaHeight);

        double areaLeft = area.Left;
        double areaWidth = area.Width;

        float TimestampToX(long ts)
        {
            if (endUs <= startUs) return (float)areaLeft;
            double norm = (double)(ts - startUs) / (endUs - startUs);
            norm = Math.Clamp(norm, 0.0, 1.0);
            return (float)(areaLeft + norm * areaWidth);
        }

        var build = _aeegBuilder.Build(
            snapshot.MinValues,
            snapshot.MaxValues,
            snapshot.Timestamps,
            snapshot.QualityFlags,
            mapper,
            (float)area.Top,
            TimestampToX,
            startUs,
            endUs);

        renderData = new AeegTrendRenderData
        {
            Points = build.Points,
            Segments = build.Segments,
            Gaps = build.Gaps,
            RenderArea = area
        };

        return true;
    }

    private void RenderAeeg(
        ID2D1DeviceContext context,
        in Vortice.Mathematics.Rect area,
        AeegTrendRenderData? data)
    {
        if (area.Width <= 0 || area.Height <= 0)
            return;

        var bgBrush = _resourceCache.GetSolidBrush(UiAeegPalette.Background);
        context.FillRectangle(area, bgBrush);

        var yAxisArea = GetAeegYAxisArea(area);
        var trendArea = GetAeegTrendArea(area);
        var timeAxisArea = GetAeegTimeAxisArea(area);

        // Calculate visible time range for aEEG
        long endUs = _currentTimestampUs;
        long startUs = Math.Max(0, endUs - _aeegVisibleDurationUs);
        var visibleRange = new TimeRange(startUs, endUs);

        // Render Y-axis labels on the left
        _aeegGridRenderer.Render(context, _resourceCache, yAxisArea, showMinorTicks: false, showLabels: true, labelMargin: 8f);

        // Render trend area with grid (no labels)
        _aeegGridRenderer.Render(context, _resourceCache, trendArea, showMinorTicks: true, showLabels: false);

        // Optional semi-log region visualization (background + 10uV boundary + labels).
        var visOptions = AeegSemiLogVisualizer.VisualizationOptions.Default with
        {
            ShowRegionBackground = true,
            ShowBoundaryLine = true,
            ShowRegionLabels = false,
            BoundaryColor = UiAeegPalette.BoundaryLine,
            LabelColor = UiAeegPalette.AxisLabel
        };
        _semiLogVisualizer.Render(context, _resourceCache, trendArea, visOptions);

        // Render enhanced time axis and vertical time grid.
        var axisOptions = TimeAxisRenderer.RenderOptions.Default with
        {
            ShowMinorTicks = true,
            ShowLabels = true,
            ShowCurrentTimeIndicator = false,
            BackgroundColor = UiAeegPalette.Background,
            MajorGridColor = UiAeegPalette.MajorGridLine,
            MinorGridColor = UiAeegPalette.MinorGridLine,
            LabelColor = UiAeegPalette.AxisLabel,
            CurrentTimeColor = UiAeegPalette.BoundaryLine
        };
        _timeAxisRenderer.Render(context, _resourceCache, visibleRange, trendArea, timeAxisArea, axisOptions);

        if (data.HasValue)
        {
            // Display aEEG as polylines (same visual mode class as EEG, with distinct aEEG palette).
            _aeegRenderer.Render(context, _resourceCache, data.Value, useLineMode: true);
        }
        else
        {
            RenderPlaceholder(context, "aEEG Trend", trendArea);
        }
    }

    private void RenderAeegDualChannel(
        ID2D1DeviceContext context,
        in Vortice.Mathematics.Rect area)
    {
        if (area.Width <= 0 || area.Height <= 0)
            return;

        // Fill background
        var bgBrush = _resourceCache.GetSolidBrush(UiAeegPalette.Background);
        context.FillRectangle(area, bgBrush);

        var yAxisArea = GetAeegYAxisArea(area);
        var trendArea = GetAeegTrendArea(area);
        var timeAxisArea = GetAeegTimeAxisArea(area);

        // Calculate visible time range
        long endUs = _currentTimestampUs;
        long startUs = endUs - _aeegVisibleDurationUs;
        var visibleRange = new TimeRange(startUs, endUs);

        // Render Y-axis labels
        _aeegGridRenderer.Render(context, _resourceCache, yAxisArea, showMinorTicks: false, showLabels: true, labelMargin: 8f);

        // Render grid
        _aeegGridRenderer.Render(context, _resourceCache, trendArea, showMinorTicks: true, showLabels: false);

        var visOptions = AeegSemiLogVisualizer.VisualizationOptions.Default with
        {
            ShowRegionBackground = true,
            ShowBoundaryLine = true,
            ShowRegionLabels = false,
            BoundaryColor = UiAeegPalette.BoundaryLine,
            LabelColor = UiAeegPalette.AxisLabel
        };
        _semiLogVisualizer.Render(context, _resourceCache, trendArea, visOptions);

        var axisOptions = TimeAxisRenderer.RenderOptions.Default with
        {
            ShowMinorTicks = true,
            ShowLabels = true,
            ShowCurrentTimeIndicator = false,
            BackgroundColor = UiAeegPalette.Background,
            MajorGridColor = UiAeegPalette.MajorGridLine,
            MinorGridColor = UiAeegPalette.MinorGridLine,
            LabelColor = UiAeegPalette.AxisLabel,
            CurrentTimeColor = UiAeegPalette.BoundaryLine
        };
        _timeAxisRenderer.Render(context, _resourceCache, visibleRange, trendArea, timeAxisArea, axisOptions);

        // Render both channels
        if (_aeegCh1Ready)
            _aeegRenderer.Render(context, _resourceCache, _aeegCh1RenderData, useLineMode: true);
        if (_aeegCh2Ready)
            _aeegRenderer.Render(context, _resourceCache, _aeegCh2RenderData, useLineMode: true);
    }

    private void RenderTimeAxisLabels(
        ID2D1DeviceContext context,
        in Vortice.Mathematics.Rect area,
        in TimeRange visibleRange)
    {
        if (area.Width <= 0 || area.Height <= 0)
            return;

        var bgBrush = _resourceCache.GetSolidBrush(UiAeegPalette.Background);
        context.FillRectangle(area, bgBrush);

        var textBrush = _resourceCache.GetSolidBrush(UiAeegPalette.AxisLabel);
        var textFormat = _resourceCache.GetTextFormat("Segoe UI", 9.0f);

        double durationHours = _aeegVisibleDurationUs / (3600.0 * 1_000_000);
        long tickIntervalUs = durationHours switch
        {
            <= 3 => 60L * 60 * 1_000_000,      // 1 hour
            <= 6 => 60L * 60 * 1_000_000,      // 1 hour
            <= 12 => 2L * 60 * 60 * 1_000_000, // 2 hours
            _ => 3L * 60 * 60 * 1_000_000      // 3 hours
        };

        long firstTickUs = (visibleRange.StartUs / tickIntervalUs + 1) * tickIntervalUs;

        for (long ts = firstTickUs; ts <= visibleRange.EndUs; ts += tickIntervalUs)
        {
            if (ts < visibleRange.StartUs) continue;

            double norm = (double)(ts - visibleRange.StartUs) / Math.Max(1, visibleRange.DurationUs);
            float x = (float)(area.Left + norm * area.Width);

            string label = FormatTimestamp(ts);
            var labelRect = new Vortice.Mathematics.Rect(x - 28, area.Top + 2, 56, area.Height - 4);
            context.DrawText(label, textFormat, labelRect, textBrush);
        }
    }

    // aEEG layout constants - based on reference design
    private const float AeegYAxisLabelWidth = 84f;   // Keep enough room for large left-axis labels (14px)
    private float AeegGsWidthRatio => 0.0f;  // GS histogram removed from UI baseline
    private const float AeegTimeAxisHeight = 18f;    // X-axis time labels at bottom

    private Vortice.Mathematics.Rect GetAeegYAxisArea(in Vortice.Mathematics.Rect area)
    {
        // Add small top padding to prevent labels from being too close to previous region
        const float labelTopPadding = 5f;
        float labelHeight = Math.Max(0f, (float)area.Height - AeegTimeAxisHeight - labelTopPadding);
        return new Vortice.Mathematics.Rect(area.Left, area.Top + labelTopPadding, AeegYAxisLabelWidth, labelHeight);
    }

    private Vortice.Mathematics.Rect GetAeegTrendArea(in Vortice.Mathematics.Rect area)
    {
        float gsWidth = GetAeegGsWidth((float)area.Width);
        float trendWidth = Math.Max(0f, (float)area.Width - AeegYAxisLabelWidth - gsWidth);
        float trendHeight = Math.Max(0f, (float)area.Height - AeegTimeAxisHeight);
        return new Vortice.Mathematics.Rect(area.Left + AeegYAxisLabelWidth, area.Top, trendWidth, trendHeight);
    }

    private Vortice.Mathematics.Rect GetAeegGsArea(in Vortice.Mathematics.Rect area)
    {
        float gsWidth = GetAeegGsWidth((float)area.Width);
        float gsHeight = Math.Max(0f, (float)area.Height - AeegTimeAxisHeight);
        return new Vortice.Mathematics.Rect(area.Right - gsWidth, area.Top, gsWidth, gsHeight);
    }

    private Vortice.Mathematics.Rect GetAeegTimeAxisArea(in Vortice.Mathematics.Rect area)
    {
        float gsWidth = GetAeegGsWidth((float)area.Width);
        float axisWidth = Math.Max(0f, (float)area.Width - AeegYAxisLabelWidth - gsWidth);
        return new Vortice.Mathematics.Rect(area.Left + AeegYAxisLabelWidth, area.Bottom - AeegTimeAxisHeight, axisWidth, AeegTimeAxisHeight);
    }

    private float GetAeegGsWidth(float areaWidth)
    {
        if (AeegGsWidthRatio <= 0f)
        {
            return 0f;
        }

        return Math.Max(40f, areaWidth * AeegGsWidthRatio);
    }

    private static string FormatTimestamp(long timestampUs)
    {
        long totalSeconds = Math.Max(0, timestampUs / 1_000_000);
        int hours = (int)(totalSeconds / 3600);
        int minutes = (int)((totalSeconds % 3600) / 60);
        int seconds = (int)(totalSeconds % 60);
        return $"{hours:D2}:{minutes:D2}:{seconds:D2}";
    }

    private void RenderSeparators(ID2D1DeviceContext context)
    {
        var majorBrush = _resourceCache.GetSolidBrush(UiAeegPalette.BoundaryLine);
        var normalBrush = _resourceCache.GetSolidBrush(UiAeegPalette.MajorGridLine);
        float left = (float)_layout.Aeeg1.Left;
        float right = (float)_layout.Aeeg1.Right;

        // Major separators between aEEG and EEG bands (clinically important boundaries)
        context.DrawLine(
            new Vector2(left, (float)_layout.Aeeg1.Bottom),
            new Vector2(right, (float)_layout.Aeeg1.Bottom),
            majorBrush,
            2.0f);

        context.DrawLine(
            new Vector2(left, (float)_layout.Aeeg2.Bottom),
            new Vector2(right, (float)_layout.Aeeg2.Bottom),
            majorBrush,
            2.0f);

        // Normal separators for other region boundaries
        context.DrawLine(
            new Vector2(left, (float)_layout.EegPreview1.Bottom),
            new Vector2(right, (float)_layout.EegPreview1.Bottom),
            normalBrush,
            1.0f);

        context.DrawLine(
            new Vector2(left, (float)_layout.EegPreview2.Bottom),
            new Vector2(right, (float)_layout.EegPreview2.Bottom),
            normalBrush,
            1.0f);

        context.DrawLine(
            new Vector2(left, (float)_layout.Nirs.Bottom),
            new Vector2(right, (float)_layout.Nirs.Bottom),
            normalBrush,
            1.0f);
    }

    /// <summary>
    /// Attempts to recover from a lost D3D device.
    /// </summary>
    public bool TryRecoverDevice()
    {
        if (_disposed) return false;

        if (_renderer.TryRecoverDevice())
        {
            // Reinitialize ResourceCache
            if (_renderer.DeviceContext != null)
            {
                _resourceCache.Clear();
                _resourceCache.Initialize(_renderer.DeviceContext);
            }
            _layeredRenderer.InvalidateAll();
            return true;
        }

        return false;
    }

    /// <summary>
    /// Handles theme change events and updates rendering colors.
    /// </summary>
    private void OnThemeChanged(object? sender, ThemeType newTheme)
    {
        // Convert UI theme to AEEG theme
        var aeegTheme = newTheme == ThemeType.Apple
            ? AeegThemeType.Apple
            : AeegThemeType.Medical;

        // Update AEEG color palette
        AeegColorPalette.SetTheme(aeegTheme);
        UiAeegPalette.SetTheme(newTheme == ThemeType.Apple);
        UiEegPalette.SetTheme(newTheme == ThemeType.Apple);

        // Force redraw by clearing resource cache (brushes will be recreated with new colors)
        // Note: ResourceCache doesn't have Invalidate, resources will update on next render
        _layeredRenderer.InvalidateAll();
    }

    /// <summary>
    /// Releases all resources.
    /// </summary>
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        // Unsubscribe from theme changes
        if (_themeService != null)
        {
            _themeService.ThemeChanged -= OnThemeChanged;
        }

        Stop();
        _dataBridge.Dispose();
        _resourceCache.Dispose();
        _layeredRenderer.Dispose();
        _renderer.Dispose();
        _stopwatch.Stop();
    }
}
