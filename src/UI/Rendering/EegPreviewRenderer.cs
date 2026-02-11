// EegPreviewRenderer.cs
// Renders small EEG preview strip showing ±range reference lines and waveforms
// Based on reference design: narrow white strip with ±50μV or ±100μV markers

using System;
using System.Numerics;
using Neo.Rendering.EEG;
using Neo.Rendering.Resources;
using Vortice.Direct2D1;
using Vortice.Mathematics;

namespace Neo.UI.Rendering;

/// <summary>
/// Renders a small EEG preview strip showing voltage range reference lines and waveforms.
/// </summary>
/// <remarks>
/// Reference design shows:
/// - White background
/// - Two horizontal lines: +50μV (or +100μV) and -50μV (or -100μV)
/// - Compressed EEG waveform
/// - Time labels at bottom (e.g., "08:16:58  08:17:05  08:17:13")
/// - Very narrow height (about 5% of total)
/// </remarks>
public sealed class EegPreviewRenderer
{
    public void Render(
        ID2D1DeviceContext context,
        ResourceCache resources,
        in Rect area,
        int rangeUv,
        in SweepChannelData sweepData)
    {
        if (area.Width <= 0 || area.Height <= 0)
            return;

        // Fill themed background
        var bgBrush = resources.GetSolidBrush(UiEegPalette.BackgroundColor);
        context.FillRectangle(area, bgBrush);

        float centerY = (float)(area.Top + area.Height / 2.0);
        float left = (float)area.Left;
        float right = (float)area.Right;
        float areaHeight = (float)area.Height;

        // Draw upper range line (+rangeUv)
        float upperY = (float)(area.Top + area.Height * 0.25);
        var lineBrush = resources.GetSolidBrush(UiEegPalette.RangeDividerColor);
        context.DrawLine(new Vector2(left, upperY), new Vector2(right, upperY), lineBrush, 0.5f);

        // Draw lower range line (-rangeUv)
        float lowerY = (float)(area.Top + area.Height * 0.75);
        context.DrawLine(new Vector2(left, lowerY), new Vector2(right, lowerY), lineBrush, 0.5f);

        // Draw center baseline
        context.DrawLine(new Vector2(left, centerY), new Vector2(right, centerY), lineBrush, 0.3f);

        // Draw EEG waveform if data is available
        var samples = sweepData.Samples.Span;
        if (samples.Length > 0)
        {
            var waveformBrush = resources.GetSolidBrush(EegColorPalette.GetChannelColor(sweepData.ChannelIndex));
            int samplesPerSweep = sweepData.SamplesPerSweep;
            float width = (float)area.Width;

            // Scale factor for voltage to pixel mapping
            float uvToPixelScale = (areaHeight / (2.0f * rangeUv)) * 0.7f; // Use 70% of height

            // Draw waveform as line segments (similar to SweepModeRenderer)
            Vector2? lastPoint = null;
            int step = Math.Max(1, samplesPerSweep / (int)width);

            for (int i = 0; i < samplesPerSweep; i += step)
            {
                // Calculate X position (right-to-left sweep)
                float x = left + EegDataBridge.SampleIndexToX(i, samplesPerSweep, width);

                // Calculate Y position (inverted: positive voltage = upward)
                float uv = samples[i];
                float y = centerY - uv * uvToPixelScale;

                // Clamp to avoid drawing outside bounds
                y = Math.Clamp(y, (float)area.Top, (float)area.Bottom);

                var currentPoint = new Vector2(x, y);

                // Draw line segment if we have a previous point
                if (lastPoint.HasValue)
                {
                    context.DrawLine(lastPoint.Value, currentPoint, waveformBrush, 0.8f);
                }

                lastPoint = currentPoint;
            }
        }

        // Draw range labels (with boundary checking to prevent overflow)
        var labelBrush = resources.GetSolidBrush(UiEegPalette.RangeTextColor);
        var smallFormat = resources.GetTextFormat("Segoe UI", 8.0f);

        string upperLabel = $"+{rangeUv}μV";
        string lowerLabel = $"-{rangeUv}μV";

        // Clamp label positions to stay within area bounds
        float upperLabelTop = Math.Max((float)area.Top, upperY - 10);
        float lowerLabelTop = Math.Min((float)(area.Bottom - 12), lowerY - 2);

        var upperLabelRect = new Rect(left + 5, upperLabelTop, 60, 12);
        var lowerLabelRect = new Rect(left + 5, lowerLabelTop, 60, 12);

        context.DrawText(upperLabel, smallFormat, upperLabelRect, labelBrush);
        context.DrawText(lowerLabel, smallFormat, lowerLabelRect, labelBrush);
    }
}
