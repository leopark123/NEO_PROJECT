// ClockDomain.cs
// 时钟域定义 - 来源: CONSENSUS_BASELINE.md §5.2

namespace Neo.Core.Enums;

/// <summary>
/// 时钟域标识。
/// 标记数据时间戳的来源，用于同步对齐。
/// </summary>
/// <remarks>
/// 依据: CONSENSUS_BASELINE.md §5.2
/// 当前状态: 采用 Host 时钟域（ADR-010/ADR-012）
/// </remarks>
public enum ClockDomain
{
    /// <summary>
    /// 设备硬件时钟（最高精度）。
    /// 当前未使用 - 依据 ADR-012 统一采用主机打点。
    /// </summary>
    Device = 0,

    /// <summary>
    /// 主机单调时钟（当前使用）。
    /// 所有数据源（EEG/NIRS/Video）统一使用此时钟域。
    /// </summary>
    Host = 1,

    /// <summary>
    /// 时钟来源不可靠或未知。
    /// </summary>
    Unknown = 2
}
