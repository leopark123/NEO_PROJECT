// TimeAxisRenderer.cs
// Shared time axis and vertical time-grid renderer for timeline-style plots.

using System.Numerics;
using Neo.Rendering.Core;
using Neo.Rendering.Resources;
using Vortice.Direct2D1;
using Vortice.Mathematics;

namespace Neo.Rendering.Layers;

/// <summary>
/// Renders time grid/ticks/labels for a plot area and its axis strip.
/// </summary>
public sealed class TimeAxisRenderer
{
    public readonly record struct RenderOptions(
        bool ShowMinorTicks,
        bool ShowLabels,
        bool ShowCurrentTimeIndicator,
        long MajorGridIntervalUs,
        long MinorGridIntervalUs,
        Color4 BackgroundColor,
        Color4 MajorGridColor,
        Color4 MinorGridColor,
        Color4 LabelColor,
        Color4 CurrentTimeColor)
    {
        public static RenderOptions Default => new(
            ShowMinorTicks: true,
            ShowLabels: true,
            ShowCurrentTimeIndicator: false,
            MajorGridIntervalUs: 300L * 1_000_000, // 5 min
            MinorGridIntervalUs: 60L * 1_000_000,  // 1 min
            BackgroundColor: new Color4(0f, 0f, 0f, 1f),
            MajorGridColor: new Color4(0.23f, 0.29f, 0.23f, 0.6f),
            MinorGridColor: new Color4(0.16f, 0.23f, 0.16f, 0.3f),
            LabelColor: new Color4(0.8f, 0.8f, 0.8f, 1f),
            CurrentTimeColor: new Color4(0.3f, 0.45f, 0.3f, 0.9f));
    }

    /// <summary>
    /// Selects label tick interval by visible duration.
    /// </summary>
    public static long SelectLabelIntervalUs(long durationUs)
    {
        double durationHours = durationUs / (3600.0 * 1_000_000);
        return durationHours switch
        {
            <= 6 => 60L * 60 * 1_000_000,      // 1h
            <= 12 => 2L * 60 * 60 * 1_000_000, // 2h
            _ => 3L * 60 * 60 * 1_000_000      // 3h
        };
    }

    /// <summary>
    /// Aligns timestamp down to interval boundary.
    /// </summary>
    public static long AlignToIntervalStart(long timestampUs, long intervalUs)
    {
        if (intervalUs <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(intervalUs), "Interval must be positive.");
        }

