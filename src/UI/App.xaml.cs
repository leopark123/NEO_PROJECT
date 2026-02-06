using System.Windows;
using Neo.Host;
using Neo.UI.Services;
using Neo.UI.ViewModels;
using Neo.Video;

namespace Neo.UI;

public partial class App : Application
{
    private ServiceRegistry? _services;
    private VideoPanelController? _videoController;
    private NirsPanelController? _nirsController;
    private NirsWiring? _nirsWiring;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        _services = new ServiceRegistry();

        _services.Routes.Register("Home", () => new HomeViewModel());
        _services.Routes.Register("History", () => new HomeViewModel());
        _services.Routes.Register("Export", () => new HomeViewModel());

        var toolbar = new ToolbarViewModel(_services.Audit, _services.Theme);
        var status = new StatusViewModel();
        var waveform = new WaveformViewModel(_services.Audit, _services.Theme);
        var video = new VideoViewModel();
        var nirs = new NirsViewModel();

        TryStartVideoPipeline(video);
        TryStartNirsPipeline(nirs);

        var viewModel = new MainWindowViewModel(
            _services.Navigation,
            _services.Audit,
            _services.Dialog,
            toolbar,
            status,
            waveform,
            video,
            nirs);

        var mainWindow = new MainWindow
        {
            DataContext = viewModel
        };

        MainWindow = mainWindow;
        mainWindow.Show();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _videoController?.Dispose();
        _nirsController?.Dispose();
        _nirsWiring?.Dispose();
        base.OnExit(e);
    }

    private void TryStartVideoPipeline(VideoViewModel viewModel)
    {
        try
        {
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();
            var source = new UsbCameraSource(() =>
                stopwatch.ElapsedTicks * 1_000_000 / System.Diagnostics.Stopwatch.Frequency);

            _videoController = new VideoPanelController(source, viewModel, Dispatcher);
            _videoController.Start();
        }
        catch
        {
            viewModel.SetDeviceConnected(false);
        }
    }

    private void TryStartNirsPipeline(NirsViewModel viewModel)
    {
        try
        {
            // 使用 NirsWiring 创建带 MockNirsSource 的 Shell
            _nirsWiring = new NirsWiring();
            _nirsWiring.Start();

            _nirsController = new NirsPanelController(_nirsWiring.Shell, viewModel, Dispatcher);
            _nirsController.Start();
        }
        catch
        {
            viewModel.PanelStatus = "NIRS initialization failed";
        }
    }
}
