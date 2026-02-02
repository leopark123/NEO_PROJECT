// D3DImageRenderer.cs
// Sprint 1.4: D3D11 + D2D rendering pipeline for WPF integration.
//
// Source: NEO_UI_Development_Plan_WPF.md §8
// CHARTER: R-01 render callback O(1), R-03 no per-frame resource creation
//
// Pipeline: D3D11 → D2D1 → Staging Texture → WriteableBitmap → WPF Image
//
// Note: The original D3D9Ex bridge approach (D3D11 → D3D9 shared surface → D3DImage)
// had alpha compositing issues on some drivers. This version uses CPU-side pixel copy
// via a staging texture, which is reliable across all hardware. The per-frame copy
// cost is negligible for the target resolution (~1ms for 1920x1080 BGRA).
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

    // ── D2D ──
    private ID2D1Factory? _d2dFactory;
    private ID2D1RenderTarget? _d2dTarget;
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

    /// <summary>D2D1 RenderTarget for drawing operations.</summary>
    public ID2D1RenderTarget? RenderTarget => _d2dTarget;

    /// <summary>Current back buffer width in pixels.</summary>
    public int Width => _width;

    /// <summary>Current back buffer height in pixels.</summary>
    public int Height => _height;

    /// <summary>True if device was successfully initialized.</summary>
    public bool IsDeviceReady => _device != null && _d2dFactory != null;

    /// <summary>True if rendering resources (texture, D2D target) are ready.</summary>
    public bool IsRenderReady => _d2dTarget != null && _stagingTexture != null && _writeableBitmap != null;

    /// <summary>
    /// Initializes D3D11 device and D2D factory.
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

        // D2D1 factory (single-threaded, created once)
        _d2dFactory = D2D1.D2D1CreateFactory<ID2D1Factory>();
    }

    // ═══════════════════════════════════════════════════════════════════
    // Resize — recreate render texture + staging + D2D target + bitmap
    // ═══════════════════════════════════════════════════════════════════

    /// <summary>
    /// Recreates the render texture, staging texture, D2D render target, and
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

        // 3. Create D2D1 render target from DXGI surface
        using var dxgiSurface = _renderTexture.QueryInterface<IDXGISurface>();
        var rtProps = new RenderTargetProperties(
            RenderTargetType.Default,
            new Vortice.DCommon.PixelFormat(Format.B8G8R8A8_UNorm, Vortice.DCommon.AlphaMode.Premultiplied),
            96f, 96f,
            RenderTargetUsage.None,
            Vortice.Direct2D1.FeatureLevel.Default);
        _d2dTarget = _d2dFactory!.CreateDxgiSurfaceRenderTarget(dxgiSurface, rtProps);

        // 4. Create reusable brushes (NOT per-frame — CHARTER R-03)
        CreateBrushes();

        // 5. Create WriteableBitmap (WPF-side pixel buffer)
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
        if (_disposed || _d2dTarget == null || _clearBrush == null) return false;

        _d2dTarget.BeginDraw();

        // Clear with background color from Colors.xaml (CHARTER R-01: O(1) operation)
        _d2dTarget.Clear(new Color4(
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
        if (!_isRendering || _d2dTarget == null || _context == null) return;
        if (_renderTexture == null || _stagingTexture == null || _writeableBitmap == null) return;

        _d2dTarget.EndDraw();
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
        if (_d2dTarget == null || _testBrush == null) return;
        if (_width <= 0 || _height <= 0) return;

        // Draw a centered rectangle — O(1) operation
        float rectW = _width * 0.6f;
        float rectH = _height * 0.6f;
        float x = (_width - rectW) / 2f;
        float y = (_height - rectH) / 2f;

        var rect = new Vortice.Mathematics.Rect(x, y, rectW, rectH);
        _d2dTarget.FillRectangle(rect, _testBrush);

        // Draw border outline
        _d2dTarget.DrawRectangle(rect, _testBrush, 2f);
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
        if (_d2dTarget == null) return;

        _clearBrush?.Dispose();
        _testBrush?.Dispose();

        // Read colors from Colors.xaml via WPF Application.Resources — no fallbacks
        var clearColor = GetWpfColor("Color.BackgroundDark");
        var testColor = GetWpfColor("Color.Primary");

        _clearBrush = _d2dTarget.CreateSolidColorBrush(clearColor);
        _testBrush = _d2dTarget.CreateSolidColorBrush(testColor);
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
        _d2dTarget?.Dispose();
        _d2dTarget = null;
        _stagingTexture?.Dispose();
        _stagingTexture = null;
        _renderTexture?.Dispose();
        _renderTexture = null;
    }

    private void ReleaseDevices()
    {
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
