// ToolbarViewModelTests.cs
// Sprint 2.1: Tests for ToolbarViewModel commands and default values.

using Neo.UI.Services;
using Neo.UI.ViewModels;
using Xunit;

namespace Neo.UI.Tests;

public class ToolbarViewModelTests
{
    private static ToolbarViewModel CreateVm(out AuditServiceAdapter audit)
    {
        audit = new AuditServiceAdapter();
        var vm = new ToolbarViewModel(audit);
        return vm;
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
    public void PlaybackCommand_Play_LogsMonitoringStart()
    {
        var vm = CreateVm(out var audit);

        vm.PlaybackCommand.Execute(null);

        var events = audit.GetRecentEvents(10);
        Assert.Single(events);
        Assert.Equal(AuditEventTypes.MonitoringStart, events[0].EventType);

        vm.StopClock();
    }

    [Fact]
    public void PlaybackCommand_Pause_LogsMonitoringStop()
    {
        var vm = CreateVm(out var audit);

        // Play then Pause
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
    public void CurrentUser_DefaultValue()
    {
        var vm = CreateVm(out _);
        Assert.Equal("用户: --", vm.CurrentUser);
        vm.StopClock();
    }

    [Fact]
    public void BedNumber_DefaultValue()
    {
        var vm = CreateVm(out _);
        Assert.Equal("床位: --", vm.BedNumber);
        vm.StopClock();
    }

    [Fact]
    public void AllCommands_CanExecute()
    {
        var vm = CreateVm(out _);

        Assert.True(vm.PlaybackCommand.CanExecute(null));
        Assert.True(vm.ScreenshotCommand.CanExecute(null));
        Assert.True(vm.AnnotationCommand.CanExecute(null));

        vm.StopClock();
    }
}
