// SemiLogLinearSegmentTests.cs
// 线性段映射测试 (0-10 μV) - 来源: DSP_SPEC.md

using Neo.Rendering.Mapping;
using Xunit;

namespace Neo.Rendering.Tests.Mapping;

/// <summary>
/// 线性段映射测试 (0-10 μV)。
/// </summary>
/// <remarks>
/// 验证规格:
/// - 0-10 μV 映射到显示高度的下半区 (50%)
/// - 线性映射: 相同 μV 间隔 → 相同像素间隔
/// </remarks>
public sealed class SemiLogLinearSegmentTests
{
    private const double TotalHeight = 1000.0;
    private const double LinearHeight = 500.0;  // 50%
    private const double Tolerance = 0.001;

    [Fact]
    public void LinearHeightRatio_Is50Percent()
    {
        Assert.Equal(0.5, AeegSemiLogMapper.LinearHeightRatio);
    }

    [Fact]
    public void Mapper_0uV_MapsToBottom()
    {
        var mapper = new AeegSemiLogMapper(TotalHeight);
        double y = mapper.MapVoltageToY(0.0);

        // 0 μV → Y = totalHeight (底部)
        Assert.Equal(TotalHeight, y, Tolerance);
    }

    [Fact]
    public void Mapper_10uV_MapsToMiddle()
    {
        var mapper = new AeegSemiLogMapper(TotalHeight);
        double y = mapper.MapVoltageToY(10.0);

        // 10 μV → Y = totalHeight / 2 (中间，线性/对数分界)
        Assert.Equal(TotalHeight / 2, y, Tolerance);
    }

    [Fact]
    public void Mapper_5uV_MapsToLinearMidpoint()
    {
        var mapper = new AeegSemiLogMapper(TotalHeight);
        double y = mapper.MapVoltageToY(5.0);

        // 5 μV → Y = 750 (线性段中点)
        // 线性段: 500 到 1000，中点是 750
        double expected = TotalHeight - (0.5 * LinearHeight);  // 1000 - 250 = 750
        Assert.Equal(expected, y, Tolerance);
    }

    [Theory]
    [InlineData(0.0, 1000.0)]
    [InlineData(1.0, 950.0)]
    [InlineData(2.0, 900.0)]
    [InlineData(3.0, 850.0)]
    [InlineData(4.0, 800.0)]
    [InlineData(5.0, 750.0)]
    [InlineData(6.0, 700.0)]
    [InlineData(7.0, 650.0)]
    [InlineData(8.0, 600.0)]
    [InlineData(9.0, 550.0)]
    [InlineData(10.0, 500.0)]
    public void Mapper_LinearSegment_CorrectMapping(double voltageUv, double expectedY)
    {
        var mapper = new AeegSemiLogMapper(TotalHeight);
        double y = mapper.MapVoltageToY(voltageUv);
        Assert.Equal(expectedY, y, Tolerance);
    }

    [Fact]
    public void Mapper_LinearSegment_EqualSpacing()
    {
        var mapper = new AeegSemiLogMapper(TotalHeight);

        // 验证线性段内间隔相等
        double y0 = mapper.MapVoltageToY(0.0);
        double y5 = mapper.MapVoltageToY(5.0);
        double y10 = mapper.MapVoltageToY(10.0);

        double delta1 = y0 - y5;   // 0→5 的间隔
        double delta2 = y5 - y10;  // 5→10 的间隔

        Assert.Equal(delta1, delta2, Tolerance);
    }

    [Fact]
    public void Mapper_LinearSegment_InverseMapping()
    {
        var mapper = new AeegSemiLogMapper(TotalHeight);

        // 验证逆映射
        for (double voltage = 0.0; voltage <= 10.0; voltage += 1.0)
        {
            double y = mapper.MapVoltageToY(voltage);
            double backVoltage = mapper.MapYToVoltage(y);
            Assert.Equal(voltage, backVoltage, Tolerance);
        }
    }

    [Fact]
    public void Mapper_LinearSegment_DifferentHeights()
    {
        // 验证不同总高度下的映射正确性
        double[] heights = [100, 500, 1000, 2000];

        foreach (double height in heights)
        {
            var mapper = new AeegSemiLogMapper(height);

            // 0 μV → 底部
            Assert.Equal(height, mapper.MapVoltageToY(0.0), Tolerance);

            // 10 μV → 中间
            Assert.Equal(height / 2, mapper.MapVoltageToY(10.0), Tolerance);
        }
    }

    [Fact]
    public void Mapper_LinearHeight_IsHalfTotal()
    {
        var mapper = new AeegSemiLogMapper(TotalHeight);
        Assert.Equal(TotalHeight * 0.5, mapper.LinearHeightPx, Tolerance);
    }

    [Fact]
    public void Mapper_NegativeVoltage_ReturnsNaN()
    {
        var mapper = new AeegSemiLogMapper(TotalHeight);

        Assert.True(double.IsNaN(mapper.MapVoltageToY(-1.0)));
        Assert.True(double.IsNaN(mapper.MapVoltageToY(-0.001)));
        Assert.True(double.IsNaN(mapper.MapVoltageToY(double.NegativeInfinity)));
    }
}
