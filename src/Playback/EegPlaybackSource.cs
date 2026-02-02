// EegPlaybackSource.cs
// EEG playback adapter - reads from EegRingBuffer and re-emits samples at playback rate.
// S3-03 Video + EEG Synchronized Playback
//
// Iron Law 2: No waveform fabrication.
// Iron Law 11: Unified int64 us timeline.

using System.Diagnostics;
using Neo.Core;
using Neo.Core.Enums;
using Neo.Core.Interfaces;
using Neo.Core.Models;
using Neo.DSP.Filters;
using Neo.Infrastructure.Buffers;

namespace Neo.Playback;

/// <summary>
/// EEG playback source. Reads historical samples from an <see cref="EegRingBuffer"/>
/// and re-emits them via <see cref="SampleReceived"/> at the playback clock rate.
/// </summary>
/// <remarks>
/// Thread model:
/// - Playback thread: dedicated background thread, reads buffer and fires events.
/// - SampleReceived events fire on the playback thread.
///
/// Iron Law compliance:
/// - All timestamps come from the original EegRingBuffer data (Host clock domain).
/// - No samples are fabricated; gaps in the buffer result in no events for that range.
/// </remarks>
public sealed class EegPlaybackSource : ITimeSeriesSource<EegSample>, IDisposable
{
    private readonly EegRingBuffer _buffer;
    private readonly PlaybackClock _clock;
    private readonly EegFilterChain? _zeroPhaseFilter;
    private readonly object _lock = new();

    private Thread? _playbackThread;
    private volatile bool _stopRequested;
    private volatile bool _playing;
    private bool _disposed;

    // AT-19: Pre-filtered buffer for zero-phase playback
    private EegRingBuffer? _filteredBuffer;

    // Playback state
    private long _lastEmittedTimestampUs = long.MinValue;

    private const int SampleRateHz = 160;
    private const int ChannelCountValue = 4;
    private const int TickIntervalMs = 6; // ~160Hz emission rate
    private const long GapThresholdUs = 25_000; // 25ms = 4 samples @ 160Hz (DSP_SPEC §6.1)

    public int SampleRate => SampleRateHz;
    public int ChannelCount => ChannelCountValue;
    public event Action<EegSample>? SampleReceived;

    /// <summary>
    /// Create an EEG playback source.
    /// </summary>
    /// <param name="buffer">Ring buffer containing recorded EEG data.</param>
    /// <param name="clock">Shared playback clock for synchronization.</param>
    /// <param name="zeroPhaseFilter">Optional filter chain for zero-phase filtering (AT-19).
    /// When provided, each contiguous batch is filtered via ProcessBlockZeroPhase before emission.</param>
    public EegPlaybackSource(EegRingBuffer buffer, PlaybackClock clock, EegFilterChain? zeroPhaseFilter = null)
    {
        _buffer = buffer ?? throw new ArgumentNullException(nameof(buffer));
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
        _zeroPhaseFilter = zeroPhaseFilter;
    }

    /// <summary>
    /// Start emitting samples from the buffer at the playback clock rate.
    /// </summary>
    public void Start()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (_playing) return;

        if (_buffer.IsEmpty)
        {
            Trace.TraceWarning("[EegPlaybackSource] Buffer is empty, cannot start playback.");
            return;
        }

        // AT-19: Pre-filter the entire buffer with zero-phase if filter is provided.
        // Zero-phase (filtfilt) requires the full signal to eliminate phase delay,
        // so we filter all available data upfront rather than per-tick.
        if (_zeroPhaseFilter != null)
        {
            BuildFilteredBuffer();
        }

        _stopRequested = false;
        _playing = true;
        _lastEmittedTimestampUs = _clock.GetCurrentUs();

        _playbackThread = new Thread(PlaybackLoop)
        {
            Name = "EegPlayback",
            IsBackground = true
        };
        _playbackThread.Start();

