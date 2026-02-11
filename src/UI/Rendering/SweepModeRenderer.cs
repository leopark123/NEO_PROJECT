// SweepModeRenderer.cs
// Sprint 3.2-fix: Sweep mode waveform renderer
//
// Renders EEG waveforms in sweep mode:
// - New data appears from right, moves left
// - Existing data stays in place
// - Clear band ahead of sweep line
// - 15-second sweep period

using System.Numerics;
using Neo.Rendering.EEG;
using Neo.Rendering.Resources;
using Vortice.Direct2D1;
using Vortice.Mathematics;

namespace Neo.UI.Rendering;

/// <summary>
/// Renders EEG waveforms in sweep mode (right-to-left scan).
/// </summary>
public sealed class SweepModeRenderer
{
    private const float ChannelPadding = 6.0f;
    private const float RangeGutterWidth = 84.0f;

    /// <summary>
    /// Renders sweep mode waveforms.
    /// </summary>
    /// <param name="context">D2D device context.</param>
    /// <param name="resources">Resource cache.</param>
    /// <param name="channel">Sweep channel data.</param>
    /// <param name="area">Render area rectangle.</param>
    /// <param name="yAxisRangeUv">Y-axis display range in ±μV (default ±200).</param>
    /// <param name="gainMicrovoltsPerCm">Gain setting in μV/cm (default 100). Lower = more sensitive.</param>
    public void RenderChannel(
        ID2D1DeviceContext context,
        ResourceCache resources,
        in SweepChannelData channel,
        in Rect area,
        int yAxisRangeUv = 200,
        int gainMicrovoltsPerCm = 100)
    {
        if (area.Width <= 0 || area.Height <= 0)
            return;

        float width = (float)area.Width;
        float height = (float)area.Height;
        float gutterWidth = Math.Min(RangeGutterWidth, Math.Max(0f, width - 40f));
        float plotWidth = width - gutterWidth;
        if (plotWidth <= 10f)
            return;

        var gutterArea = new Rect(area.Left, area.Top, gutterWidth, area.Height);
        var plotArea = new Rect(area.Left + gutterWidth, area.Top, area.Width - gutterWidth, area.Height);

        float yOffset = (float)plotArea.Top + ChannelPadding;
        float baselineY = yOffset + (height - ChannelPadding * 2) / 2.0f;

        // Get brushes
        var clearBandBrush = resources.GetSolidBrush(UiEegPalette.ClearBandColor);
        var sweepLineBrush = resources.GetSolidBrush(UiEegPalette.SweepLineColor);
        var backgroundBrush = resources.GetSolidBrush(UiEegPalette.BackgroundColor);
        var baselineBrush = resources.GetSolidBrush(UiEegPalette.BaselineColor);

        // Get sweep parameters
        int samplesPerSweep = channel.SamplesPerSweep;
        int writeIndex = channel.WriteIndex;
        int clearBandSamples = channel.ClearBandSamples;

        // Calculate sweep line X position (right-to-left)
        float sweepLineX = (float)plotArea.Left + EegDataBridge.SampleIndexToX(writeIndex, samplesPerSweep, plotWidth);

        // Calculate clear band range (ahead of sweep line in rightward direction)
        int clearEndIndex = writeIndex;
        int clearStartIndex = (writeIndex + clearBandSamples) % samplesPerSweep;
        float clearStartX = (float)plotArea.Left + EegDataBridge.SampleIndexToX(clearStartIndex, samplesPerSweep, plotWidth);
        float clearEndX = (float)plotArea.Left + EegDataBridge.SampleIndexToX(clearEndIndex, samplesPerSweep, plotWidth);

        float channelHeight = height - ChannelPadding * 2;
        if (channelHeight < 10)
            return;

        // Draw background
        var channelRect = new Rect(area.Left, area.Top, area.Width, area.Height);
        context.FillRectangle(channelRect, backgroundBrush);

        // Draw grid
        DrawGrid(context, resources, plotArea, baselineY, channelHeight, plotWidth, yAxisRangeUv, gainMicrovoltsPerCm, channel);

        // Draw baseline
        context.DrawLine(
            new Vector2((float)plotArea.Left, baselineY),
            new Vector2((float)plotArea.Right, baselineY),
            baselineBrush, 1.0f);

        // Draw clear band
        if (clearStartX < clearEndX)
        {
            var clearRect = new Rect(clearStartX, plotArea.Top, clearEndX - clearStartX, plotArea.Height);
            context.FillRectangle(clearRect, clearBandBrush);
        }
        else
        {
            var clearRect1 = new Rect(plotArea.Left, plotArea.Top, clearEndX - (float)plotArea.Left, plotArea.Height);
            var clearRect2 = new Rect(clearStartX, plotArea.Top, (float)plotArea.Right - clearStartX, plotArea.Height);
            context.FillRectangle(clearRect1, clearBandBrush);
            context.FillRectangle(clearRect2, clearBandBrush);
        }

        // Draw waveform
        RenderChannelWaveform(context, resources, channel, plotWidth, (float)plotArea.Top, plotArea.Height, baselineY, channel.ChannelIndex, (float)plotArea.Left, yAxisRangeUv, gainMicrovoltsPerCm);

        // Draw sweep line
        context.DrawLine(
            new Vector2(sweepLineX, (float)plotArea.Top),
            new Vector2(sweepLineX, (float)plotArea.Bottom),
            sweepLineBrush, 2.0f);

        // Divider between range gutter and waveform plot area.
        var dividerBrush = resources.GetSolidBrush(UiEegPalette.RangeDividerColor);
        context.DrawLine(
            new Vector2((float)plotArea.Left, (float)plotArea.Top),
            new Vector2((float)plotArea.Left, (float)plotArea.Bottom),
            dividerBrush,
            1.0f);

        // Draw Y-range bracket and labels last, so it is never covered by clear band/waveform.
        DrawRangeBracket(context, resources, gutterArea, yOffset, channelHeight, yAxisRangeUv);
    }