        return (timestampUs / intervalUs) * intervalUs;
    }

    /// <summary>
    /// Formats timestamp (microseconds) as HH:mm:ss.
    /// </summary>
    public static string FormatTimestamp(long timestampUs)
    {
        long totalSeconds = Math.Max(0, timestampUs / 1_000_000);
        int hours = (int)(totalSeconds / 3600);
        int minutes = (int)((totalSeconds % 3600) / 60);
        int seconds = (int)(totalSeconds % 60);
        return $"{hours:D2}:{minutes:D2}:{seconds:D2}";
    }

    /// <summary>
    /// Renders vertical time grid on plot and labels on axis strip.
    /// </summary>
    public void Render(
        ID2D1DeviceContext context,
        ResourceCache resources,
        in TimeRange visibleRange,
        in Rect plotArea,
        in Rect axisArea,
        RenderOptions options)
    {
        if (visibleRange.DurationUs <= 0)
        {
            return;
        }

        if (plotArea.Width <= 0 || plotArea.Height <= 0 || axisArea.Width <= 0 || axisArea.Height <= 0)
        {
            return;
        }

        long majorIntervalUs = Math.Max(1_000_000, options.MajorGridIntervalUs);
        long minorIntervalUs = Math.Max(1_000_000, options.MinorGridIntervalUs);
        if (majorIntervalUs < minorIntervalUs)
        {
            majorIntervalUs = minorIntervalUs;
        }

        var majorGridBrush = resources.GetSolidBrush(options.MajorGridColor);
        var minorGridBrush = resources.GetSolidBrush(options.MinorGridColor);
        var axisBgBrush = resources.GetSolidBrush(options.BackgroundColor);
        var labelBrush = resources.GetSolidBrush(options.LabelColor);
        var currentTimeBrush = resources.GetSolidBrush(options.CurrentTimeColor);
        var textFormat = resources.GetTextFormat("Segoe UI", 9.0f);

        context.FillRectangle(axisArea, axisBgBrush);

        long startUs = visibleRange.StartUs;
        long durationUs = visibleRange.DurationUs;
        float plotLeft = (float)plotArea.Left;
        float plotWidth = (float)plotArea.Width;

        float TimestampToX(long timestampUs)
        {
            double normalized = (double)(timestampUs - startUs) / durationUs;
            normalized = Math.Clamp(normalized, 0.0, 1.0);
            return plotLeft + (float)(normalized * plotWidth);
        }

        if (options.ShowMinorTicks)
        {
            long firstMinorUs = AlignToIntervalStart(visibleRange.StartUs, minorIntervalUs);
            for (long ts = firstMinorUs; ts <= visibleRange.EndUs; ts += minorIntervalUs)
            {
                if (ts < visibleRange.StartUs || ts % majorIntervalUs == 0)
                {
                    continue;
                }

                float x = TimestampToX(ts);
                context.DrawLine(
                    new Vector2(x, (float)plotArea.Top),
                    new Vector2(x, (float)plotArea.Bottom),
                    minorGridBrush,
                    0.5f);
            }
        }

        long firstMajorUs = AlignToIntervalStart(visibleRange.StartUs, majorIntervalUs);
        for (long ts = firstMajorUs; ts <= visibleRange.EndUs; ts += majorIntervalUs)
        {
            if (ts < visibleRange.StartUs)
            {
                continue;
            }

            float x = TimestampToX(ts);
            context.DrawLine(
                new Vector2(x, (float)plotArea.Top),
                new Vector2(x, (float)plotArea.Bottom),
                majorGridBrush,
                1.0f);
        }

        if (options.ShowLabels)
        {
            long labelIntervalUs = SelectLabelIntervalUs(visibleRange.DurationUs);
            long firstLabelUs = AlignToIntervalStart(visibleRange.StartUs, labelIntervalUs) + labelIntervalUs;

            for (long ts = firstLabelUs; ts <= visibleRange.EndUs; ts += labelIntervalUs)
            {
                if (ts < visibleRange.StartUs)
                {
                    continue;
                }

                float x = TimestampToX(ts);
                var labelRect = new Rect(x - 28f, axisArea.Top + 2f, 56f, Math.Max(0f, axisArea.Height - 4f));
                context.DrawText(FormatTimestamp(ts), textFormat, labelRect, labelBrush);
            }

            // Always render edge labels so short windows still have visible timestamps.
            string startLabel = FormatTimestamp(visibleRange.StartUs);
            string endLabel = FormatTimestamp(visibleRange.EndUs);
            float labelHeight = Math.Max(0f, (float)axisArea.Height - 4f);

            var startRect = new Rect((float)axisArea.Left + 2f, (float)axisArea.Top + 2f, 58f, labelHeight);
            context.DrawText(startLabel, textFormat, startRect, labelBrush);

            if (axisArea.Width > 130f)
            {
                var endRect = new Rect((float)axisArea.Right - 60f, (float)axisArea.Top + 2f, 58f, labelHeight);
                context.DrawText(endLabel, textFormat, endRect, labelBrush);
            }
        }

        if (options.ShowCurrentTimeIndicator)
        {
            float x = (float)plotArea.Right;
            context.DrawLine(
                new Vector2(x, (float)plotArea.Top),
                new Vector2(x, (float)axisArea.Bottom),
                currentTimeBrush,
                1.5f);
        }
    }
}
