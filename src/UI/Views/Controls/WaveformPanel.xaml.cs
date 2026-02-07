using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
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

        // Legacy global properties (will be deprecated in Commit 4)
        _renderHost.GainMicrovoltsPerCm = vm.Waveform.SelectedGain;
        _renderHost.YAxisRangeUv = vm.Waveform.SelectedYAxis;
        _renderHost.AeegVisibleHours = vm.Waveform.SelectedAeegHours;
        _renderHost.ShowGsHistogram = vm.Waveform.ShowGsHistogram;

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
            case nameof(WaveformViewModel.SelectedLeadCombination):
                ApplyLeadCombinationMapping(vm.SelectedLeadCombination);
                break;

            // Per-lane source configuration (new model)
            case nameof(WaveformViewModel.Eeg1Source):
            case nameof(WaveformViewModel.Eeg2Source):
                ApplyPerLaneChannelMapping(vm.Eeg1Source, vm.Eeg2Source);
                LogChannelMapChange(vm.Eeg1Source, vm.Eeg2Source);
                break;

            // Per-lane gain/range (temporarily set global properties until Commit 4)
            case nameof(WaveformViewModel.Eeg1Gain):
            case nameof(WaveformViewModel.Eeg2Gain):
                // TODO (Commit 4): Set per-lane gain once RenderHost supports it
                // For now, use EEG-1 gain as the global fallback
                _renderHost.GainMicrovoltsPerCm = vm.Eeg1Gain;
                break;
            case nameof(WaveformViewModel.Eeg1Range):
            case nameof(WaveformViewModel.Eeg2Range):
                // TODO (Commit 4): Set per-lane range once RenderHost supports it
                // For now, use EEG-1 range as the global fallback
                _renderHost.YAxisRangeUv = vm.Eeg1Range;
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
