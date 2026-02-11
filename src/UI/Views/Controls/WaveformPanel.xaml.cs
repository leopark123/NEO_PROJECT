using System.ComponentModel;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Threading;
using Neo.Mock;
using Neo.UI.Rendering;
using Neo.UI.Services;
using Neo.UI.ViewModels;

namespace Neo.UI.Views.Controls;

public partial class WaveformPanel : UserControl
{
    private WaveformRenderHost? _renderHost;
    private MockEegSource? _mockEegSource;
    private MainWindowViewModel? _boundViewModel;
    private bool _deviceLost;
    private bool _isSeekDragging;
    private readonly DispatcherTimer _nirsUiTimer = new() { Interval = TimeSpan.FromMilliseconds(350) };
    private readonly double[] _nirsTrendValues = [68.0, 71.0, 66.0, 73.0];
    private readonly double[] _nirsPhaseOffsets = [0.0, 0.9, 1.7, 2.6];
    private double _nirsUiPhase;

    public WaveformRenderHost? RenderHost => _renderHost;

    public WaveformPanel()
    {
        InitializeComponent();
        _nirsUiTimer.Tick += OnNirsUiTimerTick;
        UpdateNirsUiPlaceholders();

        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
        DataContextChanged += OnDataContextChanged;
        SizeChanged += OnSizeChanged;
        RenderImage.MouseLeftButtonDown += OnMouseLeftButtonDown;
        RenderImage.MouseLeftButtonUp += OnMouseLeftButtonUp;
        RenderImage.MouseMove += OnMouseMove;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        StartNirsUiAnimation();

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

            if (TryGetRenderSurfaceSize(out int width, out int height, out double dpiX, out double dpiY))
            {
                _renderHost.Resize(width, height, dpiX, dpiY);
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
        StopNirsUiAnimation();
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

    private void OnNirsUiTimerTick(object? sender, EventArgs e)
    {
        _nirsUiPhase += 0.26;
        UpdateNirsUiPlaceholders();
    }

    private void StartNirsUiAnimation()
    {
        if (_nirsUiTimer.IsEnabled)
        {
            return;
        }

        UpdateNirsUiPlaceholders();
        _nirsUiTimer.Start();
    }

    private void StopNirsUiAnimation()
    {
        if (_nirsUiTimer.IsEnabled)
        {
            _nirsUiTimer.Stop();
        }
    }

    private void UpdateNirsUiPlaceholders()
    {
        RefreshNirsTrendSourceValues();
        UpdateNirsSaturationLabels();
        UpdateNirsTrendAreas();
    }

    private void RefreshNirsTrendSourceValues()
    {
        for (int i = 0; i < 4; i++)
        {
            if (!TryGetSourceRso2Percent(i + 1, out double value))
            {
                continue;
            }

            // Smoothly track right-side rSO2 values to avoid jumpy fill animation.
            _nirsTrendValues[i] = (_nirsTrendValues[i] * 0.65) + (value * 0.35);
        }
    }

    private void UpdateNirsSaturationLabels()
    {
        NirsSaturationCh1.Text = $"CH1  {GetSourceStatusTextOrDefault(1)}";
        NirsSaturationCh2.Text = $"CH2  {GetSourceStatusTextOrDefault(2)}";
        NirsSaturationCh3.Text = $"CH3  {GetSourceStatusTextOrDefault(3)}";
        NirsSaturationCh4.Text = $"CH4  {GetSourceStatusTextOrDefault(4)}";
    }

    private void UpdateNirsTrendAreas()
    {
        NirsTrendAreaCh1.Points = BuildNirsAreaPoints(0);
        NirsTrendAreaCh2.Points = BuildNirsAreaPoints(1);
        NirsTrendAreaCh3.Points = BuildNirsAreaPoints(2);
        NirsTrendAreaCh4.Points = BuildNirsAreaPoints(3);
    }

    private PointCollection BuildNirsAreaPoints(int channel)
    {
        const double width = 200.0;
        const double floorY = 18.0;
        const int steps = 24;

        double value = _nirsTrendValues[channel];
        double phase = _nirsUiPhase + _nirsPhaseOffsets[channel];
        double center = 10.0 - ((value - 72.0) * 0.05);
        double amp1 = 1.8 + (channel * 0.2);
        double amp2 = 0.9 + (channel * 0.1);

        var points = new PointCollection(steps + 3);
        for (int i = 0; i <= steps; i++)
        {
            double t = i / (double)steps;
            double x = width * t;
            double y = center
                - Math.Sin((t * 7.0) + phase) * amp1
                - Math.Sin((t * 17.0) + (phase * 0.63)) * amp2;
            y = Math.Clamp(y, 3.0, 16.5);
            points.Add(new Point(x, y));
        }

        points.Add(new Point(width, floorY));
        points.Add(new Point(0.0, floorY));
        return points;
    }

    private bool TryGetSourceRso2Percent(int channelIndex, out double value)
    {
        value = 0d;
        string status = GetSourceStatusTextOrDefault(channelIndex);
        if (status.EndsWith("%", StringComparison.Ordinal))
        {
            string numericPart = status[..^1];
            if (double.TryParse(numericPart, NumberStyles.Number, CultureInfo.InvariantCulture, out double parsed))
            {
                value = Math.Clamp(parsed, 0d, 100d);
                return true;
            }
        }

        return false;
    }

    private string GetSourceStatusTextOrDefault(int channelIndex)
    {
        var channels = _boundViewModel?.Nirs?.Channels;
        if (channels is null || channelIndex < 1 || channelIndex > channels.Count)
        {
            return "--%";
        }

        string status = channels[channelIndex - 1].StatusText;
        return string.IsNullOrWhiteSpace(status) ? "--%" : status.Trim();
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

        // Per-lane gain/range configuration (Commit 4)
        _renderHost.Lane0GainMicrovoltsPerCm = vm.Waveform.Eeg1Gain;
        _renderHost.Lane1GainMicrovoltsPerCm = vm.Waveform.Eeg2Gain;
        _renderHost.Lane0YAxisRangeUv = vm.Waveform.Eeg1Range;
        _renderHost.Lane1YAxisRangeUv = vm.Waveform.Eeg2Range;

        // Legacy global properties (kept for backward compatibility)
#pragma warning disable CS0618 // Using obsolete property for backward compatibility
        _renderHost.GainMicrovoltsPerCm = vm.Waveform.SelectedGain;
        _renderHost.YAxisRangeUv = vm.Waveform.SelectedYAxis;
#pragma warning restore CS0618
        _renderHost.AeegVisibleHours = vm.Waveform.SelectedAeegHours;

        // Per-lane channel mapping from Eeg1Source/Eeg2Source
        ApplyPerLaneChannelMapping(vm.Waveform.Eeg1Source, vm.Waveform.Eeg2Source);

        // Legacy lead combination mapping (deprecated, but keep for transition)
        ApplyLeadCombinationMapping(vm.Waveform.SelectedLeadCombination);

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
            // Legacy global properties (deprecated, but kept for transition)
            case nameof(WaveformViewModel.SelectedGain):
#pragma warning disable CS0618 // Using obsolete property for backward compatibility
                _renderHost.GainMicrovoltsPerCm = vm.SelectedGain;
#pragma warning restore CS0618
                break;
            case nameof(WaveformViewModel.SelectedYAxis):
#pragma warning disable CS0618
                _renderHost.YAxisRangeUv = vm.SelectedYAxis;
#pragma warning restore CS0618
                break;
            case nameof(WaveformViewModel.SelectedAeegHours):
                _renderHost.AeegVisibleHours = vm.SelectedAeegHours;
                break;
            case nameof(WaveformViewModel.SelectedLeadCombination):
                ApplyLeadCombinationMapping(vm.SelectedLeadCombination);
                break;

            // Per-lane source configuration (new model)
            case nameof(WaveformViewModel.Eeg1Source):
            case nameof(WaveformViewModel.Eeg2Source):
                ApplyPerLaneChannelMapping(vm.Eeg1Source, vm.Eeg2Source);
                LogChannelMapChange(vm.Eeg1Source, vm.Eeg2Source);
                break;

            // Per-lane gain/range (Commit 4: RenderHost now supports per-lane configuration)
            case nameof(WaveformViewModel.Eeg1Gain):
                _renderHost.Lane0GainMicrovoltsPerCm = vm.Eeg1Gain;
                break;
            case nameof(WaveformViewModel.Eeg2Gain):
                _renderHost.Lane1GainMicrovoltsPerCm = vm.Eeg2Gain;
                break;
            case nameof(WaveformViewModel.Eeg1Range):
                _renderHost.Lane0YAxisRangeUv = vm.Eeg1Range;
                break;
            case nameof(WaveformViewModel.Eeg2Range):
                _renderHost.Lane1YAxisRangeUv = vm.Eeg2Range;
                break;
        }
    }

    private void ApplyPerLaneChannelMapping(ChannelSourceOption? eeg1Source, ChannelSourceOption? eeg2Source)
    {
        if (_renderHost == null || eeg1Source == null || eeg2Source == null)
        {
            return;
        }

        // Protocol facts: CH1=C3-P3 (A-B), CH2=C4-P4 (C-D), CH4=C3-C4 (A-D, cross-channel/computed)
        // Physical channel mapping: 0→CH1, 1→CH2, 2→CH3, 3→CH4
        // Display lane 0 (top) → eeg1Source.PhysicalChannel
        // Display lane 1 (bottom) → eeg2Source.PhysicalChannel
        _renderHost.DataBridge.SetChannelMapping(eeg1Source.PhysicalChannel, eeg2Source.PhysicalChannel);
    }

    private void LogChannelMapChange(ChannelSourceOption? eeg1Source, ChannelSourceOption? eeg2Source)
    {
        if (_boundViewModel?.Audit == null || eeg1Source == null || eeg2Source == null)
        {
            return;
        }

        // Log CHANNEL_MAP_CHANGE audit event (required per task Commit 3)
        string details = $"EEG-1: {eeg1Source.Label}, EEG-2: {eeg2Source.Label}";
        _boundViewModel.Audit.Log(AuditEventTypes.ChannelMapChange, details);
    }

    private void ApplyLeadCombinationMapping(LeadCombinationOption? leadOption)
    {
        if (_renderHost == null || leadOption == null)
        {
            return;
        }

        // Hardware constraint (CONSENSUS_BASELINE.md §6.2):
        // Physical CH1=C3-P3 (index 0), CH2=C4-P4 (index 1), CH3=P3-P4 (index 2), CH4=C3-C4 (index 3)
        // Only C3-P3/C4-P4 combination is supported to prevent clinical mislabeling
        var (ch1Physical, ch2Physical) = leadOption.Label switch
        {
            "C3-P3 / C4-P4" => (0, 1),  // Maps to physical CH1/CH2
            _ => (0, 1)                  // Defensive fallback (should not occur after cleanup)
        };

        _renderHost.DataBridge.SetChannelMapping(ch1Physical, ch2Physical);
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
        ResizeRenderSurface();
    }

    private void OnMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (_renderHost is null)
        {
            return;
        }

        var point = e.GetPosition(RenderImage);
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

        var point = e.GetPosition(RenderImage);
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

            if (TryGetRenderSurfaceSize(out int width, out int height, out double dpiX, out double dpiY))
            {
                _renderHost.Resize(width, height, dpiX, dpiY);
                RenderImage.Source = _renderHost.ImageSource;
            }

            _renderHost.Start();
        }
        else
        {
            ErrorOverlay.MouseLeftButtonDown += OnRecoveryClick;
        }
    }

    private bool TryGetRenderSurfaceSize(out int width, out int height, out double dpiX, out double dpiY)
    {
        var dpi = VisualTreeHelper.GetDpi(RenderHostContainer);
        dpiX = dpi.PixelsPerInchX;
        dpiY = dpi.PixelsPerInchY;

        // Render into physical pixels to avoid post-scale blur on high-DPI displays.
        width = (int)Math.Round(RenderHostContainer.ActualWidth * dpi.DpiScaleX);
        height = (int)Math.Round(RenderHostContainer.ActualHeight * dpi.DpiScaleY);
        return width > 0 && height > 0;
    }

    private void ResizeRenderSurface()
    {
        if (_renderHost == null || _deviceLost)
        {
            return;
        }

        if (!TryGetRenderSurfaceSize(out int width, out int height, out double dpiX, out double dpiY))
        {
            return;
        }

        try
        {
            _renderHost.Resize(width, height, dpiX, dpiY);
            RenderImage.Source = _renderHost.ImageSource;
        }
        catch
        {
            ShowDeviceLostOverlay();
        }
    }
}
