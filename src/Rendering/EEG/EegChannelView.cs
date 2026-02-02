// EegChannelView.cs
// EEG 通道视图配置 - 来源: ARCHITECTURE.md §5, CONSENSUS_BASELINE.md §6

using Vortice.Mathematics;

namespace Neo.Rendering.EEG;

/// <summary>
/// EEG 通道视图配置。
/// 定义单个 EEG 通道的显示参数。
/// </summary>
/// <remarks>
/// 依据: ARCHITECTURE.md §5, CONSENSUS_BASELINE.md §6
///
/// 设计原则:
/// - 只包含显示配置，不包含数据处理逻辑
/// - 不包含 DSP/滤波/LOD 相关参数
/// - 支持 μV 到像素的线性映射
///
/// 铁律约束:
/// - 铁律6: 渲染线程只 Draw
/// - 铁律2: 不伪造波形
/// </remarks>
public readonly record struct EegChannelView
{
    /// <summary>
    /// 通道索引 (0-3)。
    /// </summary>
    public int ChannelIndex { get; init; }

    /// <summary>
    /// 通道名称 (如 "CH1", "C3-P3")。
    /// </summary>
    public string ChannelName { get; init; }

    /// <summary>
    /// 通道显示颜色。
    /// </summary>
    public Color4 Color { get; init; }

    /// <summary>
    /// 通道区域 Y 起始位置（像素）。
    /// </summary>
    public float YOffset { get; init; }

    /// <summary>
    /// 通道区域高度（像素）。
    /// </summary>
    public float Height { get; init; }

    /// <summary>
    /// 振幅比例 (μV 到像素)。
    /// 默认值基于 ±200 μV 满屏显示。
    /// </summary>
    /// <remarks>
    /// 计算公式: pixels = uV * UvToPixelScale
    /// 无 DSP/缩放/LOD 处理，仅线性映射。
    /// </remarks>
    public float UvToPixelScale { get; init; }

    /// <summary>
    /// 基线 Y 位置（像素，通道区域中心）。
    /// </summary>
    public float BaselineY => YOffset + Height / 2.0f;

    /// <summary>
    /// 是否显示此通道。
    /// </summary>
    public bool IsVisible { get; init; }

    /// <summary>
    /// 波形线宽（像素）。
    /// </summary>
    public float LineWidth { get; init; }

    /// <summary>
    /// 创建默认通道视图配置。
    /// </summary>
    /// <param name="channelIndex">通道索引 (0-3)。</param>
    /// <param name="yOffset">Y 起始位置。</param>
    /// <param name="height">通道高度。</param>
    /// <param name="dpiScale">DPI 缩放因子。</param>
    /// <returns>通道视图配置。</returns>
    public static EegChannelView CreateDefault(int channelIndex, float yOffset, float height, float dpiScale = 1.0f)
    {
        // 通道名称 (基于 CONSENSUS_BASELINE.md §6.2)
        string[] channelNames = ["CH1 (C3-P3)", "CH2 (C4-P4)", "CH3 (P3-P4)", "CH4 (C3-C4)"];
        string name = channelIndex >= 0 && channelIndex < channelNames.Length
            ? channelNames[channelIndex]
            : $"CH{channelIndex + 1}";

        // 默认振幅范围: ±200 μV 对应通道高度
        // UvToPixelScale = height / (2 * 200) = height / 400
        float uvToPixelScale = height / 400.0f;

        return new EegChannelView
        {
            ChannelIndex = channelIndex,
            ChannelName = name,
            Color = EegColorPalette.GetChannelColor(channelIndex),
            YOffset = yOffset,
            Height = height,
            UvToPixelScale = uvToPixelScale,
            IsVisible = true,
            LineWidth = 1.5f * dpiScale
        };
    }

    /// <summary>
    /// 将 μV 值转换为 Y 像素坐标。
    /// </summary>
    /// <param name="uv">电压值 (μV)。</param>
    /// <returns>Y 像素坐标。</returns>
    /// <remarks>
    /// 线性映射，无任何 DSP 处理。
    /// 正电压向上（Y 减小），负电压向下（Y 增大）。
    /// </remarks>
    public float UvToY(double uv)
    {
        // 注意: 屏幕坐标 Y 向下增加，所以负号反转
        return BaselineY - (float)(uv * UvToPixelScale);
    }

    /// <summary>
    /// 将 Y 像素坐标转换为 μV 值。
    /// </summary>
    /// <param name="y">Y 像素坐标。</param>
    /// <returns>电压值 (μV)。</returns>
    public double YToUv(float y)
    {
        if (Math.Abs(UvToPixelScale) < 1e-10)
            return 0.0;

        return (BaselineY - y) / UvToPixelScale;
    }

    /// <summary>
    /// 检查 Y 坐标是否在通道区域内。
    /// </summary>
    /// <param name="y">Y 像素坐标。</param>
    /// <returns>如果在区域内返回 true。</returns>
    public bool ContainsY(float y)
    {
        return y >= YOffset && y < YOffset + Height;
    }
}
