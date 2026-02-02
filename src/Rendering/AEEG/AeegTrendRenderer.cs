// AeegTrendRenderer.cs
// aEEG 趋势渲染器 - 来源: DSP_SPEC.md §3, ARCHITECTURE.md §5

using System.Numerics;
using Neo.Rendering.Resources;
using Vortice.Direct2D1;
using Vortice.Mathematics;

namespace Neo.Rendering.AEEG;

/// <summary>
/// aEEG 趋势预构建渲染数据。
/// </summary>
/// <remarks>
/// 此结构由 AeegSeriesBuilder 在预处理阶段构建，
/// 渲染器只读取并执行 Draw 调用。
///
/// 铁律6: 渲染线程只做 Draw，无 O(N) 计算。
/// </remarks>
public readonly struct AeegTrendRenderData
{
    /// <summary>
    /// 预构建的趋势点数组。
    /// </summary>
    public required AeegTrendPoint[] Points { get; init; }

    /// <summary>
    /// 连续线段列表。
    /// </summary>
    public required AeegTrendSegment[] Segments { get; init; }

    /// <summary>
    /// 间隙区域列表。
    /// </summary>
    public required AeegGapInfo[] Gaps { get; init; }

    /// <summary>
    /// 渲染区域。
    /// </summary>
    public required Rect RenderArea { get; init; }

    /// <summary>
    /// 是否有有效数据。
    /// </summary>
    public bool HasData => Points.Length > 0;
}

/// <summary>
/// aEEG 趋势渲染器。
/// 使用半对数 Y 轴渲染 aEEG 趋势。
/// </summary>
/// <remarks>
/// 依据: DSP_SPEC.md §3, ARCHITECTURE.md §5
///
/// 设计原则:
/// - 接收预构建的 AeegTrendRenderData（由 AeegSeriesBuilder 构建）
/// - 只执行 Draw 调用，无 O(N) 计算
/// - 使用 AeegSemiLogMapper (S2-04) 进行 Y 轴映射（在构建阶段）
///
/// 铁律约束:
/// - 铁律2: 不伪造波形，间隙必须断线
/// - 铁律5: 缺失/饱和必须可见
/// - 铁律6: 渲染只做 Draw，无 O(N) 计算
///
/// 使用方式:
/// 1. 预处理线程调用 AeegSeriesBuilder.Build() 构建数据
/// 2. 将结果封装为 AeegTrendRenderData
/// 3. 渲染线程调用 AeegTrendRenderer.Render() 只做 Draw
/// </remarks>
public sealed class AeegTrendRenderer
{
    /// <summary>
    /// 渲染 aEEG 趋势。
    /// </summary>
    /// <param name="context">D2D 设备上下文。</param>
    /// <param name="resources">资源缓存。</param>
    /// <param name="renderData">预构建的渲染数据。</param>
    /// <remarks>
    /// 铁律6: 只做 Draw 调用，无 O(N) 计算。
    /// 所有数据已由 AeegSeriesBuilder 在预处理阶段构建。
    /// </remarks>
    public void Render(
        ID2D1DeviceContext context,
        ResourceCache resources,
        in AeegTrendRenderData renderData)
    {
        if (!renderData.HasData)
            return;

        // 获取画刷（ResourceCache 已缓存，O(1) 操作）
        var trendBrush = resources.GetSolidBrush(AeegColorPalette.TrendFill);
        var upperBoundBrush = resources.GetSolidBrush(AeegColorPalette.UpperBound);
        var lowerBoundBrush = resources.GetSolidBrush(AeegColorPalette.LowerBound);
        var gapBrush = resources.GetSolidBrush(AeegColorPalette.GapMask);

        var points = renderData.Points;
        var segments = renderData.Segments;
        var gaps = renderData.Gaps;
        var renderArea = renderData.RenderArea;

        // 绘制间隙遮罩（铁律5: 缺失可见）
        for (int i = 0; i < gaps.Length; i++)
        {
            DrawGapMask(context, gapBrush, gaps[i], renderArea);
        }

        // 绘制趋势线段（每个段是连续的，无间隙）
        for (int s = 0; s < segments.Length; s++)
        {
            var segment = segments[s];
            int endIndex = segment.StartIndex + segment.PointCount;

            // 绘制段内的线条
            for (int i = segment.StartIndex + 1; i < endIndex; i++)
            {
                var prev = points[i - 1];
                var curr = points[i];

                // 绘制带状区域
                DrawTrendBand(context, trendBrush, prev, curr);

                // 绘制上边界线
                context.DrawLine(
                    new Vector2(prev.X, prev.MaxY),
                    new Vector2(curr.X, curr.MaxY),
                    upperBoundBrush,
                    1.5f);

                // 绘制下边界线
                context.DrawLine(
                    new Vector2(prev.X, prev.MinY),
                    new Vector2(curr.X, curr.MinY),
                    lowerBoundBrush,
                    1.5f);
            }
        }
    }

    /// <summary>
    /// 绘制趋势带状区域。
    /// </summary>
    private static void DrawTrendBand(
        ID2D1DeviceContext context,
        ID2D1SolidColorBrush brush,
        in AeegTrendPoint prev,
        in AeegTrendPoint curr)
    {
        // 绘制填充矩形（近似带状区域）
        var rect = new Rect(
            Math.Min(prev.X, curr.X),
            Math.Min(Math.Min(prev.MinY, curr.MinY), Math.Min(prev.MaxY, curr.MaxY)),
            Math.Abs(curr.X - prev.X),
            Math.Max(Math.Max(prev.MinY, curr.MinY), Math.Max(prev.MaxY, curr.MaxY)) -
            Math.Min(Math.Min(prev.MinY, curr.MinY), Math.Min(prev.MaxY, curr.MaxY)));

        context.FillRectangle(rect, brush);
    }

    /// <summary>
    /// 绘制间隙遮罩。
    /// </summary>
    private static void DrawGapMask(
        ID2D1DeviceContext context,
        ID2D1SolidColorBrush brush,
        in AeegGapInfo gap,
        in Rect renderArea)
    {
        var rect = new Rect(
            gap.StartX,
            renderArea.Top,
            gap.EndX - gap.StartX,
            renderArea.Height);
        context.FillRectangle(rect, brush);
    }
}
