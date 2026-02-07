using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Neo.UI.Services;

namespace Neo.UI.ViewModels;

public partial class MainWindowViewModel : ViewModelBase
{
    private readonly INavigationService _navigation;
    private readonly IAuditService _audit;
    private readonly IDialogService _dialog;

    [ObservableProperty]
    private ViewModelBase? _currentPage;

    [ObservableProperty]
    private string _activeRoute = "Home";

    public ToolbarViewModel Toolbar { get; }

    public StatusViewModel Status { get; }

    public WaveformViewModel Waveform { get; }

    public VideoViewModel Video { get; }

    public NirsViewModel Nirs { get; }

    public IAuditService Audit => _audit;

    public MainWindowViewModel(
        INavigationService navigation,
        IAuditService audit,
        IDialogService dialog,
        ToolbarViewModel toolbar,
        StatusViewModel status,
        WaveformViewModel waveform,
        VideoViewModel video,
        NirsViewModel nirs)
    {
        _navigation = navigation;
        _audit = audit;
        _dialog = dialog;
        Toolbar = toolbar;
        Status = status;
        Waveform = waveform;
        Video = video;
        Nirs = nirs;

        _navigation.CurrentViewModelChanged += (_, vm) => CurrentPage = vm;
        _navigation.NavigateTo("Home");
    }

    [RelayCommand]
    private void Navigate(string routeKey)
    {
        if (string.Equals(routeKey, "History", StringComparison.OrdinalIgnoreCase))
        {
            ShowHistoryDialog();
            return;
        }

        if (_navigation.NavigateTo(routeKey))
        {
            ActiveRoute = routeKey;
        }
    }

    [RelayCommand]
    private void ShowFilterDialog()
    {
        ActiveRoute = "Filter";
        var result = _dialog.ShowDialog("Filter");
        if (result.Confirmed)
        {
            _audit.Log(AuditEventTypes.FilterChange, "Filter dialog confirmed");
        }
    }

    [RelayCommand]
    private void ShowDisplayDialog()
    {
        ActiveRoute = "Display";
        var result = _dialog.ShowDialog("Display");
        if (result.Confirmed)
        {
            _audit.Log(AuditEventTypes.GainChange, "Display dialog confirmed");
        }
    }

    [RelayCommand]
    private void ShowUserManagement()
    {
        ActiveRoute = "User";
        var result = _dialog.ShowDialog("UserManagement");
        if (result.Confirmed)
        {
            _audit.Log(AuditEventTypes.UserLogin, "User management dialog confirmed");
        }
    }

    [RelayCommand]
    private void ShowHistoryDialog()
    {
        ActiveRoute = "History";
        _dialog.ShowDialog("History");
    }

    [RelayCommand]
    private void RequestShutdown()
    {
        ActiveRoute = "Shutdown";
        if (_dialog.ShowConfirmation("Shutdown Confirmation", "Do you want to close the monitoring system?"))
        {
            _audit.Log(AuditEventTypes.MonitoringStop, "System shutdown requested");
            System.Windows.Application.Current?.Shutdown();
        }
    }
}
