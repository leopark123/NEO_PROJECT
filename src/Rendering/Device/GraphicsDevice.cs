// GraphicsDevice.cs
// D3D11 设备管理 - 来源: ARCHITECTURE.md §5, ADR-002 (Vortice)

using System.Diagnostics.CodeAnalysis;
using Vortice.Direct3D;
using Vortice.Direct3D11;
using Vortice.DXGI;

namespace Neo.Rendering.Device;

/// <summary>
/// D3D11 图形设备管理器。
/// 负责 Device 创建、Feature Level 协商和资源管理。
/// </summary>
/// <remarks>
/// 依据: ARCHITECTURE.md §5, ADR-002 (Vortice 渲染引擎)
///
/// 支持的 Feature Level（按优先级降序）:
/// - D3D_FEATURE_LEVEL_11_1
/// - D3D_FEATURE_LEVEL_11_0
/// - D3D_FEATURE_LEVEL_10_1
/// - D3D_FEATURE_LEVEL_10_0
/// </remarks>
public sealed class GraphicsDevice : IDisposable
{
    private static readonly FeatureLevel[] SupportedFeatureLevels =
    [
        FeatureLevel.Level_11_1,
        FeatureLevel.Level_11_0,
        FeatureLevel.Level_10_1,
        FeatureLevel.Level_10_0
    ];

    private ID3D11Device? _device;
    private ID3D11DeviceContext? _context;
    private IDXGIFactory2? _dxgiFactory;
    private bool _disposed;

    /// <summary>
    /// 获取 D3D11 设备实例。
    /// </summary>
    /// <exception cref="InvalidOperationException">设备未创建或已丢失。</exception>
    public ID3D11Device Device => _device ?? throw new InvalidOperationException("Device not created or lost.");

    /// <summary>
    /// 获取 D3D11 设备上下文。
    /// </summary>
    /// <exception cref="InvalidOperationException">设备未创建或已丢失。</exception>
    public ID3D11DeviceContext Context => _context ?? throw new InvalidOperationException("Device not created or lost.");

    /// <summary>
    /// 获取 DXGI 工厂。
    /// </summary>
    /// <exception cref="InvalidOperationException">设备未创建或已丢失。</exception>
    public IDXGIFactory2 DxgiFactory => _dxgiFactory ?? throw new InvalidOperationException("Device not created or lost.");

    /// <summary>
    /// 获取当前 Feature Level。
    /// </summary>
    public FeatureLevel FeatureLevel { get; private set; }

    /// <summary>
    /// 设备是否有效可用。
    /// </summary>
    [MemberNotNullWhen(true, nameof(_device), nameof(_context), nameof(_dxgiFactory))]
    public bool IsDeviceValid => _device != null && _context != null && _dxgiFactory != null && !_disposed;

    /// <summary>
    /// 设备丢失事件。
    /// </summary>
    public event Action? DeviceLost;

    /// <summary>
    /// 设备恢复事件。
    /// </summary>
    public event Action? DeviceRestored;

    /// <summary>
    /// 创建 D3D11 设备和 DXGI 工厂。
    /// </summary>
    /// <returns>如果创建成功返回 true。</returns>
    public bool CreateDevice()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        ReleaseDeviceResources();

        try
        {
            // 创建 DXGI Factory
            DXGI.CreateDXGIFactory1(out _dxgiFactory).CheckError();

            // 创建 D3D11 设备
            DeviceCreationFlags flags = DeviceCreationFlags.BgraSupport;
#if DEBUG
            flags |= DeviceCreationFlags.Debug;
#endif

            D3D11.D3D11CreateDevice(
                adapter: null,
                DriverType.Hardware,
                flags,
                SupportedFeatureLevels,
                out _device,
                out var featureLevel,
                out _context).CheckError();
            FeatureLevel = featureLevel;

            return true;
        }
        catch
        {
            ReleaseDeviceResources();
            return false;
        }
    }

    /// <summary>
    /// 检测设备是否丢失。
    /// </summary>
    /// <returns>如果设备已丢失返回 true。</returns>
    public bool CheckDeviceLost()
    {
        if (_device == null)
            return true;

        var reason = _device.DeviceRemovedReason;
        return reason.Failure;
    }

    /// <summary>
    /// 处理设备丢失，尝试重建设备。
    /// </summary>
    /// <returns>如果恢复成功返回 true。</returns>
    public bool HandleDeviceLost()
    {
        DeviceLost?.Invoke();

        ReleaseDeviceResources();

        if (CreateDevice())
        {
            DeviceRestored?.Invoke();
            return true;
        }

        return false;
    }

    /// <summary>
    /// 释放设备相关资源。
    /// </summary>
    private void ReleaseDeviceResources()
    {
        _context?.Dispose();
        _context = null;

        _device?.Dispose();
        _device = null;

        _dxgiFactory?.Dispose();
        _dxgiFactory = null;
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
        ReleaseDeviceResources();
    }
}
