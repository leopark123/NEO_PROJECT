// HighPassFilter.cs
// 高通滤波器 - 来源: DSP_SPEC.md §2.2

namespace Neo.DSP.Filters;

/// <summary>
/// 高通滤波器截止频率选项。
/// </summary>
/// <remarks>
/// 依据: DSP_SPEC.md §2.1
/// 可选值: 0.3, 0.5, 1.5 Hz
/// 默认值: 0.5 Hz
/// </remarks>
public enum HighPassCutoff
{
    /// <summary>0.3 Hz</summary>
    Hz0_3,

    /// <summary>0.5 Hz (默认)</summary>
    Hz0_5,

    /// <summary>1.5 Hz</summary>
    Hz1_5
}

/// <summary>
/// Butterworth IIR 高通滤波器（二阶）。
/// </summary>
/// <remarks>
/// 依据: DSP_SPEC.md §2.2
///
/// 设计参数:
/// - 类型: Butterworth
/// - 阶数: 2
/// - 采样率: 160 Hz
///
/// 系数来源: DSP_SPEC.md §2.2 (SOS格式，double精度)
/// </remarks>
public sealed class HighPassFilter : IirFilterBase
{
    /// <summary>当前截止频率设置</summary>
    public HighPassCutoff Cutoff { get; }

    private HighPassFilter(SosSection[] sections, double gain, HighPassCutoff cutoff)
        : base(sections, gain)
    {
        Cutoff = cutoff;
    }

    /// <summary>
    /// 创建高通滤波器。
    /// </summary>
    /// <param name="cutoff">截止频率</param>
    /// <returns>HighPassFilter 实例</returns>
    public static HighPassFilter Create(HighPassCutoff cutoff = HighPassCutoff.Hz0_5)
    {
        var (sections, gain) = GetCoefficients(cutoff);
        return new HighPassFilter(sections, gain, cutoff);
    }

    /// <summary>
    /// 获取滤波器系数。
    /// </summary>
    /// <remarks>
    /// 系数来源: DSP_SPEC.md §2.2
    /// 设计参数: Butterworth 2阶, fs=160Hz
    /// </remarks>
    private static (SosSection[] sections, double gain) GetCoefficients(HighPassCutoff cutoff)
    {
        return cutoff switch
        {
            // HPF_0.3Hz (DSP_SPEC.md §2.2)
            // Normalized frequency: 0.3 / 80 = 0.00375
            HighPassCutoff.Hz0_3 => (
                new[]
                {
                    new SosSection(
                        b0: 1.0,
                        b1: -2.0,
                        b2: 1.0,
                        a1: -1.98222644,
                        a2: 0.98237771)
                },
                gain: 0.99117852
            ),

            // HPF_0.5Hz (DSP_SPEC.md §2.2) - 默认
            // Normalized frequency: 0.5 / 80 = 0.00625
            HighPassCutoff.Hz0_5 => (
                new[]
                {
                    new SosSection(
                        b0: 1.0,
                        b1: -2.0,
                        b2: 1.0,
                        a1: -1.97037449,
                        a2: 0.97072991)
                },
                gain: 0.98526618
            ),

            // HPF_1.5Hz (DSP_SPEC.md §2.2)
            // Normalized frequency: 1.5 / 80 = 0.01875
            HighPassCutoff.Hz1_5 => (
                new[]
                {
                    new SosSection(
                        b0: 1.0,
                        b1: -2.0,
                        b2: 1.0,
                        a1: -1.91119707,
                        a2: 0.91497583)
                },
                gain: 0.95573826
            ),

            _ => throw new ArgumentOutOfRangeException(nameof(cutoff))
        };
    }

    /// <summary>
    /// 获取预热样本数。
    /// </summary>
    /// <remarks>
    /// 依据: DSP_SPEC.md §7.2
    /// </remarks>
    public static int GetWarmupSamples(HighPassCutoff cutoff)
    {
        return cutoff switch
        {
            HighPassCutoff.Hz0_3 => 1600,  // 10.0 × 160
            HighPassCutoff.Hz0_5 => 960,   // 6.0 × 160
            HighPassCutoff.Hz1_5 => 320,   // 2.0 × 160
            _ => 960
        };
    }

    /// <summary>
    /// 获取预热时间（秒）。
    /// </summary>
    /// <remarks>
    /// 依据: DSP_SPEC.md §7.1
    /// 基于截止频率计算: 3 / fc
    /// </remarks>
    public static double GetWarmupSeconds(HighPassCutoff cutoff)
    {
        return cutoff switch
        {
            HighPassCutoff.Hz0_3 => 10.0,
            HighPassCutoff.Hz0_5 => 6.0,
            HighPassCutoff.Hz1_5 => 2.0,
            _ => 6.0
        };
    }
}
