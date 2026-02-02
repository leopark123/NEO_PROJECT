// GraphicsDeviceTests.cs
// D3D11 设备测试

using Neo.Rendering.Device;
using Xunit;

namespace Neo.Rendering.Tests.Device;

/// <summary>
/// GraphicsDevice 单元测试。
/// </summary>
public sealed class GraphicsDeviceTests : IDisposable
{
    private readonly GraphicsDevice _device;

    public GraphicsDeviceTests()
    {
        _device = new GraphicsDevice();
    }

    public void Dispose()
    {
        _device.Dispose();
    }

    [Fact]
    public void CreateDevice_WhenCalled_ReturnsTrue()
    {
        // Act
        var result = _device.CreateDevice();

        // Assert
        Assert.True(result);
        Assert.True(_device.IsDeviceValid);
    }

    [Fact]
    public void CreateDevice_WhenSuccessful_DeviceNotNull()
    {
        // Act
        _device.CreateDevice();

        // Assert
        Assert.NotNull(_device.Device);
        Assert.NotNull(_device.Context);
        Assert.NotNull(_device.DxgiFactory);
    }

    [Fact]
    public void CreateDevice_WhenSuccessful_FeatureLevelIsValid()
    {
        // Act
        _device.CreateDevice();

        // Assert
        Assert.True(_device.FeatureLevel >= Vortice.Direct3D.FeatureLevel.Level_10_0);
    }

    [Fact]
    public void IsDeviceValid_BeforeCreate_ReturnsFalse()
    {
        // Assert
        Assert.False(_device.IsDeviceValid);
    }

    [Fact]
    public void Dispose_AfterCreate_InvalidatesDevice()
    {
        // Arrange
        _device.CreateDevice();
        Assert.True(_device.IsDeviceValid);

        // Act
        _device.Dispose();

        // Assert
        Assert.False(_device.IsDeviceValid);
    }

    [Fact]
    public void Dispose_MultipleCalls_DoesNotThrow()
    {
        // Arrange
        _device.CreateDevice();

        // Act & Assert
        _device.Dispose();
        _device.Dispose(); // Should not throw
    }

    [Fact]
    public void CheckDeviceLost_AfterCreate_ReturnsFalse()
    {
        // Arrange
        _device.CreateDevice();

        // Act
        var result = _device.CheckDeviceLost();

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void CheckDeviceLost_BeforeCreate_ReturnsTrue()
    {
        // Act
        var result = _device.CheckDeviceLost();

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void DeviceLost_Event_RaisedOnHandleDeviceLost()
    {
        // Arrange
        _device.CreateDevice();
        var eventRaised = false;
        _device.DeviceLost += () => eventRaised = true;

        // Act
        _device.HandleDeviceLost();

        // Assert
        Assert.True(eventRaised);
    }

    [Fact]
    public void DeviceRestored_Event_RaisedOnSuccessfulRecovery()
    {
        // Arrange
        _device.CreateDevice();
        var eventRaised = false;
        _device.DeviceRestored += () => eventRaised = true;

        // Act
        var result = _device.HandleDeviceLost();

        // Assert
        Assert.True(result);
        Assert.True(eventRaised);
    }
}
