// QualityIndicatorRenderer.cs
// Phase 3.6: Draws quality overlays for Missing / Saturated / LeadOff.

using Neo.Core.Enums;
using Neo.Rendering.Resources;
using Vortice.Direct2D1;
using Vortice.Mathematics;

namespace Neo.UI.Rendering;

public sealed class QualityIndicatorRenderer
{
    private static readonly Color4 MissingColor = new(0.62f, 0.62f, 0.62f, 0.5f);   // #9E9E9E @ 50%
    private static readonly Color4 SaturatedColor = new(0.96f, 0.26f, 0.21f, 0.5f); // #F44336 @ 50%
    private static readonly Color4 LeadOffColor = new(1.00f, 0.60f, 0.00f, 0.5f);   // #FF9800 @ 50%

    public void Render(
        ID2D1DeviceContext context,
        ResourceCache resources,
        in Rect area,
        QualityFlag quality)
    {
        if (area.Width <= 0 || area.Height <= 0)
            return;

        if (quality == QualityFlag.Normal)
            return;

        string? label = null;
        Color4 color;

        if ((quality & QualityFlag.LeadOff) != 0)
        {
            label = "Lead Off";
            color = LeadOffColor;
        }
        else if ((quality & QualityFlag.Missing) != 0)
        {
            label = "Missing";
            color = MissingColor;
        }
        else if ((quality & QualityFlag.Saturated) != 0)
        {
            label = "Saturated";
            color = SaturatedColor;
        }
        else
        {
            return;
        }

        var overlayBrush = resources.GetSolidBrush(color);
        context.FillRectangle(area, overlayBrush);

        var textFormat = resources.SmallTextFormat;
        var textBrush = resources.WhiteBrush;
        context.DrawText(label, textFormat, area, textBrush);
    }
}
