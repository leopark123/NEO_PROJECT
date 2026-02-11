// ThemeService.cs
// 主题切换服务

using System;
using System.Linq;
using System.Windows;
using System.Windows.Media;

namespace Neo.UI.Services;

/// <summary>
/// 主题类型枚举
/// </summary>
public enum ThemeType
{
    /// <summary>Apple 风格主题（系统蓝）</summary>
    Apple,

    /// <summary>经典医疗主题（粉红色）</summary>
    Medical
}

/// <summary>
/// 主题切换服务
/// </summary>
public interface IThemeService
{
    /// <summary>当前主题类型</summary>
    ThemeType CurrentTheme { get; }

    /// <summary>切换主题</summary>
    void SwitchTheme(ThemeType theme);

    /// <summary>主题变更事件</summary>
    event EventHandler<ThemeType>? ThemeChanged;
}

/// <summary>
/// 主题服务实现
/// </summary>
public class ThemeService : IThemeService
{
    private static readonly string[] ThemeBrushKeys =
    [
        "PrimaryBrush", "PrimaryLightBrush", "PrimaryDarkBrush",
        "BackgroundDarkBrush", "BackgroundBrush", "PanelBrush",
        "SurfaceBrush", "SurfaceDarkBrush", "BorderBrush",
        "SuccessBrush", "WarningBrush", "ErrorBrush", "InfoBrush",
        "EegChannel1Brush", "EegChannel2Brush", "AeegFillBrush", "NirsTrendBrush",
        "DividerMajorBrush", "DividerNormalBrush", "DividerMinorBrush",
        "BorderDarkBrush", "HoverDarkBrush", "BorderSubtleBrush", "BackgroundAltBrush", "DeviceInactiveBrush",
        "PanelHighlightBrush", "PanelShadowBrush",
        "NavHoverBrush", "NavPressedBrush", "PrimaryTintLightBrush", "PrimaryTintBrush", "PrimaryTintSubtleBrush",
        "TextPrimaryBrush", "TextSecondaryBrush", "TextOnPrimaryBrush", "TextOnDarkBrush",
        "TextOnSurfaceBrush", "TextMutedBrush", "TextDarkSecondaryBrush", "TextDarkMutedBrush",
        "TextDarkTertiaryBrush", "PlaceholderDarkBrush"
    ];

    private ThemeType _currentTheme = ThemeType.Medical;

    public ThemeService()
    {
        PrepareMutableThemeBrushes();
        // Ensure startup resources match current theme token.
        ApplyTheme(_currentTheme);
    }

    public ThemeType CurrentTheme => _currentTheme;

    public event EventHandler<ThemeType>? ThemeChanged;

    public void SwitchTheme(ThemeType theme)
    {
        System.Diagnostics.Debug.WriteLine($"[ThemeService] SwitchTheme called: {theme}");

        if (_currentTheme == theme)
        {
            System.Diagnostics.Debug.WriteLine($"[ThemeService] Already using theme: {theme}");
            return;
        }

        _currentTheme = theme;
        System.Diagnostics.Debug.WriteLine($"[ThemeService] Applying theme: {theme}");
        ApplyTheme(theme);

        System.Diagnostics.Debug.WriteLine($"[ThemeService] Invoking ThemeChanged event");
        ThemeChanged?.Invoke(this, theme);
    }

    private void ApplyTheme(ThemeType theme)
    {
        var app = Application.Current;
        if (app == null) return;

        var resources = app.Resources;

        switch (theme)
        {
            case ThemeType.Apple:
                ApplyAppleTheme(resources);
                break;
            case ThemeType.Medical:
                ApplyMedicalTheme(resources);
                break;
        }
    }

