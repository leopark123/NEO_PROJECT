// NirsIntegrationShell.cs
// NIRS 集成壳 - S3-01: 仅做系统层集成位，不实现协议
//
// 依据: PROJECT_STATE.md S3-00 Blocked, ADR-015

using Neo.Core.Enums;
using Neo.Core.Models;

namespace Neo.NIRS;

/// <summary>
/// NIRS 集成壳的状态。
/// </summary>
public enum NirsShellStatus
{
    /// <summary>
    /// 模块被规格证据阻塞，无法运行。
    /// </summary>
    BlockedByMissingEvidence,

    /// <summary>
    /// 模块已就绪（未来解锁后使用）。
    /// </summary>
    Ready
}

/// <summary>
/// NIRS 集成壳。
/// 仅提供系统层注册位，不实现任何协议或算法。
/// </summary>
/// <remarks>
/// S3-01 范围:
/// - 类型存在、接线存在、生命周期存在
/// - 系统在没有 NIRS 实现的情况下可运行
/// - 所有 NIRS 数值为 double.NaN
/// - 质量标志: QualityFlag.Undocumented | QualityFlag.BlockedBySpec
/// </remarks>
public sealed class NirsIntegrationShell : IDisposable
{
    private bool _disposed;

    /// <summary>
    /// 当前模块状态。
    /// </summary>
    public NirsShellStatus Status { get; private set; } = NirsShellStatus.BlockedByMissingEvidence;

    /// <summary>
    /// 阻塞原因描述。
    /// </summary>
    public string BlockReason => "NIRS is blocked by missing protocol evidence (S3-00).";

    /// <summary>
    /// 模块是否可用。
    /// </summary>
    public bool IsAvailable => Status == NirsShellStatus.Ready;

    /// <summary>
    /// 启动 NIRS 集成壳。
    /// 当前仅记录阻塞状态，不启动任何数据采集。
    /// </summary>
    public void Start()
    {
        Status = NirsShellStatus.BlockedByMissingEvidence;
        System.Diagnostics.Trace.TraceWarning(
            "[NIRS] " + BlockReason);
    }

    /// <summary>
    /// 停止 NIRS 集成壳。
    /// </summary>
    public void Stop()
    {
    }

    /// <summary>
    /// 创建一个表示阻塞状态的 NIRS 样本。
    /// 所有通道值为 NaN，质量标志为 BlockedBySpec | Undocumented。
    /// </summary>
    /// <param name="timestampUs">主机时间戳（微秒），仅用于排序。</param>
    public static NirsSample CreateBlockedSample(long timestampUs)
    {
        return new NirsSample
        {
            TimestampUs = timestampUs,
            Ch1Percent = double.NaN,
            Ch2Percent = double.NaN,
            Ch3Percent = double.NaN,
            Ch4Percent = double.NaN,
            Ch5Percent = double.NaN,
            Ch6Percent = double.NaN,
            ValidMask = 0
        };
    }

    /// <summary>
    /// 获取阻塞状态下的质量标志。
    /// </summary>
    public static QualityFlag BlockedQualityFlags =>
        QualityFlag.Undocumented | QualityFlag.BlockedBySpec;

    public void Dispose()
    {
        if (!_disposed)
        {
            Stop();
            _disposed = true;
        }
    }
}
