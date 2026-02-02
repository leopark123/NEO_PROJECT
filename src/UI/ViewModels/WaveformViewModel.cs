// WaveformViewModel.cs
// Sprint 2.4: ChannelControlPanel ViewModel with gain, lead, Y-axis, filter, sweep, aEEG params.
//
// References:
// - UI_SPEC.md §5.1: EEG display — channel combo, Y-axis range
// - UI_SPEC.md §5.2: aEEG display — time window
// - UI_SPEC.md §6.1: Gain control — 10/20/50/70/100/200/1000 μV/cm
// - UI_SPEC.md §6.2: Filter control — HPF/LPF/Notch
// - UI_SPEC.md §10: Audit — GAIN_CHANGE, FILTER_CHANGE
// - UI_SPEC.md §12: Parameter change response < 100ms

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Neo.UI.Services;

namespace Neo.UI.ViewModels;

/// <summary>
/// ViewModel for the right-side channel control / parameter panel.
/// Manages gain, lead selection, Y-axis, filter params, sweep speed, and aEEG time window.
/// </summary>
public partial class WaveformViewModel : ViewModelBase
{
    private readonly IAuditService _audit;

    // ── Lead selection ──
    [ObservableProperty]
    private string _leadCh1 = "CH1: C3-P3";

    [ObservableProperty]
    private string _leadCh2 = "CH2: C4-P4";

    // ── Gain — UI_SPEC §6.1 ──
    public static int[] GainOptions { get; } = [10, 20, 50, 70, 100, 200, 1000];

    [ObservableProperty]
    private int _selectedGain = 100;

    public string GainDisplay => $"{SelectedGain} μV/cm";

    // ── Y-axis range — UI_SPEC §5.1 ──
    public static int[] YAxisOptions { get; } = [25, 50, 100, 200];

    [ObservableProperty]
    private int _selectedYAxis = 100;

    public string YAxisDisplay => $"±{SelectedYAxis} μV";

    // ── Filter params — UI_SPEC §6.2 ──
    public static double[] HpfOptions { get; } = [0.3, 0.5, 1.5];
    public static int[] LpfOptions { get; } = [15, 35, 50, 70];
    public static int[] NotchOptions { get; } = [50, 60];

    [ObservableProperty]
    private double _selectedHpf = 0.5;

    [ObservableProperty]
    private int _selectedLpf = 35;

    [ObservableProperty]
    private int _selectedNotch = 50;

    public string HpfDisplay => $"HPF: {SelectedHpf} Hz";
    public string LpfDisplay => $"LPF: {SelectedLpf} Hz";
    public string NotchDisplay => $"Notch: {SelectedNotch} Hz";

    // ── Sweep speed — UI_SPEC §5.1 ──
    [ObservableProperty]
    private int _sweepSeconds = 15;

    public string SweepDisplay => $"{SweepSeconds} 秒/屏";

    // ── aEEG time window — UI_SPEC §5.2 ──
    public static int[] AeegTimeWindowOptions { get; } = [1, 3, 6, 12, 24];

    [ObservableProperty]
    private int _selectedAeegHours = 3;

    public string AeegTimeDisplay => $"{SelectedAeegHours} 小时";

    public WaveformViewModel(IAuditService audit)
    {
        _audit = audit;
    }

    /// <summary>
    /// Called when gain selection changes. Logs GAIN_CHANGE audit event.
    /// </summary>
    partial void OnSelectedGainChanged(int oldValue, int newValue)
    {
        OnPropertyChanged(nameof(GainDisplay));
        if (oldValue != 0) // Skip initial set
            _audit.Log(AuditEventTypes.GainChange, $"{oldValue} -> {newValue} μV/cm");
    }

    /// <summary>
    /// Called when Y-axis selection changes.
    /// </summary>
    partial void OnSelectedYAxisChanged(int value)
    {
        OnPropertyChanged(nameof(YAxisDisplay));
    }

    /// <summary>
    /// Called when HPF selection changes. Logs FILTER_CHANGE audit event.
    /// </summary>
    partial void OnSelectedHpfChanged(double oldValue, double newValue)
    {
        OnPropertyChanged(nameof(HpfDisplay));
        if (oldValue != 0) // Skip initial set
            _audit.Log(AuditEventTypes.FilterChange, $"HPF: {oldValue} -> {newValue} Hz");
    }

    /// <summary>
    /// Called when LPF selection changes. Logs FILTER_CHANGE audit event.
    /// </summary>
    partial void OnSelectedLpfChanged(int oldValue, int newValue)
    {
        OnPropertyChanged(nameof(LpfDisplay));
        if (oldValue != 0)
            _audit.Log(AuditEventTypes.FilterChange, $"LPF: {oldValue} -> {newValue} Hz");
    }

    /// <summary>
    /// Called when Notch selection changes. Logs FILTER_CHANGE audit event.
    /// </summary>
    partial void OnSelectedNotchChanged(int oldValue, int newValue)
    {
        OnPropertyChanged(nameof(NotchDisplay));
        if (oldValue != 0)
            _audit.Log(AuditEventTypes.FilterChange, $"Notch: {oldValue} -> {newValue} Hz");
    }

    partial void OnSweepSecondsChanged(int value)
    {
        OnPropertyChanged(nameof(SweepDisplay));
    }

    partial void OnSelectedAeegHoursChanged(int value)
    {
        OnPropertyChanged(nameof(AeegTimeDisplay));
    }
}
