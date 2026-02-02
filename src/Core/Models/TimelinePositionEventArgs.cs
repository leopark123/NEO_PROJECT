// TimelinePositionEventArgs.cs
// Timeline position change event arguments - ARCHITECTURE.md §9

namespace Neo.Core;

/// <summary>
/// Event arguments for timeline position changes.
/// </summary>
public sealed class TimelinePositionEventArgs : EventArgs
{
    /// <summary>Current position in microseconds (Host clock domain).</summary>
    public required long PositionUs { get; init; }

    /// <summary>New playback state.</summary>
    public required PlaybackState State { get; init; }
}
