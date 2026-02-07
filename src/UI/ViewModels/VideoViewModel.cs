using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Neo.UI.ViewModels;

public partial class VideoViewModel : ViewModelBase
{
    [ObservableProperty]
    private ImageSource? _frameImage;

    [ObservableProperty]
    private bool _hasDevice;

    [ObservableProperty]
    private string _statusText = "No camera device";

    public void SetDeviceConnected(bool connected)
    {
        HasDevice = connected;
        if (!connected)
        {
            FrameImage = null;
            StatusText = "No camera device";
            return;
        }

        StatusText = FrameImage is null ? "Camera connected" : "Camera streaming";
    }

    public void UpdateFrame(ImageSource? frame)
    {
        FrameImage = frame;
        HasDevice = frame is not null || HasDevice;
        StatusText = frame is null
            ? (HasDevice ? "Camera connected" : "No camera device")
            : "Camera streaming";
    }
}
