// EegDataBridge.cs
// Sprint 3.2: Bridge between ITimeSeriesSource<EegSample> and rendering pipeline
// Sprint 3.2-fix: Added sweep mode support (right-to-left scan)
//
// Source: ARCHITECTURE.md §3, §5
// CHARTER: R-03 no per-frame resource creation
//
// Sweep Mode:
// - New data writes from right to left
// - Existing data stays in place (not scrolling)
// - After sweep period (15s), wraps back to right side
// - Clear band ahead of sweep line

using Neo.Core.Enums;
using Neo.Core.Interfaces;
using Neo.Core.Models;
using Neo.DSP.GS;
using Neo.Rendering.Core;

namespace Neo.UI.Rendering;

/// <summary>
/// Sweep mode render data for a single channel.
/// </summary>
public readonly struct SweepChannelData
{
    /// <summary>Channel index (0-3).</summary>
    public int ChannelIndex { get; init; }

    /// <summary>Channel name.</summary>
    public string ChannelName { get; init; }

    /// <summary>All samples in the sweep buffer.</summary>
    public ReadOnlyMemory<float> Samples { get; init; }

    /// <summary>Quality flags for each sample.</summary>
    public ReadOnlyMemory<byte> Quality { get; init; }

    /// <summary>Total samples in one sweep period.</summary>
    public int SamplesPerSweep { get; init; }

    /// <summary>Sample rate in Hz.</summary>
    public int SampleRate { get; init; }

    /// <summary>Current write index (sweep position).</summary>
    public int WriteIndex { get; init; }

    /// <summary>Number of samples in clear band.</summary>
    public int ClearBandSamples { get; init; }
}

/// <summary>
/// Bridges EEG data source to rendering pipeline.
/// Supports sweep mode (right-to-left scan with clear band).
/// </summary>
public sealed class EegDataBridge : IDisposable
{
    private const int ChannelCount = 4;
    private const int DefaultSweepSeconds = 15;
    private const int DefaultClearBandMs = 200;

    // Cached constants to avoid per-frame allocation (GC optimization)
    private static readonly string[] ChannelNames = ["CH1 (C3-P3)", "CH2 (C4-P4)", "CH3 (P3-P4)", "CH4 (C3-C4)"];

    private readonly int _sampleRate;
    private readonly int _samplesPerSweep;
    private readonly int _clearBandSamples;
    private readonly object _lock = new();
    private readonly Random _clinicalRandom = new(20260205);

    // Sweep buffer for each channel (pre-allocated)
    private readonly float[][] _channelBuffers;
    private readonly byte[][] _qualityBuffers;
    private readonly int[] _missingCounts;
    private readonly int[] _saturatedCounts;
    private readonly int[] _leadOffCounts;
    private readonly SweepChannelData[] _cachedSweepData; // Cached result to avoid per-frame allocation

    // aEEG trend buffers (1 Hz, 24h capacity)
    private const int AeegChannels = 2;
    private const int AeegRateHz = 1;
    private const long AeegIntervalUs = 1_000_000 / AeegRateHz;
    private const int AeegCapacity = 24 * 60 * 60; // 24 hours @ 1 Hz

    private readonly float[][] _aeegMin;
    private readonly float[][] _aeegMax;
    private readonly long[][] _aeegTs;
    private readonly byte[][] _aeegQuality;
    private int _aeegWriteIndex;
    private int _aeegCount;
    private int _aeegVersion;

    private long _aeegBucketStartUs;
    private int _aeegBucketSamples;
    private readonly float[] _aeegBucketMin = new float[AeegChannels];
    private readonly float[] _aeegBucketMax = new float[AeegChannels];
    private byte _aeegBucketQuality;

    private int _writeIndex;
    private bool _hasData;

    private ITimeSeriesSource<EegSample>? _source;
    private bool _disposed;
    private bool _enableClinicalMockShaping;
    private long _lastShapingTimestampUs;
    private int _artifactSamplesRemaining;
    private int _artifactChannelMask;
    private float _artifactPhase;
    private float _artifactFrequencyHz;
    private float _artifactAmplitudeUv;
    private int _suppressionSamplesRemaining;
    private int _suppressionChannelMask;
    private float _suppressionGain;
    private int _burstSamplesRemaining;
    private int _burstChannelMask;
    private float _burstGain;
    private float _burstNoiseGain;
    private float _burstPhase;
    private float _burstFrequencyHz;
    private float _burstToneAmplitudeUv;
    private int _spikeCooldownSamples;
    private readonly ChannelShapingState[] _shapingStates;

