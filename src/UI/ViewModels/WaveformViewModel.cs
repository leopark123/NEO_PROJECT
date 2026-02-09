using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Neo.UI.Services;

namespace Neo.UI.ViewModels;

public sealed record LeadCombinationOption(string Label, string Ch1, string Ch2);

/// <summary>
/// Represents a selectable EEG channel source option.
/// </summary>
/// <param name="Label">Display label (e.g., "CH1 (C3-P3)")</param>
/// <param name="PhysicalChannel">Physical channel index (0-3)</param>
public sealed record ChannelSourceOption(string Label, int PhysicalChannel);

public partial class WaveformViewModel : ViewModelBase
{
    private readonly IAuditService _audit;
    private readonly IThemeService _themeService;

    // Hardware-supported lead combinations only (CONSENSUS_BASELINE.md §6.2)
    // Hardware provides: CH1=C3-P3, CH2=C4-P4, CH3=P3-P4, CH4=C3-C4
    // Only C3-P3/C4-P4 combination is directly supported without remapping
    public static IReadOnlyList<LeadCombinationOption> LeadCombinationOptions { get; } =
    [
        new("C3-P3 / C4-P4", "C3-P3", "C4-P4")  // Maps to physical CH1/CH2
    ];

    /// <summary>
    /// Available channel source options for per-EEG lane configuration.
    /// Protocol facts: CH1=C3-P3 (A-B), CH2=C4-P4 (C-D), CH3=P3-P4 (B-C), CH4=C3-C4 (A-D, cross-channel/computed)
    /// </summary>
    public static IReadOnlyList<ChannelSourceOption> SourceOptions { get; } =
    [
        new("CH1 (C3-P3)", 0),              // Physical channel 0
        new("CH2 (C4-P4)", 1),              // Physical channel 1
        new("CH3 (P3-P4)", 2),              // Physical channel 2
        new("跨导联", 3)                   // Physical channel 3, computed/cross-channel
    ];

    public static int[] GainOptions { get; } = [10, 20, 50, 70, 100, 200, 1000];
    public static int[] YAxisOptions { get; } = [25, 50, 100, 200];
    public static double[] HpfOptions { get; } = [0.3, 0.5, 1.5];
    public static int[] LpfOptions { get; } = [15, 35, 50, 70];
    public static int[] NotchOptions { get; } = [50, 60];
    public static int[] AeegTimeWindowOptions { get; } = [1, 3, 6, 12, 24];

    [ObservableProperty]
    private LeadCombinationOption? _selectedLeadCombination;

    [ObservableProperty]
    private string _leadCh1 = "CH1: C3-P3";

    [ObservableProperty]
    private string _leadCh2 = "CH2: C4-P4";

    [ObservableProperty]
    private int _selectedGain = 100;

    [ObservableProperty]
    private int _selectedYAxis = 100;

    [ObservableProperty]
    private double _selectedHpf = 0.5;

    [ObservableProperty]
    private int _selectedLpf = 35;

    [ObservableProperty]
    private int _selectedNotch = 50;

    [ObservableProperty]
    private int _sweepSeconds = 15;

    [ObservableProperty]
    private int _selectedAeegHours = 3;

    [ObservableProperty]
    private bool _showGsHistogram = false; // Default: hidden

    // Per-EEG lane configuration (new model, replaces global SelectedGain/SelectedYAxis)
    // EEG-1 (top display lane)
    [ObservableProperty]
    private ChannelSourceOption? _eeg1Source;

    [ObservableProperty]
    private int _eeg1Gain = 100;  // μV/cm

    [ObservableProperty]
    private int _eeg1Range = 100; // ±μV

    // EEG-2 (bottom display lane)
    [ObservableProperty]
    private ChannelSourceOption? _eeg2Source;

    [ObservableProperty]
    private int _eeg2Gain = 100;  // μV/cm

    [ObservableProperty]
    private int _eeg2Range = 100; // ±μV

    public string GainDisplay => $"{SelectedGain} uV/cm";
    public string YAxisDisplay => $"+/-{SelectedYAxis} uV";
    public string HpfDisplay => $"HPF: {SelectedHpf:0.0} Hz";
    public string LpfDisplay => $"LPF: {SelectedLpf} Hz";
    public string NotchDisplay => $"Notch: {SelectedNotch} Hz";
    public string SweepDisplay => $"{SweepSeconds} s";
    public string AeegTimeDisplay => $"{SelectedAeegHours} h";

