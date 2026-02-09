using System.Collections.ObjectModel;
using System.IO.Ports;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Neo.UI.ViewModels;

public enum NirsChannelState
{
    Unknown,
    Percentage,
    Fault,
    Blocked
}

public partial class NirsChannelViewModel : ObservableObject
{
    private string _lastActiveStatus = "--%";

    public int ChannelIndex { get; }

    public string ChannelLabel => $"CH{ChannelIndex}";

    [ObservableProperty]
    private bool _isEnabled;

    [ObservableProperty]
    private string _statusText;

    public NirsChannelViewModel(int channelIndex, bool isEnabled)
    {
        ChannelIndex = channelIndex;
        _isEnabled = isEnabled;
        _statusText = isEnabled ? "--%" : "Blocked";
    }

    partial void OnIsEnabledChanged(bool value)
    {
        StatusText = value ? _lastActiveStatus : "Blocked";
    }

    public void SetState(NirsChannelState state, int? percentage = null)
    {
        string next = state switch
        {
            NirsChannelState.Unknown => "--%",
            NirsChannelState.Fault => "Fault",
            NirsChannelState.Blocked => "Blocked",
            NirsChannelState.Percentage => $"{Math.Clamp(percentage ?? 0, 0, 100)}%",
            _ => "--%"
        };

        _lastActiveStatus = next;
        if (IsEnabled)
        {
            StatusText = next;
        }
    }
}

public partial class NirsViewModel : ViewModelBase
{
    public static IReadOnlyList<string> SourceModeOptions { get; } = ["mock", "real"];

    public ObservableCollection<NirsChannelViewModel> Channels { get; } = [];
    public ObservableCollection<string> SerialPortOptions { get; } = [];

    [ObservableProperty]
    private string _panelStatus = "NIRS standby";

    [ObservableProperty]
    private string _selectedSourceMode = ResolveInitialSourceMode();

    [ObservableProperty]
    private string? _selectedSerialPort;

    public bool IsSerialPortEditable => string.Equals(SelectedSourceMode, "real", StringComparison.OrdinalIgnoreCase);

    public event Action<NirsSourceSwitchRequest>? SourceSwitchRequested;
    private readonly Func<IReadOnlyList<string>> _serialPortProvider;

    public NirsViewModel(Func<IReadOnlyList<string>>? serialPortProvider = null)
    {
        _serialPortProvider = serialPortProvider ?? GetSerialPortsFromSystem;

        for (int i = 1; i <= 6; i++)
        {
            Channels.Add(new NirsChannelViewModel(i, i <= 4));
        }

        RefreshSerialPorts();
    }

    public void SetChannelState(int channelIndex, NirsChannelState state, int? percentage = null)
    {
        if (channelIndex < 1 || channelIndex > Channels.Count)
        {
            throw new ArgumentOutOfRangeException(nameof(channelIndex));
        }

        Channels[channelIndex - 1].SetState(state, percentage);
    }

    partial void OnSelectedSourceModeChanged(string value)
    {
        OnPropertyChanged(nameof(IsSerialPortEditable));

        if (IsSerialPortEditable)
        {
            RefreshSerialPorts();
        }
    }

    [RelayCommand]
    private void ApplySourceSwitch()
    {
        string mode = string.IsNullOrWhiteSpace(SelectedSourceMode)
            ? "mock"
            : SelectedSourceMode.Trim().ToLowerInvariant();

        if (mode is not ("mock" or "real"))
        {
            PanelStatus = "NIRS source mode must be 'mock' or 'real'.";
            return;
        }

        string? port = string.IsNullOrWhiteSpace(SelectedSerialPort)
            ? null
            : SelectedSerialPort.Trim();

        if (mode == "real" && string.IsNullOrWhiteSpace(port))
        {
            PanelStatus = "Real mode requires selecting a detected COM port.";
            return;
        }

        SourceSwitchRequested?.Invoke(new NirsSourceSwitchRequest(mode, port));
    }

    [RelayCommand]
    private void RefreshSerialPorts()
    {
        string? current = string.IsNullOrWhiteSpace(SelectedSerialPort) ? null : SelectedSerialPort.Trim();
        string? preferred = ResolveInitialSerialPortName();

        var ports = _serialPortProvider()
            .Where(port => !string.IsNullOrWhiteSpace(port))
            .Select(port => port.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(port => port, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        SerialPortOptions.Clear();
        foreach (string port in ports)
        {
            SerialPortOptions.Add(port);
        }

        SelectedSerialPort = ResolveSelectedPort(current, preferred, ports);

        if (ports.Length == 0 && IsSerialPortEditable)
        {
            PanelStatus = "No COM ports detected. Connect USB-Serial adapter and click Refresh.";
        }
    }

    private static string ResolveInitialSourceMode()
    {
        string? raw = Environment.GetEnvironmentVariable("NEO_NIRS_MODE");
        if (string.IsNullOrWhiteSpace(raw))
        {
            return "mock";
        }

        string normalized = raw.Trim().ToLowerInvariant();
        return normalized is "mock" or "real" ? normalized : "mock";
    }

    private static string ResolveInitialSerialPortName()
    {
        string? raw = Environment.GetEnvironmentVariable("NEO_NIRS_PORT");
        return string.IsNullOrWhiteSpace(raw) ? string.Empty : raw.Trim();
    }

    private static string? ResolveSelectedPort(string? current, string? preferred, IReadOnlyList<string> ports)
    {
        if (!string.IsNullOrWhiteSpace(current) && ports.Contains(current, StringComparer.OrdinalIgnoreCase))
        {
            return ports.First(port => string.Equals(port, current, StringComparison.OrdinalIgnoreCase));
        }

        if (!string.IsNullOrWhiteSpace(preferred) && ports.Contains(preferred, StringComparer.OrdinalIgnoreCase))
        {
            return ports.First(port => string.Equals(port, preferred, StringComparison.OrdinalIgnoreCase));
        }

        return ports.Count > 0 ? ports[0] : null;
    }

    private static IReadOnlyList<string> GetSerialPortsFromSystem()
    {
        return SerialPort.GetPortNames();
    }
}

public readonly record struct NirsSourceSwitchRequest(string Mode, string? PortName);
