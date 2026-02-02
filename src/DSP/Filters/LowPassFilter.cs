// LowPassFilter.cs
// 低通滤波器 - 来源: DSP_SPEC.md §2.3

namespace Neo.DSP.Filters;

/// <summary>
/// 低通滤波器截止频率选项。
/// </summary>
/// <remarks>
/// 依据: DSP_SPEC.md §2.1
/// 可选值: 15, 35, 50, 70 Hz
/// 默认值: 35 Hz
/// </remarks>
public enum LowPassCutoff
{
    /// <summary>15 Hz</summary>
    Hz15,

    /// <summary>35 Hz (默认)</summary>
    Hz35,

    /// <summary>50 Hz</summary>
    Hz50,

    /// <summary>70 Hz</summary>
    Hz70
}

/// <summary>
/// Butterworth IIR 低通滤波器（四阶）。
/// </summary>
/// <remarks>
/// 依据: DSP_SPEC.md §2.3
///
/// 设计参数:
/// - 类型: Butterworth
/// - 阶数: 4 (2 个 SOS 节级联)
/// - 采样率: 160 Hz
///
/// 系数来源: DSP_SPEC.md §2.3 (SOS格式，double精度)
/// </remarks>
public sealed class LowPassFilter : IirFilterBase
{
    /// <summary>当前截止频率设置</summary>
    public LowPassCutoff Cutoff { get; }

    private LowPassFilter(SosSection[] sections, double gain, LowPassCutoff cutoff)
        : base(sections, gain)
    {
        Cutoff = cutoff;
    }

    /// <summary>
    /// 创建低通滤波器。
    /// </summary>
    /// <param name="cutoff">截止频率</param>
    /// <returns>LowPassFilter 实例</returns>
    public static LowPassFilter Create(LowPassCutoff cutoff = LowPassCutoff.Hz35)
    {
        var (sections, gain) = GetCoefficients(cutoff);
        return new LowPassFilter(sections, gain, cutoff);
    }

    /// <summary>
    /// 获取滤波器系数。
    /// </summary>
    /// <remarks>
    /// 系数来源: DSP_SPEC.md §2.3
    /// 设计参数: Butterworth 4阶, fs=160Hz
    /// 注意: 4阶滤波器由 2 个 SOS 节级联实现
    /// </remarks>
    private static (SosSection[] sections, double gain) GetCoefficients(LowPassCutoff cutoff)
    {
        return cutoff switch
        {
            // LPF_15Hz (DSP_SPEC.md §2.3)
            // Normalized frequency: 15 / 80 = 0.1875
            LowPassCutoff.Hz15 => (
                new[]
                {
                    new SosSection(
                        b0: 1.0,
                        b1: 2.0,
                        b2: 1.0,
                        a1: -0.87727063,
                        a2: 0.42650599),
                    new SosSection(
                        b0: 1.0,
                        b1: 2.0,
                        b2: 1.0,
                        a1: -0.63208028,
                        a2: 0.17953611)
                },
                gain: 0.02952402
            ),

            // LPF_35Hz (DSP_SPEC.md §2.3) - 默认
            // Normalized frequency: 35 / 80 = 0.4375
            LowPassCutoff.Hz35 => (
                new[]
                {
                    new SosSection(
                        b0: 1.0,
                        b1: 2.0,
                        b2: 1.0,
                        a1: -0.17016047,
                        a2: 0.39433849),
                    new SosSection(
                        b0: 1.0,
                        b1: 2.0,
                        b2: 1.0,
                        a1: 0.08621025,
                        a2: 0.03716562)
                },
                gain: 0.09515069
            ),

            // LPF_50Hz (DSP_SPEC.md §2.3)
            // Normalized frequency: 50 / 80 = 0.625
            LowPassCutoff.Hz50 => (
                new[]
                {
                    new SosSection(
                        b0: 1.0,
                        b1: 2.0,
                        b2: 1.0,
                        a1: 0.29618463,
                        a2: 0.55408880),
                    new SosSection(
                        b0: 1.0,
                        b1: 2.0,
                        b2: 1.0,
                        a1: 0.63218069,
                        a2: 0.06278633)
                },
                gain: 0.21324752
            ),

            // LPF_70Hz (DSP_SPEC.md §2.3)
            // Normalized frequency: 70 / 80 = 0.875
            LowPassCutoff.Hz70 => (
                new[]
                {
                    new SosSection(
                        b0: 1.0,
                        b1: 2.0,
                        b2: 1.0,
                        a1: 1.18063222,
                        a2: 0.79591209),
                    new SosSection(
                        b0: 1.0,
                        b1: 2.0,
                        b2: 1.0,
                        a1: 1.49417377,
                        a2: 0.53914284)
                },
                gain: 0.53784561
            ),

            _ => throw new ArgumentOutOfRangeException(nameof(cutoff))
        };
    }

    /// <summary>
    /// 获取预热样本数。
    /// </summary>
    /// <remarks>
    /// 依据: DSP_SPEC.md §7.1
    /// 低通滤波器预热时间很短，取最大值 32 样本 (0.2秒)
    /// </remarks>
    public static int GetWarmupSamples(LowPassCutoff cutoff)
    {
        return cutoff switch
        {
            LowPassCutoff.Hz15 => 32,   // 0.2 × 160
            LowPassCutoff.Hz35 => 14,   // 0.086 × 160
            LowPassCutoff.Hz50 => 10,   // 0.06 × 160
            LowPassCutoff.Hz70 => 7,    // 0.043 × 160
            _ => 32
        };
    }

    /// <summary>
    /// 获取预热时间（秒）。
    /// </summary>
    /// <remarks>
    /// 依据: DSP_SPEC.md §7.1
    /// 基于截止频率计算: 3 / fc
    /// </remarks>
    public static double GetWarmupSeconds(LowPassCutoff cutoff)
    {
        return cutoff switch
        {
            LowPassCutoff.Hz15 => 0.2,
            LowPassCutoff.Hz35 => 0.086,
            LowPassCutoff.Hz50 => 0.06,
            LowPassCutoff.Hz70 => 0.043,
            _ => 0.2
        };
    }
}
