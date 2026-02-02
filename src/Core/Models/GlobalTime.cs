// GlobalTime.cs
// 统一时间模型 - 来源: CONSENSUS_BASELINE.md §5, 00_CONSTITUTION.md 铁律11

namespace Neo.Core.Models;

using Neo.Core.Enums;

/// <summary>
/// 全局统一时间戳。
/// 系统内所有时间表示的唯一标准。
/// </summary>
/// <remarks>
/// 依据: CONSENSUS_BASELINE.md §5.1
/// | 属性 | 规格 |
/// |------|------|
/// | 数据类型 | int64 |
/// | 单位 | 微秒 (μs) |
/// | 特性 | 单调递增 |
/// | 范围 | 0 ~ 2^63-1 (约292,000年) |
/// | 纪元 | 监护开始时刻 = 0 |
///
/// 时间戳语义（§5.3）：
/// 所有时间戳表示"样本或窗口的中心时间"，不是起始、不是结束。
///
/// 时钟域（ADR-012）：
/// 当前采用 Host 时钟域，主机打点。
/// </remarks>
public readonly record struct GlobalTime
{
    /// <summary>
    /// 时间戳值，单位：微秒 (μs)。
    /// 相对于监护开始时刻（纪元 = 0）。
    /// </summary>
    public long TimestampUs { get; init; }

    /// <summary>
    /// 时钟域标识。
    /// 依据 ADR-012，当前统一使用 Host。
    /// </summary>
    public ClockDomain ClockDomain { get; init; }

    /// <summary>
    /// 创建一个 GlobalTime 实例。
    /// </summary>
    /// <param name="timestampUs">时间戳（微秒）</param>
    /// <param name="clockDomain">时钟域</param>
    public GlobalTime(long timestampUs, ClockDomain clockDomain = ClockDomain.Host)
    {
        TimestampUs = timestampUs;
        ClockDomain = clockDomain;
    }

    /// <summary>
    /// 零时刻（监护开始）。
    /// </summary>
    public static GlobalTime Zero => new(0, ClockDomain.Host);
}
