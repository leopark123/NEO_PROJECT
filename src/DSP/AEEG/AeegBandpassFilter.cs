// AeegBandpassFilter.cs
// aEEG 带通滤波器 (2-15 Hz) - 来源: DSP_SPEC.md §3.2

using Neo.DSP.Filters;

namespace Neo.DSP.AEEG;

/// <summary>
/// aEEG 专用带通滤波器 (2-15 Hz)。
/// </summary>
/// <remarks>
/// 依据: DSP_SPEC.md §3.2
///
/// 设计参数:
/// - 类型: Butterworth
/// - 带通范围: 2-15 Hz
/// - 采样率: 160 Hz
/// - 实现: HPF(2Hz) 串联 LPF(15Hz)
///
/// 铁律4: 所有系数与状态使用 double 精度
/// </remarks>
public sealed class AeegBandpassFilter
{
    private readonly IirFilterBase _hpf;
    private readonly IirFilterBase _lpf;

    /// <summary>
    /// 低截止频率 (Hz)。
    /// </summary>
    public const double LowCutoffHz = 2.0;

    /// <summary>
    /// 高截止频率 (Hz)。
    /// </summary>
    public const double HighCutoffHz = 15.0;

    /// <summary>
    /// 采样率 (Hz)。
    /// </summary>
    public const int SampleRate = 160;

    /// <summary>
    /// 创建 aEEG 带通滤波器。
    /// </summary>
    public AeegBandpassFilter()
    {
        _hpf = new AeegHighPass2Hz();
        _lpf = new AeegLowPass15Hz();
    }

    /// <summary>
    /// 处理单个样本。
    /// </summary>
    /// <param name="input">输入样本 (μV)</param>
    /// <returns>滤波后的样本 (μV)</returns>
    public double Process(double input)
    {
        // HPF → LPF 级联
        double hpfOutput = _hpf.Process(input);
        return _lpf.Process(hpfOutput);
    }

    /// <summary>
    /// 重置滤波器状态。
    /// </summary>
    public void Reset()
    {
        _hpf.Reset();
        _lpf.Reset();
    }

    /// <summary>
    /// 预热样本数。
    /// </summary>
    /// <remarks>
    /// 依据: DSP_SPEC.md §7
    /// HPF 2Hz 预热时间约 1.5 秒 = 240 样本
    /// </remarks>
    public static int WarmupSamples => 240;

    /// <summary>
    /// 预热时间（秒）。
    /// </summary>
    public static double WarmupSeconds => 1.5;
}

/// <summary>
/// aEEG 专用高通滤波器 (2 Hz)。
/// </summary>
/// <remarks>
/// 依据: DSP_SPEC.md §3.2.1
///
/// 设计参数:
/// - 类型: Butterworth
/// - 阶数: 2
/// - 截止频率: 2 Hz
/// - 采样率: 160 Hz
/// - 归一化频率: 2.0 / 80 = 0.025
///
/// 系数来源: DSP_SPEC.md §3.2.1 (SOS格式，double精度)
/// </remarks>
internal sealed class AeegHighPass2Hz : IirFilterBase
{
    public AeegHighPass2Hz()
        : base(GetSections(), HpfGain)
    {
    }

    /// <summary>
    /// 总增益。
    /// </summary>
    /// <remarks>
    /// 来源: DSP_SPEC.md §3.2.1
    /// Butterworth 2阶 HPF, fc=2Hz, fs=160Hz
    /// </remarks>
    private const double HpfGain = 0.94597746;

    /// <summary>
    /// 获取 SOS 节。
    /// </summary>
    /// <remarks>
    /// 来源: DSP_SPEC.md §3.2.1
    /// Butterworth 2阶 HPF, fc=2Hz, fs=160Hz
    /// SOS: [1.0, -2.0, 1.0, 1.0, -1.88910739, 0.89490251]
    /// </remarks>
    private static SosSection[] GetSections()
    {
        return new[]
        {
            new SosSection(
                b0: 1.0,
                b1: -2.0,
                b2: 1.0,
                a1: -1.88910739,
                a2: 0.89490251)
        };
    }
}

/// <summary>
/// aEEG 专用低通滤波器 (15 Hz)。
/// </summary>
/// <remarks>
/// 依据: DSP_SPEC.md §3.2, §2.3
///
/// 使用 DSP_SPEC.md §2.3 LPF_15Hz 系数 (Butterworth 4阶)。
/// </remarks>
internal sealed class AeegLowPass15Hz : IirFilterBase
{
    public AeegLowPass15Hz()
        : base(GetSections(), LpfGain)
    {
    }

    /// <summary>
    /// 总增益。
    /// </summary>
    /// <remarks>
    /// 来源: DSP_SPEC.md §2.3 LPF_15Hz
    /// </remarks>
    private const double LpfGain = 0.02952402;

    /// <summary>
    /// 获取 SOS 节。
    /// </summary>
    /// <remarks>
    /// 来源: DSP_SPEC.md §2.3 LPF_15Hz
    /// Butterworth 4阶, fc=15Hz, fs=160Hz
    /// </remarks>
    private static SosSection[] GetSections()
    {
        return new[]
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
        };
    }
}
