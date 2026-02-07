using System.Windows;
using System.Windows.Media.Imaging;
using Neo.UI.ViewModels;
using Neo.Video;

namespace Neo.UI.Services;

public sealed class VideoPanelController : IDisposable
{
    private readonly IVideoSource _source;
    private readonly VideoViewModel _viewModel;
    private readonly System.Windows.Threading.Dispatcher _dispatcher;
    private byte[] _pixelBuffer = Array.Empty<byte>();
    private WriteableBitmap? _bitmap;
    private bool _started;

    public VideoPanelController(
        IVideoSource source,
        VideoViewModel viewModel,
        System.Windows.Threading.Dispatcher? dispatcher = null)
    {
        _source = source;
        _viewModel = viewModel;
        _dispatcher = dispatcher ?? Application.Current?.Dispatcher ?? System.Windows.Threading.Dispatcher.CurrentDispatcher;
    }

    public void Start()
    {
        if (_started)
        {
            return;
        }

        _started = true;
        _source.SampleReceived += OnVideoFrame;

        bool hasCamera = _source.AvailableCameras.Count > 0;
        _viewModel.SetDeviceConnected(hasCamera);
        if (!hasCamera)
        {
            return;
        }

        try
        {
            _source.Start();
        }
        catch
        {
            _viewModel.SetDeviceConnected(false);
        }
    }

    public void Stop()
    {
        if (!_started)
        {
            return;
        }

        _started = false;
        _source.SampleReceived -= OnVideoFrame;

        try
        {
            _source.Stop();
        }
        catch
        {
        }
    }

    private void OnVideoFrame(VideoFrame frame)
    {
        int requiredBytes = Math.Max(0, frame.StrideBytes * frame.Height);
        if (requiredBytes <= 0)
        {
            return;
        }

        if (_pixelBuffer.Length < requiredBytes)
        {
            _pixelBuffer = new byte[requiredBytes];
        }

        int copied = _source.CopyLatestFramePixels(_pixelBuffer.AsSpan(0, requiredBytes));
        if (copied <= 0)
        {
            return;
        }

        _ = _dispatcher.BeginInvoke(() =>
        {
            if (_bitmap is null || _bitmap.PixelWidth != frame.Width || _bitmap.PixelHeight != frame.Height)
            {
                _bitmap = new WriteableBitmap(
                    frame.Width,
                    frame.Height,
                    96,
                    96,
                    System.Windows.Media.PixelFormats.Bgra32,
                    null);
            }

            _bitmap.WritePixels(
                new Int32Rect(0, 0, frame.Width, frame.Height),
                _pixelBuffer,
                frame.StrideBytes,
                0);
            _viewModel.UpdateFrame(_bitmap);
        });
    }

    public void Dispose()
    {
        Stop();
        if (_source is IDisposable disposable)
        {
            disposable.Dispose();
        }
    }
}
