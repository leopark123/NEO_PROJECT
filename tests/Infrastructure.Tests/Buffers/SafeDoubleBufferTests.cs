// SafeDoubleBufferTests.cs
// SafeDoubleBuffer 功能测试

using Neo.Infrastructure.Buffers;
using Xunit;

namespace Neo.Infrastructure.Tests.Buffers;

/// <summary>
/// SafeDoubleBuffer 单元测试。
/// </summary>
public sealed class SafeDoubleBufferTests
{
    [Fact]
    public void Constructor_WithValidCapacity_CreatesBuffer()
    {
        // Act
        var buffer = new SafeDoubleBuffer<int>(100);

        // Assert
        Assert.Equal(100, buffer.Capacity);
        Assert.Equal(0, buffer.Version);
    }

    [Fact]
    public void Constructor_WithZeroCapacity_ThrowsException()
    {
        // Act & Assert
        Assert.Throws<ArgumentOutOfRangeException>(() => new SafeDoubleBuffer<int>(0));
    }

    [Fact]
    public void Constructor_WithNegativeCapacity_ThrowsException()
    {
        // Act & Assert
        Assert.Throws<ArgumentOutOfRangeException>(() => new SafeDoubleBuffer<int>(-1));
    }

    [Fact]
    public void AcquireWriteBuffer_ReturnsSpanWithCapacity()
    {
        // Arrange
        var buffer = new SafeDoubleBuffer<int>(100);

        // Act
        var span = buffer.AcquireWriteBuffer();

        // Assert
        Assert.Equal(100, span.Length);
    }

    [Fact]
    public void Publish_IncrementsVersion()
    {
        // Arrange
        var buffer = new SafeDoubleBuffer<int>(100);
        Assert.Equal(0, buffer.Version);

        // Act
        var span = buffer.AcquireWriteBuffer();
        span[0] = 42;
        buffer.Publish(1, 1000);

        // Assert
        Assert.Equal(1, buffer.Version);
    }

    [Fact]
    public void GetSnapshot_ReturnsPublishedData()
    {
        // Arrange
        var buffer = new SafeDoubleBuffer<int>(100);
        var span = buffer.AcquireWriteBuffer();
        span[0] = 42;
        span[1] = 43;
        span[2] = 44;
        buffer.Publish(3, 1000);

        // Act
        var snapshot = buffer.GetSnapshot();

        // Assert
        Assert.Equal(3, snapshot.Count);
        Assert.Equal(1000, snapshot.TimestampUs);
        Assert.Equal(1, snapshot.Version);
        Assert.Equal(42, snapshot.Data[0]);
        Assert.Equal(43, snapshot.Data[1]);
        Assert.Equal(44, snapshot.Data[2]);
    }

    [Fact]
    public void GetSnapshot_BeforePublish_ReturnsEmptyData()
    {
        // Arrange
        var buffer = new SafeDoubleBuffer<int>(100);

        // Act
        var snapshot = buffer.GetSnapshot();

        // Assert
        Assert.Equal(0, snapshot.Count);
        Assert.Equal(0, snapshot.Version);
    }

    [Fact]
    public void TryGetSnapshot_WithSameVersion_ReturnsFalse()
    {
        // Arrange
        var buffer = new SafeDoubleBuffer<int>(100);
        var span = buffer.AcquireWriteBuffer();
        span[0] = 42;
        buffer.Publish(1, 1000);

        var firstSnapshot = buffer.GetSnapshot();

        // Act
        bool hasNew = buffer.TryGetSnapshot(firstSnapshot.Version, out var newSnapshot);

        // Assert
        Assert.False(hasNew);
    }

    [Fact]
    public void TryGetSnapshot_WithDifferentVersion_ReturnsTrue()
    {
        // Arrange
        var buffer = new SafeDoubleBuffer<int>(100);

        var span = buffer.AcquireWriteBuffer();
        span[0] = 42;
        buffer.Publish(1, 1000);

        // Get first snapshot
        var firstSnapshot = buffer.GetSnapshot();

        // Publish new data
        var span2 = buffer.AcquireWriteBuffer();
        span2[0] = 100;
        buffer.Publish(1, 2000);

        // Act
        bool hasNew = buffer.TryGetSnapshot(firstSnapshot.Version, out var newSnapshot);

        // Assert
        Assert.True(hasNew);
        Assert.Equal(100, newSnapshot.Data[0]);
        Assert.Equal(2000, newSnapshot.TimestampUs);
    }

    [Fact]
    public void MultiplePublishes_AlternatesBuffers()
    {
        // Arrange
        var buffer = new SafeDoubleBuffer<int>(100);

        // First publish
        var span1 = buffer.AcquireWriteBuffer();
        span1[0] = 1;
        buffer.Publish(1, 1000);

        // Second publish
        var span2 = buffer.AcquireWriteBuffer();
        span2[0] = 2;
        buffer.Publish(1, 2000);

        // Third publish
        var span3 = buffer.AcquireWriteBuffer();
        span3[0] = 3;
        buffer.Publish(1, 3000);

        // Act
        var snapshot = buffer.GetSnapshot();

        // Assert
        Assert.Equal(3, snapshot.Data[0]);
        Assert.Equal(3000, snapshot.TimestampUs);
        Assert.Equal(3, snapshot.Version);
    }

    [Fact]
    public void Publish_WithZeroCount_Succeeds()
    {
        // Arrange
        var buffer = new SafeDoubleBuffer<int>(100);

        // Act
        buffer.AcquireWriteBuffer();
        buffer.Publish(0, 1000);

        // Assert
        var snapshot = buffer.GetSnapshot();
        Assert.Equal(0, snapshot.Count);
    }

    [Fact]
    public void Publish_WithCountExceedingCapacity_ThrowsException()
    {
        // Arrange
        var buffer = new SafeDoubleBuffer<int>(100);

        // Act & Assert
        buffer.AcquireWriteBuffer();
        Assert.Throws<ArgumentOutOfRangeException>(() => buffer.Publish(101, 1000));
    }

    [Fact]
    public void Reset_ClearsState()
    {
        // Arrange
        var buffer = new SafeDoubleBuffer<int>(100);
        var span = buffer.AcquireWriteBuffer();
        span[0] = 42;
        buffer.Publish(1, 1000);

        // Act
        buffer.Reset();

        // Assert
        var snapshot = buffer.GetSnapshot();
        Assert.Equal(0, snapshot.Count);
        Assert.Equal(0, buffer.Version);
    }

    [Fact]
    public void BufferSnapshot_IsEmpty_WorksCorrectly()
    {
        // Arrange
        var empty = BufferSnapshot<int>.Empty;
        var buffer = new SafeDoubleBuffer<int>(100);
        var span = buffer.AcquireWriteBuffer();
        span[0] = 42;
        buffer.Publish(1, 1000);
        var nonEmpty = buffer.GetSnapshot();

        // Assert
        Assert.True(empty.IsEmpty);
        Assert.False(nonEmpty.IsEmpty);
    }
}
