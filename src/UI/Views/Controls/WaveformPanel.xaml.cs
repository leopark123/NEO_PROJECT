using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Neo.Mock;
using Neo.UI.Rendering;
using Neo.UI.ViewModels;

namespace Neo.UI.Views.Controls;

public partial class WaveformPanel : UserControl
{
    private WaveformRenderHost? _renderHost;
    private MockEegSource? _mockEegSource;
    private MainWindowViewModel? _boundViewModel;
    private bool _deviceLost;
    private bool _isSeekDragging;

    public WaveformRenderHost? RenderHost => _renderHost;

    public WaveformPanel()
    {
        InitializeComponent();

        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
        DataContextChanged += OnDataContextChanged;
        SizeChanged += OnSizeChanged;
        MouseLeftButtonDown += OnMouseLeftButtonDown;
        MouseLeftButtonUp += OnMouseLeftButtonUp;
        MouseMove += OnMouseMove;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (_renderHost != null)
        {
            BindViewModel(DataContext as MainWindowViewModel);
            return;
        }

        try
        {
            var vm = DataContext as MainWindowViewModel;
            _renderHost = new WaveformRenderHost(vm?.Audit, vm?.Waveform?.ThemeService);
            _renderHost.DataBridge.EnableClinicalMockShaping = true;

            int width = (int)ActualWidth;
            int height = (int)ActualHeight;
            if (width > 0 && height > 0)
            {
                _renderHost.Resize(width, height);
            }

            RenderImage.Source = _renderHost.ImageSource;

            var stopwatch = System.Diagnostics.Stopwatch.StartNew();
            _mockEegSource = new MockEegSource(() =>
                stopwatch.ElapsedTicks * 1_000_000 / System.Diagnostics.Stopwatch.Frequency);
            _renderHost.AttachDataSource(_mockEegSource);

            BindViewModel(vm);

            _renderHost.Start();

            LoadingOverlay.Visibility = Visibility.Collapsed;
            ErrorOverlay.Visibility = Visibility.Collapsed;
        }
        catch
        {
            ShowDeviceLostOverlay();
        }
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        UnbindViewModel();

        _mockEegSource?.Dispose();
        _mockEegSource = null;

        if (_renderHost is null)
        {
            return;
        }

        _renderHost.Stop();
        _renderHost.Dispose();
        _renderHost = null;
    }

    private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (IsLoaded)
        {
            BindViewModel(e.NewValue as MainWindowViewModel);
        }
    }

    private void BindViewModel(MainWindowViewModel? vm)
    {
        if (ReferenceEquals(_boundViewModel, vm))
        {
            return;
        }

        UnbindViewModel();
        _boundViewModel = vm;

        if (vm is null || _renderHost is null)
        {
            return;
        }

        _renderHost.GainMicrovoltsPerCm = vm.Waveform.SelectedGain;
        _renderHost.YAxisRangeUv = vm.Waveform.SelectedYAxis;
        _renderHost.AeegVisibleHours = vm.Waveform.SelectedAeegHours;
        _renderHost.ShowGsHistogram = vm.Waveform.ShowGsHistogram;

        vm.Waveform.PropertyChanged += OnWaveformPropertyChanged;
        vm.Toolbar.PropertyChanged += OnToolbarPropertyChanged;

        SyncPlaybackState(vm.Toolbar.IsPlaying);
    }

    private void UnbindViewModel()
    {
        if (_boundViewModel is null)
        {
            return;
        }

        _boundViewModel.Waveform.PropertyChanged -= OnWaveformPropertyChanged;
        _boundViewModel.Toolbar.PropertyChanged -= OnToolbarPropertyChanged;
        _boundViewModel = null;
    }

    private void OnWaveformPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (_renderHost == null || sender is not WaveformViewModel vm)
        {
            return;
        }

        switch (e.PropertyName)
        {
            case nameof(WaveformViewModel.SelectedGain):
                _renderHost.GainMicrovoltsPerCm = vm.SelectedGain;
                break;
            case nameof(WaveformViewModel.SelectedYAxis):
                _renderHost.YAxisRangeUv = vm.SelectedYAxis;
                break;
            case nameof(WaveformViewModel.SelectedAeegHours):
                _renderHost.AeegVisibleHours = vm.SelectedAeegHours;
                break;
            case nameof(WaveformViewModel.ShowGsHistogram):
                _renderHost.ShowGsHistogram = vm.ShowGsHistogram;
                break;
        }
    }

    private void OnToolbarPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (_renderHost == null || sender is not ToolbarViewModel vm)
        {
            return;
        }

        if (e.PropertyName == nameof(ToolbarViewModel.IsPlaying))
        {
            SyncPlaybackState(vm.IsPlaying);
        }
    }

    private void SyncPlaybackState(bool isPlaying)
    {
        if (_renderHost is null)
        {
            return;
        }

        if (isPlaying)
        {
            _renderHost.PlaybackClock.Start();
        }
        else
        {
            _renderHost.PlaybackClock.Pause();
        }
    }

    private void OnSizeChanged(object sender, SizeChangedEventArgs e)
    {
        if (_renderHost == null || _deviceLost)
        {
            return;
        }

        int width = (int)e.NewSize.Width;
        int height = (int)e.NewSize.Height;

        if (width <= 0 || height <= 0)
        {
            return;
        }

        try
        {
            _renderHost.Resize(width, height);
            RenderImage.Source = _renderHost.ImageSource;
        }
        catch
        {
            ShowDeviceLostOverlay();
        }
    }

    private void OnMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (_renderHost is null)
        {
            return;
        }

        var point = e.GetPosition(this);
        if (_renderHost.TrySetSeekFromPoint(point.X, point.Y))
        {
            _isSeekDragging = true;
            CaptureMouse();
            e.Handled = true;
        }
    }

    private void OnMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (!_isSeekDragging)
        {
            return;
        }

        _isSeekDragging = false;
        ReleaseMouseCapture();
        e.Handled = true;
    }

    private void OnMouseMove(object sender, MouseEventArgs e)
    {
        if (!_isSeekDragging || _renderHost == null)
        {
            return;
        }

        var point = e.GetPosition(this);
        _renderHost.TrySetSeekFromPoint(point.X, point.Y);
        e.Handled = true;
    }

    private void ShowDeviceLostOverlay()
    {
        _deviceLost = true;
        ErrorOverlay.Visibility = Visibility.Visible;
        ErrorOverlay.MouseLeftButtonDown += OnRecoveryClick;
    }

    private void OnRecoveryClick(object sender, MouseButtonEventArgs e)
    {
        ErrorOverlay.MouseLeftButtonDown -= OnRecoveryClick;

        if (_renderHost is null)
        {
            OnLoaded(this, new RoutedEventArgs());
            return;
        }

        if (_renderHost.TryRecoverDevice())
        {
            _deviceLost = false;
            ErrorOverlay.Visibility = Visibility.Collapsed;

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
            ErrorOverlay.MouseLeftButtonDown += OnRecoveryClick;
        }
    }
}
