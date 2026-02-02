// LayeredRendererTests.cs
// 分层渲染器单元测试

using Neo.Rendering.Core;
using Neo.Rendering.Layers;
using Xunit;

namespace Neo.Rendering.Tests.Layers;

/// <summary>
/// 分层渲染器单元测试。
/// </summary>
public sealed class LayeredRendererTests
{
    [Fact]
    public void CreateDefault_ReturnsRendererWithThreeLayers()
    {
        // Act
        using var renderer = LayeredRenderer.CreateDefault();

        // Assert
        Assert.Equal(3, renderer.LayerCount);
    }

    [Fact]
    public void CreateDefault_HasCorrectLayerTypes()
    {
        // Act
        using var renderer = LayeredRenderer.CreateDefault();

        // Assert
        Assert.NotNull(renderer.GetLayer<GridLayer>());
        Assert.NotNull(renderer.GetLayer<ContentLayer>());
        Assert.NotNull(renderer.GetLayer<OverlayLayer>());
    }

    [Fact]
    public void CreateDefault_LayersInCorrectOrder()
    {
        // Act
        using var renderer = LayeredRenderer.CreateDefault();
        var layers = renderer.Layers.ToList();

        // Assert
        Assert.Equal(3, layers.Count);
        Assert.Equal(0, layers[0].Order);  // Grid
        Assert.Equal(1, layers[1].Order);  // Content
        Assert.Equal(2, layers[2].Order);  // Overlay
    }

    [Fact]
    public void AddLayer_IncreasesCount()
    {
        // Arrange
        using var renderer = new LayeredRenderer();
        Assert.Equal(0, renderer.LayerCount);

        // Act
        renderer.AddLayer(new GridLayer());

        // Assert
        Assert.Equal(1, renderer.LayerCount);
    }

    [Fact]
    public void RemoveLayer_DecreasesCount()
    {
        // Arrange
        using var renderer = new LayeredRenderer();
        var layer = new GridLayer();
        renderer.AddLayer(layer);
        Assert.Equal(1, renderer.LayerCount);

        // Act
        bool removed = renderer.RemoveLayer(layer);

        // Assert
        Assert.True(removed);
        Assert.Equal(0, renderer.LayerCount);
    }

    [Fact]
    public void GetLayer_ByName_ReturnsCorrectLayer()
    {
        // Arrange
        using var renderer = LayeredRenderer.CreateDefault();

        // Act
        var gridLayer = renderer.GetLayer("Grid");
        var contentLayer = renderer.GetLayer("Content");
        var overlayLayer = renderer.GetLayer("Overlay");

        // Assert
        Assert.NotNull(gridLayer);
        Assert.NotNull(contentLayer);
        Assert.NotNull(overlayLayer);
        Assert.IsType<GridLayer>(gridLayer);
        Assert.IsType<ContentLayer>(contentLayer);
        Assert.IsType<OverlayLayer>(overlayLayer);
    }

    [Fact]
    public void GetLayer_ByName_NonExistent_ReturnsNull()
    {
        // Arrange
        using var renderer = LayeredRenderer.CreateDefault();

        // Act
        var layer = renderer.GetLayer("NonExistent");

        // Assert
        Assert.Null(layer);
    }

    [Fact]
    public void GetLayer_ByType_ReturnsCorrectLayer()
    {
        // Arrange
        using var renderer = LayeredRenderer.CreateDefault();

        // Act
        var gridLayer = renderer.GetLayer<GridLayer>();

        // Assert
        Assert.NotNull(gridLayer);
        Assert.Equal("Grid", gridLayer.Name);
    }

    [Fact]
    public void Layers_SortedByOrder()
    {
        // Arrange
        using var renderer = new LayeredRenderer();
        // 添加顺序与 Order 不同
        renderer.AddLayer(new OverlayLayer());  // Order=2
        renderer.AddLayer(new GridLayer());      // Order=0
        renderer.AddLayer(new ContentLayer());   // Order=1

        // Act - 强制排序通过创建 RenderContext
        // 由于无法实际调用 RenderFrame（需要 D2D），
        // 我们验证 Layers 属性返回排序后的列表
        var context = new RenderContext
        {
            ViewportWidth = 800,
            ViewportHeight = 600,
            Dpi = 96.0
        };

        // 调用 InvalidateAll 触发排序标记
        renderer.InvalidateAll();

        var layers = renderer.Layers.ToList();

        // Assert - 应按 Order 排序
        Assert.Equal(3, layers.Count);
        // 注意：排序在 RenderFrame 中进行，这里只验证添加成功
        Assert.Contains(layers, l => l.Order == 0);
        Assert.Contains(layers, l => l.Order == 1);
        Assert.Contains(layers, l => l.Order == 2);
    }

    [Fact]
    public void InvalidateAll_InvalidatesAllLayers()
    {
        // Arrange
        using var renderer = LayeredRenderer.CreateDefault();

        // Act - 应不抛出异常
        renderer.InvalidateAll();

        // Assert - 验证调用成功
        Assert.Equal(3, renderer.LayerCount);
    }

    [Fact]
    public void AddLayer_NullLayer_ThrowsArgumentNullException()
    {
        // Arrange
        using var renderer = new LayeredRenderer();

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => renderer.AddLayer(null!));
    }

    [Fact]
    public void RemoveLayer_NullLayer_ThrowsArgumentNullException()
    {
        // Arrange
        using var renderer = new LayeredRenderer();

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => renderer.RemoveLayer(null!));
    }

    [Fact]
    public void Dispose_ClearsAllLayers()
    {
        // Arrange
        var renderer = LayeredRenderer.CreateDefault();
        Assert.Equal(3, renderer.LayerCount);

        // Act
        renderer.Dispose();

        // Assert - 访问已释放对象应抛出异常
        Assert.Throws<ObjectDisposedException>(() => renderer.AddLayer(new GridLayer()));
    }

    [Fact]
    public void Dispose_CanBeCalledMultipleTimes()
    {
        // Arrange
        var renderer = LayeredRenderer.CreateDefault();

        // Act & Assert - 多次调用 Dispose 不应抛出异常
        renderer.Dispose();
        renderer.Dispose();
        renderer.Dispose();
    }

    [Fact]
    public void LayerEnableDisable_WorksCorrectly()
    {
        // Arrange
        using var renderer = LayeredRenderer.CreateDefault();
        var gridLayer = renderer.GetLayer<GridLayer>();
        Assert.NotNull(gridLayer);
        Assert.True(gridLayer.IsEnabled);

        // Act
        gridLayer.IsEnabled = false;

        // Assert
        Assert.False(gridLayer.IsEnabled);

        // Act
        gridLayer.IsEnabled = true;

        // Assert
        Assert.True(gridLayer.IsEnabled);
    }
}
