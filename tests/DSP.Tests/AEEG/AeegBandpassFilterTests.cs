// AeegBandpassFilterTests.cs
// aEEG 带通滤波器测试 - 来源: DSP_SPEC.md §3.2

using Neo.DSP.AEEG;
using Xunit;

namespace Neo.DSP.Tests.AEEG;

/// <summary>
/// aEEG 带通滤波器测试。
/// </summary>
/// <remarks>
/// 依据: DSP_SPEC.md §3.2
/// 带通范围: 2-15 Hz
/// </remarks>
public class AeegBandpassFilterTests
{
    private const int SampleRate = 160;

    /// <summary>
    /// 生成正弦波。
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
    /// 计算 RMS 幅度。
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
    /// 计算增益 (dB)。
    /// </summary>
    private static double CalculateGainDb(double outputRms, double inputRms)
    {
        if (inputRms < 1e-10) return double.NegativeInfinity;
        if (outputRms < 1e-10) return double.NegativeInfinity;
        return 20 * Math.Log10(outputRms / inputRms);
    }

    /// <summary>
    /// 验证带通滤波器通过中心频带 (5-10 Hz)。
    /// </summary>
    [Theory]
    [InlineData(5.0)]
    [InlineData(8.0)]
    [InlineData(10.0)]
    public void Bandpass_PassesCenterFrequencies(double frequencyHz)
    {
        // Arrange
        var filter = new AeegBandpassFilter();
        int warmup = AeegBandpassFilter.WarmupSamples;
        var input = GenerateSineWave(frequencyHz, warmup * 4);
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

        // Passband should have near unity gain (> -3 dB)
        Assert.True(gainDb > -3.0, $"Bandpass at {frequencyHz}Hz: expected > -3dB, got {gainDb:F2}dB");
    }

    /// <summary>
    /// 验证带通滤波器衰减低频 (< 2 Hz)。
    /// </summary>
    [Theory]
    [InlineData(0.5)]
    [InlineData(1.0)]
    public void Bandpass_AttenuatesLowFrequencies(double frequencyHz)
    {
        // Arrange
        var filter = new AeegBandpassFilter();
        // Need long signal for low frequencies
        int samples = (int)(SampleRate * 30);  // 30 seconds
        var input = GenerateSineWave(frequencyHz, samples);
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

        // Stopband should attenuate (< -6 dB)
        Assert.True(gainDb < -6, $"Bandpass low stopband at {frequencyHz}Hz: expected < -6dB, got {gainDb:F2}dB");
    }

    /// <summary>
    /// 验证带通滤波器衰减高频 (> 15 Hz)。
    /// </summary>
    /// <remarks>
    /// 依据: DSP_SPEC.md §3.2
    /// 使用 LPF 15Hz (4阶 Butterworth) 衰减高频。
    /// 30 Hz 为 2x 截止频率，预期衰减约 -6 dB。
    /// 50 Hz 为 3.3x 截止频率，预期衰减约 -15 dB。
    /// </remarks>
    [Theory]
    [InlineData(30.0, -5)]   // 2x cutoff, 4th order ~-6dB
    [InlineData(50.0, -10)]  // 3.3x cutoff, more attenuation
    public void Bandpass_AttenuatesHighFrequencies(double frequencyHz, double minAttenuationDb)
    {
        // Arrange
        var filter = new AeegBandpassFilter();
        var input = GenerateSineWave(frequencyHz, SampleRate * 2);
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

        // Stopband should attenuate
        Assert.True(gainDb < minAttenuationDb, $"Bandpass high stopband at {frequencyHz}Hz: expected < {minAttenuationDb}dB, got {gainDb:F2}dB");
    }

    /// <summary>
    /// 验证带通滤波器数值稳定性。
    /// </summary>
    [Fact]
    public void Bandpass_NumericallyStable()
    {
        // Arrange
        var filter = new AeegBandpassFilter();
        var random = new Random(42);

        // Act: Process 1 hour of data
        for (int i = 0; i < SampleRate * 3600; i++)
        {
            double input = random.NextDouble() * 100 - 50;
            double output = filter.Process(input);

            // Assert: No overflow/NaN
            Assert.False(double.IsNaN(output), $"NaN at sample {i}");
            Assert.False(double.IsInfinity(output), $"Infinity at sample {i}");
            Assert.InRange(output, -10000, 10000);
        }
    }

    /// <summary>
    /// 验证 Reset 清除状态。
    /// </summary>
    [Fact]
    public void Bandpass_ResetClearsState()
    {
        // Arrange
        var filter = new AeegBandpassFilter();

        // Process some data
        for (int i = 0; i < 100; i++)
        {
            filter.Process(50.0);
        }

        // Act
        filter.Reset();

        // Assert: After reset, output should be same as fresh filter
        var freshFilter = new AeegBandpassFilter();
        double testInput = 25.0;
        Assert.Equal(freshFilter.Process(testInput), filter.Process(testInput), 10);
    }
}
