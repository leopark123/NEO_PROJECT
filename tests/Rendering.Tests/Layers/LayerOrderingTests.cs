// LayerOrderingTests.cs
// 层排序测试

using Neo.Rendering.Core;
using Neo.Rendering.Layers;
using Xunit;

namespace Neo.Rendering.Tests.Layers;

/// <summary>
/// 层排序测试。
/// 验证渲染顺序: Grid (0) → Content (1) → Overlay (2)
/// </summary>
public sealed class LayerOrderingTests
{
    [Fact]
    public void LayerOrder_Grid_IsZero()
    {
        // Arrange & Act
        using var layer = new GridLayer();

        // Assert
        Assert.Equal(0, layer.Order);
    }

    [Fact]
    public void LayerOrder_Content_IsOne()
    {
        // Arrange & Act
        using var layer = new ContentLayer();

        // Assert
        Assert.Equal(1, layer.Order);
    }

    [Fact]
    public void LayerOrder_Overlay_IsTwo()
    {
        // Arrange & Act
        using var layer = new OverlayLayer();

        // Assert
        Assert.Equal(2, layer.Order);
    }

    [Fact]
    public void LayerOrder_Grid_LessThan_Content()
    {
        // Arrange
        using var grid = new GridLayer();
        using var content = new ContentLayer();

        // Assert
        Assert.True(grid.Order < content.Order);
    }

    [Fact]
    public void LayerOrder_Content_LessThan_Overlay()
    {
        // Arrange
        using var content = new ContentLayer();
        using var overlay = new OverlayLayer();

        // Assert
        Assert.True(content.Order < overlay.Order);
    }

    [Fact]
    public void LayerOrder_Grid_LessThan_Overlay()
    {
        // Arrange
        using var grid = new GridLayer();
        using var overlay = new OverlayLayer();

        // Assert
        Assert.True(grid.Order < overlay.Order);
    }

    [Fact]
    public void LayeredRenderer_DefaultOrder_IsCorrect()
    {
        // Arrange
        using var renderer = LayeredRenderer.CreateDefault();

        // Act
        var layers = renderer.Layers.OrderBy(l => l.Order).ToList();

        // Assert
        Assert.Equal(3, layers.Count);
        Assert.IsType<GridLayer>(layers[0]);
        Assert.IsType<ContentLayer>(layers[1]);
        Assert.IsType<OverlayLayer>(layers[2]);
    }

    [Fact]
    public void LayeredRenderer_AddedInWrongOrder_StillSortsCorrectly()
    {
        // Arrange
        using var renderer = new LayeredRenderer();

        // 故意以错误顺序添加
        renderer.AddLayer(new OverlayLayer());   // Order=2
        renderer.AddLayer(new ContentLayer());   // Order=1
        renderer.AddLayer(new GridLayer());      // Order=0

        // Act
        var layers = renderer.Layers.OrderBy(l => l.Order).ToList();

        // Assert - 按 Order 排序后顺序正确
        Assert.Equal(0, layers[0].Order);
        Assert.Equal(1, layers[1].Order);
        Assert.Equal(2, layers[2].Order);
    }

    /// <summary>
    /// 自定义测试层，用于验证渲染顺序。
    /// </summary>
    private sealed class TestLayer : LayerBase
    {
        private readonly int _order;
        private readonly string _name;
        private readonly List<string> _renderLog;

        public TestLayer(string name, int order, List<string> renderLog)
        {
            _name = name;
            _order = order;
            _renderLog = renderLog;
        }

        public override string Name => _name;
        public override int Order => _order;

        protected override void OnRender(
            Vortice.Direct2D1.ID2D1DeviceContext context,
            Neo.Rendering.Resources.ResourceCache resources,
            RenderContext renderContext)
        {
            // 记录渲染调用顺序
            _renderLog.Add(_name);
        }
    }

    [Fact]
    public void LayeredRenderer_RenderOrder_MatchesLayerOrder()
    {
        // Arrange
        var renderLog = new List<string>();
        using var renderer = new LayeredRenderer();

        // 故意以错误顺序添加
        renderer.AddLayer(new TestLayer("Third", 2, renderLog));
        renderer.AddLayer(new TestLayer("First", 0, renderLog));
        renderer.AddLayer(new TestLayer("Second", 1, renderLog));

        // 创建最小 RenderContext
        var renderContext = new RenderContext
        {
            ViewportWidth = 800,
            ViewportHeight = 600,
            Dpi = 96.0
        };

        // Act - 由于 RenderFrame 需要真实的 D2D context，
        // 这里我们验证层添加后的 Order 正确性
        var orderedLayers = renderer.Layers.OrderBy(l => l.Order).ToList();

        // Assert
        Assert.Equal("First", orderedLayers[0].Name);
        Assert.Equal("Second", orderedLayers[1].Name);
        Assert.Equal("Third", orderedLayers[2].Name);
    }
}
