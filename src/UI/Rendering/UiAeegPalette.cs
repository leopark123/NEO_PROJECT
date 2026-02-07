// UiAeegPalette.cs
// UI-specific aEEG palette tuned to match reference screenshots.

using Vortice.Mathematics;

namespace Neo.UI.Rendering;

internal static class UiAeegPalette
{
    // Background + axes
    public static readonly Color4 Background = new(0f, 0f, 0f, 1.0f);
    public static readonly Color4 AxisLabel = new(0.8f, 0.8f, 0.8f, 1.0f);
    public static readonly Color4 AxisLine = new(0.35f, 0.35f, 0.35f, 1.0f);

    // Grid
    public static readonly Color4 MajorGridLine = new(0.23f, 0.29f, 0.23f, 0.6f); // #3A4A3A
    public static readonly Color4 MinorGridLine = new(0.16f, 0.23f, 0.16f, 0.3f); // #2A3A2A
    public static readonly Color4 BoundaryLine = new(0.30f, 0.45f, 0.30f, 0.7f);

    // Trend
    public static readonly Color4 UpperBound = new(0.12f, 0.56f, 1.0f, 1.0f);  // #1E90FF
    public static readonly Color4 LowerBound = new(0.25f, 0.41f, 0.88f, 1.0f);  // #4169E1
    public static readonly Color4 TrendFill = new(0.12f, 0.56f, 1.0f, 0.3f);    // 30% alpha

    // Gaps + markers
    public static readonly Color4 GapMask = new(0.15f, 0.15f, 0.15f, 0.5f);
    public static readonly Color4 SaturationMarker = new(0.9f, 0.2f, 0.2f, 1.0f);
}
