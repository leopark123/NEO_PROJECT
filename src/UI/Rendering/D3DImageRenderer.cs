// D3DImageRenderer.cs
// Sprint 1.4 / Sprint 3.1: D3D11 + D2D rendering pipeline for WPF integration.
//
// Source: NEO_UI_Development_Plan_WPF.md §8
// CHARTER: R-01 render callback O(1), R-03 no per-frame resource creation
//
// Pipeline: D3D11 → D2D1DeviceContext → Staging Texture → WriteableBitmap → WPF Image
//
// Sprint 3.1: Upgraded to ID2D1DeviceContext (from ID2D1RenderTarget) for
// compatibility with Neo.Rendering engine's LayeredRenderer and ResourceCache.
//
// This class contains NO business logic, NO medical semantics.

using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Vortice.Direct2D1;
using Vortice.Direct3D;
using Vortice.Direct3D11;
using Vortice.DXGI;
using Vortice.Mathematics;

namespace Neo.UI.Rendering;

/// <summary>
/// Manages the D3D11 → D2D1 → WriteableBitmap rendering pipeline.
/// All GPU resources are created once and reused; only Resize triggers recreation.
/// </summary>
public sealed class D3DImageRenderer : IDisposable
{
    // ── D3D11 ──
    private ID3D11Device? _device;
    private ID3D11DeviceContext? _context;
    private ID3D11Texture2D? _renderTexture;
    private ID3D11Texture2D? _stagingTexture;

    // ── D2D (Sprint 3.1: DeviceContext instead of RenderTarget) ──
    private ID2D1Factory1? _d2dFactory;
    private ID2D1Device? _d2dDevice;
    private ID2D1DeviceContext? _d2dContext;
    private ID2D1Bitmap1? _d2dBitmap;
    private ID2D1SolidColorBrush? _clearBrush;
    private ID2D1SolidColorBrush? _testBrush;

    // ── WPF WriteableBitmap ──
    private WriteableBitmap? _writeableBitmap;

    // ── State ──
    private int _width;
    private int _height;
    private bool _isRendering;
    private bool _disposed;

    /// <summary>WPF ImageSource backed by WriteableBitmap.</summary>
    public ImageSource? ImageSource => _writeableBitmap;

    /// <summary>D2D1 DeviceContext for drawing operations (Sprint 3.1).</summary>
    public ID2D1DeviceContext? DeviceContext => _d2dContext;

    /// <summary>Current back buffer width in pixels.</summary>
    public int Width => _width;

    /// <summary>Current back buffer height in pixels.</summary>
    public int Height => _height;

    /// <summary>True if device was successfully initialized.</summary>
    public bool IsDeviceReady => _device != null && _d2dFactory != null && _d2dDevice != null;

    /// <summary>True if rendering resources (texture, D2D context) are ready.</summary>
    public bool IsRenderReady => _d2dContext != null && _stagingTexture != null && _writeableBitmap != null;

    /// <summary>
    /// Initializes D3D11 device and D2D factory/device.
    /// Does NOT create render resources — call <see cref="Resize"/> first.
    /// </summary>
    public D3DImageRenderer()
    {
        InitializeDevices();
    }

    // ═══════════════════════════════════════════════════════════════════
    // Device initialization (once per lifetime)
    // ═══════════════════════════════════════════════════════════════════

    private void InitializeDevices()
    {
        // D3D11 device with BGRA support (required for D2D interop)
        D3D11.D3D11CreateDevice(
            adapter: null,
            DriverType.Hardware,
            DeviceCreationFlags.BgraSupport,
            featureLevels: [],
            out _device,
            out _context);

        // D2D1 factory (Sprint 3.1: use Factory1 for DeviceContext support)
        _d2dFactory = D2D1.D2D1CreateFactory<ID2D1Factory1>();

        // Create D2D1 device from DXGI device
        using var dxgiDevice = _device!.QueryInterface<IDXGIDevice>();
        _d2dDevice = _d2dFactory.CreateDevice(dxgiDevice);
    }

    // ═══════════════════════════════════════════════════════════════════
    // Resize — recreate render texture + staging + D2D context + bitmap
    // ═══════════════════════════════════════════════════════════════════

