// EegColorPaletteTests.cs
// EEG 通道颜色调色板测试

using Neo.Rendering.EEG;
using Xunit;

namespace Neo.Rendering.Tests.EEG;

/// <summary>
/// EegColorPalette 单元测试。
/// </summary>
public sealed class EegColorPaletteTests
{
    [Fact]
    public void Channel1_IsGreen()
    {
        // Assert - 通道1应该是绿色
        Assert.True(EegColorPalette.Channel1.G > EegColorPalette.Channel1.R);
        Assert.True(EegColorPalette.Channel1.G > EegColorPalette.Channel1.B);
    }

    [Fact]
    public void Channel2_IsBlue()
    {
        // Assert - 通道2应该是蓝色
        Assert.True(EegColorPalette.Channel2.B > EegColorPalette.Channel2.R);
    }

    [Fact]
    public void Channel3_IsOrange()
    {
        // Assert - 通道3应该是橙色 (R > G > B)
        Assert.True(EegColorPalette.Channel3.R > EegColorPalette.Channel3.G);
        Assert.True(EegColorPalette.Channel3.G > EegColorPalette.Channel3.B);
    }

    [Fact]
    public void Channel4_IsPurple()
    {
        // Assert - 通道4应该是紫色 (R和B较高)
        Assert.True(EegColorPalette.Channel4.R > EegColorPalette.Channel4.G);
        Assert.True(EegColorPalette.Channel4.B > EegColorPalette.Channel4.G);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public void GetChannelColor_ReturnsCorrectColor(int channelIndex)
    {
        // Act
        var color = EegColorPalette.GetChannelColor(channelIndex);

        // Assert - 颜色应该完全不透明
        Assert.Equal(1.0f, color.A);
    }

    [Fact]
    public void GetChannelColor_InvalidIndex_ReturnsChannel1()
    {
        // Act
        var negativeIndex = EegColorPalette.GetChannelColor(-1);
        var outOfRange = EegColorPalette.GetChannelColor(100);

        // Assert - 无效索引应返回通道1颜色
        Assert.Equal(EegColorPalette.Channel1, negativeIndex);
        Assert.Equal(EegColorPalette.Channel1, outOfRange);
    }

    [Fact]
    public void GapMask_IsSemiTransparent()
    {
        // Assert - 间隙遮罩应该是半透明的
        Assert.True(EegColorPalette.GapMask.A < 1.0f);
        Assert.True(EegColorPalette.GapMask.A > 0.0f);
    }

    [Fact]
    public void SaturationMarker_IsRed()
    {
        // Assert - 饱和标记应该是红色
        Assert.True(EegColorPalette.SaturationMarker.R > EegColorPalette.SaturationMarker.G);
        Assert.True(EegColorPalette.SaturationMarker.R > EegColorPalette.SaturationMarker.B);
    }

    [Fact]
    public void AllChannelColors_AreDistinct()
    {
        // Arrange
        var colors = new[]
        {
            EegColorPalette.Channel1,
            EegColorPalette.Channel2,
            EegColorPalette.Channel3,
            EegColorPalette.Channel4
        };

        // Assert - 所有通道颜色应该不同
        for (int i = 0; i < colors.Length; i++)
        {
            for (int j = i + 1; j < colors.Length; j++)
            {
                Assert.NotEqual(colors[i], colors[j]);
            }
        }
    }
}
