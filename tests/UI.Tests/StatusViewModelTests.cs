// StatusViewModelTests.cs
// Sprint 2.3: Tests for StatusViewModel data bindings and default values.

using Neo.UI.ViewModels;
using Xunit;

namespace Neo.UI.Tests;

public class StatusViewModelTests
{
    private static StatusViewModel CreateVm()
    {
        var vm = new StatusViewModel();
        vm.StopTimer(); // Prevent timer interference in tests
        return vm;
    }

    [Fact]
    public void FpsText_DefaultValue()
    {
        var vm = CreateVm();
        Assert.Equal("FPS: --", vm.FpsText);
    }

    [Fact]
    public void StorageText_DefaultValue()
    {
        var vm = CreateVm();
        Assert.Equal("存储: -- / --", vm.StorageText);
    }

    [Fact]
    public void EegConnected_DefaultIsFalse()
    {
        var vm = CreateVm();
        Assert.False(vm.EegConnected);
    }

    [Fact]
    public void NirsConnected_DefaultIsFalse()
    {
        var vm = CreateVm();
        Assert.False(vm.NirsConnected);
    }

    [Fact]
    public void VideoConnected_DefaultIsFalse()
    {
        var vm = CreateVm();
        Assert.False(vm.VideoConnected);
    }

    [Fact]
    public void CurrentTime_DefaultStartsWithTime()
    {
        var vm = CreateVm();
        Assert.StartsWith("Time: ", vm.CurrentTime);
    }

    [Fact]
    public void UpdateFps_SetsFormattedValue()
    {
        var vm = CreateVm();
        vm.UpdateFps(59.7);
        Assert.Equal("FPS: 60", vm.FpsText);
    }

    [Fact]
    public void UpdateStorage_SetsFormattedValue()
    {
        var vm = CreateVm();
        vm.UpdateStorage("120GB", "300GB");
        Assert.Equal("存储: 120GB/300GB", vm.StorageText);
    }

    [Fact]
    public void DeviceConnected_RaisesPropertyChanged()
    {
        var vm = CreateVm();
        var raised = false;
        vm.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName == nameof(vm.EegConnected))
                raised = true;
        };

        vm.EegConnected = true;
        Assert.True(raised);
        Assert.True(vm.EegConnected);
    }

    [Fact]
    public void AllDeviceStates_CanBeToggled()
    {
        var vm = CreateVm();

        vm.EegConnected = true;
        vm.NirsConnected = true;
        vm.VideoConnected = true;

        Assert.True(vm.EegConnected);
        Assert.True(vm.NirsConnected);
        Assert.True(vm.VideoConnected);

        vm.EegConnected = false;
        vm.NirsConnected = false;
        vm.VideoConnected = false;

        Assert.False(vm.EegConnected);
        Assert.False(vm.NirsConnected);
        Assert.False(vm.VideoConnected);
    }
}
