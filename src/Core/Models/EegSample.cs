// EegSample.cs
// EEG 样本数据模型 - 来源: CONSENSUS_BASELINE.md §6.1, §6.2

namespace Neo.Core.Models;

using Neo.Core.Enums;

/// <summary>
/// 单个 EEG 样本。
/// 包含 4 通道数据及质量标志。
/// </summary>
/// <remarks>
/// 依据: CONSENSUS_BASELINE.md §6.1, §6.2
/// | 参数 | 值 |
/// |------|-----|
/// | 采样率 | 160 Hz |
/// | 通道数 | 4 (3物理+1计算) |
/// | 数据格式 | 0.076 μV/LSB |
///
/// 通道配置:
/// - CH1: C3-P3 (物理)
/// - CH2: C4-P4 (物理)
/// - CH3: P3-P4 (物理)
/// - CH4: C3-C4 (计算)
///
/// 时间戳语义（§5.3）：
/// 该样本的采集中心时刻，不是起始、不是结束。
/// </remarks>
public readonly record struct EegSample
{
    /// <summary>
    /// 样本时间戳（微秒）。
    /// 语义：该样本的采集中心时刻。
    /// </summary>
    public long TimestampUs { get; init; }

    /// <summary>
    /// 通道1 数据值（μV）。
    /// 导联: C3-P3 (A-B)，类型: Physical。
    /// </summary>
    public double Ch1Uv { get; init; }

    /// <summary>
    /// 通道2 数据值（μV）。
    /// 导联: C4-P4 (C-D)，类型: Physical。
    /// </summary>
    public double Ch2Uv { get; init; }

    /// <summary>
    /// 通道3 数据值（μV）。
    /// 导联: P3-P4 (B-C)，类型: Physical。
    /// </summary>
    public double Ch3Uv { get; init; }

    /// <summary>
    /// 通道4 数据值（μV）。
    /// 导联: C3-C4 (A-D)，类型: Computed。
    /// </summary>
    public double Ch4Uv { get; init; }

    /// <summary>
    /// 各通道质量标志。
    /// 4 字节对应 4 通道。
    /// </summary>
    public QualityFlag QualityFlags { get; init; }
}
