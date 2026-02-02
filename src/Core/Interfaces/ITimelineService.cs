// ITimelineService.cs
// Unified timeline service interface - ARCHITECTURE.md §9
//
// Iron Law 11: Unified int64 us timeline for all data sources.

namespace Neo.Core;

/// <summary>
/// Unified timeline service for synchronized playback across all data sources.
/// </summary>
public interface ITimelineService
{
    /// <summary>Current playback position (microseconds, Host clock domain).</summary>
    long CurrentPositionUs { get; }

    /// <summary>Session start time (UTC).</summary>
    DateTimeOffset SessionStart { get; }

    /// <summary>Current playback state.</summary>
    PlaybackState State { get; }

    /// <summary>Playback speed (1.0 = real-time).</summary>
    double PlaybackRate { get; set; }

    /// <summary>Seek to the specified position (microseconds).</summary>
    void SeekTo(long positionUs);

    /// <summary>Start or resume playback.</summary>
    void Play();

    /// <summary>Pause playback at current position.</summary>
    void Pause();

    /// <summary>Position or state change event.</summary>
    event EventHandler<TimelinePositionEventArgs>? PositionChanged;
}
