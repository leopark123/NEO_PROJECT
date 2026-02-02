// D2DRenderTarget.cs
// D2D 渲染目标实现 - 来源: ARCHITECTURE.md §5, ADR-002 (Vortice)

using System.Drawing;
using System.Drawing.Imaging;
using Neo.Core.Interfaces;
using Neo.Rendering.Device;
using Neo.Rendering.Resources;
using Vortice.Direct3D11;
using Vortice.DXGI;

namespace Neo.Rendering.Core;

/// <summary>
/// Direct2D 渲染目标实现。
/// 实现 IRenderTarget 接口，提供完整的渲染基础设施。
/// </summary>
/// <remarks>
/// 依据: ARCHITECTURE.md §5, ADR-002 (Vortice 渲染引擎)
///
/// 功能:
/// - D3D11/D2D 设备管理
/// - 交换链管理
/// - DPI 处理
/// - 设备丢失恢复
/// - 资源缓存
///
/// 铁律约束（铁律6）:
/// - 渲染线程只做 GPU 绘制调用
/// - 不做 O(N) 计算
/// - 不分配大对象
/// - 不访问 SQLite
/// </remarks>
public sealed class D2DRenderTarget : IRenderTarget, IDisposable
{
    private readonly GraphicsDevice _graphicsDevice;
    private readonly SwapChainManager _swapChainManager;
    private readonly D2DDeviceManager _d2dDeviceManager;
    private readonly ResourceCache _resourceCache;

    private IntPtr _hwnd;
    private int _width;
    private int _height;
    private double _dpi;
    private bool _isDrawing;
    private bool _disposed;

    /// <inheritdoc/>
    public int Width => _width;

    /// <inheritdoc/>
    public int Height => _height;

    /// <inheritdoc/>
    public float DpiScale => (float)(_dpi / DpiHelper.StandardDpi);

    /// <inheritdoc/>
    public bool IsValid =>
        !_disposed &&
        _graphicsDevice.IsDeviceValid &&
        _swapChainManager.IsValid &&
        _d2dDeviceManager.IsValid;

    /// <summary>
    /// 当前 DPI 值。
    /// </summary>
    public double Dpi => _dpi;

    /// <summary>
    /// 获取 D2D 设备上下文。
    /// </summary>
    public Vortice.Direct2D1.ID2D1DeviceContext? D2DContext => _d2dDeviceManager.Context;

    /// <summary>
    /// 获取资源缓存。
    /// </summary>
    public ResourceCache Resources => _resourceCache;

    /// <summary>
    /// 设备丢失事件。
    /// </summary>
    public event Action? DeviceLost;

    /// <summary>
    /// 设备恢复事件。
    /// </summary>
    public event Action? DeviceRestored;

    /// <summary>
    /// DPI 变更事件。
    /// </summary>
    public event Action<double>? DpiChanged;

    /// <summary>
    /// 创建 D2DRenderTarget 实例。
    /// </summary>
    public D2DRenderTarget()
    {
        _graphicsDevice = new GraphicsDevice();
        _swapChainManager = new SwapChainManager(_graphicsDevice);
        _d2dDeviceManager = new D2DDeviceManager(_graphicsDevice);
        _resourceCache = new ResourceCache();
        _dpi = DpiHelper.StandardDpi;

        _graphicsDevice.DeviceLost += OnDeviceLost;
        _graphicsDevice.DeviceRestored += OnDeviceRestored;
    }

    /// <summary>
    /// 初始化渲染目标。
    /// </summary>
    /// <param name="hwnd">窗口句柄。</param>
    /// <param name="size">窗口大小（像素）。</param>
    /// <returns>如果初始化成功返回 true。</returns>
    public bool Initialize(IntPtr hwnd, Size size)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        _hwnd = hwnd;
        _width = Math.Max(1, size.Width);
        _height = Math.Max(1, size.Height);
        _dpi = DpiHelper.GetWindowDpi(hwnd);

        // 创建 D3D11 设备
        if (!_graphicsDevice.CreateDevice())
            return false;

        // 创建交换链
        if (!_swapChainManager.CreateSwapChain(hwnd, size))
            return false;

        // 创建 D2D 资源
        if (!_d2dDeviceManager.CreateD2DResources())
            return false;

        // 设置 D2D 渲染目标
        if (!_d2dDeviceManager.SetRenderTarget(_swapChainManager, _dpi))
            return false;

        // 初始化资源缓存
        if (_d2dDeviceManager.Context != null)
        {
            _resourceCache.Initialize(_d2dDeviceManager.Context);
        }

