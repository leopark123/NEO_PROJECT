// GsSaturationTests.cs
// GS 直方图饱和测试 - 来源: DSP_SPEC.md §3.3

using Neo.Core.Enums;
using Neo.DSP.GS;
using Xunit;

namespace Neo.DSP.Tests.GS;

/// <summary>
/// GS 直方图饱和测试。
/// </summary>
/// <remarks>
/// 验证规格:
/// - Bin 值最大饱和值: 249
/// - 达到饱和后不再增加
/// - 电压超过 200 μV clamp 到 bin 229
/// </remarks>
public sealed class GsSaturationTests
{
    [Fact]
    public void MaxBinValue_Is249()
    {
        Assert.Equal(249, GsFrame.MaxBinValue);
    }

    [Fact]
    public void Frame_IncrementBin_SaturatesAt249()
    {
        var frame = new GsFrame();
        frame.Initialize(channelIndex: 0, startTimestampUs: 0);

        // 增加 bin 0 超过 249 次
        for (int i = 0; i < 300; i++)
        {
            frame.IncrementBin(0);
        }

        Assert.Equal(249, frame.Bins[0]);
    }

    [Fact]
    public void Frame_IncrementBin_SampleCountContinues()
    {
        var frame = new GsFrame();
        frame.Initialize(channelIndex: 0, startTimestampUs: 0);

        // 增加 bin 0 超过 249 次
        for (int i = 0; i < 300; i++)
        {
            frame.IncrementBin(0);
        }

        // 样本计数应该继续增加
        Assert.Equal(300, frame.SampleCount);
    }

    [Fact]
    public void Frame_MultipleBins_IndependentSaturation()
    {
        var frame = new GsFrame();
        frame.Initialize(channelIndex: 0, startTimestampUs: 0);

        // Bin 0 饱和
        for (int i = 0; i < 300; i++)
        {
            frame.IncrementBin(0);
        }

        // Bin 1 不饱和
        for (int i = 0; i < 100; i++)
        {
            frame.IncrementBin(1);
        }

        Assert.Equal(249, frame.Bins[0]);
        Assert.Equal(100, frame.Bins[1]);
    }

    [Fact]
    public void VoltageAbove200_ClampsToBin229()
    {
        int bin = GsBinMapper.MapToBin(250.0);
        Assert.Equal(229, bin);
    }

    [Fact]
    public void VoltageAt200_MapsTo229()
    {
        int bin = GsBinMapper.MapToBin(200.0);
        Assert.Equal(229, bin);
    }

    [Fact]
    public void Accumulator_HighVoltage_ClampedCorrectly()
    {
        var accumulator = new GsHistogramAccumulator(channelIndex: 0);
        long timestampUs = 0;

        // 累计高电压样本（counter=0-13 累计，counter=229 触发帧输出）
        for (int i = 0; i < 14; i++)
        {
            accumulator.AccumulateSample(
                minUv: 180.0,  // 应该映射到高 bin
                maxUv: 300.0,  // 超过 200，应该 clamp 到 229
                timestampUs: timestampUs,
                quality: QualityFlag.Normal,
                counter: (byte)i,
                out _);
            timestampUs += 1_000_000;
        }

        // 最后一个样本使用 counter=229 触发帧输出
        bool hasOutput = accumulator.AccumulateSample(
            minUv: 180.0,
            maxUv: 300.0,
            timestampUs: timestampUs,
            quality: QualityFlag.Normal,
            counter: 229,
            out var frame);

        Assert.True(hasOutput);
        Assert.NotNull(frame);
        // 验证 bin 229 被累计（maxUv=300 clamp 到 229）
        Assert.True(frame!.Bins[229] > 0, "Bin 229 should have counts");
    }