    public WaveformViewModel(IAuditService audit, IThemeService themeService)
    {
        _audit = audit;
        _themeService = themeService;
        SelectedLeadCombination = LeadCombinationOptions[0];

        // Initialize per-lane defaults
        // EEG-1: CH1 (C3-P3), gain=100, range=100
        // EEG-2: CH2 (C4-P4), gain=100, range=100
        Eeg1Source = SourceOptions[0];  // CH1
        Eeg2Source = SourceOptions[1];  // CH2
    }

    /// <summary>
    /// Gets the theme service for passing to WaveformRenderHost.
    /// </summary>
    public IThemeService ThemeService => _themeService;

    partial void OnSelectedLeadCombinationChanged(LeadCombinationOption? oldValue, LeadCombinationOption? newValue)
    {
        if (newValue is null)
        {
            return;
        }

        LeadCh1 = $"CH1: {newValue.Ch1}";
        LeadCh2 = $"CH2: {newValue.Ch2}";

        // Audit lead combination change (matches GainChange audit pattern)
        if (oldValue != null && oldValue.Label != newValue.Label)
        {
            _audit.Log(AuditEventTypes.LeadChange, $"{oldValue.Label} -> {newValue.Label}");
        }
    }

    partial void OnSelectedGainChanged(int oldValue, int newValue)
    {
        OnPropertyChanged(nameof(GainDisplay));
        if (oldValue != 0)
        {
            _audit.Log(AuditEventTypes.GainChange, $"{oldValue} -> {newValue} uV/cm");
        }
    }

    partial void OnSelectedYAxisChanged(int value)
    {
        OnPropertyChanged(nameof(YAxisDisplay));
    }

    partial void OnSelectedHpfChanged(double oldValue, double newValue)
    {
        OnPropertyChanged(nameof(HpfDisplay));
        if (oldValue != 0)
        {
            _audit.Log(AuditEventTypes.FilterChange, $"HPF: {oldValue} -> {newValue} Hz");
        }
    }

    partial void OnSelectedLpfChanged(int oldValue, int newValue)
    {
        OnPropertyChanged(nameof(LpfDisplay));
        if (oldValue != 0)
        {
            _audit.Log(AuditEventTypes.FilterChange, $"LPF: {oldValue} -> {newValue} Hz");
        }
    }

    partial void OnSelectedNotchChanged(int oldValue, int newValue)
    {
        OnPropertyChanged(nameof(NotchDisplay));
        if (oldValue != 0)
        {
            _audit.Log(AuditEventTypes.FilterChange, $"Notch: {oldValue} -> {newValue} Hz");
        }
    }

    partial void OnSweepSecondsChanged(int value)
    {
        OnPropertyChanged(nameof(SweepDisplay));
    }

    partial void OnSelectedAeegHoursChanged(int value)
    {
        OnPropertyChanged(nameof(AeegTimeDisplay));
    }

    [RelayCommand]
    private void CycleGain()
    {
        int idx = Array.IndexOf(GainOptions, SelectedGain);
        SelectedGain = GainOptions[(idx + 1) % GainOptions.Length];
    }

    [RelayCommand]
    private void CycleYAxis()
    {
        int idx = Array.IndexOf(YAxisOptions, SelectedYAxis);
        SelectedYAxis = YAxisOptions[(idx + 1) % YAxisOptions.Length];
    }

    [RelayCommand]
    private void CycleAeegTimeWindow()
    {
        int idx = Array.IndexOf(AeegTimeWindowOptions, SelectedAeegHours);
        SelectedAeegHours = AeegTimeWindowOptions[(idx + 1) % AeegTimeWindowOptions.Length];
    }

    [RelayCommand]
    private void CycleLeadCombination()
    {
        if (SelectedLeadCombination is null)
        {
            SelectedLeadCombination = LeadCombinationOptions[0];
            return;
        }

        int index = 0;
        for (int i = 0; i < LeadCombinationOptions.Count; i++)
        {
            if (LeadCombinationOptions[i].Label == SelectedLeadCombination.Label)
            {
                index = i;
                break;
            }
        }

        SelectedLeadCombination = LeadCombinationOptions[(index + 1) % LeadCombinationOptions.Count];
    }
}
