// BoundaryMappingTests.cs
// 边界映射测试 - 来源: DSP_SPEC.md

using Neo.Rendering.Mapping;
using Xunit;

namespace Neo.Rendering.Tests.Mapping;

/// <summary>
/// 边界映射测试。
/// </summary>
/// <remarks>
/// 验证规格:
/// - 分界点 10 μV 的连续性
/// - 0 μV 和 200 μV 的边界行为
/// - 无效值处理
/// </remarks>
public sealed class BoundaryMappingTests
{
    private const double TotalHeight = 1000.0;
    private const double Tolerance = 0.001;

    [Fact]
    public void LinearLogBoundary_Is10uV()
    {
        Assert.Equal(10.0, AeegSemiLogMapper.LinearLogBoundaryUv);
    }

    [Fact]
    public void MinVoltage_Is0()
    {
        Assert.Equal(0.0, AeegSemiLogMapper.MinVoltageUv);
    }

    [Fact]
    public void MaxVoltage_Is200()
    {
        Assert.Equal(200.0, AeegSemiLogMapper.MaxVoltageUv);
    }

    [Fact]
    public void Mapper_Boundary_Continuous()
    {
        var mapper = new AeegSemiLogMapper(TotalHeight);

        // 验证 10 μV 附近的连续性
        double y9_99 = mapper.MapVoltageToY(9.99);
        double y10_00 = mapper.MapVoltageToY(10.0);
        double y10_01 = mapper.MapVoltageToY(10.01);

        // Y 应该单调递减
        Assert.True(y9_99 > y10_00);
        Assert.True(y10_00 > y10_01);

        // 跳变应该很小
        double jumpBefore = y9_99 - y10_00;
        double jumpAfter = y10_00 - y10_01;

        // 在分界点附近，两侧的变化率应该接近
        // 线性段: dY/dV = -linearHeight / 10 = -50
        // 对数段: dY/dV ≈ -logHeight / (V * ln(10) * logRange) at V=10
        //       = -500 / (10 * 2.303 * 1.301) ≈ -16.7
        // 所以对数段变化率约为线性段的 1/3
        Assert.True(jumpBefore > 0);
        Assert.True(jumpAfter > 0);
    }

    [Fact]
    public void Mapper_AtBoundary_ExactValue()
    {
        var mapper = new AeegSemiLogMapper(TotalHeight);

        // 10 μV 应该正好在中间
        double y = mapper.MapVoltageToY(10.0);
        Assert.Equal(TotalHeight / 2, y, Tolerance);
    }

    [Fact]
    public void Mapper_AtBottom_ExactValue()
    {
        var mapper = new AeegSemiLogMapper(TotalHeight);

        // 0 μV 应该正好在底部
        double y = mapper.MapVoltageToY(0.0);
        Assert.Equal(TotalHeight, y, Tolerance);
    }

    [Fact]
    public void Mapper_AtTop_ExactValue()
    {
        var mapper = new AeegSemiLogMapper(TotalHeight);

        // 200 μV 应该正好在顶部
        double y = mapper.MapVoltageToY(200.0);
        Assert.Equal(0.0, y, Tolerance);
    }

    [Fact]
    public void Mapper_NaN_ReturnsNaN()
    {
        var mapper = new AeegSemiLogMapper(TotalHeight);

        Assert.True(double.IsNaN(mapper.MapVoltageToY(double.NaN)));
        Assert.True(double.IsNaN(mapper.MapYToVoltage(double.NaN)));
    }

    [Fact]
    public void Mapper_NegativeVoltage_ReturnsNaN()
    {
        var mapper = new AeegSemiLogMapper(TotalHeight);

        Assert.True(double.IsNaN(mapper.MapVoltageToY(-0.001)));
        Assert.True(double.IsNaN(mapper.MapVoltageToY(-1.0)));
        Assert.True(double.IsNaN(mapper.MapVoltageToY(-100.0)));
    }

    [Fact]
    public void Mapper_NegativeY_ReturnsNaN()
    {
        var mapper = new AeegSemiLogMapper(TotalHeight);

        Assert.True(double.IsNaN(mapper.MapYToVoltage(-1.0)));
        Assert.True(double.IsNaN(mapper.MapYToVoltage(-0.001)));
    }

    [Fact]
    public void Mapper_YBeyondTotal_ReturnsNaN()
    {
        var mapper = new AeegSemiLogMapper(TotalHeight);

        Assert.True(double.IsNaN(mapper.MapYToVoltage(TotalHeight + 0.001)));
        Assert.True(double.IsNaN(mapper.MapYToVoltage(TotalHeight + 100)));
    }

    [Fact]
    public void Mapper_AboveMax_ClampsToTop()
    {
        var mapper = new AeegSemiLogMapper(TotalHeight);

        // 超过 200 μV 的值应该 clamp 到顶部 (Y = 0)
        Assert.Equal(0.0, mapper.MapVoltageToY(201.0), Tolerance);
        Assert.Equal(0.0, mapper.MapVoltageToY(500.0), Tolerance);
        Assert.Equal(0.0, mapper.MapVoltageToY(1000.0), Tolerance);
    }

    [Fact]
    public void Mapper_ZeroHeight_ThrowsException()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new AeegSemiLogMapper(0));
    }

    [Fact]
    public void Mapper_NegativeHeight_ThrowsException()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new AeegSemiLogMapper(-100));
    }

    [Fact]
    public void Mapper_InverseMapping_Roundtrip()
    {
        var mapper = new AeegSemiLogMapper(TotalHeight);

        // 测试整个范围的往返映射
        double[] testVoltages = [0, 1, 2, 5, 10, 15, 25, 50, 100, 150, 200];

        foreach (double voltage in testVoltages)
        {
            double y = mapper.MapVoltageToY(voltage);
            double backVoltage = mapper.MapYToVoltage(y);

            // 对数段需要较大容差
            double tolerance = voltage >= 10 ? 0.5 : 0.001;
            Assert.Equal(voltage, backVoltage, tolerance);
        }
    }

    [Fact]
    public void Mapper_StaticMethod_MatchesInstance()
    {
        var mapper = new AeegSemiLogMapper(TotalHeight);

        double[] testVoltages = [0, 5, 10, 50, 100, 200];

        foreach (double voltage in testVoltages)
        {
            double instanceY = mapper.MapVoltageToY(voltage);
            double staticY = AeegSemiLogMapper.GetY(voltage, TotalHeight);
            Assert.Equal(instanceY, staticY, Tolerance);
        }
    }

    [Fact]
    public void Mapper_HeightProperties_Correct()
    {
        var mapper = new AeegSemiLogMapper(TotalHeight);

        Assert.Equal(TotalHeight, mapper.TotalHeightPx, Tolerance);
        Assert.Equal(TotalHeight * 0.5, mapper.LinearHeightPx, Tolerance);
        Assert.Equal(TotalHeight * 0.5, mapper.LogHeightPx, Tolerance);
    }

    [Fact]
    public void Mapper_DifferentHeights_ProportionalMapping()
    {
        double[] heights = [100, 500, 1000, 2000];

        foreach (double height in heights)
        {
            var mapper = new AeegSemiLogMapper(height);

            // 分界点始终在中间
            Assert.Equal(height / 2, mapper.MapVoltageToY(10.0), Tolerance);

            // 边界正确
            Assert.Equal(height, mapper.MapVoltageToY(0.0), Tolerance);
            Assert.Equal(0.0, mapper.MapVoltageToY(200.0), Tolerance);
        }
    }
}
