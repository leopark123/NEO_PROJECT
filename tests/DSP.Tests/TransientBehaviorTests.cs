// TransientBehaviorTests.cs
// 滤波器瞬态行为测试 - 来源: DSP_SPEC.md §7

using Neo.Core.Enums;
using Neo.DSP.Filters;
using Xunit;

namespace Neo.DSP.Tests;

/// <summary>
/// 滤波器瞬态（预热）行为测试。
/// </summary>
/// <remarks>
/// 依据: DSP_SPEC.md §7
/// - 滤波器冷启动期间输出不稳定
/// - 必须标记 QualityFlag.Transient
/// - 预热时间由 HPF 截止频率决定
/// </remarks>
public class TransientBehaviorTests
{
    private const int SampleRate = 160;

    #region Warmup Sample Count Tests

    [Theory]
    [InlineData(HighPassCutoff.Hz0_3, 1600)]  // 10 sec * 160 Hz (DSP_SPEC.md §7.2)
    [InlineData(HighPassCutoff.Hz0_5, 960)]   // 6 sec * 160 Hz
    [InlineData(HighPassCutoff.Hz1_5, 320)]   // 2 sec * 160 Hz
    public void HighPassFilter_ReturnsCorrectWarmupSamples(HighPassCutoff cutoff, int expectedSamples)
    {
        // Act
        int warmupSamples = HighPassFilter.GetWarmupSamples(cutoff);

        // Assert
        Assert.Equal(expectedSamples, warmupSamples);
    }

    [Theory]
    [InlineData(LowPassCutoff.Hz15)]
    [InlineData(LowPassCutoff.Hz35)]
    [InlineData(LowPassCutoff.Hz50)]
    [InlineData(LowPassCutoff.Hz70)]
    public void LowPassFilter_WarmupSamplesAreLessThanHpf(LowPassCutoff cutoff)
    {
        // LPF warmup should always be less than HPF warmup (HPF dominates)
        int lpfWarmup = LowPassFilter.GetWarmupSamples(cutoff);
        int hpfMinWarmup = HighPassFilter.GetWarmupSamples(HighPassCutoff.Hz1_5);  // Fastest HPF

        Assert.True(lpfWarmup < hpfMinWarmup,
            $"LPF warmup ({lpfWarmup}) should be < HPF minimum warmup ({hpfMinWarmup})");
    }

    #endregion

    #region EegFilterChain Transient Marking Tests

    [Fact]
    public void EegFilterChain_MarksTransientDuringWarmup()
    {
        // Arrange
        var chain = new EegFilterChain();
        int warmupSamples = chain.WarmupSamples;
        long timestampUs = 0;
        const long sampleIntervalUs = 6250;  // 160 Hz

        // Act & Assert: During warmup, Transient flag should be set
        for (int i = 0; i < warmupSamples - 1; i++)
        {
            var result = chain.ProcessSampleUv(0, 10.0, timestampUs);
            Assert.True(result.Quality.HasFlag(QualityFlag.Transient),
                $"Sample {i} should have Transient flag during warmup");
            timestampUs += sampleIntervalUs;
        }

        // After warmup, Transient flag should NOT be set
        var postWarmup = chain.ProcessSampleUv(0, 10.0, timestampUs);
        Assert.False(postWarmup.Quality.HasFlag(QualityFlag.Transient),
            "Sample after warmup should NOT have Transient flag");
    }

    [Fact]
    public void EegFilterChain_IsWarmedUp_ReturnsFalseDuringWarmup()
    {
        // Arrange
        var chain = new EegFilterChain();
        int warmupSamples = chain.WarmupSamples;
        long timestampUs = 0;
        const long sampleIntervalUs = 6250;

        // Act & Assert: During warmup
        for (int i = 0; i < warmupSamples - 1; i++)
        {
            chain.ProcessSampleUv(0, 10.0, timestampUs);
            Assert.False(chain.IsWarmedUp(0), $"Channel should not be warmed up at sample {i}");
            timestampUs += sampleIntervalUs;
        }

        // After warmup
        chain.ProcessSampleUv(0, 10.0, timestampUs);
        Assert.True(chain.IsWarmedUp(0), "Channel should be warmed up after warmup period");
    }

    [Fact]
    public void EegFilterChain_ChannelsWarmUpIndependently()
    {
        // Arrange
        var chain = new EegFilterChain();
        int warmupSamples = chain.WarmupSamples;
        long timestampUs = 0;
        const long sampleIntervalUs = 6250;

        // Warm up channel 0 only
        for (int i = 0; i < warmupSamples; i++)
        {
            chain.ProcessSampleUv(0, 10.0, timestampUs);
            timestampUs += sampleIntervalUs;
        }

        // Assert
        Assert.True(chain.IsWarmedUp(0), "Channel 0 should be warmed up");
        Assert.False(chain.IsWarmedUp(1), "Channel 1 should NOT be warmed up");
        Assert.False(chain.IsWarmedUp(2), "Channel 2 should NOT be warmed up");
        Assert.False(chain.IsWarmedUp(3), "Channel 3 should NOT be warmed up");
    }

