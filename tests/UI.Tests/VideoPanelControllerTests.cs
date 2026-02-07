using Neo.Core.Interfaces;
using Neo.UI.Services;
using Neo.UI.ViewModels;
using Neo.Video;
using Xunit;

namespace Neo.UI.Tests;

public class VideoPanelControllerTests
{
    [Fact]
    public void Start_WithoutAvailableCamera_KeepsNoDeviceState()
    {
        var source = new FakeVideoSource();
        var vm = new VideoViewModel();
        using var controller = new VideoPanelController(source, vm);

        controller.Start();

        Assert.False(vm.HasDevice);
        Assert.Equal("No camera device", vm.StatusText);
    }

    [Fact]
    public void Stop_CanBeCalledMultipleTimes()
    {
        var source = new FakeVideoSource();
        var vm = new VideoViewModel();
        using var controller = new VideoPanelController(source, vm);

        controller.Start();
        controller.Stop();
        controller.Stop();
    }

    private sealed class FakeVideoSource : IVideoSource
    {
        public int SampleRate => 30;
        public int ChannelCount => 1;
#pragma warning disable CS0067
        public event Action<VideoFrame>? SampleReceived;
#pragma warning restore CS0067
        public IReadOnlyList<CameraDeviceInfo> AvailableCameras => [];
        public CameraDeviceInfo? SelectedCamera => null;
        public bool IsCapturing { get; private set; }
        public (int Width, int Height) Resolution => (640, 480);

        public void Start()
        {
            IsCapturing = true;
        }

        public void Stop()
        {
            IsCapturing = false;
        }

        public int CopyLatestFramePixels(Span<byte> destination)
        {
            return 0;
        }
    }
}
