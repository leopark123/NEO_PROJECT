// DpiHelperTests.cs
// DPI 工具测试

using Neo.Rendering.Device;
using Xunit;

namespace Neo.Rendering.Tests.Device;

/// <summary>
/// DpiHelper 单元测试。
/// </summary>
public sealed class DpiHelperTests
{
    private const double Tolerance = 0.0001;

    [Fact]
    public void GetSystemDpi_ReturnsPositiveValue()
    {
        // Act
        var dpi = DpiHelper.GetSystemDpi();

        // Assert
        Assert.True(dpi > 0);
        Assert.True(dpi >= 96); // 最小为 96 DPI
    }

    [Fact]
    public void GetWindowDpi_WithNullHandle_ReturnsSystemDpi()
    {
        // Act
        var dpi = DpiHelper.GetWindowDpi(IntPtr.Zero);

        // Assert
        Assert.Equal(DpiHelper.GetSystemDpi(), dpi);
    }

    [Theory]
    [InlineData(96.0, 1.0)]
    [InlineData(120.0, 1.25)]
    [InlineData(144.0, 1.5)]
    [InlineData(192.0, 2.0)]
    public void GetScaleFactor_ReturnsCorrectValue(double dpi, double expectedScale)
    {
        // Act
        var scale = DpiHelper.GetScaleFactor(dpi);

        // Assert
        Assert.Equal(expectedScale, scale, Tolerance);
    }

    [Theory]
    [InlineData(100.0, 96.0, 100.0)]
    [InlineData(100.0, 120.0, 125.0)]
    [InlineData(100.0, 144.0, 150.0)]
    [InlineData(100.0, 192.0, 200.0)]
    [InlineData(50.0, 192.0, 100.0)]
    public void DipToPixel_ReturnsCorrectValue(double dip, double dpi, double expectedPixel)
    {
        // Act
        var pixel = DpiHelper.DipToPixel(dip, dpi);

        // Assert
        Assert.Equal(expectedPixel, pixel, Tolerance);
    }

    [Theory]
    [InlineData(100.0, 96.0, 100.0)]
    [InlineData(125.0, 120.0, 100.0)]
    [InlineData(150.0, 144.0, 100.0)]
    [InlineData(200.0, 192.0, 100.0)]
    [InlineData(100.0, 192.0, 50.0)]
    public void PixelToDip_ReturnsCorrectValue(double pixel, double dpi, double expectedDip)
    {
        // Act
        var dip = DpiHelper.PixelToDip(pixel, dpi);

        // Assert
        Assert.Equal(expectedDip, dip, Tolerance);
    }

    [Theory]
    [InlineData(100.5, 96.0, 101)] // Ceiling
    [InlineData(100.1, 96.0, 101)]
    [InlineData(100.0, 96.0, 100)]
    public void DipToPixelCeiling_RoundsUp(double dip, double dpi, int expectedPixel)
    {
        // Act
        var pixel = DpiHelper.DipToPixelCeiling(dip, dpi);

        // Assert
        Assert.Equal(expectedPixel, pixel);
    }

    [Theory]
    [InlineData(100.5, 96.0, 101)] // Rounds to nearest
    [InlineData(100.4, 96.0, 100)]
    [InlineData(100.0, 96.0, 100)]
    public void DipToPixelRound_RoundsToNearest(double dip, double dpi, int expectedPixel)
    {
        // Act
        var pixel = DpiHelper.DipToPixelRound(dip, dpi);

        // Assert
        Assert.Equal(expectedPixel, pixel);
    }

    [Fact]
    public void DipToPixel_And_PixelToDip_AreInverse()
    {
        // Arrange
        double originalDip = 123.456;
        double dpi = 144.0;

        // Act
        var pixel = DpiHelper.DipToPixel(originalDip, dpi);
        var roundTrip = DpiHelper.PixelToDip(pixel, dpi);

        // Assert
        Assert.Equal(originalDip, roundTrip, Tolerance);
    }

    [Fact]
    public void StandardDpi_Is96()
    {
        // Assert
        Assert.Equal(96.0, DpiHelper.StandardDpi);
    }

    [Fact]
    public void DipToPixel_AtStandardDpi_ReturnsSameValue()
    {
        // Arrange
        double dip = 100.0;

        // Act
        var pixel = DpiHelper.DipToPixel(dip, DpiHelper.StandardDpi);

        // Assert
        Assert.Equal(dip, pixel, Tolerance);
    }
}