    /// <summary>
    /// Recreates the render texture, staging texture, D2D device context, and
    /// WriteableBitmap for the given size.
    /// Safe to call multiple times; releases old resources before creating new ones.
    /// </summary>
    public void Resize(int width, int height)
    {
        if (_disposed) return;
        if (width <= 0 || height <= 0) return;
        if (_width == width && _height == height) return;

        _width = width;
        _height = height;

        // Release old render resources (not devices)
        ReleaseRenderResources();

        // 1. Create D3D11 render texture (GPU-only, D2D draws here)
        var renderDesc = new Texture2DDescription
        {
            Width = (uint)width,
            Height = (uint)height,
            MipLevels = 1,
            ArraySize = 1,
            Format = Format.B8G8R8A8_UNorm,
            SampleDescription = new SampleDescription(1, 0),
            Usage = ResourceUsage.Default,
            BindFlags = BindFlags.RenderTarget | BindFlags.ShaderResource,
        };
        _renderTexture = _device!.CreateTexture2D(renderDesc);

        // 2. Create staging texture (CPU-readable, for pixel copy to WriteableBitmap)
        var stagingDesc = new Texture2DDescription
        {
            Width = (uint)width,
            Height = (uint)height,
            MipLevels = 1,
            ArraySize = 1,
            Format = Format.B8G8R8A8_UNorm,
            SampleDescription = new SampleDescription(1, 0),
            Usage = ResourceUsage.Staging,
            CPUAccessFlags = CpuAccessFlags.Read,
        };
        _stagingTexture = _device!.CreateTexture2D(stagingDesc);

        // 3. Create D2D1 DeviceContext (Sprint 3.1: replaces RenderTarget)
        _d2dContext = _d2dDevice!.CreateDeviceContext(DeviceContextOptions.None);

        // 4. Create D2D1 Bitmap from DXGI surface and set as target
        using var dxgiSurface = _renderTexture.QueryInterface<IDXGISurface>();
        var bitmapProps = new BitmapProperties1(
            new Vortice.DCommon.PixelFormat(Format.B8G8R8A8_UNorm, Vortice.DCommon.AlphaMode.Premultiplied),
            96f, 96f,
            BitmapOptions.Target | BitmapOptions.CannotDraw);
        _d2dBitmap = _d2dContext.CreateBitmapFromDxgiSurface(dxgiSurface, bitmapProps);
        _d2dContext.Target = _d2dBitmap;

        // 5. Create reusable brushes (NOT per-frame — CHARTER R-03)
        CreateBrushes();

        // 6. Create WriteableBitmap (WPF-side pixel buffer)
        _writeableBitmap = new WriteableBitmap(width, height, 96, 96, PixelFormats.Bgra32, null);
    }

    // ═══════════════════════════════════════════════════════════════════
    // BeginRender / EndRender — render cycle
    // ═══════════════════════════════════════════════════════════════════

    /// <summary>
    /// Begins a render frame. Begins D2D draw and clears to background color.
    /// Must be followed by <see cref="EndRender"/>.
    /// </summary>
    /// <returns>True if rendering can proceed; false if resources are not ready.</returns>
    public bool BeginRender()
    {
        if (_disposed || _d2dContext == null || _clearBrush == null) return false;

        _d2dContext.BeginDraw();

        // Clear with background color from Colors.xaml (CHARTER R-01: O(1) operation)
        _d2dContext.Clear(new Color4(
            _clearBrush!.Color.R,
            _clearBrush.Color.G,
            _clearBrush.Color.B,
            _clearBrush.Color.A));

        _isRendering = true;
        return true;
    }

    /// <summary>
    /// Ends the current render frame. Flushes D2D, copies pixels to WriteableBitmap.
    /// </summary>
    public void EndRender()
    {
        if (!_isRendering || _d2dContext == null || _context == null) return;
        if (_renderTexture == null || _stagingTexture == null || _writeableBitmap == null) return;

        _d2dContext.EndDraw();
        _context.Flush();

        // Copy GPU render texture → CPU staging texture
        _context.CopyResource(_stagingTexture, _renderTexture);

        // Map staging texture for CPU read
        var mapped = _context.Map(_stagingTexture, 0, MapMode.Read);
        try
        {
            _writeableBitmap.Lock();
            try
            {
                // Copy row-by-row (staging pitch may differ from bitmap stride)
                int rowBytes = _width * 4; // BGRA = 4 bytes per pixel
                unsafe
                {
                    byte* src = (byte*)mapped.DataPointer;
                    byte* dst = (byte*)_writeableBitmap.BackBuffer;
                    int srcPitch = (int)mapped.RowPitch;
                    int dstStride = _writeableBitmap.BackBufferStride;

                    for (int y = 0; y < _height; y++)
                    {
                        Buffer.MemoryCopy(
                            src + y * srcPitch,
                            dst + y * dstStride,
                            dstStride,
                            rowBytes);
                    }
                }
                _writeableBitmap.AddDirtyRect(new Int32Rect(0, 0, _width, _height));
            }
            finally
            {
                _writeableBitmap.Unlock();
            }
        }
        finally
        {
            _context.Unmap(_stagingTexture, 0);
        }

        _isRendering = false;
    }

