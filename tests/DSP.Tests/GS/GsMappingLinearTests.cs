// GsMappingLinearTests.cs
// GS 直方图线性区域映射测试 - 来源: DSP_SPEC.md §3.3

using Neo.DSP.GS;
using Xunit;

namespace Neo.DSP.Tests.GS;

/// <summary>
/// GS Bin 映射器线性区域测试。
/// </summary>
/// <remarks>
/// 验证规格:
/// - 0-10 μV: 线性映射 (100 bins, index 0-99)
/// - 每 0.1 μV 对应一个 bin
/// - 边界处理正确
/// </remarks>
public sealed class GsMappingLinearTests
{
    [Fact]
    public void TotalBins_Is230()
    {
        Assert.Equal(230, GsBinMapper.TotalBins);
    }

    [Fact]
    public void LinearBins_Is100()
    {
        Assert.Equal(100, GsBinMapper.LinearBins);
    }

    [Fact]
    public void LogBins_Is130()
    {
        Assert.Equal(130, GsBinMapper.LogBins);
    }

    [Fact]
    public void MapToBin_ZeroVolts_ReturnsBin0()
    {
        int bin = GsBinMapper.MapToBin(0.0);
        Assert.Equal(0, bin);
    }

    [Fact]
    public void MapToBin_Negative_ReturnsInvalid()
    {
        int bin = GsBinMapper.MapToBin(-0.1);
        Assert.Equal(GsBinMapper.InvalidBin, bin);
    }

    [Fact]
    public void MapToBin_NegativeLarge_ReturnsInvalid()
    {
        int bin = GsBinMapper.MapToBin(-100.0);
        Assert.Equal(GsBinMapper.InvalidBin, bin);
    }

    [Theory]
    [InlineData(0.0, 0)]
    [InlineData(0.05, 0)]
    [InlineData(0.09, 0)]
    [InlineData(0.1, 1)]
    [InlineData(0.5, 5)]
    [InlineData(1.0, 10)]
    [InlineData(5.0, 50)]
    [InlineData(9.0, 90)]
    [InlineData(9.9, 99)]
    [InlineData(9.99, 99)]
    public void MapToBin_LinearRegion_CorrectBin(double voltageUv, int expectedBin)
    {
        int bin = GsBinMapper.MapToBin(voltageUv);
        Assert.Equal(expectedBin, bin);
    }

    [Fact]
    public void MapToBin_At10uV_ReturnsBin100()
    {
        // 10 μV 是线性/log 分界点，应该映射到 bin 100（log 区域起始）
        int bin = GsBinMapper.MapToBin(10.0);
        Assert.Equal(100, bin);
    }

    [Fact]
    public void MapToBin_JustBelow10uV_ReturnsBin99()
    {
        int bin = GsBinMapper.MapToBin(9.999);
        Assert.Equal(99, bin);
    }

    [Fact]
    public void LinearRegion_EachBinCovers0Point1uV()
    {
        // 验证每个 bin 覆盖 0.1 μV
        for (int i = 0; i < 99; i++)
        {
            double lower = GsBinMapper.GetBinLowerBound(i);
            double upper = GsBinMapper.GetBinUpperBound(i);
            double width = upper - lower;
            Assert.Equal(0.1, width, precision: 10);
        }
    }

    [Fact]
    public void LinearRegion_BinBoundaries_Contiguous()
    {
        // 验证 bin 边界连续无间隙
        for (int i = 0; i < 99; i++)
        {
            double thisUpper = GsBinMapper.GetBinUpperBound(i);
            double nextLower = GsBinMapper.GetBinLowerBound(i + 1);
            Assert.Equal(thisUpper, nextLower, precision: 10);
        }
    }

    [Fact]
    public void LinearRegion_Bin0_LowerBoundIsZero()
    {
        double lower = GsBinMapper.GetBinLowerBound(0);
        Assert.Equal(0.0, lower, precision: 10);
    }

    [Fact]
    public void LinearRegion_Bin99_UpperBoundIs10uV()
    {
        double upper = GsBinMapper.GetBinUpperBound(99);
        Assert.Equal(10.0, upper, precision: 10);
    }

    [Fact]
    public void GetBinCenterVoltage_Bin0_ReturnsPoint05()
    {
        double center = GsBinMapper.GetBinCenterVoltage(0);
        Assert.Equal(0.05, center, precision: 10);
    }

    [Fact]
    public void GetBinCenterVoltage_Bin50_Returns5Point05()
    {
        double center = GsBinMapper.GetBinCenterVoltage(50);
        Assert.Equal(5.05, center, precision: 10);
    }

    [Fact]
    public void GetBinCenterVoltage_Bin99_Returns9Point95()
    {
        double center = GsBinMapper.GetBinCenterVoltage(99);
        Assert.Equal(9.95, center, precision: 10);
    }

    [Fact]
    public void RoundTrip_LinearRegion_CorrectMapping()
    {
        // 验证往返一致性：电压 → bin → 中心电压 应在同一 bin
        for (int expectedBin = 0; expectedBin < 100; expectedBin++)
        {
            double centerVoltage = GsBinMapper.GetBinCenterVoltage(expectedBin);
            int actualBin = GsBinMapper.MapToBin(centerVoltage);
            Assert.Equal(expectedBin, actualBin);
        }
    }
}
