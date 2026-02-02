// SwapChainManager.cs
// 交换链管理 - 来源: ARCHITECTURE.md §5, ADR-002 (Vortice)

using System.Diagnostics.CodeAnalysis;
using System.Drawing;
using Vortice.Direct3D11;
using Vortice.DXGI;

namespace Neo.Rendering.Device;

/// <summary>
/// DXGI 交换链管理器。
/// 负责 SwapChain 创建、窗口绑定、缓冲区重建和 Present。
/// </summary>
/// <remarks>
/// 依据: ARCHITECTURE.md §5, ADR-002 (Vortice 渲染引擎)
///
/// 功能:
/// - HWND 绑定
/// - 缓冲区大小调整
/// - Present 操作
/// - 设备丢失检测
/// </remarks>
public sealed class SwapChainManager : IDisposable
{
    private readonly GraphicsDevice _graphicsDevice;
    private IDXGISwapChain1? _swapChain;
    private ID3D11Texture2D? _backBuffer;
    private ID3D11RenderTargetView? _renderTargetView;
    private bool _disposed;

    private IntPtr _hwnd;
    private int _width;
    private int _height;

    /// <summary>
    /// 获取交换链实例。
    /// </summary>
    public IDXGISwapChain1? SwapChain => _swapChain;

    /// <summary>
    /// 获取后缓冲区纹理。
    /// </summary>
    public ID3D11Texture2D? BackBuffer => _backBuffer;

    /// <summary>
    /// 获取渲染目标视图。
    /// </summary>
    public ID3D11RenderTargetView? RenderTargetView => _renderTargetView;

    /// <summary>
    /// 交换链是否有效。
    /// </summary>
    [MemberNotNullWhen(true, nameof(_swapChain), nameof(_backBuffer), nameof(_renderTargetView))]
    public bool IsValid => _swapChain != null && _backBuffer != null && _renderTargetView != null && !_disposed;

    /// <summary>
    /// 当前宽度（像素）。
    /// </summary>
    public int Width => _width;

    /// <summary>
    /// 当前高度（像素）。
    /// </summary>
    public int Height => _height;

    /// <summary>
    /// 创建 SwapChainManager 实例。
    /// </summary>
    /// <param name="graphicsDevice">图形设备。</param>
    /// <exception cref="ArgumentNullException">graphicsDevice 为 null。</exception>
    public SwapChainManager(GraphicsDevice graphicsDevice)
    {
        _graphicsDevice = graphicsDevice ?? throw new ArgumentNullException(nameof(graphicsDevice));
    }

    /// <summary>
    /// 创建交换链并绑定到窗口句柄。
    /// </summary>
    /// <param name="hwnd">窗口句柄。</param>
    /// <param name="size">初始大小（像素）。</param>
    /// <returns>如果创建成功返回 true。</returns>
    public bool CreateSwapChain(IntPtr hwnd, Size size)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (!_graphicsDevice.IsDeviceValid)
            return false;

        ReleaseSwapChainResources();

        _hwnd = hwnd;
        _width = Math.Max(1, size.Width);
        _height = Math.Max(1, size.Height);

        try
        {
            var swapChainDesc = new SwapChainDescription1
            {
                Width = (uint)_width,
                Height = (uint)_height,
                Format = Format.B8G8R8A8_UNorm,
                SampleDescription = new SampleDescription(1, 0),
                BufferUsage = Usage.RenderTargetOutput,
                BufferCount = 2,
                SwapEffect = SwapEffect.FlipDiscard,
                Scaling = Scaling.Stretch,
                AlphaMode = AlphaMode.Ignore,
                Flags = SwapChainFlags.None
            };

            _swapChain = _graphicsDevice.DxgiFactory.CreateSwapChainForHwnd(
                _graphicsDevice.Device,
                _hwnd,
                swapChainDesc);

            // 禁用 Alt+Enter 全屏切换
            _graphicsDevice.DxgiFactory.MakeWindowAssociation(_hwnd, WindowAssociationFlags.IgnoreAltEnter);

            return CreateRenderTargetView();
        }
        catch
        {
            ReleaseSwapChainResources();
            return false;
        }
    }

    /// <summary>
    /// 调整交换链大小。
    /// </summary>
    /// <param name="size">新大小（像素）。</param>
    /// <returns>如果调整成功返回 true。</returns>
    public bool Resize(Size size)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (_swapChain == null)
            return false;

        int newWidth = Math.Max(1, size.Width);
        int newHeight = Math.Max(1, size.Height);

        if (newWidth == _width && newHeight == _height)
            return true;

        _width = newWidth;
        _height = newHeight;

        // 释放现有渲染目标视图和后缓冲区
        _renderTargetView?.Dispose();
        _renderTargetView = null;
        _backBuffer?.Dispose();
        _backBuffer = null;

        try
        {
            // 重建交换链缓冲区
            _swapChain.ResizeBuffers(
                0,
                (uint)_width,
                (uint)_height,
                Format.Unknown,
                SwapChainFlags.None).CheckError();

            return CreateRenderTargetView();
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// 呈现帧到屏幕。
    /// </summary>
    /// <param name="syncInterval">垂直同步间隔（0=不等待，1=等待1个VSync）。</param>
    /// <returns>如果呈现成功返回 true，如果设备丢失返回 false。</returns>
    public bool Present(int syncInterval = 1)
    {
        if (_swapChain == null)
            return false;

        var result = _swapChain.Present((uint)syncInterval, PresentFlags.None);

        // 检测设备丢失
        if (result.Failure)
        {
            if (result == Vortice.DXGI.ResultCode.DeviceRemoved ||
                result == Vortice.DXGI.ResultCode.DeviceReset)
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// 创建渲染目标视图。
    /// </summary>
    private bool CreateRenderTargetView()
    {
        if (_swapChain == null || !_graphicsDevice.IsDeviceValid)
            return false;

        try
        {
            _backBuffer = _swapChain.GetBuffer<ID3D11Texture2D>(0);
            _renderTargetView = _graphicsDevice.Device.CreateRenderTargetView(_backBuffer);
            return true;
        }
        catch
        {
            _backBuffer?.Dispose();
            _backBuffer = null;
            _renderTargetView?.Dispose();
            _renderTargetView = null;
            return false;
        }
    }

    /// <summary>
    /// 释放交换链相关资源。
    /// </summary>
    private void ReleaseSwapChainResources()
    {
        _renderTargetView?.Dispose();
        _renderTargetView = null;

        _backBuffer?.Dispose();
        _backBuffer = null;

        _swapChain?.Dispose();
        _swapChain = null;
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
        ReleaseSwapChainResources();
    }
}