    private struct ChannelShapingState
    {
        public float AlphaPhase;
        public float ThetaPhase;
        public float BetaPhase;
        public float DriftPhase;
        public float Envelope;
        public float TargetEnvelope;
        public float EnvelopeTimerSec;
        public float ColoredNoise;
        public float DriftNoise;
        public float FastNoise;
    }

    /// <summary>Sample rate in Hz.</summary>
    public int SampleRate => _sampleRate;

    /// <summary>Number of channels (always 4 for EEG).</summary>
    public int Channels => ChannelCount;

    /// <summary>Samples per sweep period.</summary>
    public int SamplesPerSweep => _samplesPerSweep;

    /// <summary>
    /// Enables a richer synthetic morphology model for UI mock visualization.
    /// Default is false to keep bridge behavior deterministic for existing tests.
    /// </summary>
    public bool EnableClinicalMockShaping
    {
        get
        {
            lock (_lock)
            {
                return _enableClinicalMockShaping;
            }
        }
        set
        {
            lock (_lock)
            {
                if (_enableClinicalMockShaping == value)
                {
                    return;
                }

                _enableClinicalMockShaping = value;
                ResetClinicalShapingState();
            }
        }
    }

    /// <summary>Current write index (sweep position).</summary>
    public int WriteIndex
    {
        get { lock (_lock) return _writeIndex; }
    }

    /// <summary>Number of samples currently in buffer.</summary>
    public int SampleCount
    {
        get { lock (_lock) return _hasData ? _samplesPerSweep : 0; }
    }

    /// <summary>True if buffer has received any data.</summary>
    public bool HasData
    {
        get { lock (_lock) return _hasData; }
    }

    /// <summary>
    /// Creates a new EEG data bridge with sweep mode.
    /// </summary>
    /// <param name="sampleRate">Sample rate in Hz (default 160).</param>
    /// <param name="sweepSeconds">Sweep duration in seconds (default 15).</param>
    /// <param name="clearBandMs">Clear band width in milliseconds (default 200).</param>
    public EegDataBridge(int sampleRate = 160, int sweepSeconds = DefaultSweepSeconds, int clearBandMs = DefaultClearBandMs)
    {
        _sampleRate = sampleRate;
        _samplesPerSweep = sampleRate * sweepSeconds;
        _clearBandSamples = (int)(sampleRate * clearBandMs / 1000.0f);

        // Pre-allocate buffers (CHARTER R-03: no per-frame allocation)
        _channelBuffers = new float[ChannelCount][];
        _qualityBuffers = new byte[ChannelCount][];
        _missingCounts = new int[ChannelCount];
        _saturatedCounts = new int[ChannelCount];
        _leadOffCounts = new int[ChannelCount];
        _cachedSweepData = new SweepChannelData[ChannelCount];
        for (int i = 0; i < ChannelCount; i++)
        {
            _channelBuffers[i] = new float[_samplesPerSweep];
            _qualityBuffers[i] = new byte[_samplesPerSweep];
        }

        _aeegMin = new float[AeegChannels][];
        _aeegMax = new float[AeegChannels][];
        _aeegTs = new long[AeegChannels][];
        _aeegQuality = new byte[AeegChannels][];
        for (int ch = 0; ch < AeegChannels; ch++)
        {
            _aeegMin[ch] = new float[AeegCapacity];
            _aeegMax[ch] = new float[AeegCapacity];
            _aeegTs[ch] = new long[AeegCapacity];
            _aeegQuality[ch] = new byte[AeegCapacity];
        }

        _shapingStates = new ChannelShapingState[ChannelCount];
        ResetClinicalShapingState();
    }

    /// <summary>
    /// Attaches a data source and starts receiving samples.
    /// </summary>
    public void AttachSource(ITimeSeriesSource<EegSample> source)
    {
        if (_disposed) return;

        DetachSource();

        _source = source;
        _source.SampleReceived += OnSampleReceived;
        _source.Start();
    }

