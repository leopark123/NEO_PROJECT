// GridLayer.cs
// 网格层 - 来源: ARCHITECTURE.md §5, ADR-008 (Layer 1)

using System.Numerics;
using Neo.Rendering.Core;
using Neo.Rendering.Resources;
using Vortice.Direct2D1;
using Vortice.Mathematics;

namespace Neo.Rendering.Layers;

/// <summary>
/// 网格层（Layer 1 - 背景层）。
/// 负责绘制背景网格和坐标参考线。
/// </summary>
/// <remarks>
/// 依据: ARCHITECTURE.md §5, ADR-008
///
/// 职责:
/// - 背景填充
/// - 网格线绘制
/// - 坐标参考线
///
/// 特性:
/// - 可缓存到纹理（DPI/窗口变化时重绘）
/// - 不依赖任何数据
/// - 可开关
///
/// 铁律约束（铁律6）:
/// - Render() 只做 Draw 调用
/// </remarks>
public sealed class GridLayer : LayerBase
{
    // 默认网格配置
    private const float MajorGridSpacingDip = 100.0f;  // 主网格间距 (DIP)
    private const float MinorGridSpacingDip = 20.0f;   // 次网格间距 (DIP)
    private const float MajorGridLineWidth = 1.0f;
    private const float MinorGridLineWidth = 0.5f;

    // 颜色配置
    private static readonly Color4 BackgroundColor = new(0.12f, 0.12f, 0.14f, 1.0f);
    private static readonly Color4 MajorGridColor = new(0.3f, 0.3f, 0.32f, 1.0f);
    private static readonly Color4 MinorGridColor = new(0.2f, 0.2f, 0.22f, 1.0f);

    /// <inheritdoc/>
    public override string Name => "Grid";

    /// <inheritdoc/>
    public override int Order => 0;  // 最底层

    /// <summary>
    /// 是否绘制次网格线。
    /// </summary>
    public bool ShowMinorGrid { get; set; } = true;

    /// <summary>
    /// 是否绘制主网格线。
    /// </summary>
    public bool ShowMajorGrid { get; set; } = true;

    /// <inheritdoc/>
    protected override void OnRender(ID2D1DeviceContext context, ResourceCache resources, RenderContext renderContext)
    {
        float width = renderContext.ViewportWidth;
        float height = renderContext.ViewportHeight;
        float dpiScale = (float)renderContext.DpiScale;

        // 1. 绘制背景
        var backgroundBrush = resources.GetSolidBrush(BackgroundColor);
        context.Clear(BackgroundColor);

        // 2. 绘制次网格线（如果启用）
        if (ShowMinorGrid)
        {
            var minorBrush = resources.GetSolidBrush(MinorGridColor);
            float minorSpacing = MinorGridSpacingDip * dpiScale;
            DrawGridLines(context, minorBrush, width, height, minorSpacing, MinorGridLineWidth);
        }

        // 3. 绘制主网格线（如果启用）
        if (ShowMajorGrid)
        {
            var majorBrush = resources.GetSolidBrush(MajorGridColor);
            float majorSpacing = MajorGridSpacingDip * dpiScale;
            DrawGridLines(context, majorBrush, width, height, majorSpacing, MajorGridLineWidth);
        }

        NeedsRedraw = false;
    }

    /// <summary>
    /// 绘制网格线。
    /// </summary>
    private static void DrawGridLines(
        ID2D1DeviceContext context,
        ID2D1SolidColorBrush brush,
        float width,
        float height,
        float spacing,
        float strokeWidth)
    {
        // 避免除零
        if (spacing < 1.0f)
            return;

        // 绘制垂直线
        for (float x = spacing; x < width; x += spacing)
        {
            context.DrawLine(
                new Vector2(x, 0),
                new Vector2(x, height),
                brush,
                strokeWidth);
        }

        // 绘制水平线
        for (float y = spacing; y < height; y += spacing)
        {
            context.DrawLine(
                new Vector2(0, y),
                new Vector2(width, y),
                brush,
                strokeWidth);
        }
    }
}
