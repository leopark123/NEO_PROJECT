using Neo.UI.Services;
using Neo.UI.ViewModels;
using Xunit;

namespace Neo.UI.Tests;

public class WaveformViewModelTests
{
    private static WaveformViewModel CreateVm(out AuditServiceAdapter audit)
    {
        audit = new AuditServiceAdapter();
        return new WaveformViewModel(audit, new StubThemeService());
    }

    [Fact]
    public void SelectedLeadCombination_DefaultsToFirstOption()
    {
        var vm = CreateVm(out _);

        Assert.NotNull(vm.SelectedLeadCombination);
        Assert.Equal("C3-P3 / C4-P4", vm.SelectedLeadCombination!.Label);
        Assert.Equal("CH1: C3-P3", vm.LeadCh1);
        Assert.Equal("CH2: C4-P4", vm.LeadCh2);
    }

    [Fact]
    public void LeadCombinationOptions_OnlyContainsHardwareSupportedLeads()
    {
        // Hardware constraint (CONSENSUS_BASELINE.md §6.2): Only C3-P3/C4-P4 directly supported
        // This prevents clinical mislabeling (showing unsupported leads like F3-P3, C3-O1)
        Assert.Single(WaveformViewModel.LeadCombinationOptions);
        Assert.Equal("C3-P3 / C4-P4", WaveformViewModel.LeadCombinationOptions[0].Label);
    }