    private void ApplyAppleTheme(ResourceDictionary resources)
    {
        // Theme (Apple accent blue)
        UpdateColorAndBrush(resources, "Primary", "#0A84FF");
        UpdateColorAndBrush(resources, "PrimaryLight", "#5AC8FA");
        UpdateColorAndBrush(resources, "PrimaryDark", "#0066CC");

        // Background (Apple light system style, reduced glare)
        UpdateColorAndBrush(resources, "BackgroundDark", "#E4EAF1");
        UpdateColorAndBrush(resources, "Background", "#EEF3F8");
        UpdateColorAndBrush(resources, "Panel", "#F6F9FC");
        UpdateColorAndBrush(resources, "Surface", "#F3F7FB");
        UpdateColorAndBrush(resources, "SurfaceDark", "#DEE6EF");
        UpdateColorAndBrush(resources, "Border", "#C5CFDB");

        // Functional
        UpdateColorAndBrush(resources, "Success", "#30D158");
        UpdateColorAndBrush(resources, "Warning", "#FF9F0A");
        UpdateColorAndBrush(resources, "Error", "#FF453A");
        UpdateColorAndBrush(resources, "Info", "#0A84FF");

        // Waveform + NIRS (NIRS intentionally softer than EEG)
        UpdateColorAndBrush(resources, "EegChannel1", "#1B6DFF");
        UpdateColorAndBrush(resources, "EegChannel2", "#3AA9FF");
        UpdateColorAndBrush(resources, "AeegFill", "#5C1B6DFF");
        UpdateColorAndBrush(resources, "NirsTrend", "#7EBEFF");

        // Dividers
        UpdateColorAndBrush(resources, "DividerMajor", "#9FB2C8");
        UpdateColorAndBrush(resources, "DividerNormal", "#BCC9D8");
        UpdateColorAndBrush(resources, "DividerMinor", "#D3DCE7");

        // UI accents
        UpdateColorAndBrush(resources, "BorderDark", "#CCD6E2");
        UpdateColorAndBrush(resources, "HoverDark", "#E9EFF7");
        UpdateColorAndBrush(resources, "BorderSubtle", "#E5EBF3");
        UpdateColorAndBrush(resources, "BackgroundAlt", "#F2F6FB");
        UpdateColorAndBrush(resources, "DeviceInactive", "#A0A9B8");
        UpdateColorAndBrush(resources, "PanelHighlight", "#80FFFFFF");
        UpdateColorAndBrush(resources, "PanelShadow", "#2A23364A");
        UpdateBrushOnly(resources, "NavHoverBrush", "#FFFFFF");
        UpdateBrushOnly(resources, "NavPressedBrush", "#FFFFFF");
        UpdateBrushOnly(resources, "PrimaryTintLightBrush", "#0A84FF");
        UpdateBrushOnly(resources, "PrimaryTintBrush", "#0A84FF");
        UpdateBrushOnly(resources, "PrimaryTintSubtleBrush", "#0A84FF");

        // Text (unified black tone)
        UpdateColorAndBrush(resources, "TextPrimary", "#111111");
        UpdateColorAndBrush(resources, "TextSecondary", "#111111");
        UpdateColorAndBrush(resources, "TextOnPrimary", "#111111");
        UpdateColorAndBrush(resources, "TextOnDark", "#111111");
        UpdateColorAndBrush(resources, "TextOnSurface", "#111111");
        UpdateColorAndBrush(resources, "TextMuted", "#111111");
        UpdateColorAndBrush(resources, "TextDarkSecondary", "#111111");
        UpdateColorAndBrush(resources, "TextDarkMuted", "#111111");
        UpdateColorAndBrush(resources, "TextDarkTertiary", "#111111");
        UpdateColorAndBrush(resources, "PlaceholderDark", "#111111");
    }