    /// <summary>
    /// Detaches the current data source.
    /// </summary>
    public void DetachSource()
    {
        if (_source != null)
        {
            _source.Stop();
            _source.SampleReceived -= OnSampleReceived;
            _source = null;
        }
    }

    /// <summary>
    /// Handles incoming samples from the data source.
    /// </summary>
    private void OnSampleReceived(EegSample sample)
    {
        if (_disposed) return;

        lock (_lock)
        {
            EegSample effectiveSample = _enableClinicalMockShaping
                ? ApplyClinicalMockShaping(sample)
                : sample;

            // Remove previous quality flags at write index before overwrite
            for (int ch = 0; ch < ChannelCount; ch++)
            {
                byte oldFlags = _qualityBuffers[ch][_writeIndex];
                RemoveQualityCounts(ch, oldFlags);
            }

            // Write to sweep buffer at current position
            _channelBuffers[0][_writeIndex] = (float)effectiveSample.Ch1Uv;
            _channelBuffers[1][_writeIndex] = (float)effectiveSample.Ch2Uv;
            _channelBuffers[2][_writeIndex] = (float)effectiveSample.Ch3Uv;
            _channelBuffers[3][_writeIndex] = (float)effectiveSample.Ch4Uv;

            byte qualityByte = (byte)effectiveSample.QualityFlags;
            _qualityBuffers[0][_writeIndex] = qualityByte;
            _qualityBuffers[1][_writeIndex] = qualityByte;
            _qualityBuffers[2][_writeIndex] = qualityByte;
            _qualityBuffers[3][_writeIndex] = qualityByte;

            // Add new quality flags
            for (int ch = 0; ch < ChannelCount; ch++)
            {
                AddQualityCounts(ch, qualityByte);
            }

            // Advance write position left-to-right (wraps from N-1 back to 0)
            _writeIndex = (_writeIndex + 1) % _samplesPerSweep;
            _hasData = true;

            // Update aEEG 1 Hz buckets (CH1/CH2, absolute amplitude)
            UpdateAeegBucket(effectiveSample);
        }
    }

    /// <summary>
    /// Gets sweep mode channel data for rendering.
    /// </summary>
    /// <returns>Array of SweepChannelData for each channel.</returns>
    public SweepChannelData[] GetSweepData()
    {
        if (_disposed) return [];

        lock (_lock)
        {
            if (!_hasData)
                return [];

            // Update cached result (reuse array to avoid GC pressure)
            for (int ch = 0; ch < ChannelCount; ch++)
            {
                _cachedSweepData[ch] = new SweepChannelData
                {
                    ChannelIndex = ch,
                    ChannelName = ChannelNames[ch],
                    Samples = _channelBuffers[ch],
                    Quality = _qualityBuffers[ch],
                    SamplesPerSweep = _samplesPerSweep,
                    SampleRate = _sampleRate,
                    WriteIndex = _writeIndex,
                    ClearBandSamples = _clearBandSamples
                };
            }

            return _cachedSweepData;
        }
    }

    /// <summary>
    /// Gets summary quality flags for a channel (O(1)).
    /// </summary>
    public QualityFlag GetQualitySummary(int channelIndex)
    {
        if (channelIndex < 0 || channelIndex >= ChannelCount)
            return QualityFlag.Normal;

        lock (_lock)
        {
            var flags = QualityFlag.Normal;
            if (_missingCounts[channelIndex] > 0) flags |= QualityFlag.Missing;
            if (_saturatedCounts[channelIndex] > 0) flags |= QualityFlag.Saturated;
            if (_leadOffCounts[channelIndex] > 0) flags |= QualityFlag.LeadOff;
            return flags;
        }
    }

    /// <summary>
    /// Snapshot of aEEG trend data (min/max/timestamps/quality).
    /// </summary>
    public readonly struct AeegSeriesSnapshot
    {
        public required float[] MinValues { get; init; }
        public required float[] MaxValues { get; init; }
        public required long[] Timestamps { get; init; }
        public required byte[] QualityFlags { get; init; }
        public required int Count { get; init; }
    }

