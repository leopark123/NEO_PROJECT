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
    private ThemeType _currentTheme = ThemeType.Apple;

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
        // 主题色
        UpdateColorAndBrush(resources, "Primary", "#007AFF");
        UpdateColorAndBrush(resources, "PrimaryLight", "#5AC8FA");
        UpdateColorAndBrush(resources, "PrimaryDark", "#0051D5");

        // 背景色
        UpdateColorAndBrush(resources, "BackgroundDark", "#1C1C1E");
        UpdateColorAndBrush(resources, "Background", "#2C2C2E");
        UpdateColorAndBrush(resources, "Panel", "#242426");
        UpdateColorAndBrush(resources, "Surface", "#F5F5F7");
        UpdateColorAndBrush(resources, "SurfaceDark", "#E5E5EA");
        UpdateColorAndBrush(resources, "Border", "#38383A");

        // 功能色
        UpdateColorAndBrush(resources, "Success", "#4CAF50");
        UpdateColorAndBrush(resources, "Warning", "#FF9800");
        UpdateColorAndBrush(resources, "Error", "#F44336");
        UpdateColorAndBrush(resources, "Info", "#2196F3");

        // UI强调色
        UpdateColorAndBrush(resources, "BorderDark", "#48484A");
        UpdateColorAndBrush(resources, "HoverDark", "#3A3A3C");
        UpdateColorAndBrush(resources, "BorderSubtle", "#2C2C2E");
        UpdateColorAndBrush(resources, "BackgroundAlt", "#1C1C1E");

        // 文本色
        UpdateColorAndBrush(resources, "TextOnDark", "#FFFFFF");
        UpdateColorAndBrush(resources, "TextDarkSecondary", "#AEAEB2");
        UpdateColorAndBrush(resources, "TextDarkMuted", "#636366");
        UpdateColorAndBrush(resources, "TextDarkTertiary", "#8E8E93");
        UpdateColorAndBrush(resources, "PlaceholderDark", "#48484A");
    }

    private void ApplyMedicalTheme(ResourceDictionary resources)
    {
        // 主题色（经典医疗粉红）
        UpdateColorAndBrush(resources, "Primary", "#D81B60");
        UpdateColorAndBrush(resources, "PrimaryLight", "#E91E63");
        UpdateColorAndBrush(resources, "PrimaryDark", "#AD1457");

        // 背景色（深色主题）
        UpdateColorAndBrush(resources, "BackgroundDark", "#1A1A1A");
        UpdateColorAndBrush(resources, "Background", "#2D2D2D");
        UpdateColorAndBrush(resources, "Panel", "#1E1E1E");
        UpdateColorAndBrush(resources, "Surface", "#F5F5F5");
        UpdateColorAndBrush(resources, "SurfaceDark", "#E0E0E0");
        UpdateColorAndBrush(resources, "Border", "#3A3A3A");

        // 功能色
        UpdateColorAndBrush(resources, "Success", "#4CAF50");
        UpdateColorAndBrush(resources, "Warning", "#FF9800");
        UpdateColorAndBrush(resources, "Error", "#F44336");
        UpdateColorAndBrush(resources, "Info", "#2196F3");

        // UI强调色
        UpdateColorAndBrush(resources, "BorderDark", "#555555");
        UpdateColorAndBrush(resources, "HoverDark", "#4A4A4A");
        UpdateColorAndBrush(resources, "BorderSubtle", "#333333");
        UpdateColorAndBrush(resources, "BackgroundAlt", "#252525");

        // 文本色
        UpdateColorAndBrush(resources, "TextOnDark", "#FFFFFF");
        UpdateColorAndBrush(resources, "TextDarkSecondary", "#AAAAAA");
        UpdateColorAndBrush(resources, "TextDarkMuted", "#666666");
        UpdateColorAndBrush(resources, "TextDarkTertiary", "#888888");
        UpdateColorAndBrush(resources, "PlaceholderDark", "#444444");
    }

    private static Color ColorFromHex(string hex)
    {
        return (Color)ColorConverter.ConvertFromString(hex);
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

    private void UpdateBrush(ResourceDictionary dict, string key, Color color)
    {
        var brush = dict[key] as SolidColorBrush;
        if (brush != null)
        {
            // 创建新的Brush以触发绑定更新
            dict[key] = new SolidColorBrush(color);
        }
    }
}
