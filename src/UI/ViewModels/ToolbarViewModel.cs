// ToolbarViewModel.cs
// Sprint 2.1: ToolbarPanel ViewModel with clock, playback, screenshot, annotation commands.
//
// References:
// - UI_SPEC.md §4.1: Toolbar row (logo, seekbar, buttons, time, user, bed)
// - UI_SPEC.md §8: Status bar time display (1-second refresh)
// - UI_SPEC.md §10: Audit requirements
// - UI_SPEC.md §2.2: Monotonic Clock — UI does not introduce a new time base.
//   Wall-clock display is derived from Stopwatch (monotonic) + initial UTC anchor.

using System.Diagnostics;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Neo.UI.Services;

namespace Neo.UI.ViewModels;

/// <summary>
/// ViewModel for the top toolbar panel.
/// Manages clock refresh, playback state, and toolbar button commands with audit logging.
/// </summary>
public partial class ToolbarViewModel : ViewModelBase
{
    private readonly IAuditService _audit;
    private readonly DispatcherTimer? _clockTimer;

    // Monotonic clock anchor — UI_SPEC §2.2: no private DateTime time base.
    // Wall-clock display is derived from Stopwatch elapsed + initial UTC snapshot.
    private readonly long _startTicks = Stopwatch.GetTimestamp();
    private readonly DateTimeOffset _startUtc = DateTimeOffset.UtcNow;

    [ObservableProperty]
    private string _currentTime = "--:--:--";

    [ObservableProperty]
    private string _currentUser = "用户: --";

    [ObservableProperty]
    private string _bedNumber = "床位: --";

    [ObservableProperty]
    private bool _isPlaying;

    public ToolbarViewModel(IAuditService audit)
    {
        _audit = audit;

        // Start 1-second clock timer — UI_SPEC §8
        _clockTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(1)
        };
        _clockTimer.Tick += (_, _) => RefreshClock();
        _clockTimer.Start();

        // Set initial time immediately
        RefreshClock();
    }

    /// <summary>
    /// Stops the clock timer. Call on cleanup/shutdown.
    /// </summary>
    public void StopClock()
    {
        _clockTimer?.Stop();
    }

    [RelayCommand]
    private void Playback()
    {
        IsPlaying = !IsPlaying;
        if (IsPlaying)
        {
            _audit.Log(AuditEventTypes.MonitoringStart, "Playback started");
        }
        else
        {
            _audit.Log(AuditEventTypes.MonitoringStop, "Playback paused");
        }
    }

    [RelayCommand]
    private void Screenshot()
    {
        _audit.Log(AuditEventTypes.Screenshot, "Screenshot requested");
    }

    [RelayCommand]
    private void Annotation()
    {
        _audit.Log(AuditEventTypes.Annotation, "Annotation requested");
    }

    /// <summary>
    /// Derives wall-clock display from monotonic Stopwatch elapsed + UTC anchor.
    /// Complies with UI_SPEC §2.2: no DateTime.Now as independent time source.
    /// </summary>
    private void RefreshClock()
    {
        var elapsedTicks = Stopwatch.GetTimestamp() - _startTicks;
        var elapsed = TimeSpan.FromSeconds((double)elapsedTicks / Stopwatch.Frequency);
        var now = _startUtc + elapsed;
        CurrentTime = now.ToLocalTime().ToString("HH:mm:ss");
    }
}