    /// <summary>
    /// Gets a snapshot of aEEG trend data for a channel (CH1/CH2).
    /// </summary>
    public AeegSeriesSnapshot GetAeegSeriesSnapshot(int channelIndex)
    {
        if (channelIndex < 0 || channelIndex >= AeegChannels)
        {
            return new AeegSeriesSnapshot
            {
                MinValues = [],
                MaxValues = [],
                Timestamps = [],
                QualityFlags = [],
                Count = 0
            };
        }

        lock (_lock)
        {
            if (_aeegCount == 0)
            {
                return new AeegSeriesSnapshot
                {
                    MinValues = [],
                    MaxValues = [],
                    Timestamps = [],
                    QualityFlags = [],
                    Count = 0
                };
            }

            var min = new float[_aeegCount];
            var max = new float[_aeegCount];
            var ts = new long[_aeegCount];
            var q = new byte[_aeegCount];

            int start = (_aeegWriteIndex - _aeegCount + AeegCapacity) % AeegCapacity;
            for (int i = 0; i < _aeegCount; i++)
            {
                int idx = (start + i) % AeegCapacity;
                min[i] = _aeegMin[channelIndex][idx];
                max[i] = _aeegMax[channelIndex][idx];
                ts[i] = _aeegTs[channelIndex][idx];
                q[i] = _aeegQuality[channelIndex][idx];
            }

            return new AeegSeriesSnapshot
            {
                MinValues = min,
                MaxValues = max,
                Timestamps = ts,
                QualityFlags = q,
                Count = _aeegCount
            };
        }
    }

    /// <summary>
    /// Current aEEG sequence version (increments each completed bucket).
    /// </summary>
    public int AeegVersion
    {
        get { lock (_lock) return _aeegVersion; }
    }

    /// <summary>
    /// Snapshot of GS histogram (15s window).
    /// </summary>
    public readonly struct GsHistogramSnapshot
    {
        public required byte[] Bins { get; init; }
        public required QualityFlag Quality { get; init; }
        public required long StartUs { get; init; }
        public required long EndUs { get; init; }
        public required int SampleCount { get; init; }
    }

    /// <summary>
    /// Builds a GS histogram from the last 15s aEEG samples (mock, 1 Hz).
    /// </summary>
    public GsHistogramSnapshot GetGsHistogramSnapshot(int channelIndex, long endTimestampUs, long windowUs = GsFrame.PeriodUs)
    {
        if (channelIndex < 0 || channelIndex >= AeegChannels || _aeegCount == 0)
        {
            return new GsHistogramSnapshot
            {
                Bins = [],
                Quality = QualityFlag.Normal,
                StartUs = 0,
                EndUs = 0,
                SampleCount = 0
            };
        }

        lock (_lock)
        {
            var bins = new byte[GsBinMapper.TotalBins];
            var quality = QualityFlag.Normal;
            long startUs = endTimestampUs - windowUs;
            long actualStart = long.MaxValue;
            long actualEnd = 0;
            int samples = 0;

            int idx = (_aeegWriteIndex - 1 + AeegCapacity) % AeegCapacity;
            for (int i = 0; i < _aeegCount; i++)
            {
                long ts = _aeegTs[channelIndex][idx];
                if (ts < startUs)
                    break;

                if (ts <= endTimestampUs)
                {
                    float minUv = _aeegMin[channelIndex][idx];
                    float maxUv = _aeegMax[channelIndex][idx];
                    byte q = _aeegQuality[channelIndex][idx];

                    quality |= (QualityFlag)q;
                    actualStart = Math.Min(actualStart, ts);
                    actualEnd = Math.Max(actualEnd, ts);
                    samples++;

                    int minBin = GsBinMapper.MapToBin(minUv);
                    int maxBin = GsBinMapper.MapToBin(maxUv);

                    if (minBin >= 0 && bins[minBin] < GsFrame.MaxBinValue)
                        bins[minBin]++;
                    if (maxBin >= 0 && bins[maxBin] < GsFrame.MaxBinValue)
                        bins[maxBin]++;
                }

                idx = (idx - 1 + AeegCapacity) % AeegCapacity;
            }

            if (samples == 0)
            {
                return new GsHistogramSnapshot
                {
                    Bins = [],
                    Quality = QualityFlag.Normal,
                    StartUs = 0,
                    EndUs = 0,
                    SampleCount = 0
                };
            }

            return new GsHistogramSnapshot
            {
                Bins = bins,
                Quality = quality,
                StartUs = actualStart == long.MaxValue ? 0 : actualStart,
                EndUs = actualEnd,
                SampleCount = samples
            };
        }
    }

