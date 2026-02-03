// WaveformPanel.xaml.cs
// Sprint 3.1: D3D11 → D2D1 → WriteableBitmap rendering host
//
// Source: NEO_UI_Development_Plan_WPF.md §8
// CHARTER: R-01 render callback O(1)
//
// This UserControl:
// 1. Creates WaveformRenderHost on Loaded
// 2. Binds Image.Source to RenderHost.ImageSource
// 3. Handles SizeChanged to resize render target
// 4. Handles device lost with recovery UI

using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Neo.Mock;
using Neo.UI.Rendering;

namespace Neo.UI.Views.Controls;

/// <summary>
/// Hosts the D3D11 → D2D1 → WPF rendering pipeline for waveform display.
/// </summary>
public partial class WaveformPanel : UserControl
{
    private WaveformRenderHost? _renderHost;
    private MockEegSource? _mockEegSource;
    private bool _deviceLost;

#if DEBUG
    /// <summary>
    /// DEBUG ONLY: Simulates device lost for testing recovery UI.
    /// Call via Immediate Window: ((Neo.UI.Views.Controls.WaveformPanel)Application.Current.MainWindow.FindName("WaveformRenderPanel")).SimulateDeviceLost()
    /// Or press Ctrl+Shift+D when panel is focused.
    /// </summary>
    public void SimulateDeviceLost()
    {
        Debug.WriteLine("WaveformPanel: Simulating device lost...");
        _renderHost?.Stop();
        ShowDeviceLostOverlay();
    }
#endif

    /// <summary>
    /// Gets the render host for external configuration.
    /// </summary>
    public WaveformRenderHost? RenderHost => _renderHost;

    public WaveformPanel()
    {
        InitializeComponent();

        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
        SizeChanged += OnSizeChanged;

#if DEBUG
        // Debug: Double-click to simulate device lost
        MouseDoubleClick += (s, e) =>
        {
            if (e.ChangedButton == MouseButton.Left)
            {
                SimulateDeviceLost();
                e.Handled = true;
            }
        };
#endif
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (_renderHost != null) return;

        try
        {
            // Create render host
            _renderHost = new WaveformRenderHost();

            // Initial resize
            int width = (int)ActualWidth;
            int height = (int)ActualHeight;
            if (width > 0 && height > 0)
            {
                _renderHost.Resize(width, height);
            }

            // Bind ImageSource
            RenderImage.Source = _renderHost.ImageSource;

            // Sprint 3.2: Create and attach MockEegSource for simulated data
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();
            _mockEegSource = new MockEegSource(() => stopwatch.ElapsedTicks * 1_000_000 / System.Diagnostics.Stopwatch.Frequency);
            _renderHost.AttachDataSource(_mockEegSource);

            // Start rendering
            _renderHost.Start();

            // Hide loading overlay
            LoadingOverlay.Visibility = Visibility.Collapsed;
            ErrorOverlay.Visibility = Visibility.Collapsed;
        }
        catch (Exception ex)
        {
            // Show error overlay
            System.Diagnostics.Debug.WriteLine($"WaveformPanel initialization failed: {ex.Message}");
            ShowDeviceLostOverlay();
        }
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        // Dispose MockEegSource first
        if (_mockEegSource != null)
        {
            _mockEegSource.Dispose();
            _mockEegSource = null;
        }

        if (_renderHost != null)
        {
            _renderHost.Stop();
            _renderHost.Dispose();
            _renderHost = null;
        }
    }

    private void OnSizeChanged(object sender, SizeChangedEventArgs e)
    {
        if (_renderHost == null || _deviceLost) return;

        int width = (int)e.NewSize.Width;
        int height = (int)e.NewSize.Height;

        if (width > 0 && height > 0)
        {
            try
            {
                _renderHost.Resize(width, height);
                // Update ImageSource binding (may be new WriteableBitmap)
                RenderImage.Source = _renderHost.ImageSource;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"WaveformPanel resize failed: {ex.Message}");
                ShowDeviceLostOverlay();
            }
        }
    }

    /// <summary>
    /// Shows the device lost overlay and enables recovery click.
    /// </summary>
    private void ShowDeviceLostOverlay()
    {
        _deviceLost = true;
        ErrorOverlay.Visibility = Visibility.Visible;
        ErrorOverlay.MouseLeftButtonDown += OnRecoveryClick;
    }

    /// <summary>
    /// Attempts device recovery when user clicks the error overlay.
    /// </summary>
    private void OnRecoveryClick(object sender, MouseButtonEventArgs e)
    {
        ErrorOverlay.MouseLeftButtonDown -= OnRecoveryClick;

        if (_renderHost == null)
        {
            // Reinitialize from scratch
            _renderHost?.Dispose();
            _renderHost = null;
            OnLoaded(this, new RoutedEventArgs());
        }
        else if (_renderHost.TryRecoverDevice())
        {
            // Recovery successful
            _deviceLost = false;
            ErrorOverlay.Visibility = Visibility.Collapsed;

            // Resize and restart
            int width = (int)ActualWidth;
            int height = (int)ActualHeight;
            if (width > 0 && height > 0)
            {
                _renderHost.Resize(width, height);
                RenderImage.Source = _renderHost.ImageSource;
            }
            _renderHost.Start();
        }
        else
        {
            // Recovery failed, keep overlay visible
            ErrorOverlay.MouseLeftButtonDown += OnRecoveryClick;
        }
    }
}
