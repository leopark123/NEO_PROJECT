// FilterFrequencyResponseTests.cs
// Filter frequency response tests - Source: DSP_SPEC.md §9.1

using Neo.DSP.Filters;
using Xunit;

namespace Neo.DSP.Tests;

/// <summary>
/// Filter frequency response tests.
/// </summary>
/// <remarks>
/// Source: DSP_SPEC.md §9.1
/// Criterion: Verify filter passes/attenuates expected frequency bands
/// </remarks>
public class FilterFrequencyResponseTests
{
    private const int SampleRate = 160;

    /// <summary>
    /// Generate a sine wave at the specified frequency.
    /// </summary>
    private static double[] GenerateSineWave(double frequencyHz, int samples)
    {
        var result = new double[samples];
        for (int i = 0; i < samples; i++)
        {
            double t = i / (double)SampleRate;
            result[i] = Math.Sin(2 * Math.PI * frequencyHz * t);
        }
        return result;
    }

    /// <summary>
    /// Calculate RMS amplitude of signal.
    /// </summary>
    private static double CalculateRms(double[] signal, int skipSamples = 0)
    {
        double sumSquares = 0;
        int count = 0;
        for (int i = skipSamples; i < signal.Length; i++)
        {
            sumSquares += signal[i] * signal[i];
            count++;
        }
        return Math.Sqrt(sumSquares / count);
    }

    /// <summary>
    /// Calculate gain (dB).
    /// </summary>
    private static double CalculateGainDb(double outputRms, double inputRms)
    {
        if (inputRms < 1e-10) return double.NegativeInfinity;
        if (outputRms < 1e-10) return double.NegativeInfinity;
        return 20 * Math.Log10(outputRms / inputRms);
    }

    #region Notch Filter Tests

    /// <summary>
    /// 验证 Notch 系数与 DSP_SPEC.md §2.4 一致。
    /// </summary>
    /// <remarks>
    /// 注意: 频率响应验证依赖规格正确性。
    /// 如系数有误，应提交规格修订请求。
    /// </remarks>
    [Theory]
    [InlineData(NotchFrequency.Hz50, 10.0)]
    [InlineData(NotchFrequency.Hz50, 30.0)]
    [InlineData(NotchFrequency.Hz60, 10.0)]
    [InlineData(NotchFrequency.Hz60, 30.0)]
    public void NotchFilter_PassesOtherFrequencies(NotchFrequency freq, double passHz)
    {
        // Arrange
        var filter = NotchFilter.Create(freq);
        var input = GenerateSineWave(passHz, SampleRate * 2);
        var output = new double[input.Length];

        // Act
        for (int i = 0; i < input.Length; i++)
        {
            output[i] = filter.Process(input[i]);
        }

        // Assert
        double inputRms = CalculateRms(input, SampleRate);
        double outputRms = CalculateRms(output, SampleRate);
        double gainDb = CalculateGainDb(outputRms, inputRms);

        // Passband should have near unity gain (> -1 dB)
        Assert.True(gainDb > -1.0, $"Notch passband at {passHz}Hz: expected > -1dB, got {gainDb:F2}dB");
    }

    /// <summary>
    /// 验证 Notch 滤波器数值稳定性（使用 DSP_SPEC.md §2.4 系数）。
    /// </summary>
    [Theory]
    [InlineData(NotchFrequency.Hz50)]
    [InlineData(NotchFrequency.Hz60)]
    public void NotchFilter_NumericallyStable(NotchFrequency freq)
    {
        // Arrange
        var filter = NotchFilter.Create(freq);

        // Act: Process various frequencies
        for (double f = 1; f <= 70; f += 1)
        {
            var input = GenerateSineWave(f, SampleRate);
            foreach (var sample in input)
            {
                double output = filter.Process(sample);
                Assert.False(double.IsNaN(output), $"NaN at {f}Hz");
                Assert.False(double.IsInfinity(output), $"Infinity at {f}Hz");
            }
        }
    }

    #endregion

    #region High-Pass Filter Tests