    #endregion

    #region Gap Detection and Reset Tests

    [Fact]
    public void EegFilterChain_ResetsOnGap()
    {
        // Arrange
        var chain = new EegFilterChain();
        int warmupSamples = chain.WarmupSamples;
        long timestampUs = 0;
        const long sampleIntervalUs = 6250;  // 160 Hz

        // Warm up channel 0
        for (int i = 0; i < warmupSamples; i++)
        {
            chain.ProcessSampleUv(0, 10.0, timestampUs);
            timestampUs += sampleIntervalUs;
        }
        Assert.True(chain.IsWarmedUp(0), "Channel should be warmed up");

        // Simulate gap (> 4 samples = > 25ms)
        timestampUs += sampleIntervalUs * 10;  // 10 sample gap

        // Act: Process after gap
        var result = chain.ProcessSampleUv(0, 10.0, timestampUs);

        // Assert: Gap causes reset, so transient flag should be back
        Assert.True(result.Quality.HasFlag(QualityFlag.Missing),
            "Sample after gap should have Missing flag");
        Assert.True(result.Quality.HasFlag(QualityFlag.Transient),
            "Sample after gap should have Transient flag (filter reset)");
        Assert.False(chain.IsWarmedUp(0),
            "Channel should NOT be warmed up after gap reset");
    }

    [Fact]
    public void EegFilterChain_NoResetOnSmallGap()
    {
        // Arrange
        var chain = new EegFilterChain();
        int warmupSamples = chain.WarmupSamples;
        long timestampUs = 0;
        const long sampleIntervalUs = 6250;

        // Warm up channel 0
        for (int i = 0; i < warmupSamples; i++)
        {
            chain.ProcessSampleUv(0, 10.0, timestampUs);
            timestampUs += sampleIntervalUs;
        }
        Assert.True(chain.IsWarmedUp(0), "Channel should be warmed up");

        // Small gap (≤ 4 samples)
        timestampUs += sampleIntervalUs * 3;  // 3 sample gap (OK)

        // Act: Process after small gap
        var result = chain.ProcessSampleUv(0, 10.0, timestampUs);

        // Assert: Small gap should NOT cause reset
        Assert.False(result.Quality.HasFlag(QualityFlag.Missing),
            "Sample after small gap should NOT have Missing flag");
        Assert.False(result.Quality.HasFlag(QualityFlag.Transient),
            "Sample after small gap should NOT have Transient flag");
        Assert.True(chain.IsWarmedUp(0),
            "Channel should still be warmed up after small gap");
    }

    [Fact]
    public void EegFilterChain_ManualResetClearsWarmup()
    {
        // Arrange
        var chain = new EegFilterChain();
        int warmupSamples = chain.WarmupSamples;
        long timestampUs = 0;
        const long sampleIntervalUs = 6250;

        // Warm up channel 0
        for (int i = 0; i < warmupSamples; i++)
        {
            chain.ProcessSampleUv(0, 10.0, timestampUs);
            timestampUs += sampleIntervalUs;
        }
        Assert.True(chain.IsWarmedUp(0), "Channel should be warmed up");

        // Act: Manual reset
        chain.ResetChannel(0);

        // Assert
        Assert.False(chain.IsWarmedUp(0), "Channel should NOT be warmed up after reset");
        Assert.Equal(0, chain.GetSamplesProcessed(0));
    }

    [Fact]
    public void EegFilterChain_ResetAllClearsAllChannels()
    {
        // Arrange
        var chain = new EegFilterChain();
        int warmupSamples = chain.WarmupSamples;
        long timestampUs = 0;
        const long sampleIntervalUs = 6250;

        // Warm up all channels
        for (int ch = 0; ch < chain.ChannelCount; ch++)
        {
            timestampUs = 0;
            for (int i = 0; i < warmupSamples; i++)
            {
                chain.ProcessSampleUv(ch, 10.0, timestampUs);
                timestampUs += sampleIntervalUs;
            }
            Assert.True(chain.IsWarmedUp(ch), $"Channel {ch} should be warmed up");
        }

        // Act: Reset all
        chain.ResetAll();

        // Assert
        for (int ch = 0; ch < chain.ChannelCount; ch++)
        {
            Assert.False(chain.IsWarmedUp(ch), $"Channel {ch} should NOT be warmed up after ResetAll");
            Assert.Equal(0, chain.GetSamplesProcessed(ch));
        }
    }

    #endregion

    #region Transient Output Behavior Tests

