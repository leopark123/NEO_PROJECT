using System.Windows;
using System.Windows.Media;
using Neo.UI.ViewModels;
using Xunit;

namespace Neo.UI.Tests;

public class VideoViewModelTests
{
    [Fact]
    public void Defaults_ShowNoDeviceStatus()
    {
        var vm = new VideoViewModel();

        Assert.False(vm.HasDevice);
        Assert.Null(vm.FrameImage);
        Assert.Equal("No camera device", vm.StatusText);
    }

    [Fact]
    public void SetDeviceConnected_True_UpdatesStatus()
    {
        var vm = new VideoViewModel();

        vm.SetDeviceConnected(true);

        Assert.True(vm.HasDevice);
        Assert.Equal("Camera connected", vm.StatusText);
    }

    [Fact]
    public void UpdateFrame_WithImage_EnablesStreamingState()
    {
        var vm = new VideoViewModel();
        var image = new DrawingImage(
            new GeometryDrawing(
                Brushes.White,
                null,
                new RectangleGeometry(new Rect(0, 0, 1, 1))));

        vm.UpdateFrame(image);

        Assert.True(vm.HasDevice);
        Assert.Equal("Camera streaming", vm.StatusText);
        Assert.Same(image, vm.FrameImage);
    }

    [Fact]
    public void SetDeviceConnected_False_ClearsFrame()
    {
        var vm = new VideoViewModel();
        var image = new DrawingImage(
            new GeometryDrawing(
                Brushes.White,
                null,
                new RectangleGeometry(new Rect(0, 0, 1, 1))));

        vm.UpdateFrame(image);
        vm.SetDeviceConnected(false);

        Assert.False(vm.HasDevice);
        Assert.Null(vm.FrameImage);
        Assert.Equal("No camera device", vm.StatusText);
    }
}
