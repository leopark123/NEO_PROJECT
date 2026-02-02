// AeegRectifier.cs
// aEEG 整流器（半波整流）- 来源: DSP_SPEC.md §3.1

namespace Neo.DSP.AEEG;

/// <summary>
/// aEEG 半波整流器。
/// </summary>
/// <remarks>
/// 依据: DSP_SPEC.md §3.1
///
/// 半波整流: y = |x|
///
/// ⚠️ 医学约束 (DSP_SPEC.md §3.0):
/// - aEEG ≠ RMS
/// - 禁止使用 RMS 替代
/// - 整流操作是医学定义的一部分，不可修改
/// </remarks>
public static class AeegRectifier
{
    /// <summary>
    /// 执行半波整流。
    /// </summary>
    /// <param name="input">输入信号 (μV)</param>
    /// <returns>整流后的信号 (μV, 非负)</returns>
    /// <remarks>
    /// 依据: DSP_SPEC.md §3.1
    /// 半波整流: y = |x|
    /// </remarks>
    public static double Rectify(double input)
    {
        return Math.Abs(input);
    }

    /// <summary>
    /// 批量整流。
    /// </summary>
    /// <param name="input">输入信号数组</param>
    /// <param name="output">输出信号数组（可与输入相同）</param>
    /// <param name="count">处理样本数</param>
    public static void RectifyBatch(double[] input, double[] output, int count)
    {
        for (int i = 0; i < count; i++)
        {
            output[i] = Math.Abs(input[i]);
        }
    }
}