    [Fact]
    public void SelectedGain_DefaultIs100()
    {
        var vm = CreateVm(out _);
        Assert.Equal(100, vm.SelectedGain);
        Assert.Equal("100 uV/cm", vm.GainDisplay);
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
    public void SelectedYAxis_DefaultIs100()
    {
        var vm = CreateVm(out _);
        Assert.Equal(100, vm.SelectedYAxis);
        Assert.Equal("+/-100 uV", vm.YAxisDisplay);
    }

    [Fact]
    public void YAxisDisplay_UpdatesOnChange()
    {
        var vm = CreateVm(out _);
        vm.SelectedYAxis = 200;
        Assert.Equal("+/-200 uV", vm.YAxisDisplay);
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
        Assert.Equal("15 s", vm.SweepDisplay);
    }

    [Fact]
    public void SelectedAeegHours_DefaultIs3()
    {
        var vm = CreateVm(out _);
        Assert.Equal(3, vm.SelectedAeegHours);
        Assert.Equal("3 h", vm.AeegTimeDisplay);
    }

    [Fact]
    public void AeegTimeDisplay_UpdatesOnChange()
    {
        var vm = CreateVm(out _);
        vm.SelectedAeegHours = 6;
        Assert.Equal("6 h", vm.AeegTimeDisplay);
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

    [Fact]
    public void CycleGain_CyclesToNextValue()
    {
        var vm = CreateVm(out _);
        vm.SelectedGain = 100;

        vm.CycleGainCommand.Execute(null);

        Assert.Equal(200, vm.SelectedGain);
    }

    [Fact]
    public void CycleGain_WrapsAroundFromLastToFirst()
    {
        var vm = CreateVm(out _);
        vm.SelectedGain = 1000;

        vm.CycleGainCommand.Execute(null);

        Assert.Equal(10, vm.SelectedGain);
    }

    [Fact]
    public void CycleYAxis_CyclesToNextValue()
    {
        var vm = CreateVm(out _);
        vm.SelectedYAxis = 100;

        vm.CycleYAxisCommand.Execute(null);

        Assert.Equal(200, vm.SelectedYAxis);
    }

    [Fact]
    public void CycleYAxis_WrapsAroundFromLastToFirst()
    {
        var vm = CreateVm(out _);
        vm.SelectedYAxis = 200;

        vm.CycleYAxisCommand.Execute(null);

        Assert.Equal(25, vm.SelectedYAxis);
    }

    [Fact]
    public void CycleAeegTimeWindow_CyclesToNextValue()
    {
        var vm = CreateVm(out _);
        vm.SelectedAeegHours = 3;

        vm.CycleAeegTimeWindowCommand.Execute(null);

        Assert.Equal(6, vm.SelectedAeegHours);
    }

    [Fact]
    public void CycleAeegTimeWindow_WrapsAroundFromLastToFirst()
    {
        var vm = CreateVm(out _);
        vm.SelectedAeegHours = 24;

        vm.CycleAeegTimeWindowCommand.Execute(null);

        Assert.Equal(1, vm.SelectedAeegHours);
    }

    [Fact]
    public void CycleLeadCombination_WithSingleOption_KeepsCurrentSelection()
    {
        // With only one hardware-supported option, cycling returns to the same selection
        var vm = CreateVm(out _);
        Assert.Equal("CH1: C3-P3", vm.LeadCh1);
        Assert.Equal("CH2: C4-P4", vm.LeadCh2);

        vm.CycleLeadCombinationCommand.Execute(null);
        Assert.Equal("CH1: C3-P3", vm.LeadCh1);
        Assert.Equal("CH2: C4-P4", vm.LeadCh2);
    }

    [Fact]
    public void LeadChange_LogsAuditEvent()
    {
        // Lead combination changes must be audited (same pattern as GainChange)
        var vm = CreateVm(out var audit);

        // Initial selection doesn't trigger audit (oldValue is null)
        var initialEvents = audit.GetRecentEvents(100);
        var leadEventsInitial = initialEvents.Count(e => e.EventType == AuditEventTypes.LeadChange);

        // Manual change to same option should not trigger audit (label unchanged)
        vm.SelectedLeadCombination = WaveformViewModel.LeadCombinationOptions[0];
        var eventsAfterSame = audit.GetRecentEvents(100);
        var leadEventsAfterSame = eventsAfterSame.Count(e => e.EventType == AuditEventTypes.LeadChange);
        Assert.Equal(leadEventsInitial, leadEventsAfterSame);

        // Note: With only one option, we cannot test actual lead change audit in this test.
        // The audit mechanism is tested via the method signature and will work when
        // multiple hardware-supported options become available in future.
    }

    [Fact]
    public void LeadChange_WithDifferentLabel_LogsAuditEventWithDetails()
    {
        // Positive test: verify LEAD_CHANGE event is logged when label actually changes
        var vm = CreateVm(out var audit);
        var initialCount = audit.GetRecentEvents(100).Count;

        // Construct a different lead combination to trigger audit
        var newLeadOption = new LeadCombinationOption("P3-P4 / C3-C4", "P3-P4", "C3-C4");
        vm.SelectedLeadCombination = newLeadOption;

        var events = audit.GetRecentEvents(100);
        Assert.Equal(initialCount + 1, events.Count);

        var leadEvent = events[^1];
        Assert.Equal(AuditEventTypes.LeadChange, leadEvent.EventType);
        Assert.NotNull(leadEvent.Details);
        Assert.Contains("C3-P3 / C4-P4", leadEvent.Details);  // Old value
        Assert.Contains("P3-P4 / C3-C4", leadEvent.Details);   // New value
        Assert.Contains("->", leadEvent.Details);              // Format: "old -> new"
    }

    // Per-EEG lane configuration tests

    [Fact]
    public void SourceOptions_Contains3Channels()
    {
        // CH1 (C3-P3), CH2 (C4-P4), CH4 (C3-C4, cross-channel)
        Assert.Equal(3, WaveformViewModel.SourceOptions.Count);
        Assert.Equal("CH1 (C3-P3)", WaveformViewModel.SourceOptions[0].Label);
        Assert.Equal(0, WaveformViewModel.SourceOptions[0].PhysicalChannel);
        Assert.Equal("CH2 (C4-P4)", WaveformViewModel.SourceOptions[1].Label);
        Assert.Equal(1, WaveformViewModel.SourceOptions[1].PhysicalChannel);
        Assert.Equal("CH4 (C3-C4, 跨导联)", WaveformViewModel.SourceOptions[2].Label);
        Assert.Equal(3, WaveformViewModel.SourceOptions[2].PhysicalChannel);
    }

    [Fact]
    public void Eeg1_DefaultsToSource0_Gain100_Range100()
    {
        var vm = CreateVm(out _);

        Assert.NotNull(vm.Eeg1Source);
        Assert.Equal(WaveformViewModel.SourceOptions[0], vm.Eeg1Source);  // CH1
        Assert.Equal(100, vm.Eeg1Gain);
        Assert.Equal(100, vm.Eeg1Range);
    }

    [Fact]
    public void Eeg2_DefaultsToSource1_Gain100_Range100()
    {
        var vm = CreateVm(out _);

        Assert.NotNull(vm.Eeg2Source);
        Assert.Equal(WaveformViewModel.SourceOptions[1], vm.Eeg2Source);  // CH2
        Assert.Equal(100, vm.Eeg2Gain);
        Assert.Equal(100, vm.Eeg2Range);
    }

    [Fact]
    public void Eeg1Source_CanBeChanged()
    {
        var vm = CreateVm(out _);

        vm.Eeg1Source = WaveformViewModel.SourceOptions[2];  // CH4

        Assert.Equal(WaveformViewModel.SourceOptions[2], vm.Eeg1Source);
        Assert.Equal(3, vm.Eeg1Source.PhysicalChannel);
    }

    [Fact]
    public void Eeg1Gain_CanBeChanged()
    {
        var vm = CreateVm(out _);

        vm.Eeg1Gain = 200;

        Assert.Equal(200, vm.Eeg1Gain);
    }

    [Fact]
    public void Eeg1Range_CanBeChanged()
    {
        var vm = CreateVm(out _);

        vm.Eeg1Range = 50;

        Assert.Equal(50, vm.Eeg1Range);
    }
}
