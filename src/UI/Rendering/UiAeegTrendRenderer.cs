// UiAeegTrendRenderer.cs
// UI-specific aEEG trend renderer with dark theme and filled band style.

using System.Numerics;
using Neo.Rendering.AEEG;
using Neo.Rendering.Resources;
using Vortice.Direct2D1;
using Vortice.Mathematics;

namespace Neo.UI.Rendering;

public sealed class UiAeegTrendRenderer
{
    public void Render(
        ID2D1DeviceContext context,
        ResourceCache resources,
        in AeegTrendRenderData renderData,
        bool useLineMode = false)
    {
        if (!renderData.HasData)
            return;

        var upperBoundBrush = resources.GetSolidBrush(UiAeegPalette.UpperBound);
        var lowerBoundBrush = resources.GetSolidBrush(UiAeegPalette.LowerBound);
        var gapBrush = resources.GetSolidBrush(UiAeegPalette.GapMask);

        var points = renderData.Points;
        var segments = renderData.Segments;
        var gaps = renderData.Gaps;
        var renderArea = renderData.RenderArea;

        for (int i = 0; i < gaps.Length; i++)
        {
            DrawGapMask(context, gapBrush, gaps[i], renderArea);
        }

        if (useLineMode)
        {
            for (int s = 0; s < segments.Length; s++)
            {
                var segment = segments[s];
                int endIndex = segment.StartIndex + segment.PointCount;

                for (int i = segment.StartIndex + 1; i < endIndex; i++)
                {
                    var prev = points[i - 1];
                    var curr = points[i];

                    context.DrawLine(
                        new Vector2(prev.X, prev.MaxY),
                        new Vector2(curr.X, curr.MaxY),
                        upperBoundBrush,
                        2.0f);

                    context.DrawLine(
                        new Vector2(prev.X, prev.MinY),
                        new Vector2(curr.X, curr.MinY),
                        lowerBoundBrush,
                        2.0f);
                }
            }
        }
        else
        {
            var trendBrush = resources.GetSolidBrush(UiAeegPalette.TrendFill);

            for (int s = 0; s < segments.Length; s++)
            {
                var segment = segments[s];
                int endIndex = segment.StartIndex + segment.PointCount;

                for (int i = segment.StartIndex + 1; i < endIndex; i++)
                {
                    var prev = points[i - 1];
                    var curr = points[i];

                    DrawTrendBand(context, trendBrush, prev, curr);

                    context.DrawLine(
                        new Vector2(prev.X, prev.MaxY),
                        new Vector2(curr.X, curr.MaxY),
                        upperBoundBrush,
                        2.0f);

                    context.DrawLine(
                        new Vector2(prev.X, prev.MinY),
                        new Vector2(curr.X, curr.MinY),
                        lowerBoundBrush,
                        2.0f);
                }
            }
        }
    }

    private static void DrawTrendBand(
        ID2D1DeviceContext context,
        ID2D1SolidColorBrush brush,
        in AeegTrendPoint prev,
        in AeegTrendPoint curr)
    {
        var rect = new Rect(
            Math.Min(prev.X, curr.X),
            Math.Min(Math.Min(prev.MinY, curr.MinY), Math.Min(prev.MaxY, curr.MaxY)),
            Math.Abs(curr.X - prev.X),
            Math.Max(Math.Max(prev.MinY, curr.MinY), Math.Max(prev.MaxY, curr.MaxY)) -
            Math.Min(Math.Min(prev.MinY, curr.MinY), Math.Min(prev.MaxY, curr.MaxY)));

        context.FillRectangle(rect, brush);
    }

    private static void DrawGapMask(
        ID2D1DeviceContext context,
        ID2D1SolidColorBrush brush,
        in AeegGapInfo gap,
        in Rect renderArea)
    {
        var rect = new Rect(
            gap.StartX,
            renderArea.Top,
            gap.EndX - gap.StartX,
            renderArea.Height);
        context.FillRectangle(rect, brush);
    }
}