    /// <summary>
    /// Gets channel render data for the visible time range (legacy scroll mode).
    /// </summary>
    public ChannelRenderData[] GetChannelData(TimeRange visibleRange)
    {
        // For backward compatibility - return empty in sweep mode
        return [];
    }

    /// <summary>
    /// Checks if a sample index is in the clear band (ahead of sweep line in rightward direction).
    /// </summary>
    public bool IsInClearBand(int sampleIndex)
    {
        lock (_lock)
        {
            int distance = (sampleIndex - _writeIndex + _samplesPerSweep) % _samplesPerSweep;
            return distance > 0 && distance <= _clearBandSamples;
        }
    }

    /// <summary>
    /// Converts sample index to X coordinate.
    /// Index 0 = left (X=0), last index = right (X=screenWidth).
    /// </summary>
    public static float SampleIndexToX(int sampleIndex, int samplesPerSweep, float screenWidth)
    {
        return screenWidth * (float)sampleIndex / samplesPerSweep;
    }

    /// <summary>
    /// Clears all buffered data.
    /// </summary>
    public void Clear()
    {
        lock (_lock)
        {
            _writeIndex = 0;
            _hasData = false;

            for (int i = 0; i < ChannelCount; i++)
            {
                Array.Clear(_channelBuffers[i]);
                Array.Clear(_qualityBuffers[i]);
                _missingCounts[i] = 0;
                _saturatedCounts[i] = 0;
                _leadOffCounts[i] = 0;
            }

            _aeegWriteIndex = 0;
            _aeegCount = 0;
            _aeegVersion = 0;
            _aeegBucketStartUs = 0;
            _aeegBucketSamples = 0;
            _aeegBucketQuality = 0;
            ResetClinicalShapingState();
        }
    }

