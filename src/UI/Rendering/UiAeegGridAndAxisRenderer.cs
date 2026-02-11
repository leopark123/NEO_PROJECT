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

        var textFormat = resources.GetTextFormat("Segoe UI", 14.0f);

        context.FillRectangle(renderArea, backgroundBrush);

        foreach (var tick in _cachedTicks!)
        {
            float y = (float)(renderArea.Top + tick.Y);
            float yAligned = MathF.Round(y);
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
                new Vector2((float)renderArea.Left, yAligned),
                new Vector2((float)renderArea.Right, yAligned),
                lineBrush,
                lineWidth);

            if (showLabels && (tick.IsMajor || tick.VoltageUv == 25))
            {
                float labelHeight = 24f;
                float labelTop = TryGetEvenlyDistributedLabelTop(
                    tick.VoltageUv,
                    renderArea,
                    labelHeight,
                    out float evenTop)
                    ? evenTop
                    : MathF.Round(Math.Clamp(
                        yAligned - labelHeight * 0.5f,
                        (float)renderArea.Top + 4f,
                        (float)renderArea.Bottom - labelHeight - 2f));
                float labelLeft = MathF.Round((float)renderArea.Left + labelMargin + 1f);
                float labelWidth = MathF.Max(42f, (float)renderArea.Width - labelMargin - 4f);
                var labelRect = new Rect(
                    labelLeft,
                    labelTop,
                    labelWidth,
                    labelHeight);

                // Mask grid lines under labels to improve legibility.
                context.FillRectangle(labelRect, backgroundBrush);
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

    private static bool TryGetEvenlyDistributedLabelTop(
        double voltageUv,
        Rect renderArea,
        float labelHeight,
        out float labelTop)
    {
        // Keep the major displayed labels roughly evenly spaced for readability:
        // 0, 5, 10, 25, 50, 100, 200.
        ReadOnlySpan<double> order = [200d, 100d, 50d, 25d, 10d, 5d, 0d];
        const double epsilon = 0.01;
        int index = -1;
        for (int i = 0; i < order.Length; i++)
        {
            if (Math.Abs(order[i] - voltageUv) < epsilon)
            {
                index = i;
                break;
            }
        }

        if (index < 0)
        {
            labelTop = 0f;
            return false;
        }

        float topInset = 8f;
        float bottomInset = 8f;
        float usableHeight = MathF.Max(1f, (float)renderArea.Height - topInset - bottomInset);
        float step = usableHeight / MathF.Max(1, order.Length - 1);
        float centerY = (float)renderArea.Top + topInset + step * index;
        labelTop = MathF.Round(Math.Clamp(
            centerY - labelHeight * 0.5f,
            (float)renderArea.Top + 4f,
            (float)renderArea.Bottom - labelHeight - 2f));
        return true;
    }
}
