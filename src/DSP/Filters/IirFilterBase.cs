// IirFilterBase.cs
// IIR 滤波器基类 - 来源: DSP_SPEC.md §2

using System.Buffers;
using System.Runtime.CompilerServices;

namespace Neo.DSP.Filters;

/// <summary>
/// IIR 滤波器基类（使用 SOS 级联实现）。
/// </summary>
/// <remarks>
/// 依据: DSP_SPEC.md §2
/// 铁律4: 滤波器系数与状态变量必须使用 double 精度
///
/// 实现: Direct Form II Transposed
/// - 数值稳定性更好
/// - 适合低截止频率滤波器
/// </remarks>
public abstract class IirFilterBase
{
    private readonly SosSection[] _sections;
    private readonly SosSectionState[] _states;
    private readonly double _gain;

    /// <summary>
    /// SOS 节数。
    /// </summary>
    public int SectionCount => _sections.Length;

    /// <summary>
    /// 总增益。
    /// </summary>
    public double Gain => _gain;

    /// <summary>
    /// 创建 IIR 滤波器。
    /// </summary>
    /// <param name="sections">SOS 节数组</param>
    /// <param name="gain">总增益</param>
    protected IirFilterBase(SosSection[] sections, double gain)
    {
        _sections = sections ?? throw new ArgumentNullException(nameof(sections));
        _states = new SosSectionState[sections.Length];
        _gain = gain;
    }

    /// <summary>
    /// 处理单个样本（实时模式）。
    /// </summary>
    /// <param name="input">输入样本</param>
    /// <returns>滤波后的样本</returns>
    /// <remarks>
    /// 使用 Direct Form II Transposed:
    /// y[n] = b0*x[n] + z1[n-1]
    /// z1[n] = b1*x[n] - a1*y[n] + z2[n-1]
    /// z2[n] = b2*x[n] - a2*y[n]
    /// </remarks>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public double Process(double input)
    {
        double output = input * _gain;

        for (int i = 0; i < _sections.Length; i++)
        {
            output = ProcessSection(ref _states[i], _sections[i], output);
        }

        return output;
    }

    /// <summary>
    /// 处理单个 SOS 节。
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static double ProcessSection(ref SosSectionState state, in SosSection sos, double input)
    {
        // Direct Form II Transposed
        double output = sos.B0 * input + state.Z1;
        state.Z1 = sos.B1 * input - sos.A1 * output + state.Z2;
        state.Z2 = sos.B2 * input - sos.A2 * output;

        return output;
    }

    /// <summary>
    /// 重置滤波器状态。
    /// </summary>
    public void Reset()
    {
        for (int i = 0; i < _states.Length; i++)
        {
            _states[i].Reset();
        }
    }

    /// <summary>
    /// 获取滤波器阶数。
    /// </summary>
    public int Order => _sections.Length * 2;

    /// <summary>
    /// Zero-phase 滤波（filtfilt: 前后向 IIR）。
    /// 用于回放模式，消除相位延迟。
    /// </summary>
    /// <remarks>
    /// 依据: AT-19 Zero-Phase 滤波
    /// 算法: 前向 → 反转 → 后向 → 反转
    /// 使用全新状态，不影响实时滤波的 _states。
    /// </remarks>
    public void ProcessZeroPhase(ReadOnlySpan<double> input, Span<double> output)
    {
        if (input.Length == 0) return;

        var temp = ArrayPool<double>.Shared.Rent(input.Length);
        var buf = temp.AsSpan(0, input.Length);

        // 1. Forward pass（全新状态，不影响实时滤波）
        var fwdStates = new SosSectionState[_sections.Length];
        for (int i = 0; i < input.Length; i++)
            buf[i] = ProcessWithStates(input[i], _sections, _gain, fwdStates);

        // 2. Reverse
        buf.Reverse();

        // 3. Backward pass（再次全新状态）
        var bwdStates = new SosSectionState[_sections.Length];
        for (int i = 0; i < buf.Length; i++)
            buf[i] = ProcessWithStates(buf[i], _sections, _gain, bwdStates);

        // 4. Reverse back
        buf.Reverse();
        buf.CopyTo(output);

        ArrayPool<double>.Shared.Return(temp);
    }

    /// <summary>
    /// 使用指定状态数组处理单个样本（不影响实例状态）。
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static double ProcessWithStates(double input, SosSection[] sections, double gain, SosSectionState[] states)
    {
        double output = input * gain;

        for (int i = 0; i < sections.Length; i++)
        {
            output = ProcessSection(ref states[i], sections[i], output);
        }

        return output;
    }
}
