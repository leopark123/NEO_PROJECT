// WaveformViewModelTests.cs
// Sprint 2.4: Tests for WaveformViewModel parameter bindings and audit logging.

using Neo.UI.Services;
using Neo.UI.ViewModels;
using Xunit;

namespace Neo.UI.Tests;

public class WaveformViewModelTests
{
    private static WaveformViewModel CreateVm(out AuditServiceAdapter audit)
    {
        audit = new AuditServiceAdapter();
        return new WaveformViewModel(audit);
    }

    [Fact]
    public void LeadCh1_DefaultValue()
    {
        var vm = CreateVm(out _);
        Assert.Equal("CH1: C3-P3", vm.LeadCh1);
    }

    [Fact]
    public void LeadCh2_DefaultValue()
    {
        var vm = CreateVm(out _);
        Assert.Equal("CH2: C4-P4", vm.LeadCh2);
    }

    [Fact]
    public void SelectedGain_DefaultIs100()
    {
        var vm = CreateVm(out _);
        Assert.Equal(100, vm.SelectedGain);
        Assert.Equal("100 μV/cm", vm.GainDisplay);
    }

    [Fact]
    public void GainChange_LogsAuditEvent()
    {
        var vm = CreateVm(out var audit);
        var initialCount = audit.GetRecentEvents(100).Count;

        vm.SelectedGain = 200;

        var events = audit.GetRecentEvents(100);
        Assert.Equal(initialCount + 1, events.Count);
        Assert.Equal(AuditEventTypes.GainChange, events[^1].EventType);
        Assert.Contains("200", events[^1].Details!);
    }

    [Fact]
    public void GainDisplay_UpdatesOnChange()
    {
        var vm = CreateVm(out _);
        vm.SelectedGain = 50;
        Assert.Equal("50 μV/cm", vm.GainDisplay);
    }

    [Fact]
    public void SelectedYAxis_DefaultIs100()
    {
        var vm = CreateVm(out _);
        Assert.Equal(100, vm.SelectedYAxis);
        Assert.Equal("±100 μV", vm.YAxisDisplay);
    }

    [Fact]
    public void YAxisDisplay_UpdatesOnChange()
    {
        var vm = CreateVm(out _);
        vm.SelectedYAxis = 200;
        Assert.Equal("±200 μV", vm.YAxisDisplay);
    }

    [Fact]
    public void SelectedHpf_DefaultIs05()
    {
        var vm = CreateVm(out _);
        Assert.Equal(0.5, vm.SelectedHpf);
        Assert.Equal("HPF: 0.5 Hz", vm.HpfDisplay);
    }

    [Fact]
    public void HpfChange_LogsFilterChangeAudit()
    {
        var vm = CreateVm(out var audit);
        var initialCount = audit.GetRecentEvents(100).Count;

        vm.SelectedHpf = 1.5;

        var events = audit.GetRecentEvents(100);
        Assert.Equal(initialCount + 1, events.Count);
        Assert.Equal(AuditEventTypes.FilterChange, events[^1].EventType);
    }

    [Fact]
    public void SelectedLpf_DefaultIs35()
    {
        var vm = CreateVm(out _);
        Assert.Equal(35, vm.SelectedLpf);
        Assert.Equal("LPF: 35 Hz", vm.LpfDisplay);
    }

    [Fact]
    public void LpfChange_LogsFilterChangeAudit()
    {
        var vm = CreateVm(out var audit);
        var initialCount = audit.GetRecentEvents(100).Count;

        vm.SelectedLpf = 50;

        var events = audit.GetRecentEvents(100);
        Assert.Equal(initialCount + 1, events.Count);
        Assert.Equal(AuditEventTypes.FilterChange, events[^1].EventType);
    }

    [Fact]
    public void SelectedNotch_DefaultIs50()
    {
        var vm = CreateVm(out _);
        Assert.Equal(50, vm.SelectedNotch);
        Assert.Equal("Notch: 50 Hz", vm.NotchDisplay);
    }

    [Fact]
    public void NotchChange_LogsFilterChangeAudit()
    {
        var vm = CreateVm(out var audit);
        var initialCount = audit.GetRecentEvents(100).Count;

        vm.SelectedNotch = 60;

        var events = audit.GetRecentEvents(100);
        Assert.Equal(initialCount + 1, events.Count);
        Assert.Equal(AuditEventTypes.FilterChange, events[^1].EventType);
    }

    [Fact]
    public void SweepSeconds_DefaultIs15()
    {
        var vm = CreateVm(out _);
        Assert.Equal(15, vm.SweepSeconds);
        Assert.Equal("15 秒/屏", vm.SweepDisplay);
    }

    [Fact]
    public void SelectedAeegHours_DefaultIs3()
    {
        var vm = CreateVm(out _);
        Assert.Equal(3, vm.SelectedAeegHours);
        Assert.Equal("3 小时", vm.AeegTimeDisplay);
    }

    [Fact]
    public void AeegTimeDisplay_UpdatesOnChange()
    {
        var vm = CreateVm(out _);
        vm.SelectedAeegHours = 6;
        Assert.Equal("6 小时", vm.AeegTimeDisplay);
    }

    [Fact]
    public void GainOptions_Contains7Values()
    {
        Assert.Equal(7, WaveformViewModel.GainOptions.Length);
        Assert.Contains(100, WaveformViewModel.GainOptions);
        Assert.Contains(1000, WaveformViewModel.GainOptions);
    }

    [Fact]
    public void AeegTimeWindowOptions_Contains5Values()
    {
        Assert.Equal(5, WaveformViewModel.AeegTimeWindowOptions.Length);
        Assert.Contains(1, WaveformViewModel.AeegTimeWindowOptions);
        Assert.Contains(24, WaveformViewModel.AeegTimeWindowOptions);
    }
}