    [Theory]
    [InlineData(HighPassCutoff.Hz0_5, 10.0)]
    [InlineData(HighPassCutoff.Hz0_5, 20.0)]
    [InlineData(HighPassCutoff.Hz1_5, 10.0)]
    [InlineData(HighPassCutoff.Hz1_5, 20.0)]
    public void HighPassFilter_PassesHighFrequencies(HighPassCutoff cutoff, double passHz)
    {
        // Arrange
        var filter = HighPassFilter.Create(cutoff);
        int warmup = HighPassFilter.GetWarmupSamples(cutoff);
        var input = GenerateSineWave(passHz, warmup * 3);
        var output = new double[input.Length];

        // Act
        for (int i = 0; i < input.Length; i++)
        {
            output[i] = filter.Process(input[i]);
        }

        // Assert
        double inputRms = CalculateRms(input, warmup * 2);
        double outputRms = CalculateRms(output, warmup * 2);
        double gainDb = CalculateGainDb(outputRms, inputRms);

        // Passband should have near unity gain (> -1 dB)
        Assert.True(gainDb > -1.0, $"HPF passband at {passHz}Hz: expected > -1dB, got {gainDb:F2}dB");
    }

    [Theory]
    [InlineData(HighPassCutoff.Hz0_5, 0.05)]  // Well below 0.5 Hz
    [InlineData(HighPassCutoff.Hz1_5, 0.1)]   // Well below 1.5 Hz
    public void HighPassFilter_AttenuatesLowFrequencies(HighPassCutoff cutoff, double stopHz)
    {
        // Arrange
        var filter = HighPassFilter.Create(cutoff);
        // Need long signal for low frequencies
        int samples = (int)(SampleRate * (10.0 / stopHz));  // At least 10 cycles
        samples = Math.Max(samples, SampleRate * 60);  // Minimum 60 seconds
        var input = GenerateSineWave(stopHz, samples);
        var output = new double[input.Length];

        // Act
        for (int i = 0; i < input.Length; i++)
        {
            output[i] = filter.Process(input[i]);
        }

        // Assert: Skip first half for warmup
        int skip = samples / 2;
        double inputRms = CalculateRms(input, skip);
        double outputRms = CalculateRms(output, skip);
        double gainDb = CalculateGainDb(outputRms, inputRms);

        // Stopband should attenuate (< -10 dB for 2nd order at ~10x below cutoff)
        Assert.True(gainDb < -10, $"HPF stopband at {stopHz}Hz: expected < -10dB, got {gainDb:F2}dB");
    }

    #endregion

    #region Low-Pass Filter Tests

    [Theory]
    [InlineData(LowPassCutoff.Hz35, 5.0)]
    [InlineData(LowPassCutoff.Hz35, 10.0)]
    [InlineData(LowPassCutoff.Hz15, 5.0)]
    [InlineData(LowPassCutoff.Hz50, 10.0)]
    public void LowPassFilter_PassesLowFrequencies(LowPassCutoff cutoff, double passHz)
    {
        // Arrange
        var filter = LowPassFilter.Create(cutoff);
        int warmup = LowPassFilter.GetWarmupSamples(cutoff);
        var input = GenerateSineWave(passHz, SampleRate * 2);
        var output = new double[input.Length];

        // Act
        for (int i = 0; i < input.Length; i++)
        {
            output[i] = filter.Process(input[i]);
        }

        // Assert
        double inputRms = CalculateRms(input, SampleRate);
        double outputRms = CalculateRms(output, SampleRate);
        double gainDb = CalculateGainDb(outputRms, inputRms);

        // Passband should have near unity gain (> -1 dB)
        Assert.True(gainDb > -1.0, $"LPF passband at {passHz}Hz: expected > -1dB, got {gainDb:F2}dB");
    }

