// SemiLogLogSegmentTests.cs
// 对数段映射测试 (10-200 μV) - 来源: DSP_SPEC.md

using Neo.Rendering.Mapping;
using Xunit;

namespace Neo.Rendering.Tests.Mapping;

/// <summary>
/// 对数段映射测试 (10-200 μV)。
/// </summary>
/// <remarks>
/// 验证规格:
/// - 10-200 μV 映射到显示高度的上半区 (50%)
/// - 对数映射: log10(μV)
/// </remarks>
public sealed class SemiLogLogSegmentTests
{
    private const double TotalHeight = 1000.0;
    private const double LogHeight = 500.0;  // 50%
    private const double Tolerance = 0.5;    // 对数映射允许较大容差

    [Fact]
    public void LogHeightRatio_Is50Percent()
    {
        Assert.Equal(0.5, AeegSemiLogMapper.LogHeightRatio);
    }

    [Fact]
    public void Mapper_10uV_MapsToMiddle()
    {
        var mapper = new AeegSemiLogMapper(TotalHeight);
        double y = mapper.MapVoltageToY(10.0);

        // 10 μV → Y = totalHeight / 2 (中间，线性/对数分界)
        Assert.Equal(LogHeight, y, 0.001);
    }

    [Fact]
    public void Mapper_200uV_MapsToTop()
    {
        var mapper = new AeegSemiLogMapper(TotalHeight);
        double y = mapper.MapVoltageToY(200.0);

        // 200 μV → Y = 0 (顶部)
        Assert.Equal(0.0, y, 0.001);
    }

    [Fact]
    public void Mapper_100uV_MapsCorrectly()
    {
        var mapper = new AeegSemiLogMapper(TotalHeight);
        double y = mapper.MapVoltageToY(100.0);

        // log10(100) = 2
        // log10(10) = 1
        // log10(200) ≈ 2.301
        // normalized = (2 - 1) / (2.301 - 1) = 1 / 1.301 ≈ 0.769
        // Y = 500 * (1 - 0.769) = 500 * 0.231 ≈ 115.5
        Assert.True(y > 0 && y < LogHeight);
        Assert.True(y < 200);  // 100 μV 接近顶部
    }

    [Fact]
    public void Mapper_50uV_MapsCorrectly()
    {
        var mapper = new AeegSemiLogMapper(TotalHeight);
        double y = mapper.MapVoltageToY(50.0);

        // log10(50) ≈ 1.699
        // normalized = (1.699 - 1) / 1.301 ≈ 0.537
        // Y = 500 * (1 - 0.537) ≈ 231.5
        Assert.True(y > 0 && y < LogHeight);
        Assert.True(y > mapper.MapVoltageToY(100.0));  // 50 μV 在 100 μV 下方
    }

    [Fact]
    public void Mapper_25uV_MapsCorrectly()
    {
        var mapper = new AeegSemiLogMapper(TotalHeight);
        double y = mapper.MapVoltageToY(25.0);

        // log10(25) ≈ 1.398
        // normalized = (1.398 - 1) / 1.301 ≈ 0.306
        // Y = 500 * (1 - 0.306) ≈ 347
        Assert.True(y > 0 && y < LogHeight);
        Assert.True(y > mapper.MapVoltageToY(50.0));  // 25 μV 在 50 μV 下方
    }

    [Fact]
    public void Mapper_LogSegment_MonotonicDecreasing()
    {
        var mapper = new AeegSemiLogMapper(TotalHeight);

        // 验证 Y 随电压增加而减小（向顶部移动）
        double prevY = mapper.MapVoltageToY(10.0);
        double[] testVoltages = [15, 20, 25, 50, 100, 150, 200];

        foreach (double voltage in testVoltages)
        {
            double y = mapper.MapVoltageToY(voltage);
            Assert.True(y < prevY, $"Y should decrease as voltage increases. {voltage} μV: {y} vs prev: {prevY}");
            prevY = y;
        }
    }

    [Fact]
    public void Mapper_LogSegment_LogarithmicSpacing()
    {
        var mapper = new AeegSemiLogMapper(TotalHeight);

        // 验证对数间隔：每增加 10 倍电压，Y 变化相同
        double y10 = mapper.MapVoltageToY(10.0);
        double y100 = mapper.MapVoltageToY(100.0);

        // log10(10) = 1, log10(100) = 2
        // 1 个 decade 的间隔
        double decadeSpan = y10 - y100;

        // log10(200) ≈ 2.301，所以 100 到 200 是约 0.301 decade
        double y200 = mapper.MapVoltageToY(200.0);
        double halfDecadeSpan = y100 - y200;

        // 0.301 / 1.0 ≈ 0.301 的比例
        double expectedRatio = (Math.Log10(200) - Math.Log10(100)) / (Math.Log10(100) - Math.Log10(10));
        double actualRatio = halfDecadeSpan / decadeSpan;

        Assert.Equal(expectedRatio, actualRatio, 0.01);
    }

    [Fact]
    public void Mapper_LogSegment_InverseMapping()
    {
        var mapper = new AeegSemiLogMapper(TotalHeight);

        // 验证逆映射
        double[] testVoltages = [10, 15, 20, 25, 50, 75, 100, 150, 200];

        foreach (double voltage in testVoltages)
        {
            double y = mapper.MapVoltageToY(voltage);
            double backVoltage = mapper.MapYToVoltage(y);
            Assert.Equal(voltage, backVoltage, 0.1);  // 对数映射容差较大
        }
    }

    [Fact]
    public void Mapper_Above200uV_ClampsToTop()
    {
        var mapper = new AeegSemiLogMapper(TotalHeight);

        // 超过 200 μV 应 clamp 到顶部 (Y = 0)
        Assert.Equal(0.0, mapper.MapVoltageToY(200.0), 0.001);
        Assert.Equal(0.0, mapper.MapVoltageToY(250.0), 0.001);
        Assert.Equal(0.0, mapper.MapVoltageToY(1000.0), 0.001);
    }

    [Fact]
    public void Mapper_LogHeight_IsHalfTotal()
    {
        var mapper = new AeegSemiLogMapper(TotalHeight);
        Assert.Equal(TotalHeight * 0.5, mapper.LogHeightPx, 0.001);
    }

    [Fact]
    public void Mapper_PositiveInfinity_ClampsToTop()
    {
        var mapper = new AeegSemiLogMapper(TotalHeight);
        double y = mapper.MapVoltageToY(double.PositiveInfinity);

        // 正无穷应 clamp 到顶部
        Assert.Equal(0.0, y, 0.001);
    }

    [Fact]
    public void Mapper_NaN_ReturnsNaN()
    {
        var mapper = new AeegSemiLogMapper(TotalHeight);
        Assert.True(double.IsNaN(mapper.MapVoltageToY(double.NaN)));
    }
}