        Trace.TraceInformation("[EegPlaybackSource] Playback started{0}.",
            _filteredBuffer != null ? " (zero-phase filtered)" : "");
    }

    /// <summary>
    /// Stop emitting samples.
    /// </summary>
    public void Stop()
    {
        if (!_playing) return;

        _stopRequested = true;
        _playbackThread?.Join(timeout: TimeSpan.FromSeconds(2));
        _playbackThread = null;
        _playing = false;

        Trace.TraceInformation("[EegPlaybackSource] Playback stopped.");
    }

    /// <summary>
    /// Notify that a seek occurred; reset emission state.
    /// </summary>
    public void NotifySeek(long newPositionUs)
    {
        lock (_lock)
        {
            _lastEmittedTimestampUs = newPositionUs;
        }
    }

    private void PlaybackLoop()
    {
        // Pre-allocate scratch buffer for range queries
        var scratch = new EegSample[SampleRateHz]; // 1 second max per tick

        try
        {
            while (!_stopRequested)
            {
                if (!_clock.IsRunning)
                {
                    Thread.Sleep(TickIntervalMs);
                    continue;
                }

                long currentUs = _clock.GetCurrentUs();
                long lastEmitted;

                lock (_lock)
                {
                    lastEmitted = _lastEmittedTimestampUs;
                }

                if (currentUs <= lastEmitted)
                {
                    Thread.Sleep(1);
                    continue;
                }

                // Query buffer for samples in [lastEmitted+1, currentUs]
                // AT-19: Use pre-filtered buffer when zero-phase filter is active
                var sourceBuffer = _filteredBuffer ?? _buffer;
                int count = sourceBuffer.GetRange(lastEmitted + 1, currentUs, scratch);

                for (int i = 0; i < count; i++)
                {
                    SampleReceived?.Invoke(scratch[i]);
                }

                if (count > 0)
                {
                    lock (_lock)
                    {
                        _lastEmittedTimestampUs = scratch[count - 1].TimestampUs;
                    }
                }
                else
                {
                    // Iron Law 5: Missing data must be visible.
                    // No samples in range — emit explicit gap marker if gap exceeds threshold.
                    long gapUs = currentUs - lastEmitted;
                    if (gapUs > GapThresholdUs)
                    {
                        var gapMarker = new EegSample
                        {
                            TimestampUs = currentUs,
                            Ch1Uv = double.NaN,
                            Ch2Uv = double.NaN,
                            Ch3Uv = double.NaN,
                            Ch4Uv = double.NaN,
                            QualityFlags = QualityFlag.Missing
                        };
                        SampleReceived?.Invoke(gapMarker);

                        Trace.TraceWarning(
                            "[EegPlaybackSource] Gap detected: {0} us ({1:F1} ms) at position {2} us",
                            gapUs, gapUs / 1000.0, currentUs);
                    }

                    lock (_lock)
                    {
                        _lastEmittedTimestampUs = currentUs;
                    }
                }

                Thread.Sleep(TickIntervalMs);
            }
        }
        catch (Exception ex)
        {
            if (!_stopRequested)
            {
                Trace.TraceError("[EegPlaybackSource] Playback loop error: {0}", ex.Message);
            }
        }
        finally
        {
            _playing = false;
        }
    }

    /// <summary>
    /// Pre-filter the entire buffer with zero-phase filtering and store in _filteredBuffer.
    /// </summary>
    /// <remarks>
    /// AT-19: Zero-phase (filtfilt) requires the full signal to eliminate phase delay.
    /// Pre-filtering at Start() ensures all playback samples are properly filtered.
    /// Gap markers (QualityFlag.Missing / NaN) are preserved unfiltered.
    /// </remarks>
    private void BuildFilteredBuffer()
    {
        int totalCount = _buffer.Count;
        if (totalCount == 0) return;

        // Read all samples from source buffer
        var allSamples = new EegSample[totalCount];
        int read = _buffer.GetRange(_buffer.OldestTimestampUs, _buffer.NewestTimestampUs, allSamples);

        if (read == 0) return;

        // Find contiguous segments (split on gaps) and filter each
        int segStart = 0;
        while (segStart < read)
        {
            // Skip gap markers
            if (allSamples[segStart].QualityFlags.HasFlag(QualityFlag.Missing))
            {
                segStart++;
                continue;
            }

            // Find end of contiguous segment
            int segEnd = segStart + 1;
            while (segEnd < read && !allSamples[segEnd].QualityFlags.HasFlag(QualityFlag.Missing))
            {
                segEnd++;
            }

            int segLen = segEnd - segStart;
            if (segLen >= 2)
            {
                // Apply zero-phase filtering per channel
                var chIn = new double[segLen];
                var chOut = new double[segLen];

                for (int ch = 0; ch < ChannelCountValue; ch++)
                {
                    for (int i = 0; i < segLen; i++)
                    {
                        chIn[i] = ch switch
                        {
                            0 => allSamples[segStart + i].Ch1Uv,
                            1 => allSamples[segStart + i].Ch2Uv,
                            2 => allSamples[segStart + i].Ch3Uv,
                            _ => allSamples[segStart + i].Ch4Uv,
                        };
                    }

                    _zeroPhaseFilter!.ProcessBlockZeroPhase(ch, chIn, chOut);

                    for (int i = 0; i < segLen; i++)
                    {
                        int idx = segStart + i;
                        allSamples[idx] = ch switch
                        {
                            0 => allSamples[idx] with { Ch1Uv = chOut[i] },
                            1 => allSamples[idx] with { Ch2Uv = chOut[i] },
                            2 => allSamples[idx] with { Ch3Uv = chOut[i] },
                            _ => allSamples[idx] with { Ch4Uv = chOut[i] },
                        };
                    }
                }
            }

            segStart = segEnd;
        }

        // Write filtered samples into a new buffer
        _filteredBuffer = new EegRingBuffer(read + 10);
        for (int i = 0; i < read; i++)
        {
            _filteredBuffer.Write(in allSamples[i]);
        }

        Trace.TraceInformation("[EegPlaybackSource] Pre-filtered {0} samples with zero-phase.", read);
    }

    public void Dispose()
    {
        if (_disposed) return;
        Stop();
        _disposed = true;
    }
}
