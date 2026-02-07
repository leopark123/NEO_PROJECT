// AeegGridAndAxisRenderer.cs
// aEEG 网格和轴线渲染器 - 来源: DSP_SPEC.md, CONSENSUS_BASELINE.md §6.4

using System.Numerics;
using Neo.Rendering.Core;
using Neo.Rendering.Mapping;
using Neo.Rendering.Resources;
using Vortice.Direct2D1;
using Vortice.Mathematics;

namespace Neo.Rendering.AEEG;

/// <summary>
/// aEEG 网格和轴线渲染器。
/// 绘制半对数 Y 轴刻度和网格线。
/// </summary>
/// <remarks>
/// 依据: DSP_SPEC.md, CONSENSUS_BASELINE.md §6.4
///
/// 使用 AeegAxisTicks (S2-04) 获取标准刻度。
///
/// 刻度规格（冻结，不可修改）:
/// - 主刻度: 0, 5, 10, 50, 100, 200 μV
/// - 次刻度: 1, 2, 3, 4, 25 μV
/// - 分界点: 10 μV（线性/对数分界）
///
/// 铁律约束:
/// - 铁律6: 渲染只做 Draw
/// </remarks>
public sealed class AeegGridAndAxisRenderer
{
    // 缓存
    private AeegSemiLogMapper? _mapper;
    private double _lastHeight;
    private AeegAxisTick[]? _cachedTicks;

    /// <summary>
    /// 渲染网格和轴线。
    /// </summary>
    /// <param name="context">D2D 设备上下文。</param>
    /// <param name="resources">资源缓存。</param>
    /// <param name="renderArea">渲染区域。</param>
    /// <param name="showMinorTicks">是否显示次刻度。</param>
    /// <param name="showLabels">是否显示标签。</param>
    /// <param name="labelMargin">标签边距。</param>
    /// <remarks>
    /// 铁律6: 只做 Draw 调用。
    /// </remarks>
    public void Render(
        ID2D1DeviceContext context,
        ResourceCache resources,
        Rect renderArea,
        bool showMinorTicks = true,
        bool showLabels = true,
        float labelMargin = 5.0f)
    {
        double areaHeight = renderArea.Height;

        // 确保映射器和刻度缓存有效
        if (_mapper == null || Math.Abs(_lastHeight - areaHeight) > 0.01)
        {
            _mapper = new AeegSemiLogMapper(areaHeight);
            _cachedTicks = AeegAxisTicks.GetTicks(areaHeight);
            _lastHeight = areaHeight;
        }

        // 获取画刷
        var majorGridBrush = resources.GetSolidBrush(AeegColorPalette.MajorGridLine);
        var minorGridBrush = resources.GetSolidBrush(AeegColorPalette.MinorGridLine);
        var boundaryBrush = resources.GetSolidBrush(AeegColorPalette.BoundaryLine);
        var axisBrush = resources.GetSolidBrush(AeegColorPalette.AxisLine);
        var labelBrush = resources.GetSolidBrush(AeegColorPalette.AxisLabel);

        // 获取文本格式
        var textFormat = resources.GetTextFormat("Segoe UI", 10.0f);

        // 绘制背景
        var backgroundBrush = resources.GetSolidBrush(AeegColorPalette.Background);
        context.FillRectangle(renderArea, backgroundBrush);

        // 绘制刻度线
        foreach (var tick in _cachedTicks!)
        {
            float y = (float)(renderArea.Top + tick.Y);

            // 跳过超出范围的刻度
            if (y < renderArea.Top || y > renderArea.Bottom)
                continue;

            // 选择画刷
            ID2D1SolidColorBrush lineBrush;
            float lineWidth;

            if (Math.Abs(tick.VoltageUv - AeegSemiLogMapper.LinearLogBoundaryUv) < 0.01)
            {
                // 分界点 (10 μV) 使用特殊颜色
                lineBrush = boundaryBrush;
                lineWidth = 2.0f;
            }
            else if (tick.IsMajor)
            {
                lineBrush = majorGridBrush;
                lineWidth = 1.5f;
            }
            else
            {
                if (!showMinorTicks)
                    continue;
                lineBrush = minorGridBrush;
                lineWidth = 0.5f;
            }

            // 绘制水平网格线
            context.DrawLine(
                new Vector2((float)renderArea.Left, y),
                new Vector2((float)renderArea.Right, y),
                lineBrush,
                lineWidth);

            // 绘制标签
            if (showLabels && (tick.IsMajor || tick.VoltageUv == 25))
            {
                string label = tick.Label;
                // 标签垂直居中对齐刻度线，但确保不超出renderArea顶部和底部
                float labelTop = y - 7;
                const float labelHeight = 14;

                // Clamp to prevent overflow at top
                labelTop = Math.Max((float)renderArea.Top, labelTop);
                // Clamp to prevent overflow at bottom
                labelTop = Math.Min((float)(renderArea.Bottom - labelHeight), labelTop);

                var labelRect = new Rect(
                    renderArea.Left + labelMargin,
                    labelTop,
                    renderArea.Width - labelMargin * 2,
                    labelHeight);

                context.DrawText(
                    label,
                    textFormat,
                    labelRect,
                    labelBrush);
            }
        }

        // 绘制左侧轴线
        context.DrawLine(
            new Vector2((float)renderArea.Left, (float)renderArea.Top),
            new Vector2((float)renderArea.Left, (float)renderArea.Bottom),
            axisBrush,
            2.0f);
    }

