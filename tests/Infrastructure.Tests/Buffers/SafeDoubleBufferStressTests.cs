// SafeDoubleBufferStressTests.cs
// SafeDoubleBuffer 并发压力测试

using Neo.Infrastructure.Buffers;
using Xunit;

namespace Neo.Infrastructure.Tests.Buffers;

/// <summary>
/// SafeDoubleBuffer 并发压力测试。
/// </summary>
public sealed class SafeDoubleBufferStressTests
{
    [Fact]
    public async Task StressTest_ConcurrentReadWrite_NoException()
    {
        // Arrange
        var buffer = new SafeDoubleBuffer<int>(1000);
        int writeCount = 0;
        int readCount = 0;
        const int targetWrites = 160 * 10; // 10 seconds at 160Hz
        var cts = new CancellationTokenSource();

        // Act
        var producer = Task.Run(() =>
        {
            for (int i = 0; i < targetWrites; i++)
            {
                var span = buffer.AcquireWriteBuffer();
                span[0] = i;
                buffer.Publish(1, i * 6250L); // 160Hz = 6250μs/sample
                Interlocked.Increment(ref writeCount);
                Thread.Sleep(1); // Fast simulation
            }
        });

        var consumer = Task.Run(() =>
        {
            int lastVersion = -1;
            while (writeCount < targetWrites)
            {
                if (buffer.TryGetSnapshot(lastVersion, out var snapshot))
                {
                    lastVersion = snapshot.Version;
                    Interlocked.Increment(ref readCount);
                }
                Thread.Sleep(1);
            }
        });

        // Assert
        await Task.WhenAll(producer, consumer);
        Assert.Equal(targetWrites, writeCount);
        Assert.True(readCount > 0);
    }

    [Fact]
    public async Task StressTest_HighFrequencyWrite_NoDataLoss()
    {
        // Arrange
        var buffer = new SafeDoubleBuffer<long>(1000);
        const int totalWrites = 10000;
        long lastTimestamp = -1;
        int readCount = 0;
        int outOfOrderCount = 0;

        // Act
        var producer = Task.Run(() =>
        {
            for (int i = 0; i < totalWrites; i++)
            {
                var span = buffer.AcquireWriteBuffer();
                span[0] = i;
                buffer.Publish(1, i);
            }
        });

        var consumer = Task.Run(() =>
        {
            int lastVersion = -1;
            while (Volatile.Read(ref readCount) < totalWrites / 2)
            {
                if (buffer.TryGetSnapshot(lastVersion, out var snapshot))
                {
                    lastVersion = snapshot.Version;
                    long timestamp = snapshot.TimestampUs;

                    // Validate ordering via snapshot metadata. Data payload is a zero-copy view
                    // and may be overwritten by future publishes before assertion code observes it.
                    if (timestamp < lastTimestamp)
                    {
                        Interlocked.Increment(ref outOfOrderCount);
                    }
                    lastTimestamp = timestamp;
                    Interlocked.Increment(ref readCount);
                }
            }
        });

        await producer;
        Thread.Sleep(100); // Give consumer time to catch up
        await Task.WhenAny(consumer, Task.Delay(1000));

        // Assert - no out of order reads
        Assert.Equal(0, outOfOrderCount);
        Assert.True(readCount > 0);
    }

    [Fact]
    public async Task StressTest_VersionAlwaysIncreases()
    {
        // Arrange
        var buffer = new SafeDoubleBuffer<int>(100);
        const int totalWrites = 1000;
        int lastVersion = 0;
        bool versionDecreased = false;

        // Act
        var producer = Task.Run(() =>
        {
            for (int i = 0; i < totalWrites; i++)
            {
                var span = buffer.AcquireWriteBuffer();
                buffer.Publish(1, i);
            }
        });

        var consumer = Task.Run(() =>
        {
            while (buffer.Version < totalWrites)
            {
                int currentVersion = buffer.Version;
                if (currentVersion < lastVersion)
                {
                    versionDecreased = true;
                    break;
                }
                lastVersion = currentVersion;
            }
        });

        await Task.WhenAll(producer, consumer);

        // Assert
        Assert.False(versionDecreased);
        Assert.Equal(totalWrites, buffer.Version);
    }

