// AeegEnvelopeTests.cs
// aEEG 包络计算器测试 - 来源: DSP_SPEC.md §3.1

using Neo.DSP.AEEG;
using Xunit;

namespace Neo.DSP.Tests.AEEG;

/// <summary>
/// aEEG 包络计算器测试。
/// </summary>
/// <remarks>
/// 依据: DSP_SPEC.md §3.1
/// - 峰值检测: 0.5秒窗口最大值
/// - 平滑: 15秒移动平均
/// - 输出: 1 Hz (每秒 min/max 对)
/// </remarks>
public class AeegEnvelopeTests
{
    private const int SampleRate = 160;
    private const long SampleIntervalUs = 6250;  // 160 Hz = 6250 μs

    /// <summary>
    /// 验证输出率为 1 Hz。
    /// </summary>
    [Fact]
    public void Envelope_OutputsAt1Hz()
    {
        // Arrange
        var envelope = new AeegEnvelopeCalculator();
        int outputCount = 0;
        long timestampUs = 0;

        // Act: Process 5 seconds of data
        for (int i = 0; i < SampleRate * 5; i++)
        {
            if (envelope.ProcessSample(50.0, timestampUs, out _))
            {
                outputCount++;
            }
            timestampUs += SampleIntervalUs;
        }

        // Assert: Should have 5 outputs (1 per second)
        Assert.Equal(5, outputCount);
    }

    /// <summary>
    /// 验证峰值检测窗口为 0.5 秒。
    /// </summary>
    [Fact]
    public void Envelope_PeakDetectionWindow_0_5Seconds()
    {
        // Arrange
        var envelope = new AeegEnvelopeCalculator();
        long timestampUs = 0;

        // Create signal with peak at specific position
        // Window is 0.5s = 80 samples
        // Peak should be captured within each window

        double[] signal = new double[SampleRate];  // 1 second
        Array.Fill(signal, 10.0);
        signal[40] = 100.0;  // Peak in first window
        signal[120] = 100.0;  // Peak in second window (after 80 samples)

        // Act: Process the signal
        AeegOutput output = default;
        for (int i = 0; i < signal.Length; i++)
        {
            if (envelope.ProcessSample(signal[i], timestampUs, out output))
            {
                break;  // Got first output
            }
            timestampUs += SampleIntervalUs;
        }

        // Assert: Output should reflect the peaks
        // MaxUv should be influenced by the peak values
        Assert.True(output.MaxUv > 10.0, $"MaxUv should reflect peaks, got {output.MaxUv}");
    }

    /// <summary>
    /// 验证预热期间 IsValid 为 false。
    /// </summary>
    [Fact]
    public void Envelope_TransientDuringWarmup()
    {
        // Arrange
        var envelope = new AeegEnvelopeCalculator();
        long timestampUs = 0;
        int validCount = 0;
        int invalidCount = 0;

        // Act: Process 20 seconds of data (warmup is 15 seconds)
        for (int i = 0; i < SampleRate * 20; i++)
        {
            if (envelope.ProcessSample(50.0, timestampUs, out var output))
            {
                if (output.IsValid)
                    validCount++;
                else
                    invalidCount++;
            }
            timestampUs += SampleIntervalUs;
        }

        // Assert: Should have some invalid outputs during warmup
        Assert.True(invalidCount > 0, "Should have invalid outputs during warmup");
        Assert.True(validCount > 0, "Should have valid outputs after warmup");
    }

    /// <summary>
    /// 验证 15 秒移动平均平滑。
    /// </summary>
    [Fact]
    public void Envelope_SmoothingWindow_15Seconds()
    {
        // Arrange
        var envelope = new AeegEnvelopeCalculator();
        long timestampUs = 0;
        var outputs = new List<AeegOutput>();

        // Act: Process 30 seconds with step change at 15 seconds
        for (int i = 0; i < SampleRate * 30; i++)
        {
            double value = (i < SampleRate * 15) ? 10.0 : 50.0;  // Step at 15 seconds

            if (envelope.ProcessSample(value, timestampUs, out var output))
            {
                outputs.Add(output);
            }
            timestampUs += SampleIntervalUs;
        }

        // Assert: Output should transition gradually (moving average effect)
        Assert.True(outputs.Count >= 15, "Should have at least 15 outputs");

        // First outputs should be around 10, last outputs should approach 50
        // The transition should be gradual due to 15-second moving average
        double firstMax = outputs[0].MaxUv;
        double lastMax = outputs[^1].MaxUv;

        Assert.True(lastMax > firstMax, $"Output should increase: first={firstMax:F1}, last={lastMax:F1}");
    }

