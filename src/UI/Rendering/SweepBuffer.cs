// SweepBuffer.cs
// Sprint 3.2-fix: Sweep mode buffer for EEG waveform display
//
// Sweep mode: New data writes from right to left, existing data stays in place.
// After one sweep period (e.g., 15 seconds), the sweep line returns to the right.
//
// Buffer layout (right-to-left sweep):
// [oldest data ... | clear band | sweep line | ... newest data]
// Screen X:  0 -----------------> Width
// Write pos: moves from right (Width) to left (0), then wraps to right

using System.Runtime.CompilerServices;

namespace Neo.UI.Rendering;

/// <summary>
/// Fixed-size circular buffer for sweep mode waveform display.
/// Supports right-to-left sweep with clear band.
/// </summary>
public sealed class SweepBuffer
{
    private readonly float[][] _channelData;
    private readonly byte[][] _qualityData;
    private readonly int _samplesPerSweep;
    private readonly int _channelCount;
    private readonly int _clearBandSamples;

    private int _writeIndex;
    private bool _hasData;

    /// <summary>Number of samples per sweep period.</summary>
    public int SamplesPerSweep => _samplesPerSweep;

    /// <summary>Number of channels.</summary>
    public int ChannelCount => _channelCount;

    /// <summary>Current write position (0 to SamplesPerSweep-1).</summary>
    public int WriteIndex => _writeIndex;

    /// <summary>True if buffer has received any data.</summary>
    public bool HasData => _hasData;

    /// <summary>
    /// Sweep progress (0.0 = just started at right, 1.0 = completed at left).
    /// </summary>
    public float SweepProgress => (float)_writeIndex / _samplesPerSweep;

    /// <summary>
    /// Creates a sweep buffer.
    /// </summary>
    /// <param name="sampleRate">Sample rate in Hz (e.g., 160).</param>
    /// <param name="sweepDurationSeconds">Sweep duration in seconds (e.g., 15).</param>
    /// <param name="channelCount">Number of channels (e.g., 4).</param>
    /// <param name="clearBandMs">Clear band width in milliseconds (e.g., 100).</param>
    public SweepBuffer(int sampleRate, float sweepDurationSeconds, int channelCount = 4, int clearBandMs = 100)
    {
        _samplesPerSweep = (int)(sampleRate * sweepDurationSeconds);
        _channelCount = channelCount;
        _clearBandSamples = (int)(sampleRate * clearBandMs / 1000.0f);

        _channelData = new float[channelCount][];
        _qualityData = new byte[channelCount][];

        for (int i = 0; i < channelCount; i++)
        {
            _channelData[i] = new float[_samplesPerSweep];
            _qualityData[i] = new byte[_samplesPerSweep];
        }

        _writeIndex = 0;
        _hasData = false;
    }

    /// <summary>
    /// Writes a sample to the buffer at the current sweep position.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void WriteSample(float ch1, float ch2, float ch3, float ch4, byte quality)
    {
        _channelData[0][_writeIndex] = ch1;
        _channelData[1][_writeIndex] = ch2;
        _channelData[2][_writeIndex] = ch3;
        _channelData[3][_writeIndex] = ch4;

        _qualityData[0][_writeIndex] = quality;
        _qualityData[1][_writeIndex] = quality;
        _qualityData[2][_writeIndex] = quality;
        _qualityData[3][_writeIndex] = quality;

        _writeIndex = (_writeIndex + 1) % _samplesPerSweep;
        _hasData = true;
    }

    /// <summary>
    /// Gets channel data for rendering.
    /// </summary>
    /// <param name="channel">Channel index (0-3).</param>
    /// <returns>Read-only span of channel data.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ReadOnlySpan<float> GetChannelData(int channel)
    {
        if (channel < 0 || channel >= _channelCount)
            return ReadOnlySpan<float>.Empty;
        return _channelData[channel].AsSpan();
    }

    /// <summary>
    /// Gets quality data for rendering.
    /// </summary>
    /// <param name="channel">Channel index (0-3).</param>
    /// <returns>Read-only span of quality flags.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ReadOnlySpan<byte> GetQualityData(int channel)
    {
        if (channel < 0 || channel >= _channelCount)
            return ReadOnlySpan<byte>.Empty;
        return _qualityData[channel].AsSpan();
    }

    /// <summary>
    /// Converts buffer sample index to screen X coordinate (right-to-left sweep).
    /// </summary>
    /// <param name="sampleIndex">Sample index in buffer.</param>
    /// <param name="screenWidth">Screen width in pixels.</param>
    /// <returns>X coordinate (0 = left, screenWidth = right).</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public float SampleIndexToX(int sampleIndex, float screenWidth)
    {
        // Right-to-left: index 0 maps to right side, last index maps to left side
        // But we want newest data on the right of sweep line, oldest on the left
        // So: X = screenWidth * (1 - sampleIndex / samplesPerSweep)
        return screenWidth * (1.0f - (float)sampleIndex / _samplesPerSweep);
    }

    /// <summary>
    /// Gets the X coordinate of the sweep line (where new data is being written).
    /// </summary>
    /// <param name="screenWidth">Screen width in pixels.</param>
    /// <returns>Sweep line X coordinate.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public float GetSweepLineX(float screenWidth)
    {
        return SampleIndexToX(_writeIndex, screenWidth);
    }

    /// <summary>
    /// Gets the clear band range (region ahead of sweep line to be cleared).
    /// </summary>
    /// <param name="screenWidth">Screen width in pixels.</param>
    /// <returns>Tuple of (startX, endX) for clear band.</returns>
    public (float StartX, float EndX) GetClearBandRange(float screenWidth)
    {
        // Clear band is ahead of the sweep line (to the left in right-to-left mode)
        int clearEndIndex = _writeIndex;
        int clearStartIndex = (_writeIndex + _clearBandSamples) % _samplesPerSweep;

        float startX = SampleIndexToX(clearStartIndex, screenWidth);
        float endX = SampleIndexToX(clearEndIndex, screenWidth);

        // Handle wrap-around
        if (startX > endX)
        {
            // Clear band wraps around - return the portion on the left side
            return (0, endX);
        }

        return (startX, endX);
    }

    /// <summary>
    /// Checks if a sample index is in the clear band (should not be drawn).
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool IsInClearBand(int sampleIndex)
    {
        int distance = (sampleIndex - _writeIndex + _samplesPerSweep) % _samplesPerSweep;
        return distance < _clearBandSamples;
    }

    /// <summary>
    /// Resets the buffer and sweep position.
    /// </summary>
    public void Reset()
    {
        _writeIndex = 0;
        _hasData = false;

        for (int i = 0; i < _channelCount; i++)
        {
            Array.Clear(_channelData[i]);
            Array.Clear(_qualityData[i]);
        }
    }
}
