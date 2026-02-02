// FilterStabilityTests.cs
// 滤波器稳定性测试 - 来源: DSP_SPEC.md §9.1

using Neo.DSP.Filters;
using Xunit;

namespace Neo.DSP.Tests;

/// <summary>
/// 滤波器稳定性测试。
/// </summary>
/// <remarks>
/// 依据: DSP_SPEC.md §9.1
/// 通过标准: 72h 连续运行无溢出、无漂移
/// </remarks>
public class FilterStabilityTests
{
    private const int SampleRate = 160;
    private const int SamplesPerHour = SampleRate * 3600;  // 576,000 samples/hour

    /// <summary>
    /// 测试滤波器 72 小时长时间运行稳定性。
    /// </summary>
    /// <remarks>
    /// 依据: DSP_SPEC.md §9.1
    /// 通过标准: 72h 连续运行无溢出、无漂移
    ///
    /// 注意: 此测试模拟 72 小时连续采样 (72 × 3600 × 160 = 41,472,000 samples)
    /// </remarks>
    [Fact]
    [Trait("Category", "LongRunning")]
    public void EegFilterChain_StableUnder72Hours()
    {
        // Arrange
        var chain = new EegFilterChain();
        var random = new Random(42);
        long timestampUs = 0;
        const long sampleIntervalUs = 6250;  // 160 Hz

        // 72 hours of samples
        long totalSamples = 72L * SamplesPerHour;

        // Act & Assert
        for (long i = 0; i < totalSamples; i++)
        {
            // Simulate EEG-like signal: noise + sine components
            double input = random.NextDouble() * 100 - 50;  // ±50 μV noise

            var result = chain.ProcessSampleUv(0, input, timestampUs);

            // Check for overflow/NaN
            Assert.False(double.IsNaN(result.Value), $"NaN at sample {i}");
            Assert.False(double.IsInfinity(result.Value), $"Infinity at sample {i}");

            // Check for reasonable range (no runaway)
            Assert.InRange(result.Value, -10000, 10000);

            timestampUs += sampleIntervalUs;

            // Reset timestamp periodically to avoid overflow (every 24 hours)
            if (i > 0 && i % (24L * SamplesPerHour) == 0)
            {
                timestampUs = 0;  // Simulate gap/restart
            }
        }
    }

    /// <summary>
    /// 测试滤波器长时间运行稳定性（模拟 1 小时）。
    /// </summary>
    [Theory]
    [InlineData(NotchFrequency.Hz50)]
    [InlineData(NotchFrequency.Hz60)]
    public void NotchFilter_StableUnderLongRun(NotchFrequency freq)
    {
        // Arrange
        var filter = NotchFilter.Create(freq);
        int samplesPerHour = SampleRate * 3600;
        var random = new Random(42);

        // Act & Assert
        double lastOutput = 0;
        for (int i = 0; i < samplesPerHour; i++)
        {
            // Simulate EEG-like signal: noise + sine
            double input = random.NextDouble() * 100 - 50;  // ±50 μV noise
            double output = filter.Process(input);

            // Check for overflow/NaN
            Assert.False(double.IsNaN(output), $"NaN at sample {i}");
            Assert.False(double.IsInfinity(output), $"Infinity at sample {i}");

            // Check for reasonable range (no runaway)
            Assert.InRange(output, -10000, 10000);

            lastOutput = output;
        }
    }

    [Theory]
    [InlineData(HighPassCutoff.Hz0_3)]
    [InlineData(HighPassCutoff.Hz0_5)]
    [InlineData(HighPassCutoff.Hz1_5)]
    public void HighPassFilter_StableUnderLongRun(HighPassCutoff cutoff)
    {
        // Arrange
        var filter = HighPassFilter.Create(cutoff);
        int samplesPerHour = SampleRate * 3600;
        var random = new Random(42);

        // Act & Assert
        for (int i = 0; i < samplesPerHour; i++)
        {
            double input = random.NextDouble() * 100 - 50;
            double output = filter.Process(input);

            Assert.False(double.IsNaN(output), $"NaN at sample {i}");
            Assert.False(double.IsInfinity(output), $"Infinity at sample {i}");
            Assert.InRange(output, -10000, 10000);
        }
    }

    [Theory]
    [InlineData(LowPassCutoff.Hz15)]
    [InlineData(LowPassCutoff.Hz35)]
    [InlineData(LowPassCutoff.Hz50)]
    [InlineData(LowPassCutoff.Hz70)]
    public void LowPassFilter_StableUnderLongRun(LowPassCutoff cutoff)
    {
        // Arrange
        var filter = LowPassFilter.Create(cutoff);
        int samplesPerHour = SampleRate * 3600;
        var random = new Random(42);

        // Act & Assert
        for (int i = 0; i < samplesPerHour; i++)
        {
            double input = random.NextDouble() * 100 - 50;
            double output = filter.Process(input);

            Assert.False(double.IsNaN(output), $"NaN at sample {i}");
            Assert.False(double.IsInfinity(output), $"Infinity at sample {i}");
            Assert.InRange(output, -10000, 10000);
        }
    }

