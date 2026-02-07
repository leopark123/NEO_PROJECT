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

    // Clear band appearance
    private static readonly Color4 ClearBandColor = new(0.10f, 0.10f, 0.10f, 1.0f); // #1A1A1A
    private static readonly Color4 SweepLineColor = new(1.0f, 1.0f, 0.0f, 1.0f);   // #FFFF00
    private static readonly Color4 BackgroundColor = new(0.0f, 0.0f, 0.0f, 1.0f);  // #000000
    private static readonly Color4 BaselineColor = new(0.3f, 0.3f, 0.3f, 0.6f);
    private static readonly Color4 MajorGridColor = new(0.23f, 0.29f, 0.23f, 0.6f);
    private static readonly Color4 MinorGridColor = new(0.16f, 0.23f, 0.16f, 0.3f);

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
        float yOffset = (float)area.Top + ChannelPadding;
        float baselineY = yOffset + (height - ChannelPadding * 2) / 2.0f;

        // Get brushes
        var clearBandBrush = resources.GetSolidBrush(ClearBandColor);
        var sweepLineBrush = resources.GetSolidBrush(SweepLineColor);
        var backgroundBrush = resources.GetSolidBrush(BackgroundColor);
        var baselineBrush = resources.GetSolidBrush(BaselineColor);

        // Get sweep parameters
        int samplesPerSweep = channel.SamplesPerSweep;
        int writeIndex = channel.WriteIndex;
        int clearBandSamples = channel.ClearBandSamples;

        // Calculate sweep line X position (right-to-left)
        float sweepLineX = (float)area.Left + EegDataBridge.SampleIndexToX(writeIndex, samplesPerSweep, width);

        // Calculate clear band range (ahead of sweep line in rightward direction)
        int clearEndIndex = writeIndex;
        int clearStartIndex = (writeIndex + clearBandSamples) % samplesPerSweep;
        float clearStartX = (float)area.Left + EegDataBridge.SampleIndexToX(clearStartIndex, samplesPerSweep, width);
        float clearEndX = (float)area.Left + EegDataBridge.SampleIndexToX(clearEndIndex, samplesPerSweep, width);

        float channelHeight = height - ChannelPadding * 2;
        if (channelHeight < 10)
            return;

        // Draw background
        var channelRect = new Rect(area.Left, area.Top, area.Width, area.Height);
        context.FillRectangle(channelRect, backgroundBrush);

        // Draw grid
        DrawGrid(context, resources, area, baselineY, channelHeight, width, yAxisRangeUv, gainMicrovoltsPerCm, channel);

        // Draw baseline
        context.DrawLine(
            new Vector2((float)area.Left, baselineY),
            new Vector2((float)area.Right, baselineY),
            baselineBrush, 1.0f);

        // Draw clear band
        if (clearStartX < clearEndX)
        {
            var clearRect = new Rect(clearStartX, area.Top, clearEndX - clearStartX, area.Height);
            context.FillRectangle(clearRect, clearBandBrush);
        }
        else
        {
            var clearRect1 = new Rect(area.Left, area.Top, clearEndX - (float)area.Left, area.Height);
            var clearRect2 = new Rect(clearStartX, area.Top, (float)area.Right - clearStartX, area.Height);
            context.FillRectangle(clearRect1, clearBandBrush);
            context.FillRectangle(clearRect2, clearBandBrush);
        }

        // Draw waveform
        RenderChannelWaveform(context, resources, channel, width, (float)area.Top, area.Height, baselineY, channel.ChannelIndex, (float)area.Left, yAxisRangeUv, gainMicrovoltsPerCm);

        // Draw sweep line
        context.DrawLine(
            new Vector2(sweepLineX, (float)area.Top),
            new Vector2(sweepLineX, (float)area.Bottom),
            sweepLineBrush, 2.0f);
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
        var majorBrush = resources.GetSolidBrush(MajorGridColor);
        var minorBrush = resources.GetSolidBrush(MinorGridColor);

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
}