    private void ApplyMedicalTheme(ResourceDictionary resources)
    {
        // Theme (clinical blue/cyan)
        UpdateColorAndBrush(resources, "Primary", "#1E88E5");
        UpdateColorAndBrush(resources, "PrimaryLight", "#26C6DA");
        UpdateColorAndBrush(resources, "PrimaryDark", "#1565C0");

        // Background
        UpdateColorAndBrush(resources, "BackgroundDark", "#171C22");
        UpdateColorAndBrush(resources, "Background", "#20262C");
        UpdateColorAndBrush(resources, "Panel", "#1B2026");
        UpdateColorAndBrush(resources, "Surface", "#F2F6FA");
        UpdateColorAndBrush(resources, "SurfaceDark", "#E1E8EF");
        UpdateColorAndBrush(resources, "Border", "#2F3A44");

        // Functional
        UpdateColorAndBrush(resources, "Success", "#2EAD5F");
        UpdateColorAndBrush(resources, "Warning", "#F5A623");
        UpdateColorAndBrush(resources, "Error", "#E53935");
        UpdateColorAndBrush(resources, "Info", "#29B6F6");

        // Waveform + NIRS
        UpdateColorAndBrush(resources, "EegChannel1", "#2E7DFF");
        UpdateColorAndBrush(resources, "EegChannel2", "#00BCD4");
        UpdateColorAndBrush(resources, "AeegFill", "#6629B6F6");
        UpdateColorAndBrush(resources, "NirsTrend", "#26C6DA");

        // Dividers
        UpdateColorAndBrush(resources, "DividerMajor", "#6FA8D6");
        UpdateColorAndBrush(resources, "DividerNormal", "#4B5E70");
        UpdateColorAndBrush(resources, "DividerMinor", "#33414C");

        // UI accents
        UpdateColorAndBrush(resources, "BorderDark", "#3D4B58");
        UpdateColorAndBrush(resources, "HoverDark", "#2B333C");
        UpdateColorAndBrush(resources, "BorderSubtle", "#26313A");
        UpdateColorAndBrush(resources, "BackgroundAlt", "#171C22");
        UpdateColorAndBrush(resources, "DeviceInactive", "#8895A3");
        UpdateColorAndBrush(resources, "PanelHighlight", "#66FFFFFF");
        UpdateColorAndBrush(resources, "PanelShadow", "#46000000");
        UpdateBrushOnly(resources, "NavHoverBrush", "#FFFFFF");
        UpdateBrushOnly(resources, "NavPressedBrush", "#FFFFFF");
        UpdateBrushOnly(resources, "PrimaryTintLightBrush", "#1E88E5");
        UpdateBrushOnly(resources, "PrimaryTintBrush", "#1E88E5");
        UpdateBrushOnly(resources, "PrimaryTintSubtleBrush", "#1E88E5");

        // Text
        UpdateColorAndBrush(resources, "TextPrimary", "#1F2D3D");
        UpdateColorAndBrush(resources, "TextSecondary", "#5B6B7C");
        UpdateColorAndBrush(resources, "TextOnPrimary", "#FFFFFF");
        UpdateColorAndBrush(resources, "TextOnDark", "#EAF1F8");
        UpdateColorAndBrush(resources, "TextOnSurface", "#1F2D3D");
        UpdateColorAndBrush(resources, "TextMuted", "#8A99AA");
        UpdateColorAndBrush(resources, "TextDarkSecondary", "#B4C2CF");
        UpdateColorAndBrush(resources, "TextDarkMuted", "#7E8C98");
        UpdateColorAndBrush(resources, "TextDarkTertiary", "#95A3AF");
        UpdateColorAndBrush(resources, "PlaceholderDark", "#4D5A65");
    }

    private static Color ColorFromHex(string hex)
    {
        return (Color)ColorConverter.ConvertFromString(hex);
    }

    private static void PrepareMutableThemeBrushes()
    {
        var app = Application.Current;
        if (app == null)
        {
            return;
        }

        var dictionaries = new[] { app.Resources }.Concat(app.Resources.MergedDictionaries);
        foreach (var dict in dictionaries)
        {
            foreach (var key in ThemeBrushKeys)
            {
                if (!dict.Contains(key))
                {
                    continue;
                }

                if (dict[key] is not SolidColorBrush brush)
                {
                    continue;
                }

                // Replace startup brush instances with mutable brushes once.
                dict[key] = new SolidColorBrush(brush.Color);
            }
        }
    }

    private void UpdateColorAndBrush(ResourceDictionary resources, string name, string hexColor)
    {
        var color = ColorFromHex(hexColor);

        // 更新Color资源
        var colorKey = $"Color.{name}";
        resources[colorKey] = color;

        // 更新对应的Brush资源（需要查找所有合并的字典）
        var brushKey = $"{name}Brush";

        // 先尝试从主Resources更新
        if (resources.Contains(brushKey))
        {
            UpdateBrush(resources, brushKey, color);
        }

        // 然后遍历所有MergedDictionaries
        foreach (var dict in resources.MergedDictionaries)
        {
            if (dict.Contains(brushKey))
            {
                UpdateBrush(dict, brushKey, color);
            }
            if (dict.Contains(colorKey))
            {
                dict[colorKey] = color;
            }
        }
    }

    private void UpdateBrushOnly(ResourceDictionary resources, string brushKey, string hexColor)
    {
        var color = ColorFromHex(hexColor);
        if (resources.Contains(brushKey))
        {
            UpdateBrush(resources, brushKey, color);
        }

        foreach (var dict in resources.MergedDictionaries)
        {
            if (dict.Contains(brushKey))
            {
                UpdateBrush(dict, brushKey, color);
            }
        }
    }

    private void UpdateBrush(ResourceDictionary dict, string key, Color color)
    {
        var brush = dict[key] as SolidColorBrush;
        if (brush != null)
        {
            // Keep existing brush instance so StaticResource references update immediately.
            if (!brush.IsFrozen)
            {
                brush.Color = color;
            }
            else
            {
                dict[key] = new SolidColorBrush(color);
            }
        }
    }
}
