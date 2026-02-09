namespace Neo.Tests.DataSources.Rs232;

using Neo.DataSources.Rs232;
using Xunit;

public class Rs232NirsSourceConfigTests
{
    [Fact]
    public void Constructor_WithValidNoninConfig_DoesNotThrow()
    {
        var config = new Rs232Config
        {
            PortName = "COM9",
            BaudRate = 57600,
            DataBits = 8,
            StopBits = StopBitsOption.One,
            Parity = ParityOption.None
        };

        using var source = new Rs232NirsSource(config);
    }

    [Fact]
    public void Constructor_WithInvalidBaudRate_Throws()
    {
        var config = new Rs232Config
        {
            PortName = "COM9",
            BaudRate = 115200,
            DataBits = 8,
            StopBits = StopBitsOption.One,
            Parity = ParityOption.None
        };

        var ex = Assert.Throws<ArgumentException>(() => _ = new Rs232NirsSource(config));
        Assert.Contains("BaudRate must be 57600", ex.Message);
    }

    [Fact]
    public void Constructor_WithInvalidDataBits_Throws()
    {
        var config = new Rs232Config
        {
            PortName = "COM9",
            BaudRate = 57600,
            DataBits = 7,
            StopBits = StopBitsOption.One,
            Parity = ParityOption.None
        };

        var ex = Assert.Throws<ArgumentException>(() => _ = new Rs232NirsSource(config));
        Assert.Contains("DataBits must be 8", ex.Message);
    }

    [Fact]
    public void Constructor_WithInvalidStopBits_Throws()
    {
        var config = new Rs232Config
        {
            PortName = "COM9",
            BaudRate = 57600,
            DataBits = 8,
            StopBits = StopBitsOption.Two,
            Parity = ParityOption.None
        };

        var ex = Assert.Throws<ArgumentException>(() => _ = new Rs232NirsSource(config));
        Assert.Contains("StopBits must be One", ex.Message);
    }

    [Fact]
    public void Constructor_WithInvalidParity_Throws()
    {
        var config = new Rs232Config
        {
            PortName = "COM9",
            BaudRate = 57600,
            DataBits = 8,
            StopBits = StopBitsOption.One,
            Parity = ParityOption.Even
        };

        var ex = Assert.Throws<ArgumentException>(() => _ = new Rs232NirsSource(config));
        Assert.Contains("Parity must be None", ex.Message);
    }
}
