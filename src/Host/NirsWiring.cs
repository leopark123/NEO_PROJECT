// NirsWiring.cs
// NIRS 模块装配 - S3-01: 集成壳接线与生命周期
//
// 依据: PROJECT_STATE.md S3-00 Blocked, ADR-015

using Neo.NIRS;

namespace Neo.Host;

/// <summary>
/// NIRS 模块装配（接线/DI/生命周期）。
/// </summary>
/// <remarks>
/// S3-01 范围:
/// - 注册 NirsIntegrationShell 到系统
/// - 管理 Start/Stop 生命周期
/// - 传播阻塞状态到上层
/// </remarks>
public sealed class NirsWiring : IDisposable
{
    private readonly NirsIntegrationShell _nirsShell;
    private bool _disposed;

    public NirsWiring()
    {
        _nirsShell = new NirsIntegrationShell();
    }

    /// <summary>
    /// NIRS 集成壳实例。
    /// </summary>
    public NirsIntegrationShell Shell => _nirsShell;

    /// <summary>
    /// NIRS 模块是否可用。
    /// </summary>
    public bool IsNirsAvailable => _nirsShell.IsAvailable;

    /// <summary>
    /// 启动 NIRS 模块。
    /// 当前仅注册阻塞状态。
    /// </summary>
    public void Start()
    {
        _nirsShell.Start();

        System.Diagnostics.Trace.TraceInformation(
            "[NirsWiring] NIRS module registered. Status: {0}",
            _nirsShell.Status);
    }

    /// <summary>
    /// 停止 NIRS 模块。
    /// </summary>
    public void Stop()
    {
        _nirsShell.Stop();
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            _nirsShell.Dispose();
            _disposed = true;
        }
    }
}