    private EegSample ApplyClinicalMockShaping(in EegSample sample)
    {
        float dtSec = 1.0f / Math.Max(1, _sampleRate);
        if (_lastShapingTimestampUs > 0 && sample.TimestampUs > _lastShapingTimestampUs)
        {
            float measured = (sample.TimestampUs - _lastShapingTimestampUs) / 1_000_000f;
            if (measured > 0 && measured < 0.2f)
            {
                dtSec = measured;
            }
        }
        _lastShapingTimestampUs = sample.TimestampUs;

        MaybeStartSuppression();
        MaybeStartArtifact();
        MaybeStartBurst();

        float commonDriveUv = (float)((sample.Ch1Uv + sample.Ch2Uv) * 0.5);
        Span<float> shaped = stackalloc float[ChannelCount];

        for (int ch = 0; ch < ChannelCount; ch++)
        {
            var state = _shapingStates[ch];

            state.EnvelopeTimerSec -= dtSec;
            if (state.EnvelopeTimerSec <= 0f)
            {
                state.TargetEnvelope = Lerp(0.55f, 1.75f, (float)_clinicalRandom.NextDouble());
                state.EnvelopeTimerSec = Lerp(0.8f, 3.2f, (float)_clinicalRandom.NextDouble());
            }
            state.Envelope += (state.TargetEnvelope - state.Envelope) * MathF.Min(1f, dtSec * 1.6f);

            float noiseScale = 1f;
            float burstTone = 0f;
            if (_burstSamplesRemaining > 0 && (_burstChannelMask & (1 << ch)) != 0)
            {
                noiseScale = _burstNoiseGain;
                burstTone = _burstToneAmplitudeUv * MathF.Sin(_burstPhase + ch * 0.4f);
            }

            state.ColoredNoise = 0.95f * state.ColoredNoise + NextGaussian(6.6f * noiseScale);
            state.DriftNoise = 0.995f * state.DriftNoise + NextGaussian(0.55f);
            state.FastNoise = 0.80f * state.FastNoise + NextGaussian(2.6f * noiseScale);

            state.AlphaPhase = AdvancePhase(state.AlphaPhase, (8.0f + ch * 0.8f + 0.9f * MathF.Sin(state.DriftPhase)) * dtSec);
            state.ThetaPhase = AdvancePhase(state.ThetaPhase, (3.2f + ch * 0.2f) * dtSec);
            state.BetaPhase = AdvancePhase(state.BetaPhase, (17.0f + ch * 1.3f) * dtSec);
            state.DriftPhase = AdvancePhase(state.DriftPhase, (0.16f + ch * 0.02f) * dtSec);

            float burstGain = 1f;
            if (_burstSamplesRemaining > 0 && (_burstChannelMask & (1 << ch)) != 0)
            {
                burstGain = _burstGain;
            }

            float rhythmicUv =
                state.Envelope * burstGain * (34f * MathF.Sin(state.AlphaPhase)
                + 12f * MathF.Sin(state.ThetaPhase)
                + 7f * MathF.Sin(state.BetaPhase));

            float driftUv = 11f * MathF.Sin(state.DriftPhase) + state.DriftNoise;
            float stochasticUv = state.ColoredNoise + state.FastNoise + burstTone;

            float channelFactor = ch switch
            {
                0 => 1.00f,
                1 => 0.93f,
                2 => 1.08f,
                _ => 0.89f
            };

            float sharedFactor = ch switch
            {
                0 => 0.23f,
                1 => 0.19f,
                2 => 0.16f,
                _ => 0.14f
            };

            float uv = (rhythmicUv + driftUv + stochasticUv) * channelFactor + commonDriveUv * sharedFactor;

            if (_suppressionSamplesRemaining > 0 && (_suppressionChannelMask & (1 << ch)) != 0)
            {
                uv *= _suppressionGain;
            }

            if (_artifactSamplesRemaining > 0 && (_artifactChannelMask & (1 << ch)) != 0)
            {
                uv += _artifactAmplitudeUv * MathF.Sin(_artifactPhase + ch * 0.6f) + NextGaussian(10f);
                if ((_artifactSamplesRemaining & 0x7) == 0)
                {
                    uv += _artifactAmplitudeUv * 0.48f * (_clinicalRandom.NextDouble() > 0.5 ? 1f : -1f);
                }
            }

            if (_spikeCooldownSamples == 0 && _clinicalRandom.NextDouble() < 0.0035)
            {
                uv += (_clinicalRandom.NextDouble() > 0.5 ? 1f : -1f) * Lerp(90f, 200f, (float)_clinicalRandom.NextDouble());
                _spikeCooldownSamples = _clinicalRandom.Next(18, 60);
            }

            shaped[ch] = Math.Clamp(uv, -240f, 240f);
            _shapingStates[ch] = state;
        }

        if (_artifactSamplesRemaining > 0)
        {
            _artifactSamplesRemaining--;
            _artifactPhase = AdvancePhase(_artifactPhase, _artifactFrequencyHz * dtSec);
        }

        if (_burstSamplesRemaining > 0)
        {
            _burstSamplesRemaining--;
            _burstPhase = AdvancePhase(_burstPhase, _burstFrequencyHz * dtSec);
        }

        if (_suppressionSamplesRemaining > 0)
        {
            _suppressionSamplesRemaining--;
        }

        if (_spikeCooldownSamples > 0)
        {
            _spikeCooldownSamples--;
        }

        return sample with
        {
            Ch1Uv = shaped[0],
            Ch2Uv = shaped[1],
            Ch3Uv = shaped[2],
            Ch4Uv = shaped[3]
        };
    }

    private void MaybeStartArtifact()
    {
        if (_artifactSamplesRemaining > 0)
        {
            return;
        }

        if (_clinicalRandom.NextDouble() >= 0.0018)
        {
            return;
        }

        _artifactSamplesRemaining = _clinicalRandom.Next((int)(_sampleRate * 0.15f), (int)(_sampleRate * 0.65f));
        int baseChannel = _clinicalRandom.Next(0, 2); // keep visual focus on CH1/CH2
        _artifactChannelMask = _clinicalRandom.NextDouble() < 0.35
            ? (1 << 0) | (1 << 1)
            : (1 << baseChannel);
        _artifactFrequencyHz = Lerp(18f, 35f, (float)_clinicalRandom.NextDouble());
        _artifactAmplitudeUv = Lerp(55f, 140f, (float)_clinicalRandom.NextDouble());
        _artifactPhase = Lerp(0f, MathF.PI * 2f, (float)_clinicalRandom.NextDouble());
    }

