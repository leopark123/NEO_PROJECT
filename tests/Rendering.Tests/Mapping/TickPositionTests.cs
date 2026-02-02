// TickPositionTests.cs
// 刻度位置测试 - 来源: DSP_SPEC.md

using Neo.Rendering.Mapping;
using Xunit;

namespace Neo.Rendering.Tests.Mapping;

/// <summary>
/// 刻度位置测试。
/// </summary>
/// <remarks>
/// 验证规格:
/// - 标准刻度点: 0, 1, 2, 3, 4, 5, 10, 25, 50, 100, 200 μV
/// - 刻度点数量固定
/// - 刻度 Y 位置与映射器一致
/// </remarks>
public sealed class TickPositionTests
{
    private const double TotalHeight = 1000.0;
    private const double Tolerance = 0.001;

    [Fact]
    public void StandardTicks_CorrectCount()
    {
        Assert.Equal(11, AeegAxisTicks.StandardTicksUv.Length);
        Assert.Equal(11, AeegAxisTicks.TickCount);
    }

    [Fact]
    public void StandardTicks_CorrectValues()
    {
        double[] expected = [0, 1, 2, 3, 4, 5, 10, 25, 50, 100, 200];
        Assert.Equal(expected, AeegAxisTicks.StandardTicksUv);
    }

    [Fact]
    public void MajorTicks_CorrectValues()
    {
        double[] expected = [0, 5, 10, 50, 100, 200];
        Assert.Equal(expected, AeegAxisTicks.MajorTicksUv);
    }

    [Fact]
    public void GetTicks_ReturnsCorrectCount()
    {
        var ticks = AeegAxisTicks.GetTicks(TotalHeight);
        Assert.Equal(11, ticks.Length);
    }

    [Fact]
    public void GetTicks_CorrectVoltageValues()
    {
        var ticks = AeegAxisTicks.GetTicks(TotalHeight);

        double[] expectedVoltages = [0, 1, 2, 3, 4, 5, 10, 25, 50, 100, 200];

        for (int i = 0; i < expectedVoltages.Length; i++)
        {
            Assert.Equal(expectedVoltages[i], ticks[i].VoltageUv, Tolerance);
        }
    }

    [Fact]
    public void GetTicks_YPositionsMatchMapper()
    {
        var mapper = new AeegSemiLogMapper(TotalHeight);
        var ticks = AeegAxisTicks.GetTicks(TotalHeight);

        foreach (var tick in ticks)
        {
            double expectedY = mapper.MapVoltageToY(tick.VoltageUv);
            Assert.Equal(expectedY, tick.Y, 0.5);  // 允许较大容差（对数段）
        }
    }

    [Fact]
    public void GetTicks_LabelsAreIntegers()
    {
        var ticks = AeegAxisTicks.GetTicks(TotalHeight);

        string[] expectedLabels = ["0", "1", "2", "3", "4", "5", "10", "25", "50", "100", "200"];

        for (int i = 0; i < expectedLabels.Length; i++)
        {
            Assert.Equal(expectedLabels[i], ticks[i].Label);
        }
    }

    [Fact]
    public void GetTicks_MajorFlagsCorrect()
    {
        var ticks = AeegAxisTicks.GetTicks(TotalHeight);

        // 0, 5, 10, 50, 100, 200 是主刻度
        Assert.True(ticks[0].IsMajor);   // 0
        Assert.False(ticks[1].IsMajor);  // 1
        Assert.False(ticks[2].IsMajor);  // 2
        Assert.False(ticks[3].IsMajor);  // 3
        Assert.False(ticks[4].IsMajor);  // 4
        Assert.True(ticks[5].IsMajor);   // 5
        Assert.True(ticks[6].IsMajor);   // 10
        Assert.False(ticks[7].IsMajor);  // 25
        Assert.True(ticks[8].IsMajor);   // 50
        Assert.True(ticks[9].IsMajor);   // 100
        Assert.True(ticks[10].IsMajor);  // 200
    }

    [Fact]
    public void GetTicks_0uV_AtBottom()
    {
        var ticks = AeegAxisTicks.GetTicks(TotalHeight);
        Assert.Equal(TotalHeight, ticks[0].Y, Tolerance);
    }

