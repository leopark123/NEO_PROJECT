// GsHistogramRenderer.cs
// Phase 3.4: Renders GS histogram (230 bins) in a dedicated area.
// Updated: Vertical layout for right-side display (per reference design)

using System.Numerics;
using Neo.DSP.GS;
using Neo.Rendering.AEEG;
using Neo.Rendering.Mapping;
using Neo.Rendering.Resources;
using Vortice.Direct2D1;
using Vortice.Mathematics;

namespace Neo.UI.Rendering;

public sealed class GsHistogramRenderer
{
    private static readonly Color4 HistogramColor = new(0.12f, 0.56f, 1.0f, 0.7f);
    private static readonly Color4 LabelColor = new(0.8f, 0.8f, 0.8f, 0.9f);

    public void Render(
        ID2D1DeviceContext context,
        ResourceCache resources,
        in Rect area,
        ReadOnlySpan<byte> bins)
    {
        if (area.Width <= 0 || area.Height <= 0)
            return;

        if (bins.Length == 0)
            return;

        var barBrush = resources.GetSolidBrush(HistogramColor);
        var labelBrush = resources.GetSolidBrush(LabelColor);

        // Fill background to match aEEG black area
        var bgBrush = resources.GetSolidBrush(UiAeegPalette.Background);
        context.FillRectangle(area, bgBrush);

        bool hasData = false;
        for (int i = 0; i < bins.Length; i++)
        {
            if (bins[i] > 0)
            {
                hasData = true;
                break;
            }
        }
        if (!hasData)
        {
            var smallFormat = resources.SmallTextFormat;
            context.DrawText("GS", smallFormat, area, labelBrush);
            return;
        }

        float width = (float)area.Width;
        float height = (float)area.Height;
        float left = (float)area.Left;
        float top = (float)area.Top;

        // Vertical histogram: bins map to Y (using semi-log scale like aEEG)
        // The GS represents the amplitude distribution, so Y-axis is voltage (0-200μV semi-log)
        var mapper = new AeegSemiLogMapper(height);

        // Find max count for normalization
        int maxCount = 0;
        for (int i = 0; i < bins.Length; i++)
        {
            if (bins[i] > maxCount) maxCount = bins[i];
        }
        if (maxCount == 0) return;

        // Draw vertical bars - each bin corresponds to a voltage level
        float barMaxWidth = width - 4; // Leave some margin

        for (int i = 0; i < GsBinMapper.TotalBins && i < bins.Length; i++)
        {
            byte count = bins[i];
            if (count == 0)
                continue;

            // Get voltage for this bin
            float voltageUv = (float)GsBinMapper.GetBinCenterVoltage(i);
            if (voltageUv < 0 || voltageUv > 200) continue;

            // Map voltage to Y position
            double y = mapper.MapVoltageToY(voltageUv);
            float yPos = top + (float)y;

            // Bar width proportional to count
            float normalized = count / (float)maxCount;
            float barWidth = Math.Max(1.0f, normalized * barMaxWidth);

            // Draw horizontal bar from left
            var rect = new Rect(left + 2, yPos - 0.5f, barWidth, 1.5f);
            context.FillRectangle(rect, barBrush);
        }

        // Draw "GS" label at top
        var textFormat = resources.GetTextFormat("Segoe UI", 8.0f);
        var labelRect = new Rect(left + 2, top + 2, width - 4, 12);
        context.DrawText("GS", textFormat, labelRect, labelBrush);
    }
}
