// SosSection.cs
// 二阶节（Second-Order Section）- 来源: DSP_SPEC.md §2

namespace Neo.DSP.Filters;

/// <summary>
/// 二阶节（SOS）滤波器系数。
/// </summary>
/// <remarks>
/// 依据: DSP_SPEC.md §2 (SOS格式，double精度)
/// 铁律4: 滤波器系数必须使用 double 精度
///
/// 传递函数:
/// H(z) = gain * (b0 + b1*z^-1 + b2*z^-2) / (1 + a1*z^-1 + a2*z^-2)
///
/// SOS 格式: [b0, b1, b2, 1.0, a1, a2]
/// </remarks>
public readonly struct SosSection
{
    // 分子系数 (feedforward)
    public readonly double B0;
    public readonly double B1;
    public readonly double B2;

    // 分母系数 (feedback) - a0 总是 1.0
    public readonly double A1;
    public readonly double A2;

    /// <summary>
    /// 创建 SOS 节。
    /// </summary>
    /// <param name="b0">分子系数 b0</param>
    /// <param name="b1">分子系数 b1</param>
    /// <param name="b2">分子系数 b2</param>
    /// <param name="a1">分母系数 a1</param>
    /// <param name="a2">分母系数 a2</param>
    public SosSection(double b0, double b1, double b2, double a1, double a2)
    {
        B0 = b0;
        B1 = b1;
        B2 = b2;
        A1 = a1;
        A2 = a2;
    }

    /// <summary>
    /// 从 DSP_SPEC 格式创建 [b0, b1, b2, 1.0, a1, a2]。
    /// </summary>
    public static SosSection FromArray(double[] sos)
    {
        if (sos.Length != 6)
            throw new ArgumentException("SOS array must have 6 elements", nameof(sos));

        return new SosSection(sos[0], sos[1], sos[2], sos[4], sos[5]);
    }
}

/// <summary>
/// 二阶节滤波器状态（Direct Form II Transposed）。
/// </summary>
/// <remarks>
/// 使用 Direct Form II Transposed 实现，数值稳定性更好。
/// 铁律4: 状态变量必须使用 double 精度
/// </remarks>
public struct SosSectionState
{
    /// <summary>延迟线状态 z^-1</summary>
    public double Z1;

    /// <summary>延迟线状态 z^-2</summary>
    public double Z2;

    /// <summary>
    /// 重置状态为零。
    /// </summary>
    public void Reset()
    {
        Z1 = 0.0;
        Z2 = 0.0;
    }
}
