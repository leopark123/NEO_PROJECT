// SeekBarRenderer.cs
// Phase 3.5: Draws a basic SeekBar timeline inside a target rect.

using System.Numerics;
using Neo.Rendering.Resources;
using Vortice.Direct2D1;
using Vortice.Mathematics;

namespace Neo.UI.Rendering;

public sealed class SeekBarRenderer
{
    private static readonly Color4 TrackColor = new(0.20f, 0.22f, 0.25f, 1.0f);
    private static readonly Color4 FillColor = new(0.55f, 0.65f, 0.90f, 1.0f);
    private static readonly Color4 HandleColor = new(0.95f, 0.95f, 1.0f, 1.0f);
    private static readonly Color4 LabelColor = new(0.80f, 0.80f, 0.80f, 1.0f);
    private static readonly Color4 TickColor = new(0.60f, 0.60f, 0.60f, 0.8f);

    public void Render(
        ID2D1DeviceContext context,
        ResourceCache resources,
        in Rect area,
        float position,
        long currentTimestampUs,
        long totalDurationUs,
        long startTimestampUs)
    {
        if (area.Width <= 0 || area.Height <= 0)
            return;

        position = Math.Clamp(position, 0f, 1f);

        float trackHeight = Math.Max(4f, area.Height * 0.18f);
        float labelBandHeight = Math.Max(12f, area.Height * 0.35f);
        float trackY = (float)(area.Top + area.Height - trackHeight - 4);
        float trackX = (float)(area.Left + area.Width * 0.06);
        float trackW = (float)(area.Width * 0.88);

        var trackRect = new Rect(trackX, trackY, trackW, trackHeight);
        var fillRect = new Rect(trackX, trackY, trackW * position, trackHeight);

        var trackBrush = resources.GetSolidBrush(TrackColor);
        var fillBrush = resources.GetSolidBrush(FillColor);
        var handleBrush = resources.GetSolidBrush(HandleColor);

        // Track + fill
        context.FillRectangle(trackRect, trackBrush);
        context.FillRectangle(fillRect, fillBrush);

        // Handle
        float handleX = trackX + trackW * position;
        float handleRadius = Math.Max(6f, trackHeight * 1.4f);
        var handleCenter = new Vector2(handleX, trackY + trackHeight * 0.5f);
        context.FillEllipse(new Ellipse(handleCenter, handleRadius, handleRadius), handleBrush);

        DrawTimeAxis(context, resources, area, trackX, trackW, labelBandHeight, totalDurationUs, startTimestampUs);
        DrawCurrentTime(context, resources, area, currentTimestampUs);
    }

    private void DrawTimeAxis(
        ID2D1DeviceContext context,
        ResourceCache resources,
        in Rect area,
        float trackX,
        float trackW,
        float labelBandHeight,
        long totalDurationUs,
        long startTimestampUs)
    {
        long durationUs = Math.Max(1, totalDurationUs);
        var tickBrush = resources.GetSolidBrush(TickColor);
        var labelBrush = resources.GetSolidBrush(GetThemeTextColorOrDefault(LabelColor));
        var textFormat = resources.GetTextFormat("Segoe UI", 8.5f);

        long majorIntervalUs = 60L * 60 * 1_000_000;      // 1h
        long minorIntervalUs = 15L * 60 * 1_000_000;      // 15m
        if (durationUs <= 2 * 60L * 60 * 1_000_000)
        {
            majorIntervalUs = 30L * 60 * 1_000_000;       // 30m
            minorIntervalUs = 5L * 60 * 1_000_000;        // 5m
        }

        long firstMinor = (startTimestampUs / minorIntervalUs) * minorIntervalUs;
        for (long ts = firstMinor; ts <= startTimestampUs + durationUs; ts += minorIntervalUs)
        {
            if (ts < startTimestampUs) continue;
            double norm = (double)(ts - startTimestampUs) / durationUs;
            float x = trackX + (float)(norm * trackW);
            bool isMajor = ts % majorIntervalUs == 0;
            float tickHeight = isMajor ? labelBandHeight * 0.8f : labelBandHeight * 0.4f;
            context.DrawLine(
                new Vector2(x, (float)area.Top + 2),
                new Vector2(x, (float)area.Top + 2 + tickHeight),
                tickBrush,
                isMajor ? 1.0f : 0.6f);

            if (isMajor)
            {
                string label = FormatTimestamp(ts);
                var labelRect = new Rect(x - 24, area.Top + 2, 48, labelBandHeight);
                context.DrawText(label, textFormat, labelRect, labelBrush);
            }
        }
    }

    private void DrawCurrentTime(
        ID2D1DeviceContext context,
        ResourceCache resources,
        in Rect area,
        long currentTimestampUs)
    {
        var labelBrush = resources.GetSolidBrush(GetThemeTextColorOrDefault(LabelColor));
        var textFormat = resources.GetTextFormat("Segoe UI", 8.5f);
        string label = FormatTimestamp(currentTimestampUs);
        var labelRect = new Rect(area.Right - 60, area.Top + 2, 56, 12);
        context.DrawText(label, textFormat, labelRect, labelBrush);
    }

    private static Color4 GetThemeTextColorOrDefault(Color4 fallback)
    {
        try
        {
            if (System.Windows.Application.Current?.Resources["TextOnDarkBrush"] is System.Windows.Media.SolidColorBrush brush)
            {
                var c = brush.Color;
                return new Color4(c.ScR, c.ScG, c.ScB, c.ScA);
            }
        }
        catch
        {
            // Fallback keeps previous behavior when resources are unavailable.
        }

        return fallback;
    }

    private static string FormatTimestamp(long timestampUs)
    {
        long totalSeconds = Math.Max(0, timestampUs / 1_000_000);
        int hours = (int)(totalSeconds / 3600);
        int minutes = (int)((totalSeconds % 3600) / 60);
        int seconds = (int)(totalSeconds % 60);
        return $"{hours:D2}:{minutes:D2}:{seconds:D2}";
    }
}
