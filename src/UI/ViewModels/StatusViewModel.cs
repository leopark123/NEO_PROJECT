// StatusViewModel.cs
// Sprint 2.3: StatusBarPanel ViewModel with FPS, storage, device states, time.
//
// References:
// - UI_SPEC.md §8: Status bar — FPS, storage, device connection, time
// - UI_SPEC.md §2.2: Monotonic Clock — wall-clock derived from Stopwatch + UTC anchor

using System.Diagnostics;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using Neo.UI.Services;

namespace Neo.UI.ViewModels;

/// <summary>
/// ViewModel for the bottom status bar panel.
/// Manages FPS display, storage usage, device connection indicators, and current time.
/// </summary>
public partial class StatusViewModel : ViewModelBase
{
    // Monotonic clock anchor — UI_SPEC §2.2
    private readonly long _startTicks = Stopwatch.GetTimestamp();
    private readonly DateTimeOffset _startUtc = DateTimeOffset.UtcNow;
    private readonly DispatcherTimer? _refreshTimer;

    [ObservableProperty]
    private string _fpsText = "FPS: --";

    [ObservableProperty]
    private string _storageText = "存储: -- / --";

    [ObservableProperty]
    private bool _eegConnected;

    [ObservableProperty]
    private bool _nirsConnected;

    [ObservableProperty]
    private bool _videoConnected;

    [ObservableProperty]
    private string _currentTime = "Time: --:--:--";

    public StatusViewModel()
    {
        // Start 1-second refresh timer — UI_SPEC §8
        _refreshTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(1)
        };
        _refreshTimer.Tick += (_, _) => RefreshTime();
        _refreshTimer.Start();

        // Set initial time immediately
        RefreshTime();
    }

    /// <summary>
    /// Stops the refresh timer. Call on cleanup/shutdown.
    /// </summary>
    public void StopTimer()
    {
        _refreshTimer?.Stop();
    }

    /// <summary>
    /// Update FPS display value. Called externally by render loop.
    /// </summary>
    public void UpdateFps(double fps)
    {
        FpsText = $"FPS: {fps:F0}";
    }

    /// <summary>
    /// Update storage display. Called externally by storage monitor.
    /// </summary>
    public void UpdateStorage(string usedGb, string totalGb)
    {
        StorageText = $"存储: {usedGb}/{totalGb}";
    }

    /// <summary>
    /// Derives wall-clock display from monotonic Stopwatch elapsed + UTC anchor.
    /// Complies with UI_SPEC §2.2: no DateTime.Now as independent time source.
    /// </summary>
    private void RefreshTime()
    {
        var elapsedTicks = Stopwatch.GetTimestamp() - _startTicks;
        var elapsed = TimeSpan.FromSeconds((double)elapsedTicks / Stopwatch.Frequency);
        var now = _startUtc + elapsed;
        CurrentTime = $"Time: {now.ToLocalTime():HH:mm:ss}";
    }
}
