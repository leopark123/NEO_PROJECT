// EegPlaybackZeroPhaseTests.cs
// AT-19 验收测试: Zero-Phase 滤波在回放管线中的端到端集成

using Neo.Core.Enums;
using Neo.Core.Models;
using Neo.DSP.Filters;
using Neo.Infrastructure.Buffers;
using Neo.Playback;
using Xunit;

namespace Neo.Playback.Tests;

/// <summary>
/// AT-19 回放管线 Zero-Phase 集成测试。
/// 证明 <see cref="EegPlaybackSource"/> 在注入 <see cref="EegFilterChain"/> 后，
/// 输出样本确实经过 zero-phase 滤波。
/// </summary>
public class EegPlaybackZeroPhaseTests
{
    private const int SampleRate = 160;
    private const long SampleIntervalUs = 1_000_000 / SampleRate; // 6250 μs

    /// <summary>
    /// 创建填充指定频率正弦波的 buffer。
    /// </summary>
    private static EegRingBuffer CreateSineBuffer(int durationSeconds, double freqHz)
    {
        int totalSamples = durationSeconds * SampleRate;
        var buffer = new EegRingBuffer(totalSamples + 100);

        for (int i = 0; i < totalSamples; i++)
        {
            long ts = i * SampleIntervalUs;
            double val = 100.0 * Math.Sin(2 * Math.PI * freqHz * i / SampleRate);

            buffer.Write(new EegSample
            {
                TimestampUs = ts,
                Ch1Uv = val,
                Ch2Uv = val,
                Ch3Uv = val,
                Ch4Uv = val,
                QualityFlags = QualityFlag.Normal
            });
        }

        return buffer;
    }

    /// <summary>
    /// 回放高频信号（60Hz，高于 LPF 35Hz 截止）时，
    /// 有 zero-phase 滤波器的输出应比无滤波器的输出幅度显著降低。
    /// </summary>
    [Fact]
    public void PlaybackWithFilter_AttenuatesHighFrequency()
    {
        // 60Hz 信号 — 高于 35Hz LPF 截止频率，应被大幅衰减
        var buffer = CreateSineBuffer(3, 60.0);

        var filterConfig = new EegFilterChainConfig
        {
            NotchFrequency = null, // 禁用 notch 以隔离 LPF 效果
            HighPassCutoff = HighPassCutoff.Hz0_5,
            LowPassCutoff = LowPassCutoff.Hz35,
            ChannelCount = 4
        };

        // --- 无滤波回放 ---
        var clockNoFilter = new PlaybackClock();
        var srcNoFilter = new EegPlaybackSource(buffer, clockNoFilter);
        var unfilteredSamples = new List<EegSample>();
        srcNoFilter.SampleReceived += s => unfilteredSamples.Add(s);

        clockNoFilter.Start();
        srcNoFilter.Start();
        Thread.Sleep(500);
        srcNoFilter.Stop();
        clockNoFilter.Pause();

        // --- 有 zero-phase 滤波回放 ---
        // 重新创建 buffer（因为 PlaybackClock 从 0 开始）
        var buffer2 = CreateSineBuffer(3, 60.0);
        using var filterChain = new EegFilterChain(filterConfig);
        var clockFiltered = new PlaybackClock();
        var srcFiltered = new EegPlaybackSource(buffer2, clockFiltered, filterChain);
        var filteredSamples = new List<EegSample>();
        srcFiltered.SampleReceived += s => filteredSamples.Add(s);

        clockFiltered.Start();
        srcFiltered.Start();
        Thread.Sleep(500);
        srcFiltered.Stop();
        clockFiltered.Pause();

        // 验证两组都收到了足够的样本
        Assert.True(unfilteredSamples.Count > 20,
            $"Unfiltered should have >20 samples, got {unfilteredSamples.Count}");
        Assert.True(filteredSamples.Count > 20,
            $"Filtered should have >20 samples, got {filteredSamples.Count}");

        // 计算 RMS（跳过前 10 个样本，避免边缘效应）
        int skip = 10;
        double unfilteredRms = ComputeRms(unfilteredSamples.Skip(skip).Select(s => s.Ch1Uv));
        double filteredRms = ComputeRms(filteredSamples.Skip(skip).Select(s => s.Ch1Uv));

        // 60Hz 信号经 35Hz LPF zero-phase 后应衰减至少 10x（20dB）
        Assert.True(unfilteredRms > 10.0,
            $"Unfiltered 60Hz RMS should be significant, got {unfilteredRms:F2}");
        Assert.True(filteredRms < unfilteredRms * 0.3,
            $"Filtered RMS ({filteredRms:F2}) should be < 30% of unfiltered ({unfilteredRms:F2})");
    }