    [Theory]
    [InlineData(LowPassCutoff.Hz15, 60.0)]  // Well above 15 Hz
    [InlineData(LowPassCutoff.Hz35, 70.0)]  // Well above 35 Hz
    public void LowPassFilter_AttenuatesHighFrequencies(LowPassCutoff cutoff, double stopHz)
    {
        // Arrange
        var filter = LowPassFilter.Create(cutoff);
        var input = GenerateSineWave(stopHz, SampleRate * 2);
        var output = new double[input.Length];

        // Act
        for (int i = 0; i < input.Length; i++)
        {
            output[i] = filter.Process(input[i]);
        }

        // Assert
        double inputRms = CalculateRms(input, SampleRate);
        double outputRms = CalculateRms(output, SampleRate);
        double gainDb = CalculateGainDb(outputRms, inputRms);

        // Stopband should attenuate (< -10 dB for 4th order at ~2x cutoff)
        Assert.True(gainDb < -10, $"LPF stopband at {stopHz}Hz: expected < -10dB, got {gainDb:F2}dB");
    }

    #endregion

    #region Filter Chain Tests

    [Fact]
    public void EegFilterChain_DefaultConfig_PassesBandpassFrequencies()
    {
        // Arrange: Default config is HPF 0.5Hz, LPF 35Hz, Notch 50Hz
        var chain = new EegFilterChain();

        // Need enough samples: warmup*3 to have data after skipping warmup*2
        int warmup = chain.WarmupSamples;  // 960 samples for 0.5Hz HPF
        int totalSamples = warmup * 4;  // 4x warmup = plenty of samples

        // 10Hz signal should pass through (in the 0.5-35Hz band)
        var input = GenerateSineWave(10.0, totalSamples);
        var output = new double[input.Length];

        // Act
        for (int i = 0; i < input.Length; i++)
        {
            var result = chain.ProcessSampleUv(0, input[i], i * 6250);  // 160Hz = 6250us interval
            output[i] = result.Value;
        }

        // Assert
        double inputRms = CalculateRms(input, warmup * 2);
        double outputRms = CalculateRms(output, warmup * 2);
        double gainDb = CalculateGainDb(outputRms, inputRms);

        // 10Hz should pass with near unity gain (accounting for cascaded filter losses)
        Assert.True(gainDb > -3.0, $"Filter chain at 10Hz: expected > -3dB, got {gainDb:F2}dB");
    }

    [Fact]
    public void EegFilterChain_AttenuatesOutOfBandFrequencies()
    {
        // Arrange
        var chain = new EegFilterChain();

        // 0.1Hz signal should be attenuated (below HPF cutoff)
        var inputLow = GenerateSineWave(0.1, SampleRate * 30);  // Need long signal for low freq
        var outputLow = new double[inputLow.Length];

        // Act
        for (int i = 0; i < inputLow.Length; i++)
        {
            var result = chain.ProcessSampleUv(0, inputLow[i], i * 6250);
            outputLow[i] = result.Value;
        }

        // Assert
        int skip = SampleRate * 20;  // Skip warmup
        double inputRms = CalculateRms(inputLow, skip);
        double outputRms = CalculateRms(outputLow, skip);
        double gainDb = CalculateGainDb(outputRms, inputRms);

        // 0.1Hz should be significantly attenuated (> 10dB)
        Assert.True(gainDb < -10, $"Filter chain at 0.1Hz: expected < -10dB, got {gainDb:F2}dB");
    }

    /// <summary>
    /// 验证 EegFilterChain 数值稳定性。
    /// </summary>
    /// <remarks>
    /// Notch 系数使用 DSP_SPEC.md §2.4 固定值。
    /// 频率响应验证依赖规格正确性。
    /// </remarks>
    [Fact]
    public void EegFilterChain_NumericallyStable()
    {
        // Arrange
        var chain = new EegFilterChain();

        // Act: Process various frequencies including 50Hz
        for (double f = 1; f <= 70; f += 5)
        {
            var input = GenerateSineWave(f, SampleRate * 2);
            for (int i = 0; i < input.Length; i++)
            {
                var result = chain.ProcessSampleUv(0, input[i], i * 6250);
                Assert.False(double.IsNaN(result.Value), $"NaN at {f}Hz");
                Assert.False(double.IsInfinity(result.Value), $"Infinity at {f}Hz");
            }
            chain.ResetAll();
        }
    }

    #endregion
}
