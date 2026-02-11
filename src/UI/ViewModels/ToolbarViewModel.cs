using System.Diagnostics;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Neo.UI.Services;

namespace Neo.UI.ViewModels;

public partial class ToolbarViewModel : ViewModelBase
{
    private readonly IAuditService _audit;
    private readonly IThemeService _themeService;
    private readonly DispatcherTimer? _clockTimer;
    private readonly long _startTicks = Stopwatch.GetTimestamp();
    private readonly DateTimeOffset _startUtc = DateTimeOffset.UtcNow;

    [ObservableProperty]
    private string _currentTime = "--:--:--";

    [ObservableProperty]
    private string _currentUser = "User: --";

    [ObservableProperty]
    private string _bedNumber = "--";

    [ObservableProperty]
    private bool _isPlaying;

    [ObservableProperty]
    private bool _isPinned;

    [ObservableProperty]
    private bool _isNavDrawerOpen;

    [ObservableProperty]
    private string _appVersion = GetVersionText();

    [ObservableProperty]
    private string _currentThemeName = "Medical";

    public string CurrentThemeDisplay => CurrentThemeName == ThemeType.Apple.ToString() ? "Apple" : "Default";

    public string ThemeToggleButtonText => CurrentThemeName == ThemeType.Apple.ToString() ? "切换 Default" : "切换 Apple";

    public string BedNumberDisplay => $"Bed: {BedNumber}";

    public ToolbarViewModel(IAuditService audit, IThemeService themeService)
    {
        _audit = audit;
        _themeService = themeService;
        CurrentUser = "User: Operator";
        BedNumber = "01";
        CurrentThemeName = _themeService.CurrentTheme.ToString();

        _clockTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(1)
        };
        _clockTimer.Tick += (_, _) => RefreshClock();
        _clockTimer.Start();

        RefreshClock();
    }

    public void StopClock()
    {
        _clockTimer?.Stop();
    }

    partial void OnBedNumberChanged(string value)
    {
        OnPropertyChanged(nameof(BedNumberDisplay));
    }

    partial void OnCurrentThemeNameChanged(string value)
    {
        _ = value;
        OnPropertyChanged(nameof(CurrentThemeDisplay));
        OnPropertyChanged(nameof(ThemeToggleButtonText));
    }

    [RelayCommand]
    private void ToggleNavDrawer()
    {
        IsNavDrawerOpen = !IsNavDrawerOpen;
    }

    [RelayCommand]
    private void OpenVersionEntry()
    {
    }

    [RelayCommand]
    private void Playback()
    {
        IsPlaying = !IsPlaying;
        if (IsPlaying)
        {
            _audit.Log(AuditEventTypes.MonitoringStart, "Playback started");
        }
        else
        {
            _audit.Log(AuditEventTypes.MonitoringStop, "Playback paused");
        }
    }

    [RelayCommand]
    private void Pin()
    {
        IsPinned = !IsPinned;
    }

    [RelayCommand]
    private void Screenshot()
    {
        _audit.Log(AuditEventTypes.Screenshot, "Screenshot requested");
    }

    [RelayCommand]
    private void Annotation()
    {
        _audit.Log(AuditEventTypes.Annotation, "Annotation requested");
    }

    [RelayCommand]
    private void SwitchTheme()
    {
        var newTheme = _themeService.CurrentTheme == ThemeType.Apple
            ? ThemeType.Medical
            : ThemeType.Apple;
        ApplyThemeInternal(newTheme);
    }

    [RelayCommand]
    private void ApplyAppleTheme()
    {
        ApplyThemeInternal(ThemeType.Apple);
    }

    [RelayCommand]
    private void ApplyMedicalTheme()
    {
        ApplyThemeInternal(ThemeType.Medical);
    }

    private void ApplyThemeInternal(ThemeType targetTheme)
    {
        if (_themeService.CurrentTheme == targetTheme)
        {
            CurrentThemeName = targetTheme.ToString();
            return;
        }

        _themeService.SwitchTheme(targetTheme);
        CurrentThemeName = targetTheme.ToString();
        _audit.Log(AuditEventTypes.ConfigChange, $"Theme switched to {(targetTheme == ThemeType.Apple ? "Apple" : "Default")}");
    }

    private static string GetVersionText()
    {
        var version = typeof(ToolbarViewModel).Assembly.GetName().Version;
        if (version is null)
        {
            return "v0.0.0";
        }

        return $"v{version.Major}.{version.Minor}.{Math.Max(0, version.Build)}";
    }

    private void RefreshClock()
    {
        var elapsedTicks = Stopwatch.GetTimestamp() - _startTicks;
        var elapsed = TimeSpan.FromSeconds((double)elapsedTicks / Stopwatch.Frequency);
        var now = _startUtc + elapsed;
        CurrentTime = now.ToLocalTime().ToString("HH:mm:ss");
    }
}
