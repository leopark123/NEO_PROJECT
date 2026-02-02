// App.xaml.cs
// Sprint 1.2-fix: DI bootstrap with route registration and service injection.
//
// References:
// - UI_SPEC.md §3: MVVM architecture
// - UI_SPEC.md §4.1: Navigation bar routes

using System.Windows;
using Neo.UI.Services;
using Neo.UI.ViewModels;

namespace Neo.UI;

public partial class App : Application
{
    private ServiceRegistry? _services;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // Bootstrap services
        _services = new ServiceRegistry();

        // Register navigation routes — UI_SPEC §4.1
        _services.Routes.Register("Home", () => new HomeViewModel());
        _services.Routes.Register("History", () => new HomeViewModel());  // placeholder until Phase 3+
        _services.Routes.Register("Export", () => new HomeViewModel());   // placeholder until Phase 3+

        // Create ViewModels with audit service — Sprint 2.1 / 2.3 / 2.4
        var toolbar = new ToolbarViewModel(_services.Audit);
        var status = new StatusViewModel();
        var waveform = new WaveformViewModel(_services.Audit);

        // Create MainWindow with injected services
        var viewModel = new MainWindowViewModel(
            _services.Navigation,
            _services.Audit,
            _services.Dialog,
            toolbar,
            status,
            waveform);

        var mainWindow = new MainWindow
        {
            DataContext = viewModel
        };

        MainWindow = mainWindow;
        mainWindow.Show();
    }
}
