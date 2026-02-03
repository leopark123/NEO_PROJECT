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

using Neo.Core.Interfaces;
using Neo.Core.Models;
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

    private readonly int _sampleRate;
    private readonly int _samplesPerSweep;
    private readonly int _clearBandSamples;
    private readonly object _lock = new();

    // Sweep buffer for each channel (pre-allocated)
    private readonly float[][] _channelBuffers;
    private readonly byte[][] _qualityBuffers;

    private int _writeIndex;
    private bool _hasData;

    private ITimeSeriesSource<EegSample>? _source;
    private bool _disposed;

    /// <summary>Sample rate in Hz.</summary>
    public int SampleRate => _sampleRate;

    /// <summary>Number of channels (always 4 for EEG).</summary>
    public int Channels => ChannelCount;

    /// <summary>Samples per sweep period.</summary>
    public int SamplesPerSweep => _samplesPerSweep;

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
        for (int i = 0; i < ChannelCount; i++)
        {
            _channelBuffers[i] = new float[_samplesPerSweep];
            _qualityBuffers[i] = new byte[_samplesPerSweep];
        }
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
            // Write to sweep buffer at current position
            _channelBuffers[0][_writeIndex] = (float)sample.Ch1Uv;
            _channelBuffers[1][_writeIndex] = (float)sample.Ch2Uv;
            _channelBuffers[2][_writeIndex] = (float)sample.Ch3Uv;
            _channelBuffers[3][_writeIndex] = (float)sample.Ch4Uv;

            byte qualityByte = (byte)sample.QualityFlags;
            _qualityBuffers[0][_writeIndex] = qualityByte;
            _qualityBuffers[1][_writeIndex] = qualityByte;
            _qualityBuffers[2][_writeIndex] = qualityByte;
            _qualityBuffers[3][_writeIndex] = qualityByte;

            // Advance write position (wraps around for continuous sweep)
            _writeIndex = (_writeIndex + 1) % _samplesPerSweep;
            _hasData = true;
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

            string[] channelNames = ["CH1 (C3-P3)", "CH2 (C4-P4)", "CH3 (P3-P4)", "CH4 (C3-C4)"];
            var result = new SweepChannelData[ChannelCount];

            for (int ch = 0; ch < ChannelCount; ch++)
            {
                result[ch] = new SweepChannelData
                {
                    ChannelIndex = ch,
                    ChannelName = channelNames[ch],
                    Samples = _channelBuffers[ch],
                    Quality = _qualityBuffers[ch],
                    SamplesPerSweep = _samplesPerSweep,
                    WriteIndex = _writeIndex,
                    ClearBandSamples = _clearBandSamples
                };
            }

            return result;
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
    /// Checks if a sample index is in the clear band.
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
    /// Converts sample index to X coordinate (right-to-left sweep).
    /// Index 0 = right side, last index = left side.
    /// </summary>
    public static float SampleIndexToX(int sampleIndex, int samplesPerSweep, float screenWidth)
    {
        // Right-to-left: index 0 at right, increasing index moves left
        return screenWidth * (1.0f - (float)sampleIndex / samplesPerSweep);
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
            }
        }
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
