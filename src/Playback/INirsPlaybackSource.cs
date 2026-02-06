// INirsPlaybackSource.cs
// Interface for NIRS playback source - Phase 3
//
// Iron Law 11: Unified int64 us timeline.

namespace Neo.Playback;

/// <summary>
/// Interface for NIRS (Near-Infrared Spectroscopy) playback source.
/// Provides synchronized NIRS data playback alongside EEG and Video.
/// </summary>
/// <remarks>
/// This is a structural integration point for future NIRS hardware/protocol implementation.
/// When NIRS data becomes available, implement this interface to plug into the
/// MultiStreamCoordinator without further architectural changes.
/// </remarks>
public interface INirsPlaybackSource : IDisposable
{
    /// <summary>
    /// Start NIRS data playback.
    /// </summary>
    void Start();

    /// <summary>
    /// Stop NIRS data playback.
    /// </summary>
    void Stop();

    /// <summary>
    /// Notify the source of a seek operation.
    /// </summary>
    /// <param name="positionUs">Target position in microseconds.</param>
    void NotifySeek(long positionUs);

    /// <summary>
    /// Whether the source is currently running.
    /// </summary>
    bool IsRunning { get; }
}
