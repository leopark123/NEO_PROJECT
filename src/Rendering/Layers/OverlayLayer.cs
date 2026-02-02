// OverlayLayer.cs
// 覆盖层 - 来源: ARCHITECTURE.md §5, ADR-008 (Layer 3)

using System.Numerics;
using Neo.Rendering.Core;
using Neo.Rendering.Resources;
using Vortice.Direct2D1;
using Vortice.DirectWrite;
using Vortice.Mathematics;

namespace Neo.Rendering.Layers;

/// <summary>
/// 覆盖层（Layer 3 - 最上层）。
/// 负责绘制时间轴、光标、标记等叠加元素。
/// </summary>
/// <remarks>
/// 依据: ARCHITECTURE.md §5, ADR-008
///
/// S1-04 范围（占位实现）:
/// - 时间轴刻度（占位）
/// - 文本标签占位
/// - 光标/标记占位
///
/// 未来扩展:
/// - 时间游标
/// - 测量标注
/// - 事件标记
/// - 伪迹遮罩
///
/// 铁律约束（铁律6）:
/// - Render() 只做 Draw 调用
/// </remarks>
public sealed class OverlayLayer : LayerBase
{
    // 配置
    private const float TimeAxisHeight = 30.0f;
    private const float TickMarkMajorLength = 10.0f;
    private const float TickMarkMinorLength = 5.0f;
    private const float CursorLineWidth = 2.0f;

    // 颜色
    private static readonly Color4 TimeAxisColor = new(0.8f, 0.8f, 0.8f, 1.0f);
    private static readonly Color4 CursorColor = new(1.0f, 0.8f, 0.2f, 1.0f);
    private static readonly Color4 TextColor = new(0.9f, 0.9f, 0.9f, 1.0f);

    /// <inheritdoc/>
    public override string Name => "Overlay";

    /// <inheritdoc/>
    public override int Order => 2;  // 最上层

    /// <summary>
    /// 是否显示时间轴。
    /// </summary>
    public bool ShowTimeAxis { get; set; } = true;

    /// <summary>
    /// 是否显示光标（占位）。
    /// </summary>
    public bool ShowCursor { get; set; } = true;

    /// <summary>
    /// 光标位置（归一化 0-1）。
    /// </summary>
    public float CursorPosition { get; set; } = 0.5f;

    /// <inheritdoc/>
    protected override void OnRender(ID2D1DeviceContext context, ResourceCache resources, RenderContext renderContext)
    {
        float width = renderContext.ViewportWidth;
        float height = renderContext.ViewportHeight;
        float dpiScale = (float)renderContext.DpiScale;

        // 1. 绘制时间轴（底部）
        if (ShowTimeAxis)
        {
            DrawTimeAxis(context, resources, width, height, dpiScale, renderContext);
        }

        // 2. 绘制光标（占位）
        if (ShowCursor)
        {
            DrawCursor(context, resources, width, height, dpiScale);
        }

        NeedsRedraw = false;
    }

    /// <summary>
    /// 绘制时间轴。
    /// </summary>
    private void DrawTimeAxis(
        ID2D1DeviceContext context,
        ResourceCache resources,
        float width,
        float height,
        float dpiScale,
        RenderContext renderContext)
    {
        float axisHeight = TimeAxisHeight * dpiScale;
        float axisY = height - axisHeight;

        // 绘制时间轴背景
        var bgBrush = resources.GetSolidBrush(0.15f, 0.15f, 0.17f, 0.9f);
        var rect = new Rect(0, axisY, width, axisHeight);
        context.FillRectangle(rect, bgBrush);

        // 绘制时间轴顶部线
        var lineBrush = resources.GetSolidBrush(TimeAxisColor);
        context.DrawLine(
            new Vector2(0, axisY),
            new Vector2(width, axisY),
            lineBrush,
            1.0f);

        // 绘制刻度（占位：固定间隔）
        float majorTickSpacing = 100.0f * dpiScale;  // 主刻度间距
        float minorTickSpacing = 20.0f * dpiScale;   // 次刻度间距

        // 次刻度
        for (float x = 0; x <= width; x += minorTickSpacing)
        {
            context.DrawLine(
                new Vector2(x, axisY),
                new Vector2(x, axisY + TickMarkMinorLength * dpiScale),
                lineBrush,
                0.5f);
        }

        // 主刻度 + 标签
        var textBrush = resources.GetSolidBrush(TextColor);
        var textFormat = resources.SmallTextFormat;
        int labelIndex = 0;

        for (float x = 0; x <= width; x += majorTickSpacing)
        {
            // 绘制主刻度
            context.DrawLine(
                new Vector2(x, axisY),
                new Vector2(x, axisY + TickMarkMajorLength * dpiScale),
                lineBrush,
                1.0f);

            // 绘制时间标签（占位）
            string label = $"{labelIndex}s";
            var textRect = new Rect(
                x - 20 * dpiScale,
                axisY + TickMarkMajorLength * dpiScale + 2,
                40 * dpiScale,
                axisHeight - TickMarkMajorLength * dpiScale);

            context.DrawText(
                label,
                textFormat,
                textRect,
                textBrush);

            labelIndex++;
        }
    }

    /// <summary>
    /// 绘制光标（占位）。
    /// </summary>
    private void DrawCursor(
        ID2D1DeviceContext context,
        ResourceCache resources,
        float width,
        float height,
        float dpiScale)
    {
        // 限制光标位置
        float pos = Math.Clamp(CursorPosition, 0f, 1f);
        float cursorX = pos * width;

        // 绘制光标线
        var cursorBrush = resources.GetSolidBrush(CursorColor);
        context.DrawLine(
            new Vector2(cursorX, 0),
            new Vector2(cursorX, height - TimeAxisHeight * dpiScale),
            cursorBrush,
            CursorLineWidth);

        // 绘制光标顶部标记（三角形占位）
        float markerSize = 8.0f * dpiScale;
        DrawTriangleMarker(context, cursorBrush, cursorX, 0, markerSize);
    }

    /// <summary>
    /// 绘制三角形标记。
    /// </summary>
    private static void DrawTriangleMarker(
        ID2D1DeviceContext context,
        ID2D1SolidColorBrush brush,
        float x,
        float y,
        float size)
    {
        // 绘制简单的向下三角形（用线条近似）
        context.DrawLine(
            new Vector2(x - size / 2, y),
            new Vector2(x, y + size),
            brush,
            2.0f);
        context.DrawLine(
            new Vector2(x + size / 2, y),
            new Vector2(x, y + size),
            brush,
            2.0f);
        context.DrawLine(
            new Vector2(x - size / 2, y),
            new Vector2(x + size / 2, y),
            brush,
            2.0f);
    }
}