    /// <summary>
    /// Draws a test rectangle using the Primary color from Colors.xaml.
    /// Used for Sprint 1.4 validation only.
    /// </summary>
    public void DrawTestRect()
    {
        if (_d2dContext == null || _testBrush == null) return;
        if (_width <= 0 || _height <= 0) return;

        // Draw a centered rectangle — O(1) operation
        float rectW = _width * 0.6f;
        float rectH = _height * 0.6f;
        float x = (_width - rectW) / 2f;
        float y = (_height - rectH) / 2f;

        var rect = new Vortice.Mathematics.Rect(x, y, rectW, rectH);
        _d2dContext.FillRectangle(rect, _testBrush);

        // Draw border outline
        _d2dContext.DrawRectangle(rect, _testBrush, 2f);
    }

    // ═══════════════════════════════════════════════════════════════════
    // Device Lost recovery
    // ═══════════════════════════════════════════════════════════════════

    /// <summary>
    /// Attempts to recover from a lost device by recreating all resources.
    /// </summary>
    public bool TryRecoverDevice()
    {
        if (_disposed) return false;

        try
        {
            int savedWidth = _width;
            int savedHeight = _height;

            // Release everything
            ReleaseRenderResources();
            ReleaseDevices();

            // Recreate
            InitializeDevices();

            if (savedWidth > 0 && savedHeight > 0)
            {
                _width = 0; // Force Resize to run
                _height = 0;
                Resize(savedWidth, savedHeight);
            }

            return IsDeviceReady;
        }
        catch
        {
            return false;
        }
    }

    // ═══════════════════════════════════════════════════════════════════
    // Brush creation — uses WPF resource colors (no hardcoding)
    // ═══════════════════════════════════════════════════════════════════

    private void CreateBrushes()
    {
        if (_d2dContext == null) return;

        _clearBrush?.Dispose();
        _testBrush?.Dispose();

        // Read colors from Colors.xaml via WPF Application.Resources — no fallbacks
        var clearColor = GetWpfColor("Color.BackgroundDark");
        var testColor = GetWpfColor("Color.Primary");

        _clearBrush = _d2dContext.CreateSolidColorBrush(clearColor);
        _testBrush = _d2dContext.CreateSolidColorBrush(testColor);
    }

    /// <summary>
    /// Reads a Color resource from WPF Application.Resources (Colors.xaml).
    /// Throws if the resource is not found — all colors must be defined in Colors.xaml.
    /// </summary>
    private static Color4 GetWpfColor(string resourceKey)
    {
        if (Application.Current?.Resources[resourceKey] is System.Windows.Media.Color wpfColor)
        {
            return new Color4(
                wpfColor.R / 255f,
                wpfColor.G / 255f,
                wpfColor.B / 255f,
                wpfColor.A / 255f);
        }
        throw new InvalidOperationException(
            $"Color resource '{resourceKey}' not found in Colors.xaml.");
    }

    // ═══════════════════════════════════════════════════════════════════
    // Resource cleanup
    // ═══════════════════════════════════════════════════════════════════

    private void ReleaseRenderResources()
    {
        _clearBrush?.Dispose();
        _clearBrush = null;
        _testBrush?.Dispose();
        _testBrush = null;
        _d2dBitmap?.Dispose();
        _d2dBitmap = null;
        _d2dContext?.Dispose();
        _d2dContext = null;
        _stagingTexture?.Dispose();
        _stagingTexture = null;
        _renderTexture?.Dispose();
        _renderTexture = null;
    }

    private void ReleaseDevices()
    {
        _d2dDevice?.Dispose();
        _d2dDevice = null;
        _d2dFactory?.Dispose();
        _d2dFactory = null;
        _context?.Dispose();
        _context = null;
        _device?.Dispose();
        _device = null;
    }

    // ═══════════════════════════════════════════════════════════════════
    // Dispose — full release, multi-call safe
    // ═══════════════════════════════════════════════════════════════════

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        ReleaseRenderResources();
        ReleaseDevices();
        _writeableBitmap = null;
    }
}
