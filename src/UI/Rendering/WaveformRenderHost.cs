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
    private readonly GsHistogramRenderer _gsRenderer;
    private readonly EegPreviewRenderer _eegPreviewRenderer;
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
    private byte[]? _gsBinsCh1;
    private byte[]? _gsBinsCh2;
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
        _gsRenderer = new GsHistogramRenderer();
        _eegPreviewRenderer = new EegPreviewRenderer();
        _playbackClock = new PlaybackClock();
        _playbackClock.SeekTo(_playbackStartUs);

        // Subscribe to theme changes
        if (_themeService != null)
        {
            _themeService.ThemeChanged += OnThemeChanged;
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
    public void Resize(int width, int height)
    {
        if (_disposed) return;
        if (width <= 0 || height <= 0) return;
        if (_renderer.Width == width && _renderer.Height == height) return;

        // Resize D3D resources
        _renderer.Resize(width, height);

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
                    _aeegCh1Ready ? _aeegCh1RenderData : null, _gsBinsCh1);

                // EEG Preview Ch1 (5%) - narrow strip with waveform (Lane 0 / EEG-1)
                if (sweepData.Length >= 1)
                {
                    _sweepRenderer.RenderChannel(_renderer.DeviceContext, _resourceCache,
                        sweepData[0], _layout.EegPreview1, _lane0YAxisRangeUv, _lane0GainMicrovoltsPerCm);
                }

                // aEEG Ch2 (25%)
                RenderAeeg(_renderer.DeviceContext, _layout.Aeeg2,
                    _aeegCh2Ready ? _aeegCh2RenderData : null, _gsBinsCh2);

                // EEG Preview Ch2 (5%) - narrow strip with waveform (Lane 1 / EEG-2)
                if (sweepData.Length >= 2)
                {
                    _sweepRenderer.RenderChannel(_renderer.DeviceContext, _resourceCache,
                        sweepData[1], _layout.EegPreview2, _lane1YAxisRangeUv, _lane1GainMicrovoltsPerCm);
                }

                // NIRS placeholder
                RenderPlaceholder(_renderer.DeviceContext, "NIRS Trend (6ch)", _layout.Nirs);

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
            Dpi = 96.0,
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
        var textBrush = _resourceCache.LightGrayBrush;
        var textFormat = _resourceCache.SmallTextFormat;

        context.FillRectangle(area, background);
        context.DrawText(label, textFormat, area, textBrush);
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

        var gs1 = _dataBridge.GetGsHistogramSnapshot(0, endUs);
        var gs2 = _dataBridge.GetGsHistogramSnapshot(1, endUs);
        _gsBinsCh1 = gs1.Bins;
        _gsBinsCh2 = gs2.Bins;
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
        AeegTrendRenderData? data,
        byte[]? gsBins)
    {
        if (area.Width <= 0 || area.Height <= 0)
            return;

        var bgBrush = _resourceCache.GetSolidBrush(UiAeegPalette.Background);
        context.FillRectangle(area, bgBrush);

        var yAxisArea = GetAeegYAxisArea(area);
        var trendArea = GetAeegTrendArea(area);
        var gsArea = GetAeegGsArea(area);
        var timeAxisArea = GetAeegTimeAxisArea(area);

        // Calculate visible time range for aEEG
        long endUs = _currentTimestampUs;
        long startUs = Math.Max(0, endUs - _aeegVisibleDurationUs);
        var visibleRange = new TimeRange(startUs, endUs);

        // Render Y-axis labels on the left
        _aeegGridRenderer.Render(context, _resourceCache, yAxisArea, showMinorTicks: false, showLabels: true, labelMargin: 2f);

        // Render trend area with grid (no labels)
        _aeegGridRenderer.Render(context, _resourceCache, trendArea, showMinorTicks: true, showLabels: false);

        // Render time grid and X-axis labels
        _aeegGridRenderer.RenderTimeGrid(context, _resourceCache, trendArea, visibleRange, (int)trendArea.Width, majorIntervalSeconds: 300.0, minorIntervalSeconds: 60.0);
        RenderTimeAxisLabels(context, timeAxisArea, visibleRange);

        if (data.HasValue)
        {
            _aeegRenderer.Render(context, _resourceCache, data.Value, useLineMode: false);
        }
        else
        {
            RenderPlaceholder(context, "aEEG Trend", trendArea);
        }

        if (gsBins != null && gsBins.Length > 0)
        {
            _gsRenderer.Render(context, _resourceCache, gsArea, gsBins);
        }
        else
        {
            RenderPlaceholder(context, "GS", gsArea);
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
        var gsArea = GetAeegGsArea(area);
        var timeAxisArea = GetAeegTimeAxisArea(area);

        // Calculate visible time range
        long endUs = _currentTimestampUs;
        long startUs = endUs - _aeegVisibleDurationUs;
        var visibleRange = new TimeRange(startUs, endUs);

        // Render Y-axis labels
        _aeegGridRenderer.Render(context, _resourceCache, yAxisArea, showMinorTicks: false, showLabels: true, labelMargin: 2f);

        // Render grid
        _aeegGridRenderer.Render(context, _resourceCache, trendArea, showMinorTicks: true, showLabels: false);

        // Render time grid and X-axis labels
        _aeegGridRenderer.RenderTimeGrid(context, _resourceCache, trendArea, visibleRange, (int)trendArea.Width, majorIntervalSeconds: 300.0, minorIntervalSeconds: 60.0);
        RenderTimeAxisLabels(context, timeAxisArea, visibleRange);

        // Render both channels
        if (_aeegCh1Ready)
            _aeegRenderer.Render(context, _resourceCache, _aeegCh1RenderData, useLineMode: false);
        if (_aeegCh2Ready)
            _aeegRenderer.Render(context, _resourceCache, _aeegCh2RenderData, useLineMode: false);

        // Render GS histograms (split vertically if both channels have data)
        if (_showGsHistogram)
        {
            if (_gsBinsCh1 != null && _gsBinsCh1.Length > 0)
            {
                var gs1Area = new Vortice.Mathematics.Rect(
                    gsArea.Left,
                    gsArea.Top,
                    gsArea.Width,
                    gsArea.Height * 0.5f);
                _gsRenderer.Render(context, _resourceCache, gs1Area, _gsBinsCh1);
            }
            if (_gsBinsCh2 != null && _gsBinsCh2.Length > 0)
            {
                var gs2Area = new Vortice.Mathematics.Rect(
                    gsArea.Left,
                    gsArea.Top + gsArea.Height * 0.5f,
                    gsArea.Width,
                    gsArea.Height * 0.5f);
                _gsRenderer.Render(context, _resourceCache, gs2Area, _gsBinsCh2);
            }
        }
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
    private const float AeegYAxisLabelWidth = 50f;   // Y-axis labels on left (reduced to avoid overlap with toolbar)
    private float AeegGsWidthRatio => _showGsHistogram ? 0.30f : 0.0f;  // GS histogram on right (30% if enabled, 0% if disabled)
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
        float gsWidth = Math.Max(40f, (float)area.Width * AeegGsWidthRatio);
        float trendWidth = Math.Max(0f, (float)area.Width - AeegYAxisLabelWidth - gsWidth);
        float trendHeight = Math.Max(0f, (float)area.Height - AeegTimeAxisHeight);
        return new Vortice.Mathematics.Rect(area.Left + AeegYAxisLabelWidth, area.Top, trendWidth, trendHeight);
    }

    private Vortice.Mathematics.Rect GetAeegGsArea(in Vortice.Mathematics.Rect area)
    {
        float gsWidth = Math.Max(40f, (float)area.Width * AeegGsWidthRatio);
        float gsHeight = Math.Max(0f, (float)area.Height - AeegTimeAxisHeight);
        return new Vortice.Mathematics.Rect(area.Right - gsWidth, area.Top, gsWidth, gsHeight);
    }

    private Vortice.Mathematics.Rect GetAeegTimeAxisArea(in Vortice.Mathematics.Rect area)
    {
        float gsWidth = Math.Max(40f, (float)area.Width * AeegGsWidthRatio);
        float axisWidth = Math.Max(0f, (float)area.Width - AeegYAxisLabelWidth - gsWidth);
        return new Vortice.Mathematics.Rect(area.Left + AeegYAxisLabelWidth, area.Bottom - AeegTimeAxisHeight, axisWidth, AeegTimeAxisHeight);
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
        var separatorBrush = _resourceCache.GetSolidBrush(UiAeegPalette.MajorGridLine);
        float left = (float)_layout.Aeeg1.Left;
        float right = (float)_layout.Aeeg1.Right;

        // Draw separator lines between all 5 regions
        float[] ys =
        [
            (float)_layout.Aeeg1.Bottom,        // After aEEG Ch1
            (float)_layout.EegPreview1.Bottom,  // After EEG Preview Ch1
            (float)_layout.Aeeg2.Bottom,        // After aEEG Ch2
            (float)_layout.EegPreview2.Bottom,  // After EEG Preview Ch2
            (float)_layout.Nirs.Bottom          // After NIRS
        ];

        foreach (float y in ys)
        {
            context.DrawLine(new Vector2(left, y), new Vector2(right, y), separatorBrush, 1.0f);
        }
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

        // Force redraw by clearing resource cache (brushes will be recreated with new colors)
        // Note: ResourceCache doesn't have Invalidate, resources will update on next render
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