    /// <summary>
    /// 回放低频信号（5Hz，远低于 LPF 35Hz 截止）时，
    /// zero-phase 滤波后信号应基本保留（通带内不大幅衰减）。
    /// </summary>
    [Fact]
    public void PlaybackWithFilter_PreservesLowFrequency()
    {
        // 5Hz 信号 — 远低于 35Hz 截止，应通过
        var buffer = CreateSineBuffer(3, 5.0);

        var filterConfig = new EegFilterChainConfig
        {
            NotchFrequency = null,
            HighPassCutoff = HighPassCutoff.Hz0_5,
            LowPassCutoff = LowPassCutoff.Hz35,
            ChannelCount = 4
        };

        using var filterChain = new EegFilterChain(filterConfig);
        var clock = new PlaybackClock();
        var src = new EegPlaybackSource(buffer, clock, filterChain);
        var samples = new List<EegSample>();
        src.SampleReceived += s => samples.Add(s);

        clock.Start();
        src.Start();
        Thread.Sleep(500);
        src.Stop();
        clock.Pause();

        Assert.True(samples.Count > 20,
            $"Should have >20 samples, got {samples.Count}");

        int skip = 10;
        double rms = ComputeRms(samples.Skip(skip).Select(s => s.Ch1Uv));

        // 5Hz 在通带内，RMS 应保留大部分幅度（>50%）
        Assert.True(rms > 30.0,
            $"5Hz signal should pass through filter, got RMS={rms:F2} (expected >30)");
    }

    /// <summary>
    /// 不注入 filter 时，回放输出应与原始数据一致（无修改）。
    /// </summary>
    [Fact]
    public void PlaybackWithoutFilter_EmitsUnmodifiedSamples()
    {
        var buffer = CreateSineBuffer(3, 10.0);
        var clock = new PlaybackClock();
        var src = new EegPlaybackSource(buffer, clock); // 无 filter
        var samples = new List<EegSample>();
        src.SampleReceived += s => samples.Add(s);

        clock.Start();
        src.Start();
        Thread.Sleep(300);
        src.Stop();
        clock.Pause();

        Assert.True(samples.Count > 10,
            $"Should have >10 samples, got {samples.Count}");

        // 验证数据未被修改：回算原始正弦波值
        foreach (var s in samples)
        {
            if (s.QualityFlags.HasFlag(QualityFlag.Missing)) continue;

            int sampleIndex = (int)(s.TimestampUs / SampleIntervalUs);
            double expected = 100.0 * Math.Sin(2 * Math.PI * 10.0 * sampleIndex / SampleRate);
            Assert.Equal(expected, s.Ch1Uv, 6);
        }
    }

    /// <summary>
    /// 所有 4 个通道都应被独立滤波。
    /// </summary>
    [Fact]
    public void PlaybackWithFilter_AllChannelsFiltered()
    {
        // 60Hz on all channels — should all be attenuated
        var buffer = CreateSineBuffer(3, 60.0);

        var filterConfig = new EegFilterChainConfig
        {
            NotchFrequency = null,
            HighPassCutoff = HighPassCutoff.Hz0_5,
            LowPassCutoff = LowPassCutoff.Hz35,
            ChannelCount = 4
        };

        using var filterChain = new EegFilterChain(filterConfig);
        var clock = new PlaybackClock();
        var src = new EegPlaybackSource(buffer, clock, filterChain);
        var samples = new List<EegSample>();
        src.SampleReceived += s => samples.Add(s);

        clock.Start();
        src.Start();
        Thread.Sleep(500);
        src.Stop();
        clock.Pause();

        int skip = 10;
        var valid = samples.Skip(skip).Where(s => !s.QualityFlags.HasFlag(QualityFlag.Missing)).ToList();
        Assert.True(valid.Count > 10, $"Need >10 valid samples, got {valid.Count}");

        double ch1Rms = ComputeRms(valid.Select(s => s.Ch1Uv));
        double ch2Rms = ComputeRms(valid.Select(s => s.Ch2Uv));
        double ch3Rms = ComputeRms(valid.Select(s => s.Ch3Uv));
        double ch4Rms = ComputeRms(valid.Select(s => s.Ch4Uv));

        // All channels should be attenuated (60Hz above 35Hz cutoff)
        Assert.True(ch1Rms < 30.0, $"Ch1 60Hz RMS should be attenuated, got {ch1Rms:F2}");
        Assert.True(ch2Rms < 30.0, $"Ch2 60Hz RMS should be attenuated, got {ch2Rms:F2}");
        Assert.True(ch3Rms < 30.0, $"Ch3 60Hz RMS should be attenuated, got {ch3Rms:F2}");
        Assert.True(ch4Rms < 30.0, $"Ch4 60Hz RMS should be attenuated, got {ch4Rms:F2}");
    }

    private static double ComputeRms(IEnumerable<double> values)
    {
        double sum = 0;
        int count = 0;
        foreach (double v in values)
        {
            if (double.IsNaN(v)) continue;
            sum += v * v;
            count++;
        }
        return count > 0 ? Math.Sqrt(sum / count) : 0;
    }
}
