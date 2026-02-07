// MultiStreamCoordinator.cs
// Multi-stream synchronized playback coordinator - S3-03
//
// Iron Law 2: No waveform fabrication.
// Iron Law 11: Unified int64 us timeline.
// AT-17: Video + EEG sync, seek tolerance < 100ms.

using System.Diagnostics;
using Neo.Core;
using Neo.Core.Models;
using Neo.Video;

namespace Neo.Playback;

/// <summary>
/// Coordinates synchronized playback of EEG and Video streams.
/// Implements <see cref="ITimelineService"/> for unified timeline control.
/// </summary>
/// <remarks>
/// Thread model:
/// - Tick thread: dedicated background thread, drives position updates.
/// - SeekTo/Play/Pause: called from any thread, thread-safe via lock.
/// - PositionChanged events fire on the tick thread.
///
/// Sync strategy:
/// - PlaybackClock provides the master virtual time.
/// - EegPlaybackSource reads from EegRingBuffer at clock rate.
/// - VideoPlaybackSource seeks to matching timestamp.
/// - Sync tolerance: ±100ms (AT-17).
/// </remarks>
public sealed class MultiStreamCoordinator : ITimelineService, IDisposable
{
    private readonly PlaybackClock _clock;
    private readonly EegPlaybackSource? _eegSource;
    private readonly VideoPlaybackSource? _videoSource;
    private readonly INirsPlaybackSource? _nirsSource;
    private readonly object _lock = new();

    private Thread? _tickThread;
    private volatile bool _stopRequested;
    private volatile PlaybackState _state = PlaybackState.Paused;
    private DateTimeOffset _sessionStart = DateTimeOffset.UtcNow;
    private bool _disposed;

    private const int TickIntervalMs = 50;         // 20 Hz position updates
    private const long SyncToleranceUs = 100_000;  // 100ms (AT-17)

    // Sync monitoring
    private long _lastEegTimestampUs;
    private long _syncViolationCount;
    private long _syncCheckCount;

    public long CurrentPositionUs => _clock.GetCurrentUs();
    public DateTimeOffset SessionStart => _sessionStart;
    public PlaybackState State => _state;

    public double PlaybackRate
    {
        get => _clock.Rate;
        set => _clock.Rate = value;
    }

    public event EventHandler<TimelinePositionEventArgs>? PositionChanged;

    /// <summary>
    /// Create a multi-stream coordinator.
    /// </summary>
    /// <param name="clock">Shared playback clock.</param>
    /// <param name="eegSource">EEG playback source (optional).</param>
    /// <param name="videoSource">Video playback source (optional).</param>
    /// <param name="nirsSource">NIRS playback source (optional).</param>
    public MultiStreamCoordinator(
        PlaybackClock clock,
        EegPlaybackSource? eegSource = null,
        VideoPlaybackSource? videoSource = null,
        INirsPlaybackSource? nirsSource = null)
    {
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
        _eegSource = eegSource;
        _videoSource = videoSource;
        _nirsSource = nirsSource;

        // Subscribe to EEG samples for sync drift monitoring
        if (_eegSource != null)
        {
            _eegSource.SampleReceived += OnEegSampleForSyncMonitor;
        }
    }

    /// <summary>
    /// Set the session start time for UTC mapping.
    /// </summary>
    public void SetSessionStart(DateTimeOffset sessionStart)
    {
        _sessionStart = sessionStart;
    }

    /// <summary>
    /// Start or resume playback.
    /// </summary>
    public void Play()
    {
        lock (_lock)
        {
            if (_state == PlaybackState.Playing) return;

            _clock.Start();
            _eegSource?.Start();
            _videoSource?.Start();
            _nirsSource?.Start();

            _state = PlaybackState.Playing;
            StartTickThread();

            Trace.TraceInformation("[MultiStreamCoordinator] Play at position {0} us",
                _clock.GetCurrentUs());
        }

        RaisePositionChanged();
    }

    /// <summary>
    /// Pause playback at the current position.
    /// </summary>
    public void Pause()
    {
        lock (_lock)
        {
            if (_state == PlaybackState.Paused) return;

            _clock.Pause();
            _eegSource?.Stop();
            _nirsSource?.Stop();
            // Video: keep current frame visible, don't stop

            _state = PlaybackState.Paused;

            Trace.TraceInformation("[MultiStreamCoordinator] Paused at position {0} us",
                _clock.GetCurrentUs());
        }

        RaisePositionChanged();
    }