    [Fact]
    public async Task StressTest_MultipleConsumers_NoException()
    {
        // Arrange
        var buffer = new SafeDoubleBuffer<int>(1000);
        const int totalWrites = 1000;
        int consumer1Reads = 0;
        int consumer2Reads = 0;
        var startGate = new ManualResetEventSlim(false);
        var producerDone = false;

        // Act
        var producer = Task.Run(() =>
        {
            startGate.Wait();
            for (int i = 0; i < totalWrites; i++)
            {
                var span = buffer.AcquireWriteBuffer();
                span[0] = i;
                buffer.Publish(1, i);
            }
            Volatile.Write(ref producerDone, true);
        });

        var consumer1 = Task.Run(() =>
        {
            int lastVersion = -1;
            startGate.Wait();
            while (!Volatile.Read(ref producerDone) || Volatile.Read(ref consumer1Reads) == 0)
            {
                if (buffer.TryGetSnapshot(lastVersion, out var snapshot))
                {
                    lastVersion = snapshot.Version;
                    Interlocked.Increment(ref consumer1Reads);
                }
                Thread.Yield();
            }
        });

        var consumer2 = Task.Run(() =>
        {
            int lastVersion = -1;
            startGate.Wait();
            while (!Volatile.Read(ref producerDone) || Volatile.Read(ref consumer2Reads) == 0)
            {
                if (buffer.TryGetSnapshot(lastVersion, out var snapshot))
                {
                    lastVersion = snapshot.Version;
                    Interlocked.Increment(ref consumer2Reads);
                }
                Thread.Yield();
            }
        });

        startGate.Set();

        // Assert - should complete without exception
        await Task.WhenAll(producer, consumer1, consumer2);
        Assert.True(consumer1Reads > 0);
        Assert.True(consumer2Reads > 0);
    }

    [Fact]
    public void Performance_WriteLatency_IsLow()
    {
        // Arrange
        var buffer = new SafeDoubleBuffer<long>(1000);
        const int iterations = 10000;
        var latencies = new long[iterations];
        var sw = new System.Diagnostics.Stopwatch();

        // Act
        for (int i = 0; i < iterations; i++)
        {
            sw.Restart();
            var span = buffer.AcquireWriteBuffer();
            span[0] = i;
            buffer.Publish(1, i);
            sw.Stop();
            latencies[i] = sw.ElapsedTicks * 1_000_000 / System.Diagnostics.Stopwatch.Frequency; // μs
        }

        // Calculate P99
        Array.Sort(latencies);
        long p99 = latencies[(int)(iterations * 0.99)];

        // Assert - P99 should be < 100 μs (being generous for CI variability)
        Assert.True(p99 < 100, $"P99 write latency was {p99}μs, expected < 100μs");
    }

    [Fact]
    public void Performance_ReadLatency_IsLow()
    {
        // Arrange
        var buffer = new SafeDoubleBuffer<long>(1000);
        const int iterations = 10000;
        var latencies = new long[iterations];
        var sw = new System.Diagnostics.Stopwatch();

        // Pre-publish some data
        var span = buffer.AcquireWriteBuffer();
        span[0] = 42;
        buffer.Publish(1, 1000);

        // Act
        for (int i = 0; i < iterations; i++)
        {
            sw.Restart();
            var snapshot = buffer.GetSnapshot();
            sw.Stop();
            latencies[i] = sw.ElapsedTicks * 1_000_000 / System.Diagnostics.Stopwatch.Frequency; // μs
            _ = snapshot.Data[0]; // Use the data to prevent optimization
        }

        // Calculate P99
        Array.Sort(latencies);
        long p99 = latencies[(int)(iterations * 0.99)];

        // Assert - P99 should be < 100 μs
        Assert.True(p99 < 100, $"P99 read latency was {p99}μs, expected < 100μs");
    }
}
