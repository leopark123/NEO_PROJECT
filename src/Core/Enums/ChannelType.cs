// ChannelType.cs
// 通道类型定义 - 来源: CONSENSUS_BASELINE.md §6.2, ARCHITECTURE.md §2.1

namespace Neo.Core.Enums;

/// <summary>
/// EEG 通道类型。
/// </summary>
/// <remarks>
/// 依据: CONSENSUS_BASELINE.md §6.2
/// CH1-CH3 为物理采集通道，CH4 为计算通道 (A-D)
/// </remarks>
public enum ChannelType
{
    /// <summary>
    /// 物理采集通道（CH1-CH3）。
    /// 直接来自 EEG 采集设备。
    /// </summary>
    Physical = 0,

    /// <summary>
    /// 计算通道（CH4 = A-D）。
    /// 由其他通道计算得出。
    /// </summary>
    Computed = 1
}
