// DataQuality.cs
// 数据质量信息模型 - 来源: 00_CONSTITUTION.md 铁律5

namespace Neo.Core.Models;

using Neo.Core.Enums;

/// <summary>
/// 数据质量描述。
/// 用于附加到样本或数据块上的质量元信息。
/// </summary>
/// <remarks>
/// 依据: 00_CONSTITUTION.md 铁律5
/// 缺失/饱和必须可见:
/// - 数据缺失 → 波形断裂 + 灰色遮罩
/// - 信号饱和 → 削顶显示 + 视觉标记
/// - 电极脱落 → 明确告警
/// </remarks>
public readonly record struct DataQuality
{
    /// <summary>
    /// 质量标志组合。
    /// </summary>
    public QualityFlag Flags { get; init; }

    /// <summary>
    /// 检查是否正常（无质量问题）。
    /// </summary>
    public bool IsNormal => Flags == QualityFlag.Normal;

    /// <summary>
    /// 检查是否存在数据缺失。
    /// </summary>
    public bool HasMissing => (Flags & QualityFlag.Missing) != 0;

    /// <summary>
    /// 检查是否存在信号饱和。
    /// </summary>
    public bool HasSaturation => (Flags & QualityFlag.Saturated) != 0;

    /// <summary>
    /// 检查是否存在电极脱落。
    /// </summary>
    public bool HasLeadOff => (Flags & QualityFlag.LeadOff) != 0;

    /// <summary>
    /// 检查是否包含插值数据。
    /// </summary>
    public bool HasInterpolation => (Flags & QualityFlag.Interpolated) != 0;

    /// <summary>
    /// 创建正常质量状态。
    /// </summary>
    public static DataQuality Normal => new() { Flags = QualityFlag.Normal };
}