    [Fact]
    public void Filters_OutputDiffersDuringTransient()
    {
        // Arrange: Two filters, one fresh, one already warmed up
        var freshFilter = HighPassFilter.Create(HighPassCutoff.Hz0_5);
        var warmedFilter = HighPassFilter.Create(HighPassCutoff.Hz0_5);
        int warmupSamples = HighPassFilter.GetWarmupSamples(HighPassCutoff.Hz0_5);

        // Warm up one filter with constant input
        for (int i = 0; i < warmupSamples; i++)
        {
            warmedFilter.Process(50.0);
        }

        // Act: Compare outputs for same input
        double testInput = 50.0;
        double freshOutput = freshFilter.Process(testInput);
        double warmedOutput = warmedFilter.Process(testInput);

        // Assert: Outputs should be different (transient vs steady-state)
        Assert.NotEqual(freshOutput, warmedOutput);

        // Fresh filter should have larger transient response
        Assert.True(Math.Abs(freshOutput) > Math.Abs(warmedOutput) * 2,
            $"Fresh filter output ({freshOutput:F4}) should be significantly larger than warmed ({warmedOutput:F4})");
    }

    [Fact]
    public void Filters_ConvergeAfterWarmup()
    {
        // Arrange
        var filter = HighPassFilter.Create(HighPassCutoff.Hz0_5);
        int warmupSamples = HighPassFilter.GetWarmupSamples(HighPassCutoff.Hz0_5);
        double steadyInput = 50.0;

        // Act: Process warmup samples
        double[] outputs = new double[warmupSamples + 100];
        for (int i = 0; i < outputs.Length; i++)
        {
            outputs[i] = filter.Process(steadyInput);
        }

        // Assert: Output should converge to near-zero (HPF removes DC)
        double earlyOutput = Math.Abs(outputs[10]);
        double lateOutput = Math.Abs(outputs[warmupSamples + 50]);

        Assert.True(earlyOutput > 1.0, $"Early output ({earlyOutput:F4}) should be significant");
        Assert.True(lateOutput < 0.01, $"Late output ({lateOutput:F6}) should be near zero");
    }

    #endregion

    #region Timestamp Preservation Tests

    [Fact]
    public void EegFilterChain_PreservesTimestamp()
    {
        // Arrange
        var chain = new EegFilterChain();
        long inputTimestamp = 123456789L;

        // Act
        var result = chain.ProcessSampleUv(0, 50.0, inputTimestamp);

        // Assert: Timestamp should be exactly preserved
        Assert.Equal(inputTimestamp, result.TimestampUs);
    }

    [Fact]
    public void EegFilterChain_PreservesTimestampAfterGap()
    {
        // Arrange
        var chain = new EegFilterChain();
        chain.ProcessSampleUv(0, 50.0, 0);

        // Simulate gap
        long timestampAfterGap = 1_000_000;  // 1 second later

        // Act
        var result = chain.ProcessSampleUv(0, 50.0, timestampAfterGap);

        // Assert: Timestamp should still be preserved
        Assert.Equal(timestampAfterGap, result.TimestampUs);
    }

    #endregion

    #region Configuration Tests

    [Fact]
    public void EegFilterChainConfig_DefaultValues()
    {
        // Arrange & Act
        var config = EegFilterChainConfig.Default;

        // Assert: Verify defaults match DSP_SPEC.md §2.1
        Assert.Equal(NotchFrequency.Hz50, config.NotchFrequency);
        Assert.Equal(HighPassCutoff.Hz0_5, config.HighPassCutoff);
        Assert.Equal(LowPassCutoff.Hz35, config.LowPassCutoff);
        Assert.Equal(4, config.ChannelCount);
        Assert.Equal(160, config.SampleRate);
    }

    [Fact]
    public void EegFilterChain_WarmupSamplesMatchesHpf()
    {
        // Arrange & Act
        var chain = new EegFilterChain();
        int expectedWarmup = HighPassFilter.GetWarmupSamples(HighPassCutoff.Hz0_5);

        // Assert: Chain warmup should match HPF warmup (HPF is slowest)
        Assert.Equal(expectedWarmup, chain.WarmupSamples);
    }

    [Fact]
    public void EegFilterChain_CustomConfig_AffectsWarmup()
    {
        // Arrange
        var config = new EegFilterChainConfig
        {
            HighPassCutoff = HighPassCutoff.Hz0_3  // Slower HPF
        };

        // Act
        var chain = new EegFilterChain(config);
        int expectedWarmup = HighPassFilter.GetWarmupSamples(HighPassCutoff.Hz0_3);

        // Assert
        Assert.Equal(expectedWarmup, chain.WarmupSamples);
    }

    [Fact]
    public void EegFilterChain_DisabledNotch_StillWorks()
    {
        // Arrange
        var config = new EegFilterChainConfig
        {
            NotchFrequency = null  // Disable notch
        };
        var chain = new EegFilterChain(config);

        // Act
        var result = chain.ProcessSampleUv(0, 50.0, 0);

        // Assert: Should still produce valid output
        Assert.False(double.IsNaN(result.Value));
        Assert.False(double.IsInfinity(result.Value));
    }

    #endregion
}