        return true;
    }

    /// <summary>
    /// 调整渲染目标大小。
    /// </summary>
    /// <param name="size">新大小（像素）。</param>
    /// <returns>如果调整成功返回 true。</returns>
    public bool Resize(Size size)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (!IsValid)
            return false;

        int newWidth = Math.Max(1, size.Width);
        int newHeight = Math.Max(1, size.Height);

        if (newWidth == _width && newHeight == _height)
            return true;

        _width = newWidth;
        _height = newHeight;

        // 清除 D2D 目标（必须在 Resize 前）
        if (_d2dDeviceManager.Context != null)
        {
            _d2dDeviceManager.Context.Target?.Dispose();
            _d2dDeviceManager.Context.Target = null;
        }

        // 调整交换链
        if (!_swapChainManager.Resize(size))
            return false;

        // 重建 D2D 渲染目标
        return _d2dDeviceManager.SetRenderTarget(_swapChainManager, _dpi);
    }

    /// <summary>
    /// 设置 DPI 值。
    /// </summary>
    /// <param name="dpi">新的 DPI 值。</param>
    public void SetDpi(double dpi)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (Math.Abs(dpi - _dpi) < 0.001)
            return;

        _dpi = dpi;

        if (_d2dDeviceManager.Context != null)
        {
            // Vortice 3.8+ uses SetDpi method
            _d2dDeviceManager.Context.SetDpi((float)dpi, (float)dpi);
        }

        DpiChanged?.Invoke(dpi);
    }

    /// <inheritdoc/>
    public void BeginDraw()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (!IsValid || _isDrawing)
            return;

        _d2dDeviceManager.Context!.BeginDraw();
        _isDrawing = true;
    }

    /// <inheritdoc/>
    public void EndDraw()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (!IsValid || !_isDrawing)
            return;

        _isDrawing = false;

        var result = _d2dDeviceManager.Context!.EndDraw();
        if (result.Failure)
        {
            // 设备丢失
            HandleDeviceLost();
            return;
        }

        // Present
        if (!_swapChainManager.Present(1))
        {
            // 设备丢失
            HandleDeviceLost();
        }
    }

    /// <inheritdoc/>
    public void HandleDeviceLost()
    {
        _isDrawing = false;
        _resourceCache.Clear();

        // 重建所有资源
        if (_graphicsDevice.HandleDeviceLost())
        {
            // 重建交换链
            _swapChainManager.CreateSwapChain(_hwnd, new Size(_width, _height));

            // 重建 D2D
            _d2dDeviceManager.CreateD2DResources();
            _d2dDeviceManager.SetRenderTarget(_swapChainManager, _dpi);

            // 重新初始化资源缓存
            if (_d2dDeviceManager.Context != null)
            {
                _resourceCache.Initialize(_d2dDeviceManager.Context);
            }
        }
    }

    private void OnDeviceLost()
    {
        DeviceLost?.Invoke();
    }

    private void OnDeviceRestored()
    {
        DeviceRestored?.Invoke();
    }

    /// <summary>
    /// 捕获当前交换链后缓冲区内容为 Bitmap。
    /// 必须在 EndDraw() 之后（Present 之后）调用。
    /// </summary>
    /// <returns>包含屏幕内容的 Bitmap，调用者负责 Dispose。如果捕获失败返回 null。</returns>
    public Bitmap? CaptureScreenshot()
    {
        if (!IsValid || _isDrawing)
            return null;

        var backBuffer = _swapChainManager.BackBuffer;
        var device = _graphicsDevice.Device;
        var context = _graphicsDevice.Context;

        if (backBuffer == null)
            return null;

        var desc = backBuffer.Description;

        // 创建 staging 纹理（CPU 可读）
        var stagingDesc = new Texture2DDescription
        {
            Width = desc.Width,
            Height = desc.Height,
            MipLevels = 1,
            ArraySize = 1,
            Format = desc.Format,  // B8G8R8A8_UNorm
            SampleDescription = new SampleDescription(1, 0),
            Usage = ResourceUsage.Staging,
            BindFlags = BindFlags.None,
            CPUAccessFlags = CpuAccessFlags.Read,
            MiscFlags = ResourceOptionFlags.None
        };

        ID3D11Texture2D? stagingTexture = null;
        try
        {
            stagingTexture = device.CreateTexture2D(stagingDesc);
            context.CopyResource(stagingTexture, backBuffer);

            var mapped = context.Map(stagingTexture, 0, MapMode.Read);
            try
            {
                var bitmap = new Bitmap((int)desc.Width, (int)desc.Height, PixelFormat.Format32bppArgb);
                var bmpData = bitmap.LockBits(
                    new Rectangle(0, 0, bitmap.Width, bitmap.Height),
                    ImageLockMode.WriteOnly,
                    PixelFormat.Format32bppArgb);

                try
                {
                    unsafe
                    {
                        byte* srcPtr = (byte*)mapped.DataPointer;
                        byte* dstPtr = (byte*)bmpData.Scan0;
                        int dstStride = bmpData.Stride;
                        int srcStride = (int)mapped.RowPitch;
                        int rowBytes = (int)desc.Width * 4;

                        for (int y = 0; y < (int)desc.Height; y++)
                        {
                            Buffer.MemoryCopy(
                                srcPtr + y * srcStride,
                                dstPtr + y * dstStride,
                                rowBytes, rowBytes);
                        }
                    }
                }
                finally
                {
                    bitmap.UnlockBits(bmpData);
                }

                return bitmap;
            }
            finally
            {
                context.Unmap(stagingTexture, 0);
            }
        }
        catch
        {
            return null;
        }
        finally
        {
            stagingTexture?.Dispose();
        }
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;

        _resourceCache.Dispose();
        _d2dDeviceManager.Dispose();
        _swapChainManager.Dispose();
        _graphicsDevice.Dispose();
    }
}
