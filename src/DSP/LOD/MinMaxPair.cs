// MinMaxPair.cs
// LOD 金字塔最小/最大值对 - AT-12
//
// 依据: ARCHITECTURE.md §4.3 (LOD 金字塔)

using System.Runtime.InteropServices;

namespace Neo.DSP.LOD;

/// <summary>
/// 最小值/最大值对（16 字节值类型）。
/// 用于 LOD 金字塔降采样中保留信号极值。
/// </summary>
[StructLayout(LayoutKind.Sequential)]
public readonly struct MinMaxPair
{
    /// <summary>区间最小值</summary>
    public readonly double Min;

    /// <summary>区间最大值</summary>
    public readonly double Max;

    public MinMaxPair(double min, double max)
    {
        Min = min;
        Max = max;
    }

    /// <summary>
    /// 从单个值创建（Min = Max = value）。
    /// </summary>
    public static MinMaxPair FromSingle(double value) => new(value, value);

    /// <summary>
    /// 合并两个 MinMaxPair（Spike 保护：保留全局极值）。
    /// </summary>
    public static MinMaxPair Merge(in MinMaxPair a, in MinMaxPair b) =>
        new(Math.Min(a.Min, b.Min), Math.Max(a.Max, b.Max));
}