    /// <summary>
    /// 验证 Min/Max 提取。
    /// </summary>
    [Fact]
    public void Envelope_ExtractsMinMax()
    {
        // Arrange
        var envelope = new AeegEnvelopeCalculator();
        long timestampUs = 0;

        // Process varying signal
        var outputs = new List<AeegOutput>();
        for (int i = 0; i < SampleRate * 20; i++)
        {
            // Oscillating signal
            double value = 50.0 + 30.0 * Math.Sin(i * 0.1);

            if (envelope.ProcessSample(value, timestampUs, out var output))
            {
                outputs.Add(output);
            }
            timestampUs += SampleIntervalUs;
        }

        // Assert: Each output should have MinUv <= MaxUv
        foreach (var output in outputs)
        {
            Assert.True(output.MinUv <= output.MaxUv,
                $"MinUv ({output.MinUv:F1}) should <= MaxUv ({output.MaxUv:F1})");
        }
    }

    /// <summary>
    /// 验证 Reset 清除状态。
    /// </summary>
    [Fact]
    public void Envelope_ResetClearsState()
    {
        // Arrange
        var envelope = new AeegEnvelopeCalculator();
        long timestampUs = 0;

        // Process some data
        for (int i = 0; i < SampleRate * 5; i++)
        {
            envelope.ProcessSample(50.0, timestampUs, out _);
            timestampUs += SampleIntervalUs;
        }

        Assert.True(envelope.SamplesProcessed > 0);

        // Act
        envelope.Reset();

        // Assert
        Assert.Equal(0, envelope.SamplesProcessed);
        Assert.False(envelope.IsWarmedUp);
    }

    /// <summary>
    /// 验证数值稳定性（长时间运行）。
    /// </summary>
    [Fact]
    public void Envelope_StableUnderLongRun()
    {
        // Arrange
        var envelope = new AeegEnvelopeCalculator();
        var random = new Random(42);
        long timestampUs = 0;

        // Act: Process 1 hour of data
        for (int i = 0; i < SampleRate * 3600; i++)
        {
            double input = random.NextDouble() * 100;  // 0-100 μV (rectified, non-negative)

            if (envelope.ProcessSample(input, timestampUs, out var output))
            {
                // Assert: No overflow/NaN
                Assert.False(double.IsNaN(output.MinUv), $"NaN MinUv at {i}");
                Assert.False(double.IsNaN(output.MaxUv), $"NaN MaxUv at {i}");
                Assert.False(double.IsInfinity(output.MinUv), $"Infinity MinUv at {i}");
                Assert.False(double.IsInfinity(output.MaxUv), $"Infinity MaxUv at {i}");
                Assert.InRange(output.MinUv, 0, 10000);
                Assert.InRange(output.MaxUv, 0, 10000);
            }

            timestampUs += SampleIntervalUs;
        }
    }
}

/// <summary>
/// aEEG 整流器测试。
/// </summary>
public class AeegRectifierTests
{
    /// <summary>
    /// 验证半波整流 y = |x|。
    /// </summary>
    [Theory]
    [InlineData(50.0, 50.0)]
    [InlineData(-50.0, 50.0)]
    [InlineData(0.0, 0.0)]
    [InlineData(-0.001, 0.001)]
    [InlineData(100.5, 100.5)]
    public void Rectify_ReturnsAbsoluteValue(double input, double expected)
    {
        // Act
        double result = AeegRectifier.Rectify(input);

        // Assert
        Assert.Equal(expected, result, 10);
    }

    /// <summary>
    /// 验证批量整流。
    /// </summary>
    [Fact]
    public void RectifyBatch_ProcessesArray()
    {
        // Arrange
        var input = new double[] { -50.0, 30.0, -10.0, 0.0, 100.0 };
        var output = new double[input.Length];

        // Act
        AeegRectifier.RectifyBatch(input, output, input.Length);

        // Assert
        Assert.Equal(50.0, output[0], 10);
        Assert.Equal(30.0, output[1], 10);
        Assert.Equal(10.0, output[2], 10);
        Assert.Equal(0.0, output[3], 10);
        Assert.Equal(100.0, output[4], 10);
    }
}
