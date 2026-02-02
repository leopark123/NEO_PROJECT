// PlaybackState.cs
// Playback state enumeration - ARCHITECTURE.md §9

namespace Neo.Core;

/// <summary>
/// Playback state for the timeline service.
/// </summary>
public enum PlaybackState
{
    /// <summary>Real-time monitoring (live data from devices).</summary>
    Live = 0,

    /// <summary>Playing back recorded data.</summary>
    Playing = 1,

    /// <summary>Playback paused at current position.</summary>
    Paused = 2,

    /// <summary>Seeking to a new position.</summary>
    Seeking = 3
}
