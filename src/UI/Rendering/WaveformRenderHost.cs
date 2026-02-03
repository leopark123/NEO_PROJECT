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

using System.Windows;
using System.Windows.Media;
using Neo.Core.Interfaces;
using Neo.Core.Models;
using Neo.Rendering.Core;
using Neo.Rendering.Resources;

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

    private RenderContext _renderContext;
    private long _frameNumber;
    private bool _isRunning;
    private bool _disposed;

    // Time tracking (microseconds)
    private long _currentTimestampUs;
    private long _visibleDurationUs = 15_000_000; // 15 seconds default (EEG view)
    private readonly System.Diagnostics.Stopwatch _stopwatch = new();

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

    /// <summary>EEG data bridge for connecting data sources.</summary>
    public EegDataBridge DataBridge => _dataBridge;

    /// <summary>
    /// Creates a new render host with default 3-layer configuration.
    /// </summary>
    public WaveformRenderHost()
    {
        _renderer = new D3DImageRenderer();
        _layeredRenderer = LayeredRenderer.CreateDefault();
        _resourceCache = new ResourceCache();
        _dataBridge = new EegDataBridge();
        _sweepRenderer = new SweepModeRenderer();
        _renderContext = CreateRenderContext();

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
        _currentTimestampUs = _stopwatch.ElapsedTicks * 1_000_000 / System.Diagnostics.Stopwatch.Frequency;

        // Begin render frame
        if (!_renderer.BeginRender()) return;

        try
        {
            // Sprint 3.2-fix: Use sweep mode renderer for EEG waveforms
            if (_renderer.DeviceContext != null)
            {
                var sweepData = _dataBridge.GetSweepData();
                if (sweepData.Length > 0)
                {
                    _sweepRenderer.Render(
                        _renderer.DeviceContext,
                        _resourceCache,
                        sweepData,
                        _renderer.Width,
                        _renderer.Height);
                }
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
    /// Releases all resources.
    /// </summary>
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        Stop();
        _dataBridge.Dispose();
        _resourceCache.Dispose();
        _layeredRenderer.Dispose();
        _renderer.Dispose();
        _stopwatch.Stop();
    }
}
