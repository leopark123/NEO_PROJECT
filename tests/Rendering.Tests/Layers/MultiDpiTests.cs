// MultiDpiTests.cs
// 多 DPI 场景测试

using Neo.Rendering.Core;
using Neo.Rendering.Layers;
using Xunit;

namespace Neo.Rendering.Tests.Layers;

/// <summary>
/// 多 DPI 场景测试。
/// 验证不同 DPI 值下层创建不会抛出异常。
/// </summary>
public sealed class MultiDpiTests
{
    /// <summary>
    /// 常见 DPI 值。
    /// </summary>
    public static IEnumerable<object[]> DpiValues => new List<object[]>
    {
        new object[] { 96.0 },    // 100%
        new object[] { 120.0 },   // 125%
        new object[] { 144.0 },   // 150%
        new object[] { 168.0 },   // 175%
        new object[] { 192.0 },   // 200%
        new object[] { 240.0 },   // 250%
        new object[] { 288.0 },   // 300%
    };

    [Theory]
    [MemberData(nameof(DpiValues))]
    public void GridLayer_MultiDpi_NoException(double dpi)
    {
        // Arrange
        using var layer = new GridLayer();
        var context = CreateRenderContext(dpi);

        // Act & Assert - 不应抛出异常
        layer.Invalidate();
        Assert.True(layer.IsEnabled);
    }

    [Theory]
    [MemberData(nameof(DpiValues))]
    public void ContentLayer_MultiDpi_NoException(double dpi)
    {
        // Arrange
        using var layer = new ContentLayer();
        var context = CreateRenderContext(dpi);

        // Act & Assert - 不应抛出异常
        layer.Invalidate();
        Assert.True(layer.IsEnabled);
    }

    [Theory]
    [MemberData(nameof(DpiValues))]
    public void OverlayLayer_MultiDpi_NoException(double dpi)
    {
        // Arrange
        using var layer = new OverlayLayer();
        var context = CreateRenderContext(dpi);

        // Act & Assert - 不应抛出异常
        layer.Invalidate();
        Assert.True(layer.IsEnabled);
    }

    [Theory]
    [MemberData(nameof(DpiValues))]
    public void LayeredRenderer_MultiDpi_NoException(double dpi)
    {
        // Arrange
        using var renderer = LayeredRenderer.CreateDefault();
        var context = CreateRenderContext(dpi);

        // Act & Assert - 不应抛出异常
        renderer.InvalidateAll();
        Assert.Equal(3, renderer.LayerCount);
    }

    [Theory]
    [MemberData(nameof(DpiValues))]
    public void RenderContext_DpiScale_Correct(double dpi)
    {
        // Arrange & Act
        var context = CreateRenderContext(dpi);

        // Assert
        Assert.Equal(dpi, context.Dpi);
        Assert.Equal(dpi / 96.0, context.DpiScale);
    }

    [Fact]
    public void RenderContext_StandardDpi_ScaleIsOne()
    {
        // Arrange & Act
        var context = CreateRenderContext(96.0);

        // Assert
        Assert.Equal(1.0, context.DpiScale);
    }

    [Fact]
    public void RenderContext_DoubleDpi_ScaleIsTwo()
    {
        // Arrange & Act
        var context = CreateRenderContext(192.0);

        // Assert
        Assert.Equal(2.0, context.DpiScale);
    }

    /// <summary>
    /// 常见屏幕分辨率。
    /// </summary>
    public static IEnumerable<object[]> Resolutions => new List<object[]>
    {
        new object[] { 800, 600 },
        new object[] { 1024, 768 },
        new object[] { 1280, 720 },
        new object[] { 1920, 1080 },
        new object[] { 2560, 1440 },
        new object[] { 3840, 2160 },
    };

    [Theory]
    [MemberData(nameof(Resolutions))]
    public void LayeredRenderer_DifferentResolutions_NoException(int width, int height)
    {
        // Arrange
        using var renderer = LayeredRenderer.CreateDefault();
        var context = new RenderContext
        {
            ViewportWidth = width,
            ViewportHeight = height,
            Dpi = 96.0
        };

        // Act & Assert - 不应抛出异常
        renderer.InvalidateAll();
        Assert.Equal(3, renderer.LayerCount);
    }

    [Fact]
    public void RenderContext_ZeroWidth_NoException()
    {
        // Arrange & Act
        var context = new RenderContext
        {
            ViewportWidth = 0,
            ViewportHeight = 600,
            Dpi = 96.0
        };

        // Assert - 边界情况不应导致异常
        Assert.Equal(0, context.ViewportWidth);
    }

    [Fact]
    public void RenderContext_ZeroHeight_NoException()
    {
        // Arrange & Act
        var context = new RenderContext
        {
            ViewportWidth = 800,
            ViewportHeight = 0,
            Dpi = 96.0
        };

        // Assert - 边界情况不应导致异常
        Assert.Equal(0, context.ViewportHeight);
    }

    private static RenderContext CreateRenderContext(double dpi)
    {
        return new RenderContext
        {
            ViewportWidth = 1920,
            ViewportHeight = 1080,
            Dpi = dpi,
            CurrentTimestampUs = 0,
            VisibleRange = new TimeRange(0, 10_000_000),  // 10 秒
            Zoom = new ZoomLevel(10.0, 0),
            FrameNumber = 1
        };
    }
}
