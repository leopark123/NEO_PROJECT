// D2DDeviceManager.cs
// Direct2D 设备管理 - 来源: ARCHITECTURE.md §5, ADR-002 (Vortice)

using System.Diagnostics.CodeAnalysis;
using Vortice.Direct2D1;
using Vortice.Direct3D11;
using Vortice.DXGI;

namespace Neo.Rendering.Device;

/// <summary>
/// Direct2D 设备管理器。
/// 负责 D2D Factory、D2D Device 和 D2D DeviceContext 的创建与管理。
/// </summary>
/// <remarks>
/// 依据: ARCHITECTURE.md §5, ADR-002 (Vortice 渲染引擎)
///
/// 架构:
/// D3D11 Device → DXGI Device → D2D Device → D2D DeviceContext
/// </remarks>
public sealed class D2DDeviceManager : IDisposable
{
    private readonly GraphicsDevice _graphicsDevice;
    private ID2D1Factory1? _d2dFactory;
    private ID2D1Device? _d2dDevice;
    private ID2D1DeviceContext? _d2dContext;
    private bool _disposed;

    /// <summary>
    /// 获取 D2D 工厂。
    /// </summary>
    public ID2D1Factory1? Factory => _d2dFactory;

    /// <summary>
    /// 获取 D2D 设备。
    /// </summary>
    public ID2D1Device? Device => _d2dDevice;

    /// <summary>
    /// 获取 D2D 设备上下文。
    /// </summary>
    public ID2D1DeviceContext? Context => _d2dContext;

    /// <summary>
    /// D2D 是否有效。
    /// </summary>
    [MemberNotNullWhen(true, nameof(_d2dFactory), nameof(_d2dDevice), nameof(_d2dContext))]
    public bool IsValid => _d2dFactory != null && _d2dDevice != null && _d2dContext != null && !_disposed;

    /// <summary>
    /// 创建 D2DDeviceManager 实例。
    /// </summary>
    /// <param name="graphicsDevice">图形设备。</param>
    /// <exception cref="ArgumentNullException">graphicsDevice 为 null。</exception>
    public D2DDeviceManager(GraphicsDevice graphicsDevice)
    {
        _graphicsDevice = graphicsDevice ?? throw new ArgumentNullException(nameof(graphicsDevice));
    }

    /// <summary>
    /// 创建 D2D 资源。
    /// </summary>
    /// <returns>如果创建成功返回 true。</returns>
    public bool CreateD2DResources()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (!_graphicsDevice.IsDeviceValid)
            return false;

        ReleaseD2DResources();

        try
        {
            // 创建 D2D Factory
            var factoryOptions = new FactoryOptions
            {
#if DEBUG
                DebugLevel = DebugLevel.Information
#else
                DebugLevel = DebugLevel.None
#endif
            };

            _d2dFactory = D2D1.D2D1CreateFactory<ID2D1Factory1>(FactoryType.SingleThreaded, factoryOptions);

            // 获取 DXGI Device
            using var dxgiDevice = _graphicsDevice.Device.QueryInterface<IDXGIDevice>();

            // 创建 D2D Device
            _d2dDevice = _d2dFactory.CreateDevice(dxgiDevice);

            // 创建 D2D DeviceContext
            _d2dContext = _d2dDevice.CreateDeviceContext(DeviceContextOptions.None);

            return true;
        }
        catch
        {
            ReleaseD2DResources();
            return false;
        }
    }

    /// <summary>
    /// 设置渲染目标为交换链后缓冲区。
    /// </summary>
    /// <param name="swapChainManager">交换链管理器。</param>
    /// <param name="dpi">DPI 值。</param>
    /// <returns>如果设置成功返回 true。</returns>
    public bool SetRenderTarget(SwapChainManager swapChainManager, double dpi)
    {
        if (!IsValid || swapChainManager.SwapChain == null)
            return false;

        try
        {
            // 获取后缓冲区的 DXGI Surface
            using var surface = swapChainManager.SwapChain.GetBuffer<IDXGISurface>(0);

            // 创建位图属性
            var bitmapProperties = new BitmapProperties1
            {
                PixelFormat = new Vortice.DCommon.PixelFormat(Format.B8G8R8A8_UNorm, Vortice.DCommon.AlphaMode.Premultiplied),
                DpiX = (float)dpi,
                DpiY = (float)dpi,
                BitmapOptions = BitmapOptions.Target | BitmapOptions.CannotDraw
            };

            // 创建 D2D 位图
            var bitmap = _d2dContext.CreateBitmapFromDxgiSurface(surface, bitmapProperties);

            // 设置渲染目标
            _d2dContext.Target = bitmap;

            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// 释放 D2D 资源。
    /// </summary>
    private void ReleaseD2DResources()
    {
        if (_d2dContext != null)
        {
            _d2dContext.Target = null;
            _d2dContext.Dispose();
            _d2dContext = null;
        }

        _d2dDevice?.Dispose();
        _d2dDevice = null;

        _d2dFactory?.Dispose();
        _d2dFactory = null;
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
        ReleaseD2DResources();
    }
}
