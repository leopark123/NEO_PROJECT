// AeegColorPalette.cs
// aEEG 颜色定义 - 来源: ARCHITECTURE.md §5

using Vortice.Mathematics;

namespace Neo.Rendering.AEEG;

/// <summary>
/// aEEG 渲染颜色定义。
/// </summary>
/// <remarks>
/// 依据: ARCHITECTURE.md §5
///
/// 颜色语义:
/// - 趋势填充: 半透明蓝色，显示 min-max 范围
/// - 上边界: 深蓝色线条，表示 max 值
/// - 下边界: 浅蓝色线条，表示 min 值
/// - 分界线: 10 μV 分界点指示线
/// - 间隙遮罩: 灰色半透明，表示数据缺失
/// </remarks>
public static class AeegColorPalette
{
    // ============================================
    // 趋势线颜色
    // ============================================

    /// <summary>
    /// 趋势填充色（半透明蓝色）。
    /// </summary>
    public static readonly Color4 TrendFill = new(0.2f, 0.5f, 0.8f, 0.3f);

    /// <summary>
    /// 上边界线颜色（深蓝色）。
    /// </summary>
    public static readonly Color4 UpperBound = new(0.1f, 0.3f, 0.7f, 1.0f);

    /// <summary>
    /// 下边界线颜色（浅蓝色）。
    /// </summary>
    public static readonly Color4 LowerBound = new(0.3f, 0.6f, 0.9f, 1.0f);

    // ============================================
    // 网格和轴线颜色
    // ============================================

    /// <summary>
    /// 主刻度线颜色。
    /// </summary>
    public static readonly Color4 MajorGridLine = new(0.4f, 0.4f, 0.4f, 0.8f);

    /// <summary>
    /// 次刻度线颜色。
    /// </summary>
    public static readonly Color4 MinorGridLine = new(0.5f, 0.5f, 0.5f, 0.4f);

    /// <summary>
    /// 分界线颜色（10 μV）。
    /// </summary>
    public static readonly Color4 BoundaryLine = new(0.8f, 0.2f, 0.2f, 0.6f);

    /// <summary>
    /// 轴线颜色。
    /// </summary>
    public static readonly Color4 AxisLine = new(0.3f, 0.3f, 0.3f, 1.0f);

    /// <summary>
    /// 轴标签颜色。
    /// </summary>
    public static readonly Color4 AxisLabel = new(0.2f, 0.2f, 0.2f, 1.0f);

    // ============================================
    // 背景和遮罩颜色
    // ============================================

    /// <summary>
    /// 背景颜色。
    /// </summary>
    public static readonly Color4 Background = new(0.98f, 0.98f, 0.98f, 1.0f);

    /// <summary>
    /// 间隙遮罩颜色（灰色半透明）。
    /// </summary>
    public static readonly Color4 GapMask = new(0.5f, 0.5f, 0.5f, 0.3f);

    /// <summary>
    /// 饱和标记颜色（红色）。
    /// </summary>
    public static readonly Color4 SaturationMarker = new(0.9f, 0.2f, 0.2f, 1.0f);

    // ============================================
    // 通道颜色
    // ============================================

    /// <summary>
    /// 通道 1 颜色（与 EEG 保持一致）。
    /// </summary>
    public static readonly Color4 Channel1 = new(0.2f, 0.7f, 0.3f, 1.0f);

    /// <summary>
    /// 通道 2 颜色。
    /// </summary>
    public static readonly Color4 Channel2 = new(0.2f, 0.4f, 0.8f, 1.0f);

    /// <summary>
    /// 通道 3 颜色。
    /// </summary>
    public static readonly Color4 Channel3 = new(0.9f, 0.5f, 0.1f, 1.0f);

    /// <summary>
    /// 通道 4 颜色。
    /// </summary>
    public static readonly Color4 Channel4 = new(0.6f, 0.2f, 0.7f, 1.0f);

    /// <summary>
    /// 获取通道颜色。
    /// </summary>
    /// <param name="channelIndex">通道索引 (0-3)。</param>
    /// <returns>通道颜色。</returns>
    public static Color4 GetChannelColor(int channelIndex)
    {
        return channelIndex switch
        {
            0 => Channel1,
            1 => Channel2,
            2 => Channel3,
            3 => Channel4,
            _ => Channel1
        };
    }
}
