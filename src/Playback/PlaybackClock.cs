// PlaybackClock.cs
// Pausable, seekable virtual clock for synchronized playback - S3-03
//
// Iron Law 11: Unified int64 us timeline.
// Iron Law 13: All records must have timestamps.

using System.Diagnostics;

namespace Neo.Playback;

/// <summary>
/// Virtual clock for playback mode. Supports pause, resume, seek, and rate control.
/// </summary>
/// <remarks>
/// Thread safety: All public methods are thread-safe via lock.
///
/// Clock behavior:
/// - Advances in real-time (scaled by PlaybackRate) while running.
/// - Pauses freeze the position.
/// - SeekTo sets an absolute position.
/// - GetCurrentUs returns the current virtual time in microseconds.
///
/// Time domain: Host monotonic clock (microseconds), same as live capture.
/// </remarks>
public sealed class PlaybackClock
{
    private readonly Stopwatch _wallClock = new();
    private readonly object _lock = new();

    private long _basePositionUs;      // Position at last anchor point
    private long _anchorWallTicks;     // Wall clock ticks at last anchor
    private double _rate = 1.0;
    private bool _running;

    /// <summary>
    /// Whether the clock is currently running.
    /// </summary>
    public bool IsRunning
    {
        get { lock (_lock) return _running; }
    }

    /// <summary>
    /// Playback rate (1.0 = real-time, 0.5 = half speed, 2.0 = double speed).
    /// </summary>
    public double Rate
    {
        get { lock (_lock) return _rate; }
        set
        {
            lock (_lock)
            {
                if (_running)
                    Anchor();
                _rate = value;
            }
        }
    }

    /// <summary>
    /// Get the current virtual time in microseconds.
    /// </summary>
    public long GetCurrentUs()
    {
        lock (_lock)
        {
            if (!_running)
                return _basePositionUs;

            long wallElapsedTicks = _wallClock.ElapsedTicks - _anchorWallTicks;
            long wallElapsedUs = wallElapsedTicks * 1_000_000 / Stopwatch.Frequency;
            return _basePositionUs + (long)(wallElapsedUs * _rate);
        }
    }

    /// <summary>
    /// Start or resume the clock.
    /// </summary>
    public void Start()
    {
        lock (_lock)
        {
            if (_running) return;

            if (!_wallClock.IsRunning)
                _wallClock.Start();

            _anchorWallTicks = _wallClock.ElapsedTicks;
            _running = true;
        }
    }

    /// <summary>
    /// Pause the clock at the current position.
    /// </summary>
    public void Pause()
    {
        lock (_lock)
        {
            if (!_running) return;
            Anchor();
            _running = false;
        }
    }

    /// <summary>
    /// Seek to an absolute position (microseconds).
    /// </summary>
    public void SeekTo(long positionUs)
    {
        lock (_lock)
        {
            _basePositionUs = positionUs;
            if (_running)
                _anchorWallTicks = _wallClock.ElapsedTicks;
        }
    }

    /// <summary>
    /// Reset the clock to position 0, stopped.
    /// </summary>
    public void Reset()
    {
        lock (_lock)
        {
            _basePositionUs = 0;
            _anchorWallTicks = 0;
            _running = false;
            _wallClock.Reset();
        }
    }

    /// <summary>
    /// Snapshot the current elapsed time into _basePositionUs.
    /// Must be called under lock.
    /// </summary>
    private void Anchor()
    {
        long wallElapsedTicks = _wallClock.ElapsedTicks - _anchorWallTicks;
        long wallElapsedUs = wallElapsedTicks * 1_000_000 / Stopwatch.Frequency;
        _basePositionUs += (long)(wallElapsedUs * _rate);
        _anchorWallTicks = _wallClock.ElapsedTicks;
    }
}
