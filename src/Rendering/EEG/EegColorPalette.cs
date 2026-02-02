// EegColorPalette.cs
// EEG 通道颜色定义 - 来源: ARCHITECTURE.md §5, CONSENSUS_BASELINE.md §6.2

using Vortice.Mathematics;

namespace Neo.Rendering.EEG;

/// <summary>
/// EEG 通道颜色调色板。
/// 为 4 个 EEG 通道定义静态颜色常量。
/// </summary>
/// <remarks>
/// 依据: ARCHITECTURE.md §5, CONSENSUS_BASELINE.md §6.2
///
/// 通道配置:
/// - CH1: C3-P3 (A-B) - 物理
/// - CH2: C4-P4 (C-D) - 物理
/// - CH3: P3-P4 (B-C) - 物理
/// - CH4: C3-C4 (A-D) - 计算
///
/// 设计原则:
/// - 颜色对比度明显，便于区分
/// - 考虑色盲友好性
/// - 与医疗监护惯例一致
/// </remarks>
public static class EegColorPalette
{
    /// <summary>
    /// 通道 1 颜色 (绿色)。
    /// </summary>
    public static readonly Color4 Channel1 = new(0.2f, 0.8f, 0.2f, 1.0f);

    /// <summary>
    /// 通道 2 颜色 (蓝色)。
    /// </summary>
    public static readonly Color4 Channel2 = new(0.2f, 0.4f, 0.9f, 1.0f);

    /// <summary>
    /// 通道 3 颜色 (橙色)。
    /// </summary>
    public static readonly Color4 Channel3 = new(0.9f, 0.6f, 0.1f, 1.0f);

    /// <summary>
    /// 通道 4 颜色 (紫色，计算通道)。
    /// </summary>
    public static readonly Color4 Channel4 = new(0.7f, 0.2f, 0.8f, 1.0f);

    /// <summary>
    /// 间隙遮罩颜色 (灰色半透明)。
    /// 依据铁律5: 数据缺失 → 灰色遮罩。
    /// </summary>
    public static readonly Color4 GapMask = new(0.5f, 0.5f, 0.5f, 0.3f);

    /// <summary>
    /// 饱和标记颜色 (红色)。
    /// 依据铁律5: 信号饱和 → 红色标记。
    /// </summary>
    public static readonly Color4 SaturationMarker = new(1.0f, 0.2f, 0.2f, 1.0f);

    /// <summary>
    /// 通道背景颜色 (深灰半透明)。
    /// </summary>
    public static readonly Color4 ChannelBackground = new(0.15f, 0.15f, 0.15f, 0.8f);

    /// <summary>
    /// 基线颜色 (浅灰)。
    /// </summary>
    public static readonly Color4 Baseline = new(0.4f, 0.4f, 0.4f, 0.5f);

    /// <summary>
    /// 预定义通道颜色数组。
    /// </summary>
    private static readonly Color4[] ChannelColors = [Channel1, Channel2, Channel3, Channel4];

    /// <summary>
    /// 获取指定通道的颜色。
    /// </summary>
    /// <param name="channelIndex">通道索引 (0-3)。</param>
    /// <returns>通道颜色。</returns>
    public static Color4 GetChannelColor(int channelIndex)
    {
        if (channelIndex < 0 || channelIndex >= ChannelColors.Length)
            return Channel1;  // 默认返回通道1颜色

        return ChannelColors[channelIndex];
    }
}
