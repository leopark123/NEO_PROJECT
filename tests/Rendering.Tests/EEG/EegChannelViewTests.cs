// EegChannelViewTests.cs
// EEG 通道视图配置测试

using Neo.Rendering.EEG;
using Xunit;

namespace Neo.Rendering.Tests.EEG;

/// <summary>
/// EegChannelView 单元测试。
/// </summary>
public sealed class EegChannelViewTests
{
    [Fact]
    public void CreateDefault_ReturnsValidChannelView()
    {
        // Arrange
        const int channelIndex = 0;
        const float yOffset = 10.0f;
        const float height = 100.0f;

        // Act
        var view = EegChannelView.CreateDefault(channelIndex, yOffset, height);

        // Assert
        Assert.Equal(channelIndex, view.ChannelIndex);
        Assert.Equal(yOffset, view.YOffset);
        Assert.Equal(height, view.Height);
        Assert.True(view.IsVisible);
        Assert.True(view.LineWidth > 0);
        Assert.True(view.UvToPixelScale > 0);
    }

    [Theory]
    [InlineData(0, "CH1 (C3-P3)")]
    [InlineData(1, "CH2 (C4-P4)")]
    [InlineData(2, "CH3 (P3-P4)")]
    [InlineData(3, "CH4 (C3-C4)")]
    public void CreateDefault_SetsCorrectChannelName(int channelIndex, string expectedName)
    {
        // Act
        var view = EegChannelView.CreateDefault(channelIndex, 0, 100);

        // Assert
        Assert.Equal(expectedName, view.ChannelName);
    }

    [Fact]
    public void BaselineY_IsAtCenterOfChannel()
    {
        // Arrange
        const float yOffset = 50.0f;
        const float height = 100.0f;

        // Act
        var view = EegChannelView.CreateDefault(0, yOffset, height);

        // Assert
        Assert.Equal(yOffset + height / 2.0f, view.BaselineY);
    }

    [Fact]
    public void UvToY_ZeroVoltage_ReturnsBaseline()
    {
        // Arrange
        var view = EegChannelView.CreateDefault(0, 0, 400);

        // Act
        float y = view.UvToY(0.0);

        // Assert - 0 μV 应该在基线位置
        Assert.Equal(view.BaselineY, y);
    }

    [Fact]
    public void UvToY_PositiveVoltage_GoesUp()
    {
        // Arrange
        var view = EegChannelView.CreateDefault(0, 0, 400);

        // Act
        float yPositive = view.UvToY(100.0);  // +100 μV

        // Assert - 正电压应该向上（Y 减小）
        Assert.True(yPositive < view.BaselineY);
    }

    [Fact]
    public void UvToY_NegativeVoltage_GoesDown()
    {
        // Arrange
        var view = EegChannelView.CreateDefault(0, 0, 400);

        // Act
        float yNegative = view.UvToY(-100.0);  // -100 μV

        // Assert - 负电压应该向下（Y 增大）
        Assert.True(yNegative > view.BaselineY);
    }

    [Fact]
    public void YToUv_InverseOfUvToY()
    {
        // Arrange
        var view = EegChannelView.CreateDefault(0, 0, 400);
        const double originalUv = 75.5;

        // Act
        float y = view.UvToY(originalUv);
        double recoveredUv = view.YToUv(y);

        // Assert - 往返转换应该得到原始值
        Assert.Equal(originalUv, recoveredUv, 3);  // 3 位精度
    }

    [Fact]
    public void ContainsY_InsideChannel_ReturnsTrue()
    {
        // Arrange
        var view = EegChannelView.CreateDefault(0, 100, 200);

        // Act & Assert
        Assert.True(view.ContainsY(100));   // 起始边界
        Assert.True(view.ContainsY(150));   // 中间
        Assert.True(view.ContainsY(299));   // 接近结束边界
    }

    [Fact]
    public void ContainsY_OutsideChannel_ReturnsFalse()
    {
        // Arrange
        var view = EegChannelView.CreateDefault(0, 100, 200);

        // Act & Assert
        Assert.False(view.ContainsY(99));   // 在起始之前
        Assert.False(view.ContainsY(300));  // 在结束边界上
        Assert.False(view.ContainsY(400));  // 在结束之后
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public void CreateDefault_SetsCorrectColor(int channelIndex)
    {
        // Act
        var view = EegChannelView.CreateDefault(channelIndex, 0, 100);

        // Assert
        Assert.Equal(EegColorPalette.GetChannelColor(channelIndex), view.Color);
    }

    [Fact]
    public void DpiScale_AffectsLineWidth()
    {
        // Arrange
        const float dpiScale1 = 1.0f;
        const float dpiScale2 = 2.0f;

        // Act
        var view1 = EegChannelView.CreateDefault(0, 0, 100, dpiScale1);
        var view2 = EegChannelView.CreateDefault(0, 0, 100, dpiScale2);

        // Assert - 高 DPI 应该有更粗的线
        Assert.True(view2.LineWidth > view1.LineWidth);
    }

    [Fact]
    public void UvToPixelScale_DefaultRange_Is200uV()
    {
        // Arrange - 默认 ±200 μV 满屏
        const float height = 400.0f;

        // Act
        var view = EegChannelView.CreateDefault(0, 0, height);

        // Assert
        // UvToPixelScale = height / 400 = 1.0 (for 400 pixel height)
        // 200 μV 应该映射到 height/2 像素
        float expectedScale = height / 400.0f;
        Assert.Equal(expectedScale, view.UvToPixelScale);
    }
}
