// RenderContextTests.cs
// 渲染上下文测试

using Neo.Rendering.Core;
using Xunit;

namespace Neo.Rendering.Tests.Core;

/// <summary>
/// RenderContext 单元测试。
/// </summary>
public sealed class RenderContextTests
{
    private const double Tolerance = 0.0001;

    [Fact]
    public void TimeRange_DurationUs_ReturnsCorrectValue()
    {
        // Arrange
        var range = new TimeRange(1000, 5000);

        // Act
        var duration = range.DurationUs;

        // Assert
        Assert.Equal(4000, duration);
    }

    [Fact]
    public void ZoomLevel_Properties_AreAccessible()
    {
        // Arrange
        var zoom = new ZoomLevel(10.0, 2);

        // Assert
        Assert.Equal(10.0, zoom.SecondsPerScreen);
        Assert.Equal(2, zoom.LodLevel);
    }

    [Fact]
    public void RenderContext_DpiScale_CalculatesCorrectly()
    {
        // Arrange
        var context = new RenderContext { Dpi = 144.0 };

        // Act
        var scale = context.DpiScale;

        // Assert
        Assert.Equal(1.5, scale, Tolerance);
    }

    [Fact]
    public void RenderContext_DefaultDpi_Is96()
    {
        // Arrange
        var context = new RenderContext();

        // Assert
        Assert.Equal(96.0, context.Dpi);
        Assert.Equal(1.0, context.DpiScale, Tolerance);
    }

    [Fact]
    public void TimestampToX_AtStartOfRange_ReturnsZero()
    {
        // Arrange
        var context = new RenderContext
        {
            VisibleRange = new TimeRange(0, 1_000_000), // 1 second
            ViewportWidth = 1000
        };

        // Act
        var x = context.TimestampToX(0);

        // Assert
        Assert.Equal(0.0, x, Tolerance);
    }

    [Fact]
    public void TimestampToX_AtEndOfRange_ReturnsViewportWidth()
    {
        // Arrange
        var context = new RenderContext
        {
            VisibleRange = new TimeRange(0, 1_000_000), // 1 second
            ViewportWidth = 1000
        };

        // Act
        var x = context.TimestampToX(1_000_000);

        // Assert
        Assert.Equal(1000.0, x, Tolerance);
    }

    [Fact]
    public void TimestampToX_AtMidpoint_ReturnsHalfWidth()
    {
        // Arrange
        var context = new RenderContext
        {
            VisibleRange = new TimeRange(0, 1_000_000), // 1 second
            ViewportWidth = 1000
        };

        // Act
        var x = context.TimestampToX(500_000);

        // Assert
        Assert.Equal(500.0, x, Tolerance);
    }

    [Fact]
    public void TimestampToX_WithOffset_CalculatesCorrectly()
    {
        // Arrange
        var context = new RenderContext
        {
            VisibleRange = new TimeRange(1_000_000, 2_000_000), // 1-2 seconds
            ViewportWidth = 1000
        };

        // Act
        var x = context.TimestampToX(1_500_000); // 1.5 seconds

        // Assert
        Assert.Equal(500.0, x, Tolerance);
    }

    [Fact]
    public void XToTimestamp_AtZero_ReturnsRangeStart()
    {
        // Arrange
        var context = new RenderContext
        {
            VisibleRange = new TimeRange(1_000_000, 2_000_000),
            ViewportWidth = 1000
        };

        // Act
        var timestamp = context.XToTimestamp(0);

        // Assert
        Assert.Equal(1_000_000, timestamp);
    }

    [Fact]
    public void XToTimestamp_AtViewportWidth_ReturnsRangeEnd()
    {
        // Arrange
        var context = new RenderContext
        {
            VisibleRange = new TimeRange(0, 1_000_000),
            ViewportWidth = 1000
        };

        // Act
        var timestamp = context.XToTimestamp(1000);

        // Assert
        Assert.Equal(1_000_000, timestamp);
    }

    [Fact]
    public void TimestampToX_And_XToTimestamp_AreInverse()
    {
        // Arrange
        var context = new RenderContext
        {
            VisibleRange = new TimeRange(0, 1_000_000),
            ViewportWidth = 1000
        };
        long originalTimestamp = 333_333;

        // Act
        var x = context.TimestampToX(originalTimestamp);
        var roundTrip = context.XToTimestamp(x);

        // Assert
        Assert.Equal(originalTimestamp, roundTrip);
    }

    [Fact]
    public void TimestampToX_ZeroDuration_ReturnsZero()
    {
        // Arrange
        var context = new RenderContext
        {
            VisibleRange = new TimeRange(1000, 1000), // Zero duration
            ViewportWidth = 1000
        };

        // Act
        var x = context.TimestampToX(1000);

        // Assert
        Assert.Equal(0.0, x);
    }

    [Fact]
    public void XToTimestamp_ZeroViewportWidth_ReturnsRangeStart()
    {
        // Arrange
        var context = new RenderContext
        {
            VisibleRange = new TimeRange(1000, 2000),
            ViewportWidth = 0
        };

        // Act
        var timestamp = context.XToTimestamp(100);

        // Assert
        Assert.Equal(1000, timestamp);
    }

    [Fact]
    public void ChannelRenderData_CanBeCreated()
    {
        // Arrange
        var data = new float[] { 1.0f, 2.0f, 3.0f };
        var quality = new byte[] { 0, 0, 0 };

        // Act
        var channelData = new ChannelRenderData
        {
            ChannelIndex = 0,
            ChannelName = "CH1",
            DataPoints = data,
            StartTimestampUs = 0,
            SampleIntervalUs = 6250,
            QualityFlags = quality
        };

        // Assert
        Assert.Equal(0, channelData.ChannelIndex);
        Assert.Equal("CH1", channelData.ChannelName);
        Assert.Equal(3, channelData.DataPoints.Length);
    }

    [Fact]
    public void RenderContext_ChannelsDefault_IsEmpty()
    {
        // Arrange
        var context = new RenderContext();

        // Assert
        Assert.Empty(context.Channels);
    }
}
