// UiAeegGridAndAxisRenderer.cs
// UI-specific aEEG grid/axis renderer with dark theme and tuned time grid.

using System.Numerics;
using Neo.Rendering.Core;
using Neo.Rendering.Mapping;
using Neo.Rendering.Resources;
using Vortice.Direct2D1;
using Vortice.Mathematics;

namespace Neo.UI.Rendering;

public sealed class UiAeegGridAndAxisRenderer
{
    private AeegSemiLogMapper? _mapper;
    private double _lastHeight;
    private AeegAxisTick[]? _cachedTicks;

    public void Render(
        ID2D1DeviceContext context,
        ResourceCache resources,
        Rect renderArea,
        bool showMinorTicks = true,
        bool showLabels = true,
        float labelMargin = 5.0f)
    {
        double areaHeight = renderArea.Height;
        if (_mapper == null || Math.Abs(_lastHeight - areaHeight) > 0.01)
        {
            _mapper = new AeegSemiLogMapper(areaHeight);
            _cachedTicks = AeegAxisTicks.GetTicks(areaHeight);
            _lastHeight = areaHeight;
        }

        var majorGridBrush = resources.GetSolidBrush(UiAeegPalette.MajorGridLine);
        var minorGridBrush = resources.GetSolidBrush(UiAeegPalette.MinorGridLine);
        var boundaryBrush = resources.GetSolidBrush(UiAeegPalette.BoundaryLine);
        var axisBrush = resources.GetSolidBrush(UiAeegPalette.AxisLine);
        var labelBrush = resources.GetSolidBrush(UiAeegPalette.AxisLabel);
        var backgroundBrush = resources.GetSolidBrush(UiAeegPalette.Background);

        var textFormat = resources.GetTextFormat("Segoe UI", 10.0f);

        context.FillRectangle(renderArea, backgroundBrush);

        foreach (var tick in _cachedTicks!)
        {
            float y = (float)(renderArea.Top + tick.Y);
            if (y < renderArea.Top || y > renderArea.Bottom)
                continue;

            ID2D1SolidColorBrush lineBrush;
            float lineWidth;

            if (Math.Abs(tick.VoltageUv - AeegSemiLogMapper.LinearLogBoundaryUv) < 0.01)
            {
                lineBrush = boundaryBrush;
                lineWidth = 2.0f;
            }
            else if (tick.IsMajor)
            {
                lineBrush = majorGridBrush;
                lineWidth = 1.2f;
            }
            else
            {
                if (!showMinorTicks)
                    continue;
                lineBrush = minorGridBrush;
                lineWidth = 0.6f;
            }

            context.DrawLine(
                new Vector2((float)renderArea.Left, y),
                new Vector2((float)renderArea.Right, y),
                lineBrush,
                lineWidth);

            if (showLabels && (tick.IsMajor || tick.VoltageUv == 25))
            {
                var labelRect = new Rect(
                    renderArea.Left + labelMargin,
                    y - 7,
                    60,
                    14);

                context.DrawText(
                    tick.Label,
                    textFormat,
                    labelRect,
                    labelBrush);
            }
        }

        context.DrawLine(
            new Vector2((float)renderArea.Left, (float)renderArea.Top),
            new Vector2((float)renderArea.Left, (float)renderArea.Bottom),
            axisBrush,
            1.5f);
    }

    public void RenderTimeGrid(
        ID2D1DeviceContext context,
        ResourceCache resources,
        Rect renderArea,
        TimeRange visibleRange,
        int viewportWidth,
        double majorIntervalSeconds = 300.0,
        double minorIntervalSeconds = 60.0)
    {
        var majorGridBrush = resources.GetSolidBrush(UiAeegPalette.MajorGridLine);
        var minorGridBrush = resources.GetSolidBrush(UiAeegPalette.MinorGridLine);

        long majorIntervalUs = (long)(majorIntervalSeconds * 1_000_000);
        long minorIntervalUs = (long)(minorIntervalSeconds * 1_000_000);

        double TimestampToX(long timestampUs)
        {
            if (visibleRange.DurationUs == 0) return 0;
            double normalized = (double)(timestampUs - visibleRange.StartUs) / visibleRange.DurationUs;
            return renderArea.Left + normalized * viewportWidth;
        }

        long firstMinorUs = (visibleRange.StartUs / minorIntervalUs) * minorIntervalUs;
        for (long ts = firstMinorUs; ts <= visibleRange.EndUs; ts += minorIntervalUs)
        {
            if (ts < visibleRange.StartUs) continue;
            if (ts % majorIntervalUs == 0) continue;

            double x = TimestampToX(ts);
            context.DrawLine(
                new Vector2((float)x, (float)renderArea.Top),
                new Vector2((float)x, (float)renderArea.Bottom),
                minorGridBrush,
                0.5f);
        }

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

    public void Invalidate()
    {
        _mapper = null;
        _cachedTicks = null;
    }
}
