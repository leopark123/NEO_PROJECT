// GsCounterBehaviorTests.cs
// GS 直方图 Counter 行为测试 - 来源: DSP_SPEC.md §3.3, §1.4

using Neo.Core.Enums;
using Neo.DSP.GS;
using Xunit;

namespace Neo.DSP.Tests.GS;

/// <summary>
/// GS Counter 语义测试。
/// </summary>
/// <remarks>
/// 验证规格:
/// - Counter 0-228: 累计中
/// - Counter 229: 本帧结束 (flush)
/// - Counter 255: 忽略 (不计入)
/// - Counter 来源: 设备 data[16]
/// </remarks>
public sealed class GsCounterBehaviorTests
{
    [Fact]
    public void CounterEndOfCycle_Is229()
    {
        Assert.Equal(229, GsFrame.CounterEndOfCycle);
    }

    [Fact]
    public void CounterIgnore_Is255()
    {
        Assert.Equal(255, GsFrame.CounterIgnore);
    }

    [Fact]
    public void AccumulatorCounterEndOfCycle_Is229()
    {
        Assert.Equal(229, GsHistogramAccumulator.CounterEndOfCycle);
    }

    [Fact]
    public void AccumulatorCounterIgnore_Is255()
    {
        Assert.Equal(255, GsHistogramAccumulator.CounterIgnore);
    }

    [Fact]
    public void PeriodSeconds_Is15()
    {
        Assert.Equal(15.0, GsFrame.PeriodSeconds);
    }

    [Fact]
    public void PeriodUs_Is15Million()
    {
        Assert.Equal(15_000_000, GsFrame.PeriodUs);
    }

    [Fact]
    public void BinCount_Is230()
    {
        Assert.Equal(230, GsFrame.BinCount);
    }

    // ============================================
    // Counter=255 忽略行为测试
    // ============================================

    [Fact]
    public void Counter255_SampleIgnored_NoAccumulation()
    {
        var accumulator = new GsHistogramAccumulator(channelIndex: 0);

        // 发送 counter=255 的样本
        bool hasOutput = accumulator.AccumulateSample(
            minUv: 5.0,
            maxUv: 10.0,
            timestampUs: 0,
            quality: QualityFlag.Normal,
            counter: 255,
            out var frame);

        Assert.False(hasOutput);
        Assert.Null(frame);
        Assert.Equal(0, accumulator.SamplesInCurrentFrame);  // 未累计
    }

    [Fact]
    public void Counter255_MultipleSamples_AllIgnored()
    {
        var accumulator = new GsHistogramAccumulator(channelIndex: 0);

        // 发送多个 counter=255 的样本
        for (int i = 0; i < 20; i++)
        {
            bool hasOutput = accumulator.AccumulateSample(
                minUv: 5.0,
                maxUv: 10.0,
                timestampUs: i * 1_000_000,
                quality: QualityFlag.Normal,
                counter: 255,
                out _);

            Assert.False(hasOutput);
        }

        Assert.Equal(0, accumulator.SamplesInCurrentFrame);
    }

    [Fact]
    public void Counter255_DoesNotAffectPendingFrame()
    {
        var accumulator = new GsHistogramAccumulator(channelIndex: 0);
        long timestampUs = 0;

        // 累计 5 个正常样本
        for (int i = 0; i < 5; i++)
        {
            accumulator.AccumulateSample(5.0, 10.0, timestampUs, QualityFlag.Normal,
                counter: (byte)i, out _);
            timestampUs += 1_000_000;
        }

        Assert.Equal(5, accumulator.SamplesInCurrentFrame);

        // 发送 counter=255 的样本
        accumulator.AccumulateSample(5.0, 10.0, timestampUs, QualityFlag.Normal,
            counter: 255, out _);

        // 计数不应变化
        Assert.Equal(5, accumulator.SamplesInCurrentFrame);
    }

    // ============================================
    // Counter=229 帧结束行为测试
    // ============================================

    [Fact]
    public void Counter229_TriggersFrameOutput()
    {
        var accumulator = new GsHistogramAccumulator(channelIndex: 0);

        // 只发送一个样本，但 counter=229
        bool hasOutput = accumulator.AccumulateSample(
            minUv: 5.0,
            maxUv: 10.0,
            timestampUs: 0,
            quality: QualityFlag.Normal,
            counter: 229,
            out var frame);

        Assert.True(hasOutput, "Counter=229 should trigger frame output");
        Assert.NotNull(frame);
        Assert.True(frame!.IsComplete);
    }

