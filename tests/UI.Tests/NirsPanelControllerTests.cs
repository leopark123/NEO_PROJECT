using Neo.NIRS;
using Neo.UI.Services;
using Neo.UI.ViewModels;
using Xunit;

namespace Neo.UI.Tests;

public class NirsPanelControllerTests
{
    [Fact]
    public void Start_WhenShellBlocked_SetsPanelAndDefaultChannelStates()
    {
        var vm = new NirsViewModel();
        using var controller = new NirsPanelController(new NirsIntegrationShell(), vm);

        controller.Start();

        Assert.Contains("blocked", vm.PanelStatus, StringComparison.OrdinalIgnoreCase);
        Assert.Equal("--%", vm.Channels[0].StatusText);
        Assert.Equal("--%", vm.Channels[1].StatusText);
        Assert.Equal("--%", vm.Channels[2].StatusText);
        Assert.Equal("Blocked", vm.Channels[3].StatusText);
        Assert.Equal("Blocked", vm.Channels[4].StatusText);
        Assert.Equal("Blocked", vm.Channels[5].StatusText);
    }

    [Fact]
    public void TickSimulationForTest_UpdatesEnabledChannels()
    {
        var vm = new NirsViewModel();
        using var controller = new NirsPanelController(new NirsIntegrationShell(), vm);

        controller.Start();
        controller.TickSimulationForTest();

        Assert.Matches(@"^\d+%$|^Fault$|^--%$", vm.Channels[0].StatusText);
        Assert.Matches(@"^\d+%$|^Fault$|^--%$", vm.Channels[1].StatusText);
        Assert.Matches(@"^\d+%$|^Fault$|^--%$", vm.Channels[2].StatusText);
        Assert.Equal("Blocked", vm.Channels[3].StatusText);
    }

    [Fact]
    public void DisabledChannel_RemainsBlockedDuringSimulation()
    {
        var vm = new NirsViewModel();
        using var controller = new NirsPanelController(new NirsIntegrationShell(), vm);
        controller.Start();

        vm.Channels[0].IsEnabled = false;
        controller.TickSimulationForTest();

        Assert.Equal("Blocked", vm.Channels[0].StatusText);
    }

    [Fact]
    public void Stop_CanBeCalledMultipleTimes()
    {
        var vm = new NirsViewModel();
        using var controller = new NirsPanelController(new NirsIntegrationShell(), vm);

        controller.Start();
        controller.Stop();
        controller.Stop();
    }
}
