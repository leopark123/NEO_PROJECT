using System.Windows;
using System.Windows.Threading;
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
        nirs.SourceSwitchRequested += request => ApplyNirsSourceSwitch(nirs, request);

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

        // Ensure main window is visible before potentially slow device initialization.
        Dispatcher.BeginInvoke(DispatcherPriority.Background, new Action(() =>
        {
            TryStartVideoPipeline(video);
            TryStartNirsPipeline(nirs);
        }));
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
            // 使用 NirsWiring 创建 NIRS Shell（NEO_NIRS_MODE=mock|real）
            _nirsWiring = new NirsWiring();
            _nirsWiring.Start();

            _nirsController = new NirsPanelController(_nirsWiring.Shell, viewModel, Dispatcher);
            _nirsController.Start();
        }
        catch (Exception ex)
        {
            viewModel.PanelStatus = $"NIRS initialization failed: {ex.Message}";
        }
    }

    private void ApplyNirsSourceSwitch(NirsViewModel viewModel, NirsSourceSwitchRequest request)
    {
        try
        {
            ConfigureNirsEnvironment(request);

            _nirsController?.Dispose();
            _nirsController = null;
            _nirsWiring?.Dispose();
            _nirsWiring = null;

            viewModel.PanelStatus = request.Mode == "real"
                ? $"Switching NIRS source to real ({request.PortName})..."
                : "Switching NIRS source to mock...";

            TryStartNirsPipeline(viewModel);
        }
        catch (Exception ex)
        {
            viewModel.PanelStatus = $"NIRS source switch failed: {ex.Message}";
        }
    }

    private static void ConfigureNirsEnvironment(NirsSourceSwitchRequest request)
    {
        string mode = request.Mode.Trim().ToLowerInvariant();
        Environment.SetEnvironmentVariable("NEO_NIRS_MODE", mode, EnvironmentVariableTarget.Process);

        if (mode == "real")
        {
            if (string.IsNullOrWhiteSpace(request.PortName))
            {
                throw new InvalidOperationException("Real mode requires COM port.");
            }

            Environment.SetEnvironmentVariable("NEO_NIRS_PORT", request.PortName.Trim(), EnvironmentVariableTarget.Process);
        }
        else
        {
            Environment.SetEnvironmentVariable("NEO_NIRS_PORT", null, EnvironmentVariableTarget.Process);
        }
    }
}
