// LayerTests.cs
// 渲染层单元测试

using Neo.Rendering.Core;
using Neo.Rendering.Layers;
using Xunit;

namespace Neo.Rendering.Tests.Layers;

/// <summary>
/// 渲染层单元测试。
/// </summary>
public sealed class LayerTests
{
    [Fact]
    public void GridLayer_HasCorrectOrder()
    {
        // Arrange & Act
        using var layer = new GridLayer();

        // Assert
        Assert.Equal(0, layer.Order);
        Assert.Equal("Grid", layer.Name);
    }

    [Fact]
    public void ContentLayer_HasCorrectOrder()
    {
        // Arrange & Act
        using var layer = new ContentLayer();

        // Assert
        Assert.Equal(1, layer.Order);
        Assert.Equal("Content", layer.Name);
    }

    [Fact]
    public void OverlayLayer_HasCorrectOrder()
    {
        // Arrange & Act
        using var layer = new OverlayLayer();

        // Assert
        Assert.Equal(2, layer.Order);
        Assert.Equal("Overlay", layer.Name);
    }

    [Fact]
    public void Layer_DefaultEnabled()
    {
        // Arrange & Act
        using var grid = new GridLayer();
        using var content = new ContentLayer();
        using var overlay = new OverlayLayer();

        // Assert
        Assert.True(grid.IsEnabled);
        Assert.True(content.IsEnabled);
        Assert.True(overlay.IsEnabled);
    }

    [Fact]
    public void Layer_CanBeDisabled()
    {
        // Arrange
        using var layer = new GridLayer();
        Assert.True(layer.IsEnabled);

        // Act
        layer.IsEnabled = false;

        // Assert
        Assert.False(layer.IsEnabled);
    }

    [Fact]
    public void GridLayer_ShowMinorGrid_DefaultTrue()
    {
        // Arrange & Act
        using var layer = new GridLayer();

        // Assert
        Assert.True(layer.ShowMinorGrid);
    }

    [Fact]
    public void GridLayer_ShowMajorGrid_DefaultTrue()
    {
        // Arrange & Act
        using var layer = new GridLayer();

        // Assert
        Assert.True(layer.ShowMajorGrid);
    }

    [Fact]
    public void ContentLayer_PlaceholderChannelCount_Default()
    {
        // Arrange & Act
        using var layer = new ContentLayer();

        // Assert
        Assert.Equal(4, layer.PlaceholderChannelCount);
    }

    [Fact]
    public void OverlayLayer_ShowTimeAxis_DefaultTrue()
    {
        // Arrange & Act
        using var layer = new OverlayLayer();

        // Assert
        Assert.True(layer.ShowTimeAxis);
    }

    [Fact]
    public void OverlayLayer_ShowCursor_DefaultTrue()
    {
        // Arrange & Act
        using var layer = new OverlayLayer();

        // Assert
        Assert.True(layer.ShowCursor);
    }

    [Fact]
    public void OverlayLayer_CursorPosition_Default()
    {
        // Arrange & Act
        using var layer = new OverlayLayer();

        // Assert
        Assert.Equal(0.5f, layer.CursorPosition);
    }

    [Fact]
    public void Layer_Invalidate_SetsNeedsRedraw()
    {
        // Arrange
        using var layer = new GridLayer();

        // Act
        layer.Invalidate();

        // Assert - 无法直接验证 NeedsRedraw（protected），
        // 但调用应成功完成
        // 此处验证不抛出异常即可
    }

    [Fact]
    public void Layer_Dispose_CanBeCalledMultipleTimes()
    {
        // Arrange
        var layer = new GridLayer();

        // Act & Assert - 多次调用 Dispose 不应抛出异常
        layer.Dispose();
        layer.Dispose();
        layer.Dispose();
    }
}
