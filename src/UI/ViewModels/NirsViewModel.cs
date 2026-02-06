using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;

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
    public ObservableCollection<NirsChannelViewModel> Channels { get; } = [];

    [ObservableProperty]
    private string _panelStatus = "NIRS standby";

    public NirsViewModel()
    {
        for (int i = 1; i <= 6; i++)
        {
            Channels.Add(new NirsChannelViewModel(i, i <= 3));
        }
    }

    public void SetChannelState(int channelIndex, NirsChannelState state, int? percentage = null)
    {
        if (channelIndex < 1 || channelIndex > Channels.Count)
        {
            throw new ArgumentOutOfRangeException(nameof(channelIndex));
        }

        Channels[channelIndex - 1].SetState(state, percentage);
    }
}
