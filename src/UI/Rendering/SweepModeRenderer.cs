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
    // Channel layout
    private const int ChannelCount = 4;
    private const float ChannelPadding = 10.0f;

    // Clear band appearance
    private static readonly Color4 ClearBandColor = new(0.1f, 0.1f, 0.15f, 1.0f);
    private static readonly Color4 SweepLineColor = new(1.0f, 1.0f, 0.0f, 0.8f);
    private static readonly Color4 BackgroundColor = new(0.05f, 0.05f, 0.08f, 1.0f);
    private static readonly Color4 BaselineColor = new(0.2f, 0.2f, 0.25f, 1.0f);

    /// <summary>
    /// Renders sweep mode waveforms.
    /// </summary>
    /// <param name="context">D2D device context.</param>
    /// <param name="resources">Resource cache.</param>
    /// <param name="sweepData">Sweep channel data array.</param>
    /// <param name="viewportWidth">Viewport width in pixels.</param>
    /// <param name="viewportHeight">Viewport height in pixels.</param>
    public void Render(
        ID2D1DeviceContext context,
        ResourceCache resources,
        SweepChannelData[] sweepData,
        int viewportWidth,
        int viewportHeight)
    {
        if (sweepData == null || sweepData.Length == 0)
            return;

        if (viewportWidth <= 0 || viewportHeight <= 0)
            return;

        float width = viewportWidth;
        float height = viewportHeight;

        // Calculate channel layout
        float totalPadding = ChannelPadding * (ChannelCount + 1);
        float channelHeight = (height - totalPadding) / ChannelCount;

        if (channelHeight < 20)
            return; // Not enough space

        // Get brushes
        var clearBandBrush = resources.GetSolidBrush(ClearBandColor);
        var sweepLineBrush = resources.GetSolidBrush(SweepLineColor);
        var backgroundBrush = resources.GetSolidBrush(BackgroundColor);
        var baselineBrush = resources.GetSolidBrush(BaselineColor);

        // Get sweep parameters from first channel
        int samplesPerSweep = sweepData[0].SamplesPerSweep;
        int writeIndex = sweepData[0].WriteIndex;
        int clearBandSamples = sweepData[0].ClearBandSamples;

        // Calculate sweep line X position (right-to-left)
        float sweepLineX = EegDataBridge.SampleIndexToX(writeIndex, samplesPerSweep, width);

        // Calculate clear band range
        int clearEndIndex = writeIndex;
        int clearStartIndex = (writeIndex + clearBandSamples) % samplesPerSweep;
        float clearStartX = EegDataBridge.SampleIndexToX(clearStartIndex, samplesPerSweep, width);
        float clearEndX = EegDataBridge.SampleIndexToX(clearEndIndex, samplesPerSweep, width);

        // Render each channel
        for (int ch = 0; ch < Math.Min(sweepData.Length, ChannelCount); ch++)
        {
            float yOffset = ChannelPadding + ch * (channelHeight + ChannelPadding);
            float baselineY = yOffset + channelHeight / 2.0f;

            // Draw channel background
            var channelRect = new Rect(0, yOffset, width, channelHeight);
            context.FillRectangle(channelRect, backgroundBrush);

            // Draw baseline
            context.DrawLine(
                new Vector2(0, baselineY),
                new Vector2(width, baselineY),
                baselineBrush, 1.0f);

            // Draw clear band
            if (clearStartX < clearEndX)
            {
                // Normal case: clear band doesn't wrap
                var clearRect = new Rect(clearStartX, yOffset, clearEndX - clearStartX, channelHeight);
                context.FillRectangle(clearRect, clearBandBrush);
            }
            else
            {
                // Wrap case: draw two rectangles
                var clearRect1 = new Rect(0, yOffset, clearEndX, channelHeight);
                var clearRect2 = new Rect(clearStartX, yOffset, width - clearStartX, channelHeight);
                context.FillRectangle(clearRect1, clearBandBrush);
                context.FillRectangle(clearRect2, clearBandBrush);
            }

            // Draw waveform
            RenderChannelWaveform(context, resources, sweepData[ch], width, yOffset, channelHeight, baselineY, ch);
        }

        // Draw sweep line (vertical yellow line)
        for (int ch = 0; ch < ChannelCount; ch++)
        {
            float yOffset = ChannelPadding + ch * (channelHeight + ChannelPadding);
            context.DrawLine(
                new Vector2(sweepLineX, yOffset),
                new Vector2(sweepLineX, yOffset + channelHeight),
                sweepLineBrush, 2.0f);
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
        int channelIndex)
    {
        var samples = channel.Samples.Span;
        int samplesPerSweep = channel.SamplesPerSweep;
        int writeIndex = channel.WriteIndex;
        int clearBandSamples = channel.ClearBandSamples;

        // Get channel color
        var channelColor = EegColorPalette.GetChannelColor(channelIndex);
        var waveformBrush = resources.GetSolidBrush(channelColor);

        // Scale factor: ±200 μV maps to channel height
        float uvToPixelScale = channelHeight / 400.0f;

        // Draw waveform as connected line segments
        // Skip samples in clear band
        Vector2? lastPoint = null;
        bool lastWasInClearBand = false;

        // Downsample for performance: draw every Nth point if too many samples
        int step = Math.Max(1, samplesPerSweep / (int)width);

        for (int i = 0; i < samplesPerSweep; i += step)
        {
            // Check if in clear band
            int distance = (i - writeIndex + samplesPerSweep) % samplesPerSweep;
            bool inClearBand = distance > 0 && distance <= clearBandSamples;

            if (inClearBand)
            {
                lastWasInClearBand = true;
                lastPoint = null;
                continue;
            }

            // Calculate X position (right-to-left)
            float x = EegDataBridge.SampleIndexToX(i, samplesPerSweep, width);

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
