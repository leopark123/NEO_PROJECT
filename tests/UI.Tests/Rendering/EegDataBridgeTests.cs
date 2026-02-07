// EegDataBridgeTests.cs
// Sprint 3.2: EegDataBridge unit tests (sweep mode)

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
        using var bridge = new EegDataBridge();
        Assert.Equal(160, bridge.SampleRate);
    }

    [Fact]
    public void Constructor_CustomSampleRate_IsUsed()
    {
        using var bridge = new EegDataBridge(sampleRate: 256);
        Assert.Equal(256, bridge.SampleRate);
    }

    [Fact]
    public void Constructor_Channels_Is4()
    {
        using var bridge = new EegDataBridge();
        Assert.Equal(4, bridge.Channels);
    }

    [Fact]
    public void Constructor_SampleCount_IsZero()
    {
        using var bridge = new EegDataBridge();
        Assert.Equal(0, bridge.SampleCount);
    }

    [Fact]
    public void Constructor_HasData_IsFalse()
    {
        using var bridge = new EegDataBridge();
        Assert.False(bridge.HasData);
    }

    [Fact]
    public void Constructor_SamplesPerSweep_MatchesSampleRateTimesDuration()
    {
        using var bridge = new EegDataBridge(sampleRate: 160, sweepSeconds: 10);
        Assert.Equal(1600, bridge.SamplesPerSweep);
    }

    [Fact]
    public void Constructor_WriteIndex_StartsAtZero()
    {
        // Left-to-right sweep: write starts at leftmost position
        using var bridge = new EegDataBridge(sampleRate: 160, sweepSeconds: 10);
        Assert.Equal(0, bridge.WriteIndex);
    }

    [Fact]
    public void GetSweepData_EmptyBuffer_ReturnsEmptyArray()
    {
        using var bridge = new EegDataBridge();
        var data = bridge.GetSweepData();
        Assert.Empty(data);
    }

    [Fact]
    public void GetChannelData_AlwaysReturnsEmpty_InSweepMode()
    {
        using var bridge = new EegDataBridge();
        var range = new TimeRange(0, 1_000_000);
        var data = bridge.GetChannelData(range);
        Assert.Empty(data);
    }

    [Fact]
    public void Clear_ResetsState()
    {
        using var bridge = new EegDataBridge();
        bridge.Clear();
        Assert.Equal(0, bridge.SampleCount);
        Assert.False(bridge.HasData);
    }

    [Fact]
    public void Dispose_CanBeCalledMultipleTimes()
    {
        var bridge = new EegDataBridge();
        bridge.Dispose();
        bridge.Dispose();
        bridge.Dispose();
    }

    [Fact]
    public void AttachSource_ThenDetach_DoesNotThrow()
    {
        using var bridge = new EegDataBridge();
        var source = new TestEegSource();
        bridge.AttachSource(source);
        bridge.DetachSource();
    }

    [Fact]
    public void AttachSource_ReceivesSamples_HasDataTrue()
    {
        using var bridge = new EegDataBridge(sampleRate: 160, sweepSeconds: 1);
        var source = new TestEegSource();

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

        Assert.True(bridge.HasData);
        // In sweep mode, SampleCount returns full buffer size once data exists
        Assert.Equal(160, bridge.SampleCount);

        bridge.DetachSource();
    }

    [Fact]
    public void WriteIndex_Increments_AfterSample()
    {
        // Left-to-right sweep: writeIndex increments
        using var bridge = new EegDataBridge(sampleRate: 160, sweepSeconds: 1);
        var source = new TestEegSource();
        bridge.AttachSource(source);

        Assert.Equal(0, bridge.WriteIndex);

        source.EmitSample(new EegSample
        {
            TimestampUs = 0,
            Ch1Uv = 1.0,
            Ch2Uv = 2.0,
            Ch3Uv = 3.0,
            Ch4Uv = 4.0,
            QualityFlags = QualityFlag.Normal
        });

        // WriteIndex should have incremented by 1
        Assert.Equal(1, bridge.WriteIndex);

        bridge.DetachSource();
    }

    [Fact]
    public void WriteIndex_WrapsAround_AtEnd()
    {
        // When writeIndex reaches N-1, next increment wraps to 0
        using var bridge = new EegDataBridge(sampleRate: 2, sweepSeconds: 1); // Only 2 samples
        var source = new TestEegSource();
        bridge.AttachSource(source);

        // Initial: writeIndex = 0
        Assert.Equal(0, bridge.WriteIndex);

        // First sample: writeIndex = 1
        source.EmitSample(new EegSample { QualityFlags = QualityFlag.Normal });
        Assert.Equal(1, bridge.WriteIndex);

        // Second sample: writeIndex wraps to 0
        source.EmitSample(new EegSample { QualityFlags = QualityFlag.Normal });
        Assert.Equal(0, bridge.WriteIndex);

        bridge.DetachSource();
    }

    [Fact]
    public void GetSweepData_WithSamples_Returns4Channels()
    {
        using var bridge = new EegDataBridge(sampleRate: 160, sweepSeconds: 1);
        var source = new TestEegSource();
        bridge.AttachSource(source);

        source.EmitSample(new EegSample
        {
            TimestampUs = 0,
            Ch1Uv = 10.0,
            Ch2Uv = 20.0,
            Ch3Uv = 30.0,
            Ch4Uv = 40.0,
            QualityFlags = QualityFlag.Normal
        });

        var data = bridge.GetSweepData();

        Assert.Equal(4, data.Length);
        Assert.Equal("CH1 (C3-P3)", data[0].ChannelName);
        Assert.Equal("CH2 (C4-P4)", data[1].ChannelName);
        Assert.Equal("CH3 (P3-P4)", data[2].ChannelName);
        Assert.Equal("CH4 (C3-C4)", data[3].ChannelName);
        Assert.Equal(160, data[0].SamplesPerSweep);

        bridge.DetachSource();
    }

    [Fact]
    public void GetSweepData_SampleValues_AreCorrect()
    {
        using var bridge = new EegDataBridge(sampleRate: 160, sweepSeconds: 1);
        var source = new TestEegSource();
        bridge.AttachSource(source);

        // Write index starts at 0, sample writes at 0, then increments to 1
        source.EmitSample(new EegSample
        {
            TimestampUs = 0,
            Ch1Uv = 42.5,
            Ch2Uv = -10.0,
            Ch3Uv = 100.0,
            Ch4Uv = 0.0,
            QualityFlags = QualityFlag.Normal
        });

        var data = bridge.GetSweepData();
        // Sample was written at index 0 (the initial writeIndex)
        Assert.Equal(42.5f, data[0].Samples.Span[0]);
        Assert.Equal(-10.0f, data[1].Samples.Span[0]);
        Assert.Equal(100.0f, data[2].Samples.Span[0]);
        Assert.Equal(0.0f, data[3].Samples.Span[0]);

        bridge.DetachSource();
    }

    [Fact]
    public void EnableClinicalMockShaping_WhenEnabled_ProducesDivergentDynamicChannels()
    {
        using var bridge = new EegDataBridge(sampleRate: 160, sweepSeconds: 1)
        {
            EnableClinicalMockShaping = true
        };
        var source = new TestEegSource();
        bridge.AttachSource(source);

        for (int i = 0; i < 160; i++)
        {
            source.EmitSample(new EegSample
            {
                TimestampUs = i * 6_250L, // 160 Hz
                Ch1Uv = 12.0,
                Ch2Uv = 12.0,
                Ch3Uv = 12.0,
                Ch4Uv = 12.0,
                QualityFlags = QualityFlag.Normal
            });
        }

        var data = bridge.GetSweepData();
        var ch1 = data[0].Samples.Span;
        var ch2 = data[1].Samples.Span;

        bool hasMeaningfulDifference = false;
        for (int i = 0; i < ch1.Length; i++)
        {
            if (Math.Abs(ch1[i] - ch2[i]) > 1.0f)
            {
                hasMeaningfulDifference = true;
                break;
            }
        }

        Assert.True(hasMeaningfulDifference);
        Assert.True(ComputeStdDev(ch1) > 10f);
        Assert.True(ComputeStdDev(ch2) > 10f);

        bridge.DetachSource();
    }

    [Fact]
    public void SampleIndexToX_MapsCorrectly()
    {
        // Index 0 = left (X=0), last index = right (X=width)
        float x0 = EegDataBridge.SampleIndexToX(0, 100, 1000.0f);
        float xMid = EegDataBridge.SampleIndexToX(50, 100, 1000.0f);
        float xEnd = EegDataBridge.SampleIndexToX(100, 100, 1000.0f);

        Assert.Equal(0.0f, x0);
        Assert.Equal(500.0f, xMid);
        Assert.Equal(1000.0f, xEnd);
    }

    private static float ComputeStdDev(ReadOnlySpan<float> samples)
    {
        if (samples.Length == 0)
        {
            return 0f;
        }

        double sum = 0;
        for (int i = 0; i < samples.Length; i++)
        {
            sum += samples[i];
        }
        double mean = sum / samples.Length;

        double variance = 0;
        for (int i = 0; i < samples.Length; i++)
        {
            double delta = samples[i] - mean;
            variance += delta * delta;
        }

        return (float)Math.Sqrt(variance / samples.Length);
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
