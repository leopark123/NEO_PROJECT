// QualityFlag.cs
// 数据质量标志 - 来源: 00_CONSTITUTION.md 铁律5, CONSENSUS_BASELINE.md 铁律5

namespace Neo.Core.Enums;

/// <summary>
/// 数据质量标志（位掩码）。
/// 用于标记样本的质量状态。
/// </summary>
/// <remarks>
/// 依据: 00_CONSTITUTION.md 铁律5 "缺失/饱和必须可见"
/// - 数据缺失 → 波形断裂 + 灰色遮罩
/// - 信号饱和 → 削顶显示 + 视觉标记
/// - 电极脱落 → 明确告警
/// </remarks>
[Flags]
public enum QualityFlag : byte
{
    /// <summary>
    /// 数据正常，无质量问题。
    /// </summary>
    Normal = 0,

    /// <summary>
    /// 数据缺失（Gap）。
    /// 依据铁律2: Gap > 4样本(25ms@160Hz) 必须断裂显示。
    /// </summary>
    Missing = 1 << 0,

    /// <summary>
    /// 信号饱和（Clipping）。
    /// 达到 ADC 量程上限或下限。
    /// </summary>
    Saturated = 1 << 1,

    /// <summary>
    /// 电极脱落。
    /// 阻抗异常或无信号。
    /// </summary>
    LeadOff = 1 << 2,

    /// <summary>
    /// 数据已插值填充。
    /// 仅限 Gap ≤ 4 样本时可选使用，必须标记。
    /// </summary>
    Interpolated = 1 << 3,

    /// <summary>
    /// 字段无文档证据支持。
    /// 依据: 证据导向原则，未证实的字段必须标记。
    /// 值设为 NaN，下游应忽略或等待新证据。
    /// </summary>
    Undocumented = 1 << 4,

    /// <summary>
    /// 滤波器预热期间的瞬态数据。
    /// 依据: DSP_SPEC.md §7 - 冷启动期间滤波器输出不稳定。
    /// 下游应降低显示优先级或标注。
    /// </summary>
    Transient = 1 << 5,

    /// <summary>
    /// 模块被规格证据阻塞，无法提供有效数据。
    /// 依据: ADR-015, PROJECT_STATE.md S3-00 Blocked。
    /// 所有数值应为 NaN，下游不得显示伪数据。
    /// </summary>
    BlockedBySpec = 1 << 6
}
