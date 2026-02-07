// AeegTheme.cs
// aEEG 主题颜色管理

using Vortice.Mathematics;

namespace Neo.Rendering.AEEG;

/// <summary>
/// aEEG 主题类型
/// </summary>
public enum AeegThemeType
{
    /// <summary>Apple 风格</summary>
    Apple,

    /// <summary>经典医疗风格</summary>
    Medical
}

/// <summary>
/// aEEG 主题配色
/// </summary>
public class AeegTheme
{
    public Color4 TrendFill { get; init; }
    public Color4 UpperBound { get; init; }
    public Color4 LowerBound { get; init; }
    public Color4 MajorGridLine { get; init; }
    public Color4 MinorGridLine { get; init; }
    public Color4 BoundaryLine { get; init; }
    public Color4 AxisLine { get; init; }
    public Color4 AxisLabel { get; init; }
    public Color4 Background { get; init; }
    public Color4 GapMask { get; init; }
    public Color4 SaturationMarker { get; init; }

    /// <summary>
    /// 获取指定主题的配色方案
    /// </summary>
    public static AeegTheme GetTheme(AeegThemeType type)
    {
        return type switch
        {
            AeegThemeType.Apple => AppleTheme,
            AeegThemeType.Medical => MedicalTheme,
            _ => AppleTheme
        };
    }

    /// <summary>
    /// Apple 风格主题（系统蓝）
    /// </summary>
    public static readonly AeegTheme AppleTheme = new()
    {
        // 趋势填充 - 深蓝色
        TrendFill = new(0.0f, 0.3f, 0.7f, 1.0f),
        UpperBound = new(0.0f, 0.2f, 0.6f, 1.0f),
        LowerBound = new(0.2f, 0.5f, 0.85f, 1.0f),

        // 网格和轴线 - Apple 风格灰色
        MajorGridLine = new(0.55f, 0.55f, 0.57f, 0.6f),
        MinorGridLine = new(0.65f, 0.65f, 0.67f, 0.3f),
        BoundaryLine = new(1.0f, 0.23f, 0.19f, 0.5f),  // Apple 红
        AxisLine = new(0.42f, 0.42f, 0.44f, 1.0f),
        AxisLabel = new(0.28f, 0.28f, 0.29f, 1.0f),

        // 背景
        Background = new(0.96f, 0.96f, 0.97f, 1.0f),  // #F5F5F7
        GapMask = new(0.56f, 0.56f, 0.58f, 0.25f),
        SaturationMarker = new(1.0f, 0.23f, 0.19f, 1.0f)
    };

    /// <summary>
    /// 经典医疗主题（绿色波形）
    /// </summary>
    public static readonly AeegTheme MedicalTheme = new()
    {
        // 趋势填充 - 医疗绿色
        TrendFill = new(0.0f, 0.7f, 0.4f, 0.7f),  // 半透明绿色
        UpperBound = new(0.0f, 0.8f, 0.5f, 1.0f),
        LowerBound = new(0.2f, 0.9f, 0.6f, 1.0f),

        // 网格和轴线 - 经典灰色
        MajorGridLine = new(0.4f, 0.4f, 0.4f, 0.8f),
        MinorGridLine = new(0.5f, 0.5f, 0.5f, 0.4f),
        BoundaryLine = new(0.8f, 0.2f, 0.2f, 0.6f),  // 红色
        AxisLine = new(0.3f, 0.3f, 0.3f, 1.0f),
        AxisLabel = new(0.2f, 0.2f, 0.2f, 1.0f),

        // 背景
        Background = new(0.98f, 0.98f, 0.98f, 1.0f),
        GapMask = new(0.5f, 0.5f, 0.5f, 0.3f),
        SaturationMarker = new(0.9f, 0.2f, 0.2f, 1.0f)
    };
}