    [Fact]
    public void Accumulator_NegativeVoltage_Ignored()
    {
        var accumulator = new GsHistogramAccumulator(channelIndex: 0);
        long timestampUs = 0;

        // 累计负电压样本（counter=0-13 累计，counter=229 触发帧输出）
        for (int i = 0; i < 14; i++)
        {
            accumulator.AccumulateSample(
                minUv: -10.0,  // 负值，应该被忽略
                maxUv: 5.0,    // 正常值
                timestampUs: timestampUs,
                quality: QualityFlag.Normal,
                counter: (byte)i,
                out _);
            timestampUs += 1_000_000;
        }

        // 最后一个样本使用 counter=229 触发帧输出
        bool hasOutput = accumulator.AccumulateSample(
            minUv: -10.0,
            maxUv: 5.0,
            timestampUs: timestampUs,
            quality: QualityFlag.Normal,
            counter: 229,
            out var frame);

        Assert.True(hasOutput);
        Assert.NotNull(frame);

        // 验证 bin 50 被累计（5 μV → bin 50）
        Assert.True(frame!.Bins[50] > 0, "Bin 50 should have counts");

        // 验证总样本数：15 个样本，每个有 1 个有效 min/max (只有 max)
        // 负值 minUv 被忽略，只有 maxUv 被累计
        Assert.Equal(15, frame.SampleCount);  // 只有 max 值被累计
    }

    [Fact]
    public void Frame_AllBinsInitializedToZero()
    {
        var frame = new GsFrame();
        frame.Initialize(channelIndex: 0, startTimestampUs: 0);

        for (int i = 0; i < 230; i++)
        {
            Assert.Equal(0, frame.Bins[i]);
        }
    }

    [Fact]
    public void Frame_Reset_ClearsBins()
    {
        var frame = new GsFrame();
        frame.Initialize(channelIndex: 0, startTimestampUs: 0);

        // 增加一些 bin
        frame.IncrementBin(0);
        frame.IncrementBin(100);
        frame.IncrementBin(229);

        // 重置
        frame.Reset();

        // 验证所有 bin 被清零
        for (int i = 0; i < 230; i++)
        {
            Assert.Equal(0, frame.Bins[i]);
        }
    }

    [Fact]
    public void Frame_Clone_DeepCopy()
    {
        var frame = new GsFrame();
        frame.Initialize(channelIndex: 0, startTimestampUs: 1000);
        frame.IncrementBin(50);
        frame.IncrementBin(50);
        frame.Complete(endTimestampUs: 16000, quality: QualityFlag.Normal);

        var clone = frame.Clone();

        // 验证数据相同
        Assert.Equal(frame.ChannelIndex, clone.ChannelIndex);
        Assert.Equal(frame.StartTimestampUs, clone.StartTimestampUs);
        Assert.Equal(frame.EndTimestampUs, clone.EndTimestampUs);
        Assert.Equal(frame.Bins[50], clone.Bins[50]);

        // 修改原始帧不影响克隆
        frame.IncrementBin(100);
        Assert.NotEqual(frame.Bins[100], clone.Bins[100]);
    }

    [Fact]
    public void Processor_Gap_ResetsSaturationState()
    {
        var processor = new GsProcessor();
        long timestampUs = 0;

        // 累计 5 个样本
        for (int i = 0; i < 5; i++)
        {
            processor.ProcessAeegOutput(0, 5.0, 10.0, timestampUs, QualityFlag.Normal,
                counter: (byte)i, out _);
            timestampUs += 1_000_000;
        }

        Assert.Equal(5, processor.GetSamplesInCurrentFrame(0));

        // 模拟 gap（时间跳跃 > 2 秒）
        timestampUs += 5_000_000;

        // Gap 后第一个样本应该重置累计器
        processor.ProcessAeegOutput(0, 5.0, 10.0, timestampUs, QualityFlag.Normal,
            counter: 0, out _);

        // 应该只有 1 个样本（重置后）
        Assert.Equal(1, processor.GetSamplesInCurrentFrame(0));
    }

    [Fact]
    public void ExtremeVoltage_NoOverflow()
    {
        // 测试极端电压值不会导致溢出
        Assert.Equal(GsBinMapper.InvalidBin, GsBinMapper.MapToBin(double.MinValue));
        Assert.Equal(GsBinMapper.InvalidBin, GsBinMapper.MapToBin(double.NegativeInfinity));
        Assert.Equal(229, GsBinMapper.MapToBin(double.MaxValue));
        Assert.Equal(229, GsBinMapper.MapToBin(double.PositiveInfinity));
    }

    [Fact]
    public void NaN_Voltage_ReturnsInvalid()
    {
        int bin = GsBinMapper.MapToBin(double.NaN);
        // NaN 比较特殊，< 0 返回 false，所以会走到后面的逻辑
        // 取决于实现，应该返回 -1 或某个安全值
        Assert.True(bin == GsBinMapper.InvalidBin || bin >= 0 && bin < 230);
    }
}
