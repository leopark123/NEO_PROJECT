// MainWindowViewModel.cs
// Sprint 1.2-fix / Sprint 2.2 / Sprint 2.3-2.4: Shell ViewModel with navigation + audit wiring.
//
// References:
// - UI_SPEC.md §3: Architecture = MVVM
// - UI_SPEC.md §4: Main interface layout
// - UI_SPEC.md §10: Audit requirements (only spec-listed event types used)
//
// Sprint 2.2: Navigation audit — UI_SPEC §10 lists exactly 10 event types;
// NAVIGATION is not among them. §2.5 "所有用户操作可审计" is a general principle
// but §10's specific enumeration takes precedence. Page navigation does not
// affect patient data/monitoring/clinical outcome → not a "关键操作" → no audit.

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Neo.UI.Services;

namespace Neo.UI.ViewModels;

/// <summary>
/// MainWindow shell ViewModel.
/// Owns the current page (ContentControl binding target) and navigation commands.
/// </summary>
public partial class MainWindowViewModel : ViewModelBase
{
    private readonly INavigationService _navigation;
    private readonly IAuditService _audit;
    private readonly IDialogService _dialog;

    [ObservableProperty]
    private ViewModelBase? _currentPage;

    [ObservableProperty]
    private string _activeRoute = "Home";

    /// <summary>Toolbar ViewModel exposed for DataContext binding.</summary>
    public ToolbarViewModel Toolbar { get; }

    /// <summary>Status bar ViewModel exposed for DataContext binding.</summary>
    public StatusViewModel Status { get; }

    /// <summary>Channel control panel ViewModel exposed for DataContext binding.</summary>
    public WaveformViewModel Waveform { get; }

    public MainWindowViewModel(
        INavigationService navigation,
        IAuditService audit,
        IDialogService dialog,
        ToolbarViewModel toolbar,
        StatusViewModel status,
        WaveformViewModel waveform)
    {
        _navigation = navigation;
        _audit = audit;
        _dialog = dialog;
        Toolbar = toolbar;
        Status = status;
        Waveform = waveform;

        // Listen for navigation changes and update CurrentPage
        _navigation.CurrentViewModelChanged += (_, vm) =>
        {
            CurrentPage = vm;
        };

        // Navigate to Home by default
        _navigation.NavigateTo("Home");
    }

    [RelayCommand]
    private void Navigate(string routeKey)
    {
        if (_navigation.NavigateTo(routeKey))
        {
            ActiveRoute = routeKey;
            // No audit: §10 has no NAVIGATION type; navigation is not a 关键操作.
        }
    }

    [RelayCommand]
    private void ShowFilterDialog()
    {
        ActiveRoute = "Filter";
        var result = _dialog.ShowDialog("Filter");
        if (result.Confirmed)
            _audit.Log(AuditEventTypes.FilterChange, "Filter dialog confirmed");
    }

    [RelayCommand]
    private void ShowDisplayDialog()
    {
        ActiveRoute = "Display";
        var result = _dialog.ShowDialog("Display");
        if (result.Confirmed)
            _audit.Log(AuditEventTypes.GainChange, "Display dialog confirmed");
    }

    [RelayCommand]
    private void ShowUserManagement()
    {
        ActiveRoute = "User";
        var result = _dialog.ShowDialog("UserManagement");
        if (result.Confirmed)
            _audit.Log(AuditEventTypes.UserLogin, "User management dialog confirmed");
    }

    [RelayCommand]
    private void RequestShutdown()
    {
        ActiveRoute = "Shutdown";
        if (_dialog.ShowConfirmation("关机确认", "确定要关闭系统吗？"))
        {
            _audit.Log(AuditEventTypes.MonitoringStop, "System shutdown requested");
            System.Windows.Application.Current?.Shutdown();
        }
    }
}