    private void DrawGrid(
        ID2D1DeviceContext context,
        ResourceCache resources,
        in Rect area,
        float baselineY,
        float channelHeight,
        float width,
        int yAxisRangeUv,
        int gainMicrovoltsPerCm,
        in SweepChannelData channel)
    {
        var majorBrush = resources.GetSolidBrush(UiEegPalette.MajorGridColor);
        var minorBrush = resources.GetSolidBrush(UiEegPalette.MinorGridColor);

        float gainFactor = 100.0f / Math.Max(1, gainMicrovoltsPerCm);
        float uvToPixelScale = (channelHeight / (2.0f * yAxisRangeUv)) * gainFactor;

        int majorUvStep = 50;
        int minorUvStep = 10;

        for (int uv = minorUvStep; uv <= yAxisRangeUv; uv += minorUvStep)
        {
            float yUp = baselineY - uv * uvToPixelScale;
            float yDown = baselineY + uv * uvToPixelScale;
            if (yUp >= area.Top && yUp <= area.Bottom)
            {
                context.DrawLine(
                    new Vector2((float)area.Left, yUp),
                    new Vector2((float)area.Right, yUp),
                    (uv % majorUvStep == 0) ? majorBrush : minorBrush,
                    (uv % majorUvStep == 0) ? 1.0f : 0.5f);
            }

            if (yDown >= area.Top && yDown <= area.Bottom)
            {
                context.DrawLine(
                    new Vector2((float)area.Left, yDown),
                    new Vector2((float)area.Right, yDown),
                    (uv % majorUvStep == 0) ? majorBrush : minorBrush,
                    (uv % majorUvStep == 0) ? 1.0f : 0.5f);
            }
        }

        int sampleRate = channel.SampleRate > 0 ? channel.SampleRate : 160;
        int samplesPerSweep = Math.Max(1, channel.SamplesPerSweep);
        int totalSeconds = Math.Max(1, samplesPerSweep / sampleRate);

        for (int sec = 0; sec <= totalSeconds; sec++)
        {
            int sampleIndex = sec * sampleRate;
            if (sampleIndex > samplesPerSweep) break;
            float x = (float)area.Left + EegDataBridge.SampleIndexToX(sampleIndex, samplesPerSweep, width);
            bool isMajor = sec % 5 == 0;
            context.DrawLine(
                new Vector2(x, (float)area.Top),
                new Vector2(x, (float)area.Bottom),
                isMajor ? majorBrush : minorBrush,
                isMajor ? 1.0f : 0.5f);
        }
    }

