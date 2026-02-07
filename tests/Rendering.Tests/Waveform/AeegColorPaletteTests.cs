// AeegColorPaletteTests.cs
// aEEG 颜色定义测试 - 来源: ARCHITECTURE.md §5

using Neo.Rendering.AEEG;
using Xunit;

namespace Neo.Rendering.Tests.Waveform;

/// <summary>
/// aEEG 颜色定义测试。
/// </summary>
public sealed class AeegColorPaletteTests
{
    // ============================================
    // 趋势线颜色测试
    // ============================================

    [Fact]
    public void TrendFill_IsSemiTransparent()
    {
        var previous = AeegColorPalette.CurrentTheme;
        try
        {
            AeegColorPalette.SetTheme(AeegThemeType.Medical);
            Assert.True(AeegColorPalette.TrendFill.A < 1.0f);
            Assert.True(AeegColorPalette.TrendFill.A > 0.0f);
        }
        finally
        {
            AeegColorPalette.SetTheme(previous);
        }
    }

    [Fact]
    public void TrendFill_IsGreen()
    {
        var previous = AeegColorPalette.CurrentTheme;
        try
        {
            AeegColorPalette.SetTheme(AeegThemeType.Medical);
            Assert.True(AeegColorPalette.TrendFill.G > AeegColorPalette.TrendFill.R);
            Assert.True(AeegColorPalette.TrendFill.G > AeegColorPalette.TrendFill.B);
            Assert.Equal(0f, AeegColorPalette.TrendFill.R, 0.01f);
            Assert.Equal(0.7f, AeegColorPalette.TrendFill.G, 0.01f);
            Assert.Equal(0.4f, AeegColorPalette.TrendFill.B, 0.01f);
            Assert.Equal(0.7f, AeegColorPalette.TrendFill.A, 0.01f);
        }
        finally
        {
            AeegColorPalette.SetTheme(previous);
        }
    }

    [Fact]
    public void UpperBound_IsOpaque()
    {
        Assert.Equal(1.0f, AeegColorPalette.UpperBound.A, 0.001f);
    }

    [Fact]
    public void LowerBound_IsOpaque()
    {
        Assert.Equal(1.0f, AeegColorPalette.LowerBound.A, 0.001f);
    }

    // ============================================
    // 网格颜色测试
    // ============================================

    [Fact]
    public void MajorGridLine_VisibleButNotFullyOpaque()
    {
        Assert.True(AeegColorPalette.MajorGridLine.A > 0.5f);
    }

    [Fact]
    public void MinorGridLine_MoreTransparentThanMajor()
    {
        Assert.True(AeegColorPalette.MinorGridLine.A < AeegColorPalette.MajorGridLine.A);
    }

    [Fact]
    public void BoundaryLine_DistinctColor()
    {
        // 分界线应该是红色系
        Assert.True(AeegColorPalette.BoundaryLine.R > AeegColorPalette.BoundaryLine.G);
        Assert.True(AeegColorPalette.BoundaryLine.R > AeegColorPalette.BoundaryLine.B);
    }

    // ============================================
    // 间隙遮罩测试
    // ============================================

    [Fact]
    public void GapMask_IsSemiTransparent()
    {
        Assert.True(AeegColorPalette.GapMask.A < 1.0f);
        Assert.True(AeegColorPalette.GapMask.A > 0.0f);
    }

    [Fact]
    public void GapMask_IsGrayish()
    {
        // 灰色：R ≈ G ≈ B
        Assert.Equal(AeegColorPalette.GapMask.R, AeegColorPalette.GapMask.G, 0.1f);
        Assert.Equal(AeegColorPalette.GapMask.G, AeegColorPalette.GapMask.B, 0.1f);
    }

    // ============================================
    // 饱和标记测试
    // ============================================

    [Fact]
    public void SaturationMarker_IsRed()
    {
        Assert.True(AeegColorPalette.SaturationMarker.R > 0.8f);
        Assert.True(AeegColorPalette.SaturationMarker.G < 0.5f);
    }

    [Fact]
    public void SaturationMarker_IsOpaque()
    {
        Assert.Equal(1.0f, AeegColorPalette.SaturationMarker.A, 0.001f);
    }

    // ============================================
    // 通道颜色测试
    // ============================================

    [Fact]
    public void GetChannelColor_ReturnsDistinctColors()
    {
        var c0 = AeegColorPalette.GetChannelColor(0);
        var c1 = AeegColorPalette.GetChannelColor(1);
        var c2 = AeegColorPalette.GetChannelColor(2);
        var c3 = AeegColorPalette.GetChannelColor(3);

        // 每个通道颜色应该不同
        Assert.NotEqual(c0, c1);
        Assert.NotEqual(c1, c2);
        Assert.NotEqual(c2, c3);
        Assert.NotEqual(c0, c3);
    }

    [Fact]
    public void GetChannelColor_OutOfRange_ReturnsChannel1()
    {
        var c5 = AeegColorPalette.GetChannelColor(5);
        var cNeg = AeegColorPalette.GetChannelColor(-1);

        Assert.Equal(AeegColorPalette.Channel1, c5);
        Assert.Equal(AeegColorPalette.Channel1, cNeg);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public void GetChannelColor_AllChannelsOpaque(int channelIndex)
    {
        var color = AeegColorPalette.GetChannelColor(channelIndex);
        Assert.Equal(1.0f, color.A, 0.001f);
    }

    // ============================================
    // 背景颜色测试
    // ============================================

    [Fact]
    public void Background_IsLight()
    {
        // 背景应该是浅色
        Assert.True(AeegColorPalette.Background.R > 0.9f);
        Assert.True(AeegColorPalette.Background.G > 0.9f);
        Assert.True(AeegColorPalette.Background.B > 0.9f);
    }

    [Fact]
    public void Background_IsOpaque()
    {
        Assert.Equal(1.0f, AeegColorPalette.Background.A, 0.001f);
    }

    // ============================================
    // 轴线颜色测试
    // ============================================

    [Fact]
    public void AxisLine_IsDark()
    {
        // 轴线应该是深色
        Assert.True(AeegColorPalette.AxisLine.R < 0.5f);
        Assert.True(AeegColorPalette.AxisLine.G < 0.5f);
        Assert.True(AeegColorPalette.AxisLine.B < 0.5f);
    }

    [Fact]
    public void AxisLabel_IsDark()
    {
        // 标签应该是深色
        Assert.True(AeegColorPalette.AxisLabel.R < 0.5f);
        Assert.True(AeegColorPalette.AxisLabel.G < 0.5f);
        Assert.True(AeegColorPalette.AxisLabel.B < 0.5f);
    }
}