    /// <summary>
    /// Seek all streams to the specified position.
    /// </summary>
    /// <param name="positionUs">Target position in microseconds (Host clock domain).</param>
    public void SeekTo(long positionUs)
    {
        PlaybackState previousState;

        lock (_lock)
        {
            previousState = _state;
            _state = PlaybackState.Seeking;
        }

        RaisePositionChanged();

        try
        {
            // 1. Seek the clock
            _clock.SeekTo(positionUs);

            // 2. Seek EEG playback
            _eegSource?.NotifySeek(positionUs);

            // 3. Seek video playback
            if (_videoSource != null)
            {
                bool seeked = _videoSource.SeekToTimestamp(positionUs);
                if (!seeked)
                {
                    Trace.TraceWarning(
                        "[MultiStreamCoordinator] Video seek failed for position {0} us",
                        positionUs);
                }
            }

            // 4. Seek NIRS playback
            _nirsSource?.NotifySeek(positionUs);

            Trace.TraceInformation("[MultiStreamCoordinator] Seeked to {0} us", positionUs);
        }
        catch (Exception ex)
        {
            Trace.TraceError("[MultiStreamCoordinator] Seek error: {0}", ex.Message);
        }

        lock (_lock)
        {
            // Return to previous state (or Paused if was Seeking)
            _state = previousState == PlaybackState.Playing
                ? PlaybackState.Playing
                : PlaybackState.Paused;
        }

        RaisePositionChanged();
    }

    /// <summary>
    /// Stop all streams and reset.
    /// </summary>
    public void Stop()
    {
        lock (_lock)
        {
            StopTickThread();

            _clock.Pause();
            _eegSource?.Stop();
            _videoSource?.Stop();
            _nirsSource?.Stop();

            _state = PlaybackState.Paused;

            Trace.TraceInformation("[MultiStreamCoordinator] Stopped.");
        }

        RaisePositionChanged();
    }

    private void StartTickThread()
    {
        if (_tickThread != null) return;

        _stopRequested = false;
        _tickThread = new Thread(TickLoop)
        {
            Name = "PlaybackTick",
            IsBackground = true
        };
        _tickThread.Start();
    }

    private void StopTickThread()
    {
        _stopRequested = true;
        _tickThread?.Join(timeout: TimeSpan.FromSeconds(2));
        _tickThread = null;
    }

    private void TickLoop()
    {
        try
        {
            while (!_stopRequested)
            {
                if (_state == PlaybackState.Playing)
                {
                    CheckSyncDrift();
                    RaisePositionChanged();
                }

                Thread.Sleep(TickIntervalMs);
            }
        }
        catch (Exception ex)
        {
            if (!_stopRequested)
            {
                Trace.TraceError("[MultiStreamCoordinator] Tick loop error: {0}", ex.Message);
            }
        }
    }

    /// <summary>
    /// Called on each EEG sample to track the latest emitted timestamp.
    /// </summary>
    private void OnEegSampleForSyncMonitor(EegSample sample)
    {
        Interlocked.Exchange(ref _lastEegTimestampUs, sample.TimestampUs);
    }

    /// <summary>
    /// Check sync drift between the playback clock and the most recent EEG sample.
    /// Logs a warning when drift exceeds SyncToleranceUs (AT-17: ±100ms).
    /// </summary>
    private void CheckSyncDrift()
    {
        long eegTs = Interlocked.Read(ref _lastEegTimestampUs);
        if (eegTs == 0) return; // No EEG samples yet

        long clockUs = _clock.GetCurrentUs();
        long driftUs = Math.Abs(clockUs - eegTs);

        Interlocked.Increment(ref _syncCheckCount);

        if (driftUs > SyncToleranceUs)
        {
            long violations = Interlocked.Increment(ref _syncViolationCount);
            Trace.TraceWarning(
                "[MultiStreamCoordinator] SYNC DRIFT: clock={0} us, eeg={1} us, drift={2} us ({3:F1} ms) " +
                "[violations={4}, checks={5}]",
                clockUs, eegTs, driftUs, driftUs / 1000.0,
                violations, Interlocked.Read(ref _syncCheckCount));
        }
    }

    private void RaisePositionChanged()
    {
        PositionChanged?.Invoke(this, new TimelinePositionEventArgs
        {
            PositionUs = _clock.GetCurrentUs(),
            State = _state
        });
    }

    /// <summary>
    /// Get the total number of sync tolerance violations detected.
    /// </summary>
    public long SyncViolationCount => Interlocked.Read(ref _syncViolationCount);

    /// <summary>
    /// Get the total number of sync checks performed.
    /// </summary>
    public long SyncCheckCount => Interlocked.Read(ref _syncCheckCount);

    public void Dispose()
    {
        if (_disposed) return;

        Stop();

        if (_eegSource != null)
        {
            _eegSource.SampleReceived -= OnEegSampleForSyncMonitor;
        }

        _eegSource?.Dispose();
        _nirsSource?.Dispose();
        // VideoPlaybackSource lifecycle managed externally

        _disposed = true;
    }
}
