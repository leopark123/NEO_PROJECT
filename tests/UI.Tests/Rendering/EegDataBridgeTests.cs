// EegDataBridgeTests.cs
// Sprint 3.2: EegDataBridge unit tests

using Neo.Core.Enums;
using Neo.Core.Models;
using Neo.Rendering.Core;
using Neo.UI.Rendering;
using Xunit;

namespace Neo.UI.Tests.Rendering;

/// <summary>
/// EegDataBridge unit tests.
/// </summary>
public sealed class EegDataBridgeTests
{
    [Fact]
    public void Constructor_DefaultSampleRate_Is160()
    {
        // Act
        using var bridge = new EegDataBridge();

        // Assert
        Assert.Equal(160, bridge.SampleRate);
    }

    [Fact]
    public void Constructor_CustomSampleRate_IsUsed()
    {
        // Act
        using var bridge = new EegDataBridge(sampleRate: 256);

        // Assert
        Assert.Equal(256, bridge.SampleRate);
    }

    [Fact]
    public void Constructor_Channels_Is4()
    {
        // Act
        using var bridge = new EegDataBridge();

        // Assert
        Assert.Equal(4, bridge.Channels);
    }

    [Fact]
    public void Constructor_SampleCount_IsZero()
    {
        // Act
        using var bridge = new EegDataBridge();

        // Assert
        Assert.Equal(0, bridge.SampleCount);
    }

    [Fact]
    public void GetChannelData_EmptyBuffer_ReturnsEmptyArray()
    {
        // Arrange
        using var bridge = new EegDataBridge();
        var range = new TimeRange(0, 1_000_000);

        // Act
        var data = bridge.GetChannelData(range);

        // Assert
        Assert.Empty(data);
    }

    [Fact]
    public void Clear_ResetsSampleCount()
    {
        // Arrange
        using var bridge = new EegDataBridge();
        // Note: We can't easily add samples without a source, but we can test Clear doesn't throw

        // Act & Assert - should not throw
        bridge.Clear();
        Assert.Equal(0, bridge.SampleCount);
    }

    [Fact]
    public void Dispose_CanBeCalledMultipleTimes()
    {
        // Arrange
        var bridge = new EegDataBridge();

        // Act & Assert - should not throw
        bridge.Dispose();
        bridge.Dispose();
        bridge.Dispose();
    }

    [Fact]
    public void AttachSource_ThenDetach_DoesNotThrow()
    {
        // Arrange
        using var bridge = new EegDataBridge();
        var source = new TestEegSource();

        // Act & Assert - should not throw
        bridge.AttachSource(source);
        bridge.DetachSource();
    }

    [Fact]
    public void AttachSource_ReceivesSamples()
    {
        // Arrange
        using var bridge = new EegDataBridge(sampleRate: 160, bufferSeconds: 1);
        var source = new TestEegSource();

        // Act
        bridge.AttachSource(source);
        source.EmitSample(new EegSample
        {
            TimestampUs = 1_000_000,
            Ch1Uv = 10.0,
            Ch2Uv = 20.0,
            Ch3Uv = 30.0,
            Ch4Uv = 40.0,
            QualityFlags = QualityFlag.Normal
        });

        // Assert
        Assert.Equal(1, bridge.SampleCount);

        // Cleanup
        bridge.DetachSource();
    }

    [Fact]
    public void GetChannelData_WithSamples_ReturnsCorrectData()
    {
        // Arrange
        using var bridge = new EegDataBridge(sampleRate: 160, bufferSeconds: 1);
        var source = new TestEegSource();
        bridge.AttachSource(source);

        // Emit some samples
        for (int i = 0; i < 10; i++)
        {
            source.EmitSample(new EegSample
            {
                TimestampUs = i * 6250, // 160 Hz = 6250 μs interval
                Ch1Uv = 10.0 + i,
                Ch2Uv = 20.0 + i,
                Ch3Uv = 30.0 + i,
                Ch4Uv = 40.0 + i,
                QualityFlags = QualityFlag.Normal
            });
        }

        // Act
        var range = new TimeRange(0, 100_000); // 100ms range
        var data = bridge.GetChannelData(range);

        // Assert
        Assert.Equal(4, data.Length); // 4 channels
        Assert.True(data[0].DataPoints.Length > 0);
        Assert.Equal("CH1 (C3-P3)", data[0].ChannelName);
        Assert.Equal("CH2 (C4-P4)", data[1].ChannelName);

        // Cleanup
        bridge.DetachSource();
    }

    [Fact]
    public void GetChannelData_OutOfRange_ReturnsEmpty()
    {
        // Arrange
        using var bridge = new EegDataBridge(sampleRate: 160, bufferSeconds: 1);
        var source = new TestEegSource();
        bridge.AttachSource(source);

        // Emit samples at time 1-2 seconds
        for (int i = 0; i < 10; i++)
        {
            source.EmitSample(new EegSample
            {
                TimestampUs = 1_000_000 + i * 6250,
                Ch1Uv = 10.0,
                Ch2Uv = 20.0,
                Ch3Uv = 30.0,
                Ch4Uv = 40.0,
                QualityFlags = QualityFlag.Normal
            });
        }

        // Act - query range before samples
        var range = new TimeRange(0, 500_000); // 0-0.5 seconds
        var data = bridge.GetChannelData(range);

        // Assert
        Assert.Empty(data);

        // Cleanup
        bridge.DetachSource();
    }

    /// <summary>
    /// Test helper: simple EEG source for testing.
    /// </summary>
    private sealed class TestEegSource : Neo.Core.Interfaces.ITimeSeriesSource<EegSample>
    {
        public int SampleRate => 160;
        public int ChannelCount => 4;
        public event Action<EegSample>? SampleReceived;

        public void Start() { }
        public void Stop() { }

        public void EmitSample(EegSample sample)
        {
            SampleReceived?.Invoke(sample);
        }
    }
}
