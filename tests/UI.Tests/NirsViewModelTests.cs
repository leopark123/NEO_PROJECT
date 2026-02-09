using Neo.UI.ViewModels;
using Xunit;

namespace Neo.UI.Tests;

public class NirsViewModelTests
{
    [Fact]
    public void Constructor_InitializesSixChannelsWithExpectedDefaults()
    {
        var vm = new NirsViewModel();

        Assert.Equal(6, vm.Channels.Count);

        Assert.True(vm.Channels[0].IsEnabled);
        Assert.True(vm.Channels[1].IsEnabled);
        Assert.True(vm.Channels[2].IsEnabled);
        Assert.True(vm.Channels[3].IsEnabled);   // CH4 now enabled by default
        Assert.False(vm.Channels[4].IsEnabled);
        Assert.False(vm.Channels[5].IsEnabled);

        Assert.Equal("--%", vm.Channels[0].StatusText);
        Assert.Equal("--%", vm.Channels[1].StatusText);
        Assert.Equal("--%", vm.Channels[2].StatusText);
        Assert.Equal("--%", vm.Channels[3].StatusText);  // CH4 now enabled by default
        Assert.Equal("Blocked", vm.Channels[4].StatusText);
        Assert.Equal("Blocked", vm.Channels[5].StatusText);
    }

    [Fact]
    public void SetChannelState_Percentage_UpdatesVisibleStatus()
    {
        var vm = new NirsViewModel();

        vm.SetChannelState(1, NirsChannelState.Percentage, 72);

        Assert.Equal("72%", vm.Channels[0].StatusText);
    }

    [Fact]
    public void SetChannelState_Fault_UpdatesVisibleStatus()
    {
        var vm = new NirsViewModel();

        vm.SetChannelState(2, NirsChannelState.Fault);

        Assert.Equal("Fault", vm.Channels[1].StatusText);
    }

    [Fact]
    public void DisabledChannel_ShowsBlockedAndRestoresLatestActiveStatusWhenReEnabled()
    {
        var vm = new NirsViewModel();

        vm.SetChannelState(3, NirsChannelState.Percentage, 68);
        Assert.Equal("68%", vm.Channels[2].StatusText);

        vm.Channels[2].IsEnabled = false;
        Assert.Equal("Blocked", vm.Channels[2].StatusText);

        vm.Channels[2].IsEnabled = true;
        Assert.Equal("68%", vm.Channels[2].StatusText);
    }

    [Fact]
    public void SetChannelState_OnDisabledChannel_KeepsBlockedUntilEnabled()
    {
        var vm = new NirsViewModel();

        // Use CH5 (index 4) which is disabled by default
        vm.SetChannelState(5, NirsChannelState.Fault);
        Assert.Equal("Blocked", vm.Channels[4].StatusText);

        vm.Channels[4].IsEnabled = true;
        Assert.Equal("Fault", vm.Channels[4].StatusText);
    }

    [Fact]
    public void SetChannelState_InvalidIndex_Throws()
    {
        var vm = new NirsViewModel();

        Assert.Throws<ArgumentOutOfRangeException>(() => vm.SetChannelState(0, NirsChannelState.Unknown));
        Assert.Throws<ArgumentOutOfRangeException>(() => vm.SetChannelState(7, NirsChannelState.Unknown));
    }

    [Fact]
    public void SetChannelState_Blocked_UpdatesStatusToBlocked()
    {
        var vm = new NirsViewModel();

        vm.SetChannelState(1, NirsChannelState.Blocked);

        Assert.Equal("Blocked", vm.Channels[0].StatusText);
    }

    [Fact]
    public void ApplySourceSwitchCommand_RealModeWithPort_RaisesRequest()
    {
        var vm = new NirsViewModel(() => ["COM7"])
        {
            SelectedSourceMode = "real"
        };

        NirsSourceSwitchRequest? captured = null;
        vm.SourceSwitchRequested += request => captured = request;

        vm.ApplySourceSwitchCommand.Execute(null);

        Assert.True(captured.HasValue);
        Assert.Equal("real", captured.Value.Mode);
        Assert.Equal("COM7", captured.Value.PortName);
    }

    [Fact]
    public void ApplySourceSwitchCommand_RealModeWithoutPort_DoesNotRaiseAndSetsStatus()
    {
        var vm = new NirsViewModel(() => Array.Empty<string>())
        {
            SelectedSourceMode = "real"
        };

        bool raised = false;
        vm.SourceSwitchRequested += _ => raised = true;

        vm.ApplySourceSwitchCommand.Execute(null);

        Assert.False(raised);
        Assert.Contains("requires selecting", vm.PanelStatus, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Constructor_LoadsDetectedSerialPorts_AndSelectsFirst()
    {
        var vm = new NirsViewModel(() => ["COM5", "COM3"]);

        Assert.Equal(2, vm.SerialPortOptions.Count);
        Assert.Equal("COM3", vm.SerialPortOptions[0]);
        Assert.Equal("COM5", vm.SerialPortOptions[1]);
        Assert.Equal("COM3", vm.SelectedSerialPort);
    }
}
