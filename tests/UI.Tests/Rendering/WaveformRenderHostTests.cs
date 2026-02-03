// WaveformRenderHostTests.cs
// Sprint 3.1: WaveformRenderHost unit tests
//
// Note: These tests verify non-GPU functionality and lifecycle behavior.
// GPU rendering is validated through integration tests and manual testing.

using Neo.UI.Rendering;
using Xunit;

namespace Neo.UI.Tests.Rendering;

/// <summary>
/// WaveformRenderHost unit tests.
/// </summary>
public sealed class WaveformRenderHostTests
{
    [Fact]
    public void Constructor_CreatesLayeredRenderer()
    {
        // Act
        using var host = new WaveformRenderHost();

        // Assert
        Assert.NotNull(host.LayeredRenderer);
        Assert.Equal(3, host.LayeredRenderer.LayerCount); // Grid, Content, Overlay
    }

    [Fact]
    public void Constructor_CreatesResourceCache()
    {
        // Act
        using var host = new WaveformRenderHost();

        // Assert
        Assert.NotNull(host.ResourceCache);
    }

    [Fact]
    public void Constructor_IsNotRunning()
    {
        // Act
        using var host = new WaveformRenderHost();

        // Assert
        Assert.False(host.IsRunning);
    }

    [Fact]
    public void Constructor_FrameNumberIsZero()
    {
        // Act
        using var host = new WaveformRenderHost();

        // Assert
        Assert.Equal(0, host.FrameNumber);
    }

    [Fact]
    public void Constructor_DefaultVisibleDuration_Is15Seconds()
    {
        // Act
        using var host = new WaveformRenderHost();

        // Assert
        Assert.Equal(15_000_000, host.VisibleDurationUs); // 15 seconds in microseconds
    }

    [Fact]
    public void VisibleDurationUs_CanBeChanged()
    {
        // Arrange
        using var host = new WaveformRenderHost();

        // Act
        host.VisibleDurationUs = 30_000_000; // 30 seconds

        // Assert
        Assert.Equal(30_000_000, host.VisibleDurationUs);
    }

    [Fact]
    public void VisibleDurationUs_MinimumIs1Second()
    {
        // Arrange
        using var host = new WaveformRenderHost();

        // Act
        host.VisibleDurationUs = 100; // Very small value

        // Assert
        Assert.Equal(1_000_000, host.VisibleDurationUs); // Clamped to 1 second
    }

    [Fact]
    public void Width_InitiallyZero()
    {
        // Act
        using var host = new WaveformRenderHost();

        // Assert
        Assert.Equal(0, host.Width);
    }

    [Fact]
    public void Height_InitiallyZero()
    {
        // Act
        using var host = new WaveformRenderHost();

        // Assert
        Assert.Equal(0, host.Height);
    }

    [Fact]
    public void Dispose_CanBeCalledMultipleTimes()
    {
        // Arrange
        var host = new WaveformRenderHost();

        // Act & Assert - should not throw
        host.Dispose();
        host.Dispose();
        host.Dispose();
    }

    [Fact]
    public void Dispose_StopsRendering()
    {
        // Arrange
        var host = new WaveformRenderHost();

        // Act
        host.Dispose();

        // Assert - IsRunning should be false after dispose
        Assert.False(host.IsRunning);
    }

    [Fact]
    public void Resize_WithZeroWidth_DoesNotThrow()
    {
        // Arrange
        using var host = new WaveformRenderHost();

        // Act & Assert - should not throw
        host.Resize(0, 100);
    }

    [Fact]
    public void Resize_WithZeroHeight_DoesNotThrow()
    {
        // Arrange
        using var host = new WaveformRenderHost();

        // Act & Assert - should not throw
        host.Resize(100, 0);
    }

    [Fact]
    public void Resize_WithNegativeValues_DoesNotThrow()
    {
        // Arrange
        using var host = new WaveformRenderHost();

        // Act & Assert - should not throw
        host.Resize(-1, -1);
    }

    [Fact]
    public void LayeredRenderer_HasGridLayer()
    {
        // Arrange
        using var host = new WaveformRenderHost();

        // Act
        var gridLayer = host.LayeredRenderer.GetLayer("Grid");

        // Assert
        Assert.NotNull(gridLayer);
    }

    [Fact]
    public void LayeredRenderer_HasContentLayer()
    {
        // Arrange
        using var host = new WaveformRenderHost();

        // Act
        var contentLayer = host.LayeredRenderer.GetLayer("Content");

        // Assert
        Assert.NotNull(contentLayer);
    }

    [Fact]
    public void LayeredRenderer_HasOverlayLayer()
    {
        // Arrange
        using var host = new WaveformRenderHost();

        // Act
        var overlayLayer = host.LayeredRenderer.GetLayer("Overlay");

        // Assert
        Assert.NotNull(overlayLayer);
    }
}