    [Fact]
    public void GetTicks_10uV_AtMiddle()
    {
        var ticks = AeegAxisTicks.GetTicks(TotalHeight);
        Assert.Equal(TotalHeight / 2, ticks[6].Y, Tolerance);
    }

    [Fact]
    public void GetTicks_200uV_AtTop()
    {
        var ticks = AeegAxisTicks.GetTicks(TotalHeight);
        Assert.Equal(0.0, ticks[10].Y, Tolerance);
    }

    [Fact]
    public void GetTickY_MatchesMapper()
    {
        var mapper = new AeegSemiLogMapper(TotalHeight);

        double[] testVoltages = [0, 5, 10, 25, 50, 100, 200];

        foreach (double voltage in testVoltages)
        {
            double tickY = AeegAxisTicks.GetTickY(voltage, TotalHeight);
            double mapperY = mapper.MapVoltageToY(voltage);
            Assert.Equal(mapperY, tickY, 0.5);
        }
    }

    [Fact]
    public void GetBoundaryY_Is10uV()
    {
        double boundaryY = AeegAxisTicks.GetBoundaryY(TotalHeight);
        double expectedY = AeegAxisTicks.GetTickY(10.0, TotalHeight);
        Assert.Equal(expectedY, boundaryY, Tolerance);
    }

    [Fact]
    public void GetBoundaryY_IsMiddle()
    {
        double boundaryY = AeegAxisTicks.GetBoundaryY(TotalHeight);
        Assert.Equal(TotalHeight / 2, boundaryY, Tolerance);
    }

    [Fact]
    public void FormatTickLabel_IntegerFormat()
    {
        Assert.Equal("0", AeegAxisTicks.FormatTickLabel(0));
        Assert.Equal("5", AeegAxisTicks.FormatTickLabel(5));
        Assert.Equal("10", AeegAxisTicks.FormatTickLabel(10));
        Assert.Equal("100", AeegAxisTicks.FormatTickLabel(100));
        Assert.Equal("200", AeegAxisTicks.FormatTickLabel(200));
    }

    [Fact]
    public void IsMajorTick_CorrectIdentification()
    {
        // 主刻度
        Assert.True(AeegAxisTicks.IsMajorTick(0));
        Assert.True(AeegAxisTicks.IsMajorTick(5));
        Assert.True(AeegAxisTicks.IsMajorTick(10));
        Assert.True(AeegAxisTicks.IsMajorTick(50));
        Assert.True(AeegAxisTicks.IsMajorTick(100));
        Assert.True(AeegAxisTicks.IsMajorTick(200));

        // 次刻度
        Assert.False(AeegAxisTicks.IsMajorTick(1));
        Assert.False(AeegAxisTicks.IsMajorTick(2));
        Assert.False(AeegAxisTicks.IsMajorTick(3));
        Assert.False(AeegAxisTicks.IsMajorTick(4));
        Assert.False(AeegAxisTicks.IsMajorTick(25));
    }

    [Fact]
    public void GetTicks_ZeroHeight_ThrowsException()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => AeegAxisTicks.GetTicks(0));
    }

    [Fact]
    public void GetTicks_NegativeHeight_ThrowsException()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => AeegAxisTicks.GetTicks(-100));
    }

    [Fact]
    public void GetTicks_DifferentHeights_ProportionalPositions()
    {
        double[] heights = [100, 500, 1000, 2000];

        foreach (double height in heights)
        {
            var ticks = AeegAxisTicks.GetTicks(height);

            // 0 μV 始终在底部
            Assert.Equal(height, ticks[0].Y, Tolerance);

            // 10 μV 始终在中间
            Assert.Equal(height / 2, ticks[6].Y, Tolerance);

            // 200 μV 始终在顶部
            Assert.Equal(0.0, ticks[10].Y, Tolerance);
        }
    }

    [Fact]
    public void GetTicks_LinearSegment_EqualSpacing()
    {
        var ticks = AeegAxisTicks.GetTicks(TotalHeight);

        // 线性段内刻度间距应该相等
        // 0, 1, 2, 3, 4, 5 μV 的间距都是 50 像素
        for (int i = 0; i < 5; i++)
        {
            double spacing = ticks[i].Y - ticks[i + 1].Y;
            Assert.Equal(50.0, spacing, Tolerance);
        }
    }
}
