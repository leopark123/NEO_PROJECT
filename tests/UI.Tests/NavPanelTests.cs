// NavPanelTests.cs
// Sprint 2.2: Tests for NavPanel navigation, ActiveRoute highlight, and audit.
// Tests exercise MainWindowViewModel commands which drive the NavPanel bindings.

using Neo.UI.Services;
using Neo.UI.ViewModels;
using Xunit;

namespace Neo.UI.Tests;

public class NavPanelTests
{
    private static MainWindowViewModel CreateVm(out AuditServiceAdapter audit, bool registerAll = true)
    {
        var routes = new RouteRegistry();
        routes.Register("Home", () => new HomeViewModel());
        if (registerAll)
        {
            routes.Register("History", () => new HomeViewModel());
            routes.Register("Export", () => new HomeViewModel());
        }
        var nav = new NavigationService(routes);
        audit = new AuditServiceAdapter();
        var dialog = new StubDialogService();
        var toolbar = new ToolbarViewModel(audit);
        var status = new StatusViewModel();
        var waveform = new WaveformViewModel(audit);
        var vm = new MainWindowViewModel(nav, audit, dialog, toolbar, status, waveform);
        toolbar.StopClock();
        status.StopTimer();
        return vm;
    }

    [Fact]
    public void ActiveRoute_DefaultIsHome()
    {
        var vm = CreateVm(out _);
        Assert.Equal("Home", vm.ActiveRoute);
    }

    [Fact]
    public void NavigateCommand_SetsActiveRoute()
    {
        var vm = CreateVm(out _);
        vm.NavigateCommand.Execute("History");
        Assert.Equal("History", vm.ActiveRoute);
    }

    [Fact]
    public void NavigateCommand_DoesNotLogAudit()
    {
        // UI_SPEC §10 has no NAVIGATION event type.
        // Navigation is not a 关键操作 → no audit emitted.
        var vm = CreateVm(out var audit);
        var initialCount = audit.GetRecentEvents(100).Count;

        vm.NavigateCommand.Execute("History");

        var events = audit.GetRecentEvents(100);
        Assert.Equal(initialCount, events.Count);
    }

    [Fact]
    public void NavigateCommand_UnregisteredRoute_DoesNotChangeActiveRoute()
    {
        var vm = CreateVm(out _);
        Assert.Equal("Home", vm.ActiveRoute);

        vm.NavigateCommand.Execute("NonExistent");
        Assert.Equal("Home", vm.ActiveRoute);
    }

    [Fact]
    public void NavigateCommand_AllRouteButtons_CanExecute()
    {
        var vm = CreateVm(out _);
        Assert.True(vm.NavigateCommand.CanExecute("Home"));
        Assert.True(vm.NavigateCommand.CanExecute("History"));
        Assert.True(vm.NavigateCommand.CanExecute("Export"));
    }

    [Fact]
    public void DialogCommands_CanExecute()
    {
        var vm = CreateVm(out _);
        Assert.True(vm.ShowDisplayDialogCommand.CanExecute(null));
        Assert.True(vm.ShowFilterDialogCommand.CanExecute(null));
        Assert.True(vm.ShowUserManagementCommand.CanExecute(null));
        Assert.True(vm.RequestShutdownCommand.CanExecute(null));
    }

    [Fact]
    public void ActiveRoute_RaisesPropertyChanged()
    {
        var vm = CreateVm(out _);
        // Navigate away from Home first
        vm.NavigateCommand.Execute("History");

        var raised = false;
        vm.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName == nameof(vm.ActiveRoute))
                raised = true;
        };

        vm.NavigateCommand.Execute("Home");
        Assert.True(raised);
    }

    [Fact]
    public void ShowFilterDialog_SetsActiveRouteToFilter()
    {
        var vm = CreateVm(out _);
        vm.ShowFilterDialogCommand.Execute(null);
        Assert.Equal("Filter", vm.ActiveRoute);
    }

    [Fact]
    public void ShowDisplayDialog_SetsActiveRouteToDisplay()
    {
        var vm = CreateVm(out _);
        vm.ShowDisplayDialogCommand.Execute(null);
        Assert.Equal("Display", vm.ActiveRoute);
    }

    [Fact]
    public void ShowUserManagement_SetsActiveRouteToUser()
    {
        var vm = CreateVm(out _);
        vm.ShowUserManagementCommand.Execute(null);
        Assert.Equal("User", vm.ActiveRoute);
    }

    [Fact]
    public void RequestShutdown_SetsActiveRouteToShutdown()
    {
        var vm = CreateVm(out _);
        // StubDialogService.ShowConfirmation returns false, so app won't actually shut down
        vm.RequestShutdownCommand.Execute(null);
        Assert.Equal("Shutdown", vm.ActiveRoute);
    }
}

/// <summary>
/// Minimal IDialogService stub for testing (no MessageBox dependency).
/// </summary>
internal sealed class StubDialogService : IDialogService
{
    public DialogResult ShowDialog(string dialogKey, object? parameter = null)
        => DialogResult.Cancel();

    public void ShowMessage(string title, string message) { }

    public bool ShowConfirmation(string title, string message) => false;
}
