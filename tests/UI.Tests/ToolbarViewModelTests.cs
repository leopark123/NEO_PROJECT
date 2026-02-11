using Neo.UI.Services;
using Neo.UI.ViewModels;
using Xunit;

namespace Neo.UI.Tests;

public class ToolbarViewModelTests
{
    private static ToolbarViewModel CreateVm(out AuditServiceAdapter audit)
    {
        audit = new AuditServiceAdapter();
        return new ToolbarViewModel(audit, new StubThemeService());
    }

    [Fact]
    public void PlaybackCommand_TogglesIsPlaying()
    {
        var vm = CreateVm(out _);
        Assert.False(vm.IsPlaying);

        vm.PlaybackCommand.Execute(null);
        Assert.True(vm.IsPlaying);

        vm.PlaybackCommand.Execute(null);
        Assert.False(vm.IsPlaying);

        vm.StopClock();
    }

    [Fact]
    public void PlaybackCommand_LogsStartAndStop()
    {
        var vm = CreateVm(out var audit);

        vm.PlaybackCommand.Execute(null);
        vm.PlaybackCommand.Execute(null);

        var events = audit.GetRecentEvents(10);
        Assert.Equal(2, events.Count);
        Assert.Equal(AuditEventTypes.MonitoringStart, events[0].EventType);
        Assert.Equal(AuditEventTypes.MonitoringStop, events[1].EventType);

        vm.StopClock();
    }

    [Fact]
    public void ScreenshotCommand_LogsScreenshot()
    {
        var vm = CreateVm(out var audit);

        vm.ScreenshotCommand.Execute(null);

        var events = audit.GetRecentEvents(10);
        Assert.Single(events);
        Assert.Equal(AuditEventTypes.Screenshot, events[0].EventType);

        vm.StopClock();
    }

    [Fact]
    public void AnnotationCommand_LogsAnnotation()
    {
        var vm = CreateVm(out var audit);

        vm.AnnotationCommand.Execute(null);

        var events = audit.GetRecentEvents(10);
        Assert.Single(events);
        Assert.Equal(AuditEventTypes.Annotation, events[0].EventType);

        vm.StopClock();
    }

    [Fact]
    public void PinCommand_TogglesPinnedState()
    {
        var vm = CreateVm(out _);

        Assert.False(vm.IsPinned);
        vm.PinCommand.Execute(null);
        Assert.True(vm.IsPinned);
        vm.PinCommand.Execute(null);
        Assert.False(vm.IsPinned);

        vm.StopClock();
    }

    [Fact]
    public void ToggleNavDrawerCommand_TogglesDrawerState()
    {
        var vm = CreateVm(out _);

        Assert.False(vm.IsNavDrawerOpen);
        vm.ToggleNavDrawerCommand.Execute(null);
        Assert.True(vm.IsNavDrawerOpen);
        vm.ToggleNavDrawerCommand.Execute(null);
        Assert.False(vm.IsNavDrawerOpen);

        vm.StopClock();
    }

    [Fact]
    public void CurrentUser_DefaultValue()
    {
        var vm = CreateVm(out _);
        Assert.Equal("User: Operator", vm.CurrentUser);
        vm.StopClock();
    }

    [Fact]
    public void BedNumberDisplay_ReflectsInput()
    {
        var vm = CreateVm(out _);
        Assert.Equal("Bed: 01", vm.BedNumberDisplay);

        vm.BedNumber = "12";
        Assert.Equal("Bed: 12", vm.BedNumberDisplay);

        vm.StopClock();
    }

    [Fact]
    public void OpenVersionEntryCommand_CanExecute()
    {
        var vm = CreateVm(out _);
        Assert.True(vm.OpenVersionEntryCommand.CanExecute(null));
        vm.StopClock();
    }

    [Fact]
    public void SwitchThemeCommand_TogglesBetweenAppleAndDefaultDisplay()
    {
        var vm = CreateVm(out _);

        Assert.Equal("Apple", vm.CurrentThemeDisplay);
        Assert.Equal("切换 Default", vm.ThemeToggleButtonText);

        vm.SwitchThemeCommand.Execute(null);
        Assert.Equal("Default", vm.CurrentThemeDisplay);
        Assert.Equal("切换 Apple", vm.ThemeToggleButtonText);

        vm.SwitchThemeCommand.Execute(null);
        Assert.Equal("Apple", vm.CurrentThemeDisplay);
        Assert.Equal("切换 Default", vm.ThemeToggleButtonText);

        vm.StopClock();
    }
}
