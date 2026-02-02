// EegPolylineRenderer.cs
// EEG 折线渲染器 - 来源: ARCHITECTURE.md §5, 00_CONSTITUTION.md 铁律6

using System.Numerics;
using Neo.Rendering.Resources;
using Vortice.Direct2D1;
using Vortice.Mathematics;

namespace Neo.Rendering.EEG;

/// <summary>
/// EEG 折线渲染器。
/// 接收预构建的渲染数据，只执行 Draw 调用。
/// </summary>
/// <remarks>
/// 依据: ARCHITECTURE.md §5, 00_CONSTITUTION.md 铁律6
///
/// 设计原则:
/// - 接收预构建的 EegWaveformRenderData（由 PolylineBuilder 构建）
/// - 只执行 Draw 调用，无 O(N) 计算
/// - 处理间隙遮罩和饱和标记
///
/// 铁律约束:
/// - 铁律2: 不伪造波形，间隙必须断线
/// - 铁律5: 缺失/饱和必须可见
/// - 铁律6: 渲染只做 Draw，无 O(N) 计算
///
/// 使用方式:
/// 1. 预处理线程调用 PolylineBuilder.Build() 构建数据
/// 2. 将结果封装为 EegWaveformRenderData
/// 3. 渲染线程调用 EegPolylineRenderer.Render() 只做 Draw
/// </remarks>
public sealed class EegPolylineRenderer
{
    /// <summary>
    /// 渲染 EEG 波形。
    /// </summary>
    /// <param name="context">D2D 设备上下文。</param>
    /// <param name="resources">资源缓存。</param>
    /// <param name="renderData">预构建的渲染数据。</param>
    /// <remarks>
    /// 铁律6: 只做 Draw 调用，无 O(N) 计算。
    /// 所有数据已由 PolylineBuilder 在预处理阶段构建。
    /// </remarks>
    public void Render(
        ID2D1DeviceContext context,
        ResourceCache resources,
        in EegWaveformRenderData renderData)
    {
        if (!renderData.HasData)
            return;

        // 获取通用画刷（ResourceCache 已缓存，O(1) 操作）
        var gapBrush = resources.GetSolidBrush(EegColorPalette.GapMask);
        var saturationBrush = resources.GetSolidBrush(EegColorPalette.SaturationMarker);
        var backgroundBrush = resources.GetSolidBrush(EegColorPalette.ChannelBackground);
        var baselineBrush = resources.GetSolidBrush(EegColorPalette.Baseline);

        var channels = renderData.Channels;

        // 渲染每个通道
        for (int c = 0; c < channels.Length; c++)
        {
            RenderChannel(context, resources, channels[c],
                gapBrush, saturationBrush, backgroundBrush, baselineBrush);
        }
    }

    /// <summary>
    /// 渲染单个通道。
    /// </summary>
    private static void RenderChannel(
        ID2D1DeviceContext context,
        ResourceCache resources,
        in EegChannelRenderData channel,
        ID2D1SolidColorBrush gapBrush,
        ID2D1SolidColorBrush saturationBrush,
        ID2D1SolidColorBrush backgroundBrush,
        ID2D1SolidColorBrush baselineBrush)
    {
        // 绘制通道背景
        context.FillRectangle(channel.ChannelArea, backgroundBrush);

        // 绘制基线
        context.DrawLine(
            new Vector2((float)channel.ChannelArea.Left, channel.BaselineY),
            new Vector2((float)channel.ChannelArea.Right, channel.BaselineY),
            baselineBrush,
            1.0f);

        // 绘制间隙遮罩（铁律5: 缺失可见）
        var gaps = channel.Gaps;
        for (int i = 0; i < gaps.Length; i++)
        {
            DrawGapMask(context, gapBrush, gaps[i], channel.ChannelArea);
        }

        // 获取通道颜色画刷
        var waveformBrush = resources.GetSolidBrush(channel.Color);

        var points = channel.Points;
        var segments = channel.Segments;
        var saturationIndices = channel.SaturationIndices;
        float lineWidth = channel.LineWidth;

        // 绘制波形线段（每个段是连续的，无间隙）
        for (int s = 0; s < segments.Length; s++)
        {
            var segment = segments[s];
            int startIdx = segment.StartIndex;
            int endIdx = startIdx + segment.PointCount;

            // 如果段内有饱和点，需要分段绘制
            if (segment.HasSaturation)
            {
                DrawSegmentWithSaturation(
                    context, waveformBrush, saturationBrush,
                    points, startIdx, endIdx,
                    saturationIndices, lineWidth);
            }
            else
            {
                // 无饱和点，直接绘制整段
                DrawSegment(context, waveformBrush, points, startIdx, endIdx, lineWidth);
            }
        }
    }

    /// <summary>
    /// 绘制连续线段（无饱和点）。
    /// </summary>
    private static void DrawSegment(
        ID2D1DeviceContext context,
        ID2D1SolidColorBrush brush,
        Vector2[] points,
        int startIdx,
        int endIdx,
        float lineWidth)
    {
        for (int i = startIdx + 1; i < endIdx; i++)
        {
            context.DrawLine(points[i - 1], points[i], brush, lineWidth);
        }
    }

    /// <summary>
    /// 绘制包含饱和点的线段。
    /// </summary>
    /// <remarks>
    /// 铁律6: 无分配。使用二分查找检查饱和状态，避免创建 HashSet。
    /// </remarks>
    private static void DrawSegmentWithSaturation(
        ID2D1DeviceContext context,
        ID2D1SolidColorBrush normalBrush,
        ID2D1SolidColorBrush saturationBrush,
        Vector2[] points,
        int startIdx,
        int endIdx,
        int[] saturationIndices,
        float lineWidth)
    {
        // 铁律6: 无分配，使用二分查找代替 HashSet
        for (int i = startIdx + 1; i < endIdx; i++)
        {
            // 使用二分查找检查饱和状态（O(log n)，无分配）
            bool isSaturated = Array.BinarySearch(saturationIndices, i) >= 0 ||
                               Array.BinarySearch(saturationIndices, i - 1) >= 0;
            var brush = isSaturated ? saturationBrush : normalBrush;
            context.DrawLine(points[i - 1], points[i], brush, lineWidth);
        }
    }

    /// <summary>
    /// 绘制间隙遮罩。
    /// </summary>
    private static void DrawGapMask(
        ID2D1DeviceContext context,
        ID2D1SolidColorBrush brush,
        in GapInfo gap,
        in Rect channelArea)
    {
        var rect = new Rect(
            gap.StartX,
            channelArea.Top,
            gap.EndX - gap.StartX,
            channelArea.Height);
        context.FillRectangle(rect, brush);
    }
}
