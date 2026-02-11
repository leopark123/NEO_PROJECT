// UiAeegPalette.cs
// UI-specific aEEG palette tuned to match reference screenshots.

using Vortice.Mathematics;

namespace Neo.UI.Rendering;

internal static class UiAeegPalette
{
    private static Color4 _background;
    private static Color4 _axisLabel;
    private static Color4 _axisLine;
    private static Color4 _majorGridLine;
    private static Color4 _minorGridLine;
    private static Color4 _boundaryLine;
    private static Color4 _upperBound;
    private static Color4 _lowerBound;
    private static Color4 _trendFill;
    private static Color4 _gapMask;
    private static Color4 _saturationMarker;

    static UiAeegPalette()
    {
        SetTheme(isApple: false);
    }

    // Background + axes
    public static Color4 Background => _background;
    public static Color4 AxisLabel => _axisLabel;
    public static Color4 AxisLine => _axisLine;

    // Grid
    public static Color4 MajorGridLine => _majorGridLine;
    public static Color4 MinorGridLine => _minorGridLine;
    public static Color4 BoundaryLine => _boundaryLine;

    // Trend
    public static Color4 UpperBound => _upperBound;
    public static Color4 LowerBound => _lowerBound;
    public static Color4 TrendFill => _trendFill;

    // Gaps + markers
    public static Color4 GapMask => _gapMask;
    public static Color4 SaturationMarker => _saturationMarker;

    public static void SetTheme(bool isApple)
    {
        if (isApple)
        {
            ApplyApplePalette();
            return;
        }

        ApplyMedicalPalette();
    }

    private static void ApplyMedicalPalette()
    {
        _background = new Color4(0f, 0f, 0f, 1.0f);
        _axisLabel = new Color4(0.72f, 0.82f, 0.95f, 1.0f);
        _axisLine = new Color4(0.35f, 0.35f, 0.35f, 1.0f);
        _majorGridLine = new Color4(0.23f, 0.29f, 0.23f, 0.6f);
        _minorGridLine = new Color4(0.16f, 0.23f, 0.16f, 0.3f);
        _boundaryLine = new Color4(0.30f, 0.45f, 0.30f, 0.7f);
        _upperBound = new Color4(0.98f, 0.64f, 0.18f, 1.0f);
        _lowerBound = new Color4(0.86f, 0.46f, 0.12f, 1.0f);
        _trendFill = new Color4(0.98f, 0.64f, 0.18f, 0.22f);
        _gapMask = new Color4(0.15f, 0.15f, 0.15f, 0.5f);
        _saturationMarker = new Color4(0.9f, 0.2f, 0.2f, 1.0f);
    }

    private static void ApplyApplePalette()
    {
        _background = new Color4(0.93f, 0.96f, 0.99f, 1.0f);
        _axisLabel = new Color4(0.07f, 0.07f, 0.07f, 1.0f);
        _axisLine = new Color4(0.70f, 0.78f, 0.88f, 1.0f);
        _majorGridLine = new Color4(0.63f, 0.74f, 0.88f, 0.75f);
        _minorGridLine = new Color4(0.74f, 0.82f, 0.91f, 0.52f);
        _boundaryLine = new Color4(0.30f, 0.53f, 0.96f, 0.88f);
        _upperBound = new Color4(0.15f, 0.56f, 1.00f, 1.0f);
        _lowerBound = new Color4(0.05f, 0.42f, 0.88f, 1.0f);
        _trendFill = new Color4(0.15f, 0.56f, 1.00f, 0.24f);
        _gapMask = new Color4(0.78f, 0.84f, 0.92f, 0.42f);
        _saturationMarker = new Color4(0.86f, 0.21f, 0.18f, 1.0f);
    }
}
