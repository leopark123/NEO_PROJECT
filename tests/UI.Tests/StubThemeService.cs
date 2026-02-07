using Neo.UI.Services;

namespace Neo.UI.Tests;

internal sealed class StubThemeService : IThemeService
{
    public ThemeType CurrentTheme { get; private set; } = ThemeType.Apple;

    public event EventHandler<ThemeType>? ThemeChanged;

    public void SwitchTheme(ThemeType theme)
    {
        if (CurrentTheme == theme)
        {
            return;
        }

        CurrentTheme = theme;
        ThemeChanged?.Invoke(this, theme);
    }
}