    /// <summary>
    /// Renders a single channel's waveform.
    /// </summary>
    private void RenderChannelWaveform(
        ID2D1DeviceContext context,
        ResourceCache resources,
        SweepChannelData channel,
        float width,
        float yOffset,
        float channelHeight,
        float baselineY,
        int channelIndex,
        float xOffset,
        int yAxisRangeUv,
        int gainMicrovoltsPerCm)
    {
        var samples = channel.Samples.Span;
        int samplesPerSweep = channel.SamplesPerSweep;
        int writeIndex = channel.WriteIndex;
        int clearBandSamples = channel.ClearBandSamples;

        // Get channel color
        var channelColor = EegColorPalette.GetChannelColor(channelIndex);
        var waveformBrush = resources.GetSolidBrush(channelColor);

        // Scale factor: ±yAxisRangeUv μV maps to channel height
        // Gain affects sensitivity: lower gain = more sensitive = larger waveform
        // Reference gain is 100 μV/cm, so gainFactor = 100 / currentGain
        float gainFactor = 100.0f / Math.Max(1, gainMicrovoltsPerCm);
        float uvToPixelScale = (channelHeight / (2.0f * yAxisRangeUv)) * gainFactor;

        // Draw waveform as connected line segments
        // Skip samples in clear band
        Vector2? lastPoint = null;
        bool lastWasInClearBand = false;

        // Downsample for performance: draw every Nth point if too many samples
        int step = Math.Max(1, samplesPerSweep / (int)width);

        for (int i = 0; i < samplesPerSweep; i += step)
        {
            // Check if in clear band (ahead of sweep line in rightward direction)
            int distance = (i - writeIndex + samplesPerSweep) % samplesPerSweep;
            bool inClearBand = distance > 0 && distance <= clearBandSamples;

            if (inClearBand)
            {
                lastWasInClearBand = true;
                lastPoint = null;
                continue;
            }

            // Calculate X position (right-to-left)
            float x = xOffset + EegDataBridge.SampleIndexToX(i, samplesPerSweep, width);

            // Calculate Y position
            float uv = samples[i];
            float y = baselineY - uv * uvToPixelScale;

            // Clamp Y to channel bounds
            y = Math.Clamp(y, yOffset, yOffset + channelHeight);

            var currentPoint = new Vector2(x, y);

            // Draw line from last point if not crossing clear band
            if (lastPoint.HasValue && !lastWasInClearBand)
            {
                context.DrawLine(lastPoint.Value, currentPoint, waveformBrush, 1.5f);
            }

            lastPoint = currentPoint;
            lastWasInClearBand = false;
        }
    }

    private static void DrawRangeBracket(
        ID2D1DeviceContext context,
        ResourceCache resources,
        in Rect area,
        float yOffset,
        float channelHeight,
        int yAxisRangeUv)
    {
        if (area.Width <= 20 || area.Height <= 20)
        {
            return;
        }

        var bracketBrush = resources.GetSolidBrush(UiEegPalette.RangeBracketColor);
        var textBrush = resources.GetSolidBrush(UiEegPalette.RangeTextColor);
        var textBgBrush = resources.GetSolidBrush(UiEegPalette.BackgroundColor);
        var textFormat = resources.GetTextFormat("Segoe UI", 14.0f);

        float dividerX = MathF.Round((float)area.Right - 1f);
        float x = MathF.Round(dividerX - 12f);
        float tickLen = 7f;
        float topY = MathF.Round(yOffset + 2f);
        float bottomY = MathF.Round(yOffset + channelHeight - 2f);
        if (bottomY - topY < 12f)
        {
            return;
        }

        // Bracket shape
        context.DrawLine(new Vector2(x, topY), new Vector2(x, bottomY), bracketBrush, 1.0f);
        context.DrawLine(new Vector2(x, topY), new Vector2(x + tickLen, topY), bracketBrush, 1.0f);
        context.DrawLine(new Vector2(x, bottomY), new Vector2(x + tickLen, bottomY), bracketBrush, 1.0f);

        // Range labels
        string topLabel = $"+{yAxisRangeUv}uV";
        string bottomLabel = $"-{yAxisRangeUv}uV";

        float labelLeft = MathF.Round((float)area.Left + 2f);
        float labelRight = MathF.Round(x - tickLen - 3f);
        float labelWidth = MathF.Max(42f, labelRight - labelLeft);
        float labelHeight = 22f;
        float topLabelY = MathF.Round(Math.Clamp(
            topY - labelHeight + 2f,
            (float)area.Top + 1f,
            (float)area.Bottom - labelHeight - 1f));
        float bottomLabelY = MathF.Round(Math.Clamp(
            bottomY - 2f,
            (float)area.Top + 1f,
            (float)area.Bottom - labelHeight - 1f));

        var topRect = new Rect(labelLeft, topLabelY, labelWidth, labelHeight);
        context.FillRectangle(topRect, textBgBrush);
        context.DrawText(
            topLabel,
            textFormat,
            topRect,
            textBrush);

        var bottomRect = new Rect(labelLeft, bottomLabelY, labelWidth, labelHeight);
        context.FillRectangle(bottomRect, textBgBrush);
        context.DrawText(
            bottomLabel,
            textFormat,
            bottomRect,
            textBrush);
    }
}
