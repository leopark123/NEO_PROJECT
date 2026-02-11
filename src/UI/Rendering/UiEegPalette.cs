// UiEegPalette.cs
// UI-specific EEG sweep palette with runtime theme switching.

using Vortice.Mathematics;

namespace Neo.UI.Rendering;

internal static class UiEegPalette
{
    private static Color4 _clearBandColor;
    private static Color4 _sweepLineColor;
    private static Color4 _backgroundColor;
    private static Color4 _baselineColor;
    private static Color4 _majorGridColor;
    private static Color4 _minorGridColor;
    private static Color4 _rangeBracketColor;
    private static Color4 _rangeTextColor;
    private static Color4 _rangeDividerColor;

    static UiEegPalette()
    {
        SetTheme(isApple: false);
    }

    public static Color4 ClearBandColor => _clearBandColor;
    public static Color4 SweepLineColor => _sweepLineColor;
    public static Color4 BackgroundColor => _backgroundColor;
    public static Color4 BaselineColor => _baselineColor;
    public static Color4 MajorGridColor => _majorGridColor;
    public static Color4 MinorGridColor => _minorGridColor;
    public static Color4 RangeBracketColor => _rangeBracketColor;
    public static Color4 RangeTextColor => _rangeTextColor;
    public static Color4 RangeDividerColor => _rangeDividerColor;

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
        _clearBandColor = new Color4(0.10f, 0.10f, 0.10f, 1.0f);
        _sweepLineColor = new Color4(1.0f, 1.0f, 0.0f, 1.0f);
        _backgroundColor = new Color4(0.0f, 0.0f, 0.0f, 1.0f);
        _baselineColor = new Color4(0.30f, 0.30f, 0.30f, 0.60f);
        _majorGridColor = new Color4(0.23f, 0.29f, 0.23f, 0.60f);
        _minorGridColor = new Color4(0.16f, 0.23f, 0.16f, 0.30f);
        _rangeBracketColor = new Color4(0.72f, 0.82f, 0.95f, 0.95f);
        _rangeTextColor = new Color4(0.72f, 0.82f, 0.95f, 1.00f);
        _rangeDividerColor = new Color4(0.28f, 0.36f, 0.48f, 0.85f);
    }

    private static void ApplyApplePalette()
    {
        _clearBandColor = new Color4(0.88f, 0.92f, 0.97f, 1.0f);
        _sweepLineColor = new Color4(0.20f, 0.52f, 0.98f, 1.0f);
        _backgroundColor = new Color4(0.94f, 0.97f, 0.995f, 1.0f);
        _baselineColor = new Color4(0.52f, 0.64f, 0.78f, 0.58f);
        _majorGridColor = new Color4(0.53f, 0.65f, 0.83f, 0.70f);
        _minorGridColor = new Color4(0.68f, 0.77f, 0.89f, 0.42f);
        _rangeBracketColor = new Color4(0.16f, 0.43f, 0.86f, 0.92f);
        _rangeTextColor = new Color4(0.07f, 0.07f, 0.07f, 0.98f);
        _rangeDividerColor = new Color4(0.44f, 0.58f, 0.78f, 0.80f);
    }
}
