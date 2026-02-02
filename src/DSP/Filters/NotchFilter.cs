// NotchFilter.cs
// 陷波滤波器 - 来源: DSP_SPEC.md §2.4

namespace Neo.DSP.Filters;

/// <summary>
/// 陷波滤波器频率选项。
/// </summary>
/// <remarks>
/// 依据: DSP_SPEC.md §2.1
/// 可选值: 50, 60 Hz
/// 默认值: 50 Hz
/// </remarks>
public enum NotchFrequency
{
    /// <summary>50 Hz (默认)</summary>
    Hz50,

    /// <summary>60 Hz</summary>
    Hz60
}

/// <summary>
/// IIR 陷波滤波器（二阶）。
/// </summary>
/// <remarks>
/// 依据: DSP_SPEC.md §2.4
///
/// 设计参数:
/// - 类型: IIR Notch (二阶)
/// - Q 因子: 30
/// - 采样率: 160 Hz
///
/// 系数来源: DSP_SPEC.md §2.4 (SOS格式，double精度)
/// </remarks>
public sealed class NotchFilter : IirFilterBase
{
    /// <summary>当前频率设置</summary>
    public NotchFrequency Frequency { get; }

    private NotchFilter(SosSection[] sections, double gain, NotchFrequency frequency)
        : base(sections, gain)
    {
        Frequency = frequency;
    }

    /// <summary>
    /// 创建陷波滤波器。
    /// </summary>
    /// <param name="frequency">陷波频率</param>
    /// <returns>NotchFilter 实例</returns>
    public static NotchFilter Create(NotchFrequency frequency = NotchFrequency.Hz50)
    {
        var (sections, gain) = GetCoefficients(frequency);
        return new NotchFilter(sections, gain, frequency);
    }

    /// <summary>
    /// 获取滤波器系数。
    /// </summary>
    /// <remarks>
    /// 系数来源: DSP_SPEC.md §2.4
    /// 设计参数: Q=30, fs=160Hz
    ///
    /// ⚠️ 警告: 这些系数直接来自 DSP_SPEC.md，未经验证。
    /// 如发现频率响应异常，应提交规格修订请求。
    /// </remarks>
    private static (SosSection[] sections, double gain) GetCoefficients(NotchFrequency frequency)
    {
        return frequency switch
        {
            // Notch_50Hz (DSP_SPEC.md §2.4)
            // Center frequency: 50 Hz, Bandwidth: 50/30 = 1.67 Hz
            // SOS: [b0, b1, b2, a0, a1, a2] = [0.98968574, -0.22460795, 0.98968574, 1.0, -0.22460795, 0.97937149]
            NotchFrequency.Hz50 => (
                new[]
                {
                    new SosSection(
                        b0: 0.98968574,
                        b1: -0.22460795,
                        b2: 0.98968574,
                        a1: -0.22460795,
                        a2: 0.97937149)
                },
                gain: 1.0
            ),

            // Notch_60Hz (DSP_SPEC.md §2.4)
            // Center frequency: 60 Hz, Bandwidth: 60/30 = 2.0 Hz
            // SOS: [b0, b1, b2, a0, a1, a2] = [0.98968574, 0.22460795, 0.98968574, 1.0, 0.22460795, 0.97937149]
            NotchFrequency.Hz60 => (
                new[]
                {
                    new SosSection(
                        b0: 0.98968574,
                        b1: 0.22460795,
                        b2: 0.98968574,
                        a1: 0.22460795,
                        a2: 0.97937149)
                },
                gain: 1.0
            ),

            _ => throw new ArgumentOutOfRangeException(nameof(frequency))
        };
    }

    /// <summary>
    /// 预热样本数。
    /// </summary>
    /// <remarks>
    /// 依据: DSP_SPEC.md §7.1
    /// Notch: 0.1 sec = 16 samples @ 160Hz
    /// </remarks>
    public static int WarmupSamples => 16;

    /// <summary>
    /// 预热时间（秒）。
    /// </summary>
    public static double WarmupSeconds => 0.1;
}