    private void MaybeStartSuppression()
    {
        if (_suppressionSamplesRemaining > 0)
        {
            return;
        }

        if (_clinicalRandom.NextDouble() >= 0.0007)
        {
            return;
        }

        _suppressionSamplesRemaining = _clinicalRandom.Next((int)(_sampleRate * 0.3f), (int)(_sampleRate * 1.6f));
        _suppressionChannelMask = _clinicalRandom.NextDouble() < 0.35
            ? (1 << 0) | (1 << 1)
            : (1 << _clinicalRandom.Next(0, 2));
        _suppressionGain = Lerp(0.12f, 0.38f, (float)_clinicalRandom.NextDouble());
    }

    private void MaybeStartBurst()
    {
        if (_burstSamplesRemaining > 0)
        {
            return;
        }

        if (_clinicalRandom.NextDouble() >= 0.0025)
        {
            return;
        }

        _burstSamplesRemaining = _clinicalRandom.Next((int)(_sampleRate * 0.25f), (int)(_sampleRate * 1.6f));
        _burstChannelMask = _clinicalRandom.NextDouble() < 0.45
            ? (1 << 0) | (1 << 1)
            : (1 << _clinicalRandom.Next(0, 2));
        _burstGain = Lerp(1.6f, 3.2f, (float)_clinicalRandom.NextDouble());
        _burstNoiseGain = Lerp(1.2f, 2.8f, (float)_clinicalRandom.NextDouble());
        _burstFrequencyHz = Lerp(24f, 38f, (float)_clinicalRandom.NextDouble());
        _burstToneAmplitudeUv = Lerp(10f, 28f, (float)_clinicalRandom.NextDouble());
        _burstPhase = Lerp(0f, MathF.PI * 2f, (float)_clinicalRandom.NextDouble());
    }

    private void ResetClinicalShapingState()
    {
        _lastShapingTimestampUs = 0;
        _artifactSamplesRemaining = 0;
        _artifactChannelMask = 0;
        _artifactPhase = 0f;
        _artifactFrequencyHz = 0f;
        _artifactAmplitudeUv = 0f;
        _suppressionSamplesRemaining = 0;
        _suppressionChannelMask = 0;
        _suppressionGain = 1f;
        _burstSamplesRemaining = 0;
        _burstChannelMask = 0;
        _burstGain = 1f;
        _burstNoiseGain = 1f;
        _burstPhase = 0f;
        _burstFrequencyHz = 0f;
        _burstToneAmplitudeUv = 0f;
        _spikeCooldownSamples = 0;

        for (int ch = 0; ch < ChannelCount; ch++)
        {
            _shapingStates[ch] = new ChannelShapingState
            {
                AlphaPhase = Lerp(0f, MathF.PI * 2f, (float)_clinicalRandom.NextDouble()),
                ThetaPhase = Lerp(0f, MathF.PI * 2f, (float)_clinicalRandom.NextDouble()),
                BetaPhase = Lerp(0f, MathF.PI * 2f, (float)_clinicalRandom.NextDouble()),
                DriftPhase = Lerp(0f, MathF.PI * 2f, (float)_clinicalRandom.NextDouble()),
                Envelope = Lerp(0.75f, 1.35f, (float)_clinicalRandom.NextDouble()),
                TargetEnvelope = Lerp(0.75f, 1.35f, (float)_clinicalRandom.NextDouble()),
                EnvelopeTimerSec = Lerp(0.6f, 1.4f, (float)_clinicalRandom.NextDouble()),
                ColoredNoise = 0f,
                DriftNoise = 0f,
                FastNoise = 0f
            };
        }
    }

    private static float AdvancePhase(float phase, float frequencyHzTimesDt)
    {
        phase += frequencyHzTimesDt * 2f * MathF.PI;
        if (phase > MathF.PI * 2f)
        {
            phase -= MathF.PI * 2f;
        }
        return phase;
    }