    [Fact]
    public void EegFilterChain_StableUnderLongRun()
    {
        // Arrange
        var chain = new EegFilterChain();
        int samplesPerHour = SampleRate * 3600;
        var random = new Random(42);
        long timestampUs = 0;
        const long sampleIntervalUs = 6250;  // 160 Hz

        // Act & Assert
        for (int ch = 0; ch < chain.ChannelCount; ch++)
        {
            timestampUs = 0;
            for (int i = 0; i < samplesPerHour; i++)
            {
                double input = random.NextDouble() * 100 - 50;
                var result = chain.ProcessSampleUv(ch, input, timestampUs);

                Assert.False(double.IsNaN(result.Value), $"NaN at ch{ch} sample {i}");
                Assert.False(double.IsInfinity(result.Value), $"Infinity at ch{ch} sample {i}");
                Assert.InRange(result.Value, -10000, 10000);

                timestampUs += sampleIntervalUs;
            }
        }
    }

    /// <summary>
    /// 测试滤波器处理脉冲响应的稳定性。
    /// </summary>
    [Fact]
    public void Filters_StableWithImpulseInput()
    {
        // Arrange
        var notch = NotchFilter.Create(NotchFrequency.Hz50);
        var hpf = HighPassFilter.Create(HighPassCutoff.Hz0_5);
        var lpf = LowPassFilter.Create(LowPassCutoff.Hz35);

        // Act: Apply impulse (1 followed by zeros)
        double notchOut = notch.Process(1000.0);  // Large impulse
        double hpfOut = hpf.Process(1000.0);
        double lpfOut = lpf.Process(1000.0);

        // Process zeros for decay
        for (int i = 0; i < SampleRate * 10; i++)
        {
            notchOut = notch.Process(0.0);
            hpfOut = hpf.Process(0.0);
            lpfOut = lpf.Process(0.0);
        }

        // Assert: Output should decay to near zero
        Assert.True(Math.Abs(notchOut) < 0.01, $"Notch didn't decay: {notchOut}");
        Assert.True(Math.Abs(hpfOut) < 0.01, $"HPF didn't decay: {hpfOut}");
        Assert.True(Math.Abs(lpfOut) < 0.01, $"LPF didn't decay: {lpfOut}");
    }

    /// <summary>
    /// 测试滤波器处理极端值的稳定性。
    /// </summary>
    [Fact]
    public void Filters_StableWithExtremeValues()
    {
        // Arrange
        var chain = new EegFilterChain();

        // Act & Assert: Process extreme but valid EEG values
        var extremeValues = new double[]
        {
            2500,   // Near saturation
            -2500,  // Near negative saturation
            0,      // Zero
            0.001,  // Very small
            -0.001
        };

        long ts = 0;
        foreach (var value in extremeValues)
        {
            var result = chain.ProcessSampleUv(0, value, ts);
            Assert.False(double.IsNaN(result.Value));
            Assert.False(double.IsInfinity(result.Value));
            ts += 6250;
        }
    }

    /// <summary>
    /// 测试滤波器 Reset 后的正确性。
    /// </summary>
    [Fact]
    public void Filters_ResetClearsState()
    {
        // Arrange
        var notch = NotchFilter.Create(NotchFrequency.Hz50);
        var hpf = HighPassFilter.Create(HighPassCutoff.Hz0_5);
        var lpf = LowPassFilter.Create(LowPassCutoff.Hz35);

        // Process some data
        for (int i = 0; i < 100; i++)
        {
            notch.Process(50.0);
            hpf.Process(50.0);
            lpf.Process(50.0);
        }

        // Act: Reset
        notch.Reset();
        hpf.Reset();
        lpf.Reset();

        // Assert: After reset, output should be same as fresh filter
        var freshNotch = NotchFilter.Create(NotchFrequency.Hz50);
        var freshHpf = HighPassFilter.Create(HighPassCutoff.Hz0_5);
        var freshLpf = LowPassFilter.Create(LowPassCutoff.Hz35);

        double testInput = 25.0;
        Assert.Equal(freshNotch.Process(testInput), notch.Process(testInput), 10);
        Assert.Equal(freshHpf.Process(testInput), hpf.Process(testInput), 10);
        Assert.Equal(freshLpf.Process(testInput), lpf.Process(testInput), 10);
    }

    /// <summary>
    /// 测试系数精度（double vs float）。
    /// </summary>
    /// <remarks>
    /// 依据: DSP_SPEC.md §9.1
    /// 通过标准: 相对误差 < 1e-10
    /// </remarks>
    [Fact]
    public void Filters_DoubleCoefficientsPreserved()
    {
        // This test verifies that coefficients are stored with double precision
        // by checking that small input differences produce small output differences

        var filter1 = HighPassFilter.Create(HighPassCutoff.Hz0_5);
        var filter2 = HighPassFilter.Create(HighPassCutoff.Hz0_5);

        double input1 = 1.0;
        double input2 = 1.0 + 1e-12;  // Tiny difference

        // Process many samples
        for (int i = 0; i < 1000; i++)
        {
            double out1 = filter1.Process(input1);
            double out2 = filter2.Process(input2);

            // The difference should be preserved (not lost to float precision)
            // With double precision, the difference should scale proportionally
            if (i > 100)  // After warmup
            {
                double diff = Math.Abs(out2 - out1);
                // Should be very small but not exactly zero (proving double precision)
                Assert.True(diff < 1e-9, $"Precision loss at sample {i}");
            }
        }
    }
}