    /// <summary>
    /// 渲染时间轴网格。
    /// </summary>
    /// <param name="context">D2D 设备上下文。</param>
    /// <param name="resources">资源缓存。</param>
    /// <param name="renderArea">渲染区域。</param>
    /// <param name="visibleRange">可见时间范围。</param>
    /// <param name="viewportWidth">视口宽度。</param>
    /// <param name="majorIntervalSeconds">主刻度间隔（秒）。</param>
    /// <param name="minorIntervalSeconds">次刻度间隔（秒）。</param>
    public void RenderTimeGrid(
        ID2D1DeviceContext context,
        ResourceCache resources,
        Rect renderArea,
        TimeRange visibleRange,
        int viewportWidth,
        double majorIntervalSeconds = 60.0,
        double minorIntervalSeconds = 15.0)
    {
        var majorGridBrush = resources.GetSolidBrush(AeegColorPalette.MajorGridLine);
        var minorGridBrush = resources.GetSolidBrush(AeegColorPalette.MinorGridLine);

        long majorIntervalUs = (long)(majorIntervalSeconds * 1_000_000);
        long minorIntervalUs = (long)(minorIntervalSeconds * 1_000_000);

        // 时间戳转 X 坐标
        double TimestampToX(long timestampUs)
        {
            if (visibleRange.DurationUs == 0) return 0;
            double normalized = (double)(timestampUs - visibleRange.StartUs) / visibleRange.DurationUs;
            return renderArea.Left + normalized * viewportWidth;
        }

        // 绘制次刻度线
        long firstMinorUs = (visibleRange.StartUs / minorIntervalUs) * minorIntervalUs;
        for (long ts = firstMinorUs; ts <= visibleRange.EndUs; ts += minorIntervalUs)
        {
            if (ts < visibleRange.StartUs) continue;

            // 跳过主刻度位置
            if (ts % majorIntervalUs == 0) continue;

            double x = TimestampToX(ts);
            context.DrawLine(
                new Vector2((float)x, (float)renderArea.Top),
                new Vector2((float)x, (float)renderArea.Bottom),
                minorGridBrush,
                0.5f);
        }

        // 绘制主刻度线
        long firstMajorUs = (visibleRange.StartUs / majorIntervalUs) * majorIntervalUs;
        for (long ts = firstMajorUs; ts <= visibleRange.EndUs; ts += majorIntervalUs)
        {
            if (ts < visibleRange.StartUs) continue;

            double x = TimestampToX(ts);
            context.DrawLine(
                new Vector2((float)x, (float)renderArea.Top),
                new Vector2((float)x, (float)renderArea.Bottom),
                majorGridBrush,
                1.0f);
        }
    }

    /// <summary>
    /// 重置缓存（视口变化时调用）。
    /// </summary>
    public void Invalidate()
    {
        _mapper = null;
        _cachedTicks = null;
    }
}
