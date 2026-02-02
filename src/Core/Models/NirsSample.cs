// NirsSample.cs
// NIRS 样本数据模型 - 来源: CONSENSUS_BASELINE.md §6.5

namespace Neo.Core.Models;

using Neo.Core.Enums;

/// <summary>
/// 单个 NIRS 样本。
/// 包含 6 通道组织氧饱和度数据。
/// </summary>
/// <remarks>
/// 依据: CONSENSUS_BASELINE.md §6.5
/// | 参数 | 值 |
/// |------|-----|
/// | 通道数 | 6 |
/// | 采样率 | 1-4 Hz |
/// | 值域 | 0-100% |
///
/// ADR-013 约束：
/// NIRS 阈值/单位禁止软件推断，当前标注 TBD，
/// 待设备规格确认后通过配置加载。
///
/// 时间戳语义（§5.3）：
/// 该样本的采集中心时刻。
/// </remarks>
public readonly record struct NirsSample
{
    /// <summary>
    /// 样本时间戳（微秒）。
    /// 语义：该样本的采集中心时刻。
    /// </summary>
    public long TimestampUs { get; init; }

    /// <summary>
    /// 通道1 SpO2 值（%）。
    /// 范围: 0-100。
    /// </summary>
    public double Ch1Percent { get; init; }

    /// <summary>
    /// 通道2 SpO2 值（%）。
    /// </summary>
    public double Ch2Percent { get; init; }

    /// <summary>
    /// 通道3 SpO2 值（%）。
    /// </summary>
    public double Ch3Percent { get; init; }

    /// <summary>
    /// 通道4 SpO2 值（%）。
    /// </summary>
    public double Ch4Percent { get; init; }

    /// <summary>
    /// 通道5 SpO2 值（%）。
    /// </summary>
    public double Ch5Percent { get; init; }

    /// <summary>
    /// 通道6 SpO2 值（%）。
    /// </summary>
    public double Ch6Percent { get; init; }

    /// <summary>
    /// 各通道有效性位掩码。
    /// bit0-bit5 对应 Ch1-Ch6，1=有效，0=无效。
    /// </summary>
    public byte ValidMask { get; init; }
}