    private float NextGaussian(float sigma)
    {
        float u1 = MathF.Max(1e-6f, (float)_clinicalRandom.NextDouble());
        float u2 = (float)_clinicalRandom.NextDouble();
        float z = MathF.Sqrt(-2f * MathF.Log(u1)) * MathF.Cos(2f * MathF.PI * u2);
        return z * sigma;
    }

    private static float Lerp(float min, float max, float t)
    {
        return min + (max - min) * t;
    }

    private void UpdateAeegBucket(EegSample sample)
    {
        long ts = sample.TimestampUs;
        float ch1 = (float)Math.Abs(sample.Ch1Uv);
        float ch2 = (float)Math.Abs(sample.Ch2Uv);
        byte q = (byte)sample.QualityFlags;

        if (_aeegBucketSamples == 0)
        {
            _aeegBucketStartUs = ts;
            _aeegBucketMin[0] = ch1;
            _aeegBucketMax[0] = ch1;
            _aeegBucketMin[1] = ch2;
            _aeegBucketMax[1] = ch2;
            _aeegBucketQuality = q;
            _aeegBucketSamples = 1;
            return;
        }

        if (ts - _aeegBucketStartUs < AeegIntervalUs)
        {
            _aeegBucketMin[0] = Math.Min(_aeegBucketMin[0], ch1);
            _aeegBucketMax[0] = Math.Max(_aeegBucketMax[0], ch1);
            _aeegBucketMin[1] = Math.Min(_aeegBucketMin[1], ch2);
            _aeegBucketMax[1] = Math.Max(_aeegBucketMax[1], ch2);
            _aeegBucketQuality |= q;
            _aeegBucketSamples++;
            return;
        }

        // Finalize current bucket and start a new one
        CommitAeegBucket();

        _aeegBucketStartUs = ts;
        _aeegBucketMin[0] = ch1;
        _aeegBucketMax[0] = ch1;
        _aeegBucketMin[1] = ch2;
        _aeegBucketMax[1] = ch2;
        _aeegBucketQuality = q;
        _aeegBucketSamples = 1;
    }

    private void CommitAeegBucket()
    {
        if (_aeegBucketSamples == 0)
            return;

        long timestampUs = _aeegBucketStartUs + (AeegIntervalUs / 2);
        for (int ch = 0; ch < AeegChannels; ch++)
        {
            _aeegMin[ch][_aeegWriteIndex] = _aeegBucketMin[ch];
            _aeegMax[ch][_aeegWriteIndex] = _aeegBucketMax[ch];
            _aeegTs[ch][_aeegWriteIndex] = timestampUs;
            _aeegQuality[ch][_aeegWriteIndex] = _aeegBucketQuality;
        }

        _aeegWriteIndex = (_aeegWriteIndex + 1) % AeegCapacity;
        _aeegCount = Math.Min(_aeegCount + 1, AeegCapacity);
        _aeegVersion++;

        _aeegBucketSamples = 0;
        _aeegBucketQuality = 0;
    }

    private void AddQualityCounts(int channelIndex, byte flags)
    {
        if ((flags & (byte)QualityFlag.Missing) != 0) _missingCounts[channelIndex]++;
        if ((flags & (byte)QualityFlag.Saturated) != 0) _saturatedCounts[channelIndex]++;
        if ((flags & (byte)QualityFlag.LeadOff) != 0) _leadOffCounts[channelIndex]++;
    }

    private void RemoveQualityCounts(int channelIndex, byte flags)
    {
        if ((flags & (byte)QualityFlag.Missing) != 0) _missingCounts[channelIndex] = Math.Max(0, _missingCounts[channelIndex] - 1);
        if ((flags & (byte)QualityFlag.Saturated) != 0) _saturatedCounts[channelIndex] = Math.Max(0, _saturatedCounts[channelIndex] - 1);
        if ((flags & (byte)QualityFlag.LeadOff) != 0) _leadOffCounts[channelIndex] = Math.Max(0, _leadOffCounts[channelIndex] - 1);
    }

    /// <summary>
    /// Releases resources.
    /// </summary>
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        DetachSource();
    }
}