    [Fact]
    public void Counter229_FrameContainsAccumulatedData()
    {
        var accumulator = new GsHistogramAccumulator(channelIndex: 0);
        long timestampUs = 0;

        // 累计 10 个样本（counter=0-9）
        for (int i = 0; i < 10; i++)
        {
            accumulator.AccumulateSample(5.0, 10.0, timestampUs, QualityFlag.Normal,
                counter: (byte)i, out _);
            timestampUs += 1_000_000;
        }

        // 发送 counter=229 触发帧输出
        bool hasOutput = accumulator.AccumulateSample(5.0, 10.0, timestampUs, QualityFlag.Normal,
            counter: 229, out var frame);

        Assert.True(hasOutput);
        Assert.NotNull(frame);
        Assert.Equal(11 * 2, frame!.SampleCount);  // 11 个 min/max 对 = 22 个 bin 累计
    }

    [Fact]
    public void Counter229_ResetsAccumulatorAfterOutput()
    {
        var accumulator = new GsHistogramAccumulator(channelIndex: 0);

        // 累计几个样本然后 counter=229
        for (int i = 0; i < 5; i++)
        {
            accumulator.AccumulateSample(5.0, 10.0, i * 1_000_000, QualityFlag.Normal,
                counter: (byte)i, out _);
        }
        accumulator.AccumulateSample(5.0, 10.0, 5_000_000, QualityFlag.Normal,
            counter: 229, out _);

        // 帧完成后计数应重置
        Assert.Equal(0, accumulator.SamplesInCurrentFrame);
    }

    [Fact]
    public void Counter229_TimestampsCorrect()
    {
        var accumulator = new GsHistogramAccumulator(channelIndex: 0);
        long startTimestamp = 1_000_000;
        long timestampUs = startTimestamp;

        // 累计样本
        for (int i = 0; i < 14; i++)
        {
            accumulator.AccumulateSample(5.0, 10.0, timestampUs, QualityFlag.Normal,
                counter: (byte)i, out _);
            timestampUs += 1_000_000;
        }

        // counter=229 触发帧输出
        accumulator.AccumulateSample(5.0, 10.0, timestampUs, QualityFlag.Normal,
            counter: 229, out var frame);

        Assert.NotNull(frame);
        Assert.Equal(startTimestamp, frame!.StartTimestampUs);
        Assert.Equal(timestampUs, frame.EndTimestampUs);
    }

    // ============================================
    // Counter=0-228 累计行为测试
    // ============================================

    [Fact]
    public void Counter0To228_AccumulatesNormally()
    {
        var accumulator = new GsHistogramAccumulator(channelIndex: 0);

        // 发送 counter=0 到 228 的样本，不应输出帧
        for (byte counter = 0; counter < 229; counter++)
        {
            bool hasOutput = accumulator.AccumulateSample(
                minUv: 5.0,
                maxUv: 10.0,
                timestampUs: counter * 1_000_000,
                quality: QualityFlag.Normal,
                counter: counter,
                out var frame);

            Assert.False(hasOutput, $"Counter={counter} should not trigger output");
            Assert.Null(frame);
        }

        Assert.Equal(229, accumulator.SamplesInCurrentFrame);
    }

    [Fact]
    public void Counter_SequentialCycle_MultipleFrames()
    {
        var accumulator = new GsHistogramAccumulator(channelIndex: 0);
        int frameCount = 0;
        long timestampUs = 0;

        // 模拟 3 个完整周期
        for (int cycle = 0; cycle < 3; cycle++)
        {
            // counter 0-228
            for (byte counter = 0; counter < 229; counter++)
            {
                accumulator.AccumulateSample(5.0, 10.0, timestampUs, QualityFlag.Normal,
                    counter, out _);
                timestampUs += 1_000_000;
            }

            // counter=229 结束周期
            if (accumulator.AccumulateSample(5.0, 10.0, timestampUs, QualityFlag.Normal,
                counter: 229, out _))
            {
                frameCount++;
            }
            timestampUs += 1_000_000;
        }

        Assert.Equal(3, frameCount);
    }

    // ============================================
    // Processor 测试
    // ============================================

    [Fact]
    public void Processor_Counter229_OutputsFrame()
    {
        var processor = new GsProcessor();

        // 发送几个样本后 counter=229
        for (byte counter = 0; counter < 10; counter++)
        {
            processor.ProcessAeegOutput(0, 5.0, 10.0, counter * 1_000_000,
                QualityFlag.Normal, counter, out _);
        }

        bool hasOutput = processor.ProcessAeegOutput(0, 5.0, 10.0, 10_000_000,
            QualityFlag.Normal, counter: 229, out var gsOutput);

        Assert.True(hasOutput);
        Assert.NotNull(gsOutput.Frame);
    }

    [Fact]
    public void Processor_Counter255_NoOutput()
    {
        var processor = new GsProcessor();

        // 只发送 counter=255
        bool hasOutput = processor.ProcessAeegOutput(0, 5.0, 10.0, 0,
            QualityFlag.Normal, counter: 255, out _);

        Assert.False(hasOutput);
        Assert.Equal(0, processor.GetSamplesInCurrentFrame(0));
    }

