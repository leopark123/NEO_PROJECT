// AeegSemiLogVisualizer.cs
// Renders semi-log region overlays for aEEG trend area.

using System.Numerics;
using Neo.Rendering.Mapping;
using Neo.Rendering.Resources;
using Vortice.Direct2D1;
using Vortice.Mathematics;

namespace Neo.Rendering.AEEG;

/// <summary>
/// Visual overlay for aEEG semi-log regions (0-10uV linear, 10-200uV logarithmic).
/// </summary>
public sealed class AeegSemiLogVisualizer
{
    public readonly record struct VisualizationOptions(
        bool ShowRegionBackground,
        bool ShowBoundaryLine,
        bool ShowRegionLabels,
        float LabelMarginLeft,
        bool ShowClinicalNote,
        Color4 LinearRegionColor,
        Color4 LogRegionColor,
        Color4 BoundaryColor,
        Color4 LabelColor)
    {
        public static VisualizationOptions Default => new(
            ShowRegionBackground: true,
            ShowBoundaryLine: true,
            ShowRegionLabels: false,
            LabelMarginLeft: 8.0f,
            ShowClinicalNote: false,
            LinearRegionColor: new Color4(0.16f, 0.23f, 0.16f, 0.12f),
            LogRegionColor: new Color4(0.23f, 0.29f, 0.23f, 0.10f),
            BoundaryColor: new Color4(0.30f, 0.45f, 0.30f, 0.85f),
            LabelColor: new Color4(0.80f, 0.80f, 0.80f, 1.00f));
    }

    /// <summary>
    /// Returns the Y coordinate of the 10uV boundary in trend area coordinates.
    /// </summary>
    public static float GetBoundaryY(in Rect trendArea)
    {
        if (trendArea.Height <= 0)
        {
            return (float)trendArea.Top;
        }

        var mapper = new AeegSemiLogMapper(trendArea.Height);
        return (float)(trendArea.Top + mapper.MapVoltageToY(AeegSemiLogMapper.LinearLogBoundaryUv));
    }

    /// <summary>
    /// Renders semi-log visual overlays on top of trend background/grid and below trend lines.
    /// </summary>
    public void Render(
        ID2D1DeviceContext context,
        ResourceCache resources,
        in Rect trendArea,
        VisualizationOptions options)
    {
        if (trendArea.Width <= 0 || trendArea.Height <= 0)
        {
            return;
        }

        float boundaryY = GetBoundaryY(trendArea);
        boundaryY = Math.Clamp(boundaryY, (float)trendArea.Top, (float)trendArea.Bottom);

        if (options.ShowRegionBackground)
        {
            var logBrush = resources.GetSolidBrush(options.LogRegionColor);
            var linearBrush = resources.GetSolidBrush(options.LinearRegionColor);

            var logRect = new Rect(
                trendArea.Left,
                trendArea.Top,
                trendArea.Width,
                Math.Max(0f, boundaryY - (float)trendArea.Top));
            var linearRect = new Rect(
                trendArea.Left,
                boundaryY,
                trendArea.Width,
                Math.Max(0f, (float)trendArea.Bottom - boundaryY));

            context.FillRectangle(logRect, logBrush);
            context.FillRectangle(linearRect, linearBrush);
        }

        if (options.ShowBoundaryLine)
        {
            var boundaryBrush = resources.GetSolidBrush(options.BoundaryColor);
            context.DrawLine(
                new Vector2((float)trendArea.Left, boundaryY),
                new Vector2((float)trendArea.Right, boundaryY),
                boundaryBrush,
                1.5f);
        }

        if (options.ShowRegionLabels)
        {
            var labelBrush = resources.GetSolidBrush(options.LabelColor);
            var textFormat = resources.GetTextFormat("Segoe UI", 9.0f);
            float labelLeft = (float)trendArea.Left + options.LabelMarginLeft;

            context.DrawText(
                "Log 10-200uV",
                textFormat,
                new Rect(labelLeft, (float)trendArea.Top + 2f, 120f, 14f),
                labelBrush);

            context.DrawText(
                "Linear 0-10uV",
                textFormat,
                new Rect(labelLeft, Math.Max((float)trendArea.Top, boundaryY + 2f), 120f, 14f),
                labelBrush);

            if (options.ShowClinicalNote)
            {
                context.DrawText(
                    "10uV boundary",
                    textFormat,
                    new Rect(labelLeft, Math.Max((float)trendArea.Top, boundaryY - 14f), 120f, 12f),
                    labelBrush);
            }
        }
    }
}
