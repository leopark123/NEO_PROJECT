// GsMappingLogTests.cs
// GS 直方图 Log 区域映射测试 - 来源: DSP_SPEC.md §3.3

using Neo.DSP.GS;
using Xunit;

namespace Neo.DSP.Tests.GS;

/// <summary>
/// GS Bin 映射器 Log 区域测试。
/// </summary>
/// <remarks>
/// 验证规格:
/// - 10-200 μV: log10 映射 (130 bins, index 100-229)
/// - 边界处理正确
/// - >= 200 μV clamp 到 bin 229
/// </remarks>
public sealed class GsMappingLogTests
{
    [Fact]
    public void MapToBin_At10uV_ReturnsBin100()
    {
        int bin = GsBinMapper.MapToBin(10.0);
        Assert.Equal(100, bin);
    }

    [Fact]
    public void MapToBin_At200uV_ReturnsBin229()
    {
        int bin = GsBinMapper.MapToBin(200.0);
        Assert.Equal(229, bin);
    }

    [Fact]
    public void MapToBin_Above200uV_ClampsToBin229()
    {
        Assert.Equal(229, GsBinMapper.MapToBin(200.0));
        Assert.Equal(229, GsBinMapper.MapToBin(201.0));
        Assert.Equal(229, GsBinMapper.MapToBin(500.0));
        Assert.Equal(229, GsBinMapper.MapToBin(1000.0));
    }

    [Theory]
    [InlineData(10.0, 100)]
    [InlineData(20.0, 130)]   // log10(20) ≈ 1.301, (1.301-1)/1.301*130 ≈ 30, 100+30=130
    [InlineData(100.0, 199)]  // log10(100) = 2, (2-1)/1.301*130 ≈ 99, 100+99=199
    public void MapToBin_LogRegion_ApproximatelyCorrect(double voltageUv, int expectedBin)
    {
        int bin = GsBinMapper.MapToBin(voltageUv);
        // 允许 ±2 的误差（浮点计算）
        Assert.InRange(bin, expectedBin - 2, expectedBin + 2);
    }

    [Fact]
    public void MapToBin_JustAbove10uV_ReturnsBin100()
    {
        int bin = GsBinMapper.MapToBin(10.001);
        Assert.Equal(100, bin);
    }

    [Fact]
    public void MapToBin_JustBelow200uV_ReturnsBin229OrLess()
    {
        int bin = GsBinMapper.MapToBin(199.9);
        Assert.InRange(bin, 228, 229);
    }

    [Fact]
    public void LogRegion_StartsAtBin100()
    {
        double lower = GsBinMapper.GetBinLowerBound(100);
        Assert.Equal(10.0, lower, precision: 5);
    }

    [Fact]
    public void LogRegion_EndsAtBin229()
    {
        double upper = GsBinMapper.GetBinUpperBound(229);
        Assert.Equal(200.0, upper, precision: 5);
    }

    [Fact]
    public void LogRegion_BinWidthIncreases()
    {
        // Log 映射导致 bin 宽度随电压增加
        double width100 = GsBinMapper.GetBinUpperBound(100) - GsBinMapper.GetBinLowerBound(100);
        double width150 = GsBinMapper.GetBinUpperBound(150) - GsBinMapper.GetBinLowerBound(150);
        double width200 = GsBinMapper.GetBinUpperBound(200) - GsBinMapper.GetBinLowerBound(200);

        Assert.True(width150 > width100, "Bin width at 150 should be greater than at 100");
        Assert.True(width200 > width150, "Bin width at 200 should be greater than at 150");
    }

    [Fact]
    public void LogRegion_Bin100_ContainsOnly10uV()
    {
        // Bin 100 应该从 10 μV 开始
        double lower = GsBinMapper.GetBinLowerBound(100);
        Assert.Equal(10.0, lower, precision: 5);
    }

    [Fact]
    public void LogRegion_AllBins_HaveValidBounds()
    {
        for (int i = 100; i < 230; i++)
        {
            double lower = GsBinMapper.GetBinLowerBound(i);
            double upper = GsBinMapper.GetBinUpperBound(i);

            Assert.True(lower >= 10.0, $"Bin {i} lower bound {lower} should be >= 10");
            Assert.True(upper <= 200.0, $"Bin {i} upper bound {upper} should be <= 200");
            Assert.True(upper > lower, $"Bin {i} upper {upper} should be > lower {lower}");
        }
    }

    [Fact]
    public void LogRegion_BinBoundaries_Contiguous()
    {
        // 验证 log 区域 bin 边界连续
        for (int i = 100; i < 229; i++)
        {
            double thisUpper = GsBinMapper.GetBinUpperBound(i);
            double nextLower = GsBinMapper.GetBinLowerBound(i + 1);
            Assert.Equal(thisUpper, nextLower, precision: 8);
        }
    }

    [Fact]
    public void LinearLogTransition_Continuous()
    {
        // 线性区域 bin 99 的上界应等于 log 区域 bin 100 的下界
        double linearUpper = GsBinMapper.GetBinUpperBound(99);
        double logLower = GsBinMapper.GetBinLowerBound(100);

        Assert.Equal(linearUpper, logLower, precision: 10);
        Assert.Equal(10.0, linearUpper, precision: 10);
        Assert.Equal(10.0, logLower, precision: 10);
    }

    [Fact]
    public void RoundTrip_LogRegion_CorrectMapping()
    {
        // 验证往返一致性
        for (int expectedBin = 100; expectedBin < 230; expectedBin++)
        {
            double centerVoltage = GsBinMapper.GetBinCenterVoltage(expectedBin);
            int actualBin = GsBinMapper.MapToBin(centerVoltage);
            Assert.Equal(expectedBin, actualBin);
        }
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(230)]
    [InlineData(300)]
    public void GetBinCenterVoltage_InvalidIndex_Throws(int binIndex)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            GsBinMapper.GetBinCenterVoltage(binIndex));
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(230)]
    public void GetBinLowerBound_InvalidIndex_Throws(int binIndex)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            GsBinMapper.GetBinLowerBound(binIndex));
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(230)]
    public void GetBinUpperBound_InvalidIndex_Throws(int binIndex)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            GsBinMapper.GetBinUpperBound(binIndex));
    }

    [Fact]
    public void FullRange_230Bins_CoverZeroTo200uV()
    {
        // 验证 230 bins 完整覆盖 0-200 μV
        double firstLower = GsBinMapper.GetBinLowerBound(0);
        double lastUpper = GsBinMapper.GetBinUpperBound(229);

        Assert.Equal(0.0, firstLower, precision: 10);
        Assert.Equal(200.0, lastUpper, precision: 10);
    }
}