    [Fact]
    public void Processor_MultiChannel_IndependentCounter()
    {
        var processor = new GsProcessor();
        long timestampUs = 0;

        // 通道 0：累计 5 个样本
        for (byte counter = 0; counter < 5; counter++)
        {
            processor.ProcessAeegOutput(0, 5.0, 10.0, timestampUs,
                QualityFlag.Normal, counter, out _);
            timestampUs += 1_000_000;
        }

        // 通道 1：发送 counter=229 立即结束
        processor.ProcessAeegOutput(1, 5.0, 10.0, timestampUs,
            QualityFlag.Normal, counter: 229, out var frame1);

        // 通道 0：继续累计
        processor.ProcessAeegOutput(0, 5.0, 10.0, timestampUs,
            QualityFlag.Normal, counter: 5, out _);

        // 通道 0 应有 6 个样本，通道 1 已完成并重置
        Assert.Equal(6, processor.GetSamplesInCurrentFrame(0));
        Assert.Equal(0, processor.GetSamplesInCurrentFrame(1));
    }

    // ============================================
    // Quality 累计测试
    // ============================================

    [Fact]
    public void Counter229_QualityAccumulated()
    {
        var accumulator = new GsHistogramAccumulator(channelIndex: 0);

        // 第一个样本正常
        accumulator.AccumulateSample(5.0, 10.0, 0, QualityFlag.Normal,
            counter: 0, out _);

        // 第二个样本有 Missing 标志
        accumulator.AccumulateSample(5.0, 10.0, 1_000_000, QualityFlag.Missing,
            counter: 1, out _);

        // counter=229 结束
        accumulator.AccumulateSample(5.0, 10.0, 2_000_000, QualityFlag.Normal,
            counter: 229, out var frame);

        Assert.NotNull(frame);
        Assert.True((frame!.Quality & QualityFlag.Missing) != 0);
    }

    // ============================================
    // FlushIncomplete 测试
    // ============================================

    [Fact]
    public void FlushIncomplete_ReturnsPartialFrame()
    {
        var accumulator = new GsHistogramAccumulator(channelIndex: 0);
        long timestampUs = 0;

        // 累计 10 个样本（不使用 counter=229）
        for (byte counter = 0; counter < 10; counter++)
        {
            accumulator.AccumulateSample(5.0, 10.0, timestampUs, QualityFlag.Normal,
                counter, out _);
            timestampUs += 1_000_000;
        }

        var frame = accumulator.FlushIncomplete(timestampUs);

        Assert.NotNull(frame);
        Assert.Equal(10 * 2, frame!.SampleCount);
        Assert.True((frame.Quality & QualityFlag.Transient) != 0);
    }

    [Fact]
    public void FlushIncomplete_NoData_ReturnsNull()
    {
        var accumulator = new GsHistogramAccumulator(channelIndex: 0);
        var frame = accumulator.FlushIncomplete(0);
        Assert.Null(frame);
    }

    [Fact]
    public void FlushIncomplete_AfterCounter255Only_ReturnsNull()
    {
        var accumulator = new GsHistogramAccumulator(channelIndex: 0);

        // 只发送 counter=255
        for (int i = 0; i < 10; i++)
        {
            accumulator.AccumulateSample(5.0, 10.0, i * 1_000_000, QualityFlag.Normal,
                counter: 255, out _);
        }

        var frame = accumulator.FlushIncomplete(10_000_000);
        Assert.Null(frame);  // 无数据，因为全被忽略
    }

    // ============================================
    // 边界情况测试
    // ============================================

    [Fact]
    public void Counter228_DoesNotTriggerOutput()
    {
        var accumulator = new GsHistogramAccumulator(channelIndex: 0);

        bool hasOutput = accumulator.AccumulateSample(5.0, 10.0, 0, QualityFlag.Normal,
            counter: 228, out _);

        Assert.False(hasOutput, "Counter=228 should not trigger output");
    }

    [Fact]
    public void Counter229_ImmediatelyAfterReset_OutputsFrame()
    {
        var accumulator = new GsHistogramAccumulator(channelIndex: 0);

        // 立即发送 counter=229
        bool hasOutput = accumulator.AccumulateSample(5.0, 10.0, 0, QualityFlag.Normal,
            counter: 229, out var frame);

        Assert.True(hasOutput);
        Assert.NotNull(frame);
        Assert.Equal(2, frame!.SampleCount);  // 1 个 min/max 对 = 2 次累计
    }

    [Fact]
    public void ChannelIndex_PreservedInFrame()
    {
        for (int ch = 0; ch < 4; ch++)
        {
            var accumulator = new GsHistogramAccumulator(channelIndex: ch);

            accumulator.AccumulateSample(5.0, 10.0, 0, QualityFlag.Normal,
                counter: 229, out var frame);

            Assert.NotNull(frame);
            Assert.Equal(ch, frame!.ChannelIndex);
        }
    }
}
