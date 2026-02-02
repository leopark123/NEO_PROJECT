// EegChunkEncoderTests.cs
// EEG Chunk 编码/解码一致性测试 - S4-01
//
// 验证: raw int16[] → BLOB → raw int16[] 往返不变

using Neo.Core.Enums;
using Neo.Core.Models;
using Xunit;

namespace Neo.Storage.Tests;

public class EegChunkEncoderTests
{
    private const double ScaleFactor = 0.076;
    private const int SampleRate = 160;
    private const int ChannelCount = 4;

    private static EegSample[] CreateSamples(int count, long startUs = 0)
    {
        var samples = new EegSample[count];
        for (int i = 0; i < count; i++)
        {
            // 使用 int16 对齐的值（raw * 0.076）以确保往返精确
            short raw1 = (short)(100 + i);
            short raw2 = (short)(200 + i);
            short raw3 = (short)(-50 + i);
            short raw4 = (short)(raw1 - raw2);

            samples[i] = new EegSample
            {
                TimestampUs = startUs + (long)(i * 1_000_000.0 / SampleRate),
                Ch1Uv = raw1 * ScaleFactor,
                Ch2Uv = raw2 * ScaleFactor,
                Ch3Uv = raw3 * ScaleFactor,
                Ch4Uv = raw4 * ScaleFactor,
                QualityFlags = QualityFlag.Normal
            };
        }
        return samples;
    }

    [Fact]
    public void Encode_Decode_RoundTrip_ValuesMatch()
    {
        var original = CreateSamples(160); // 1 second

        byte[] blob = EegChunkEncoder.Encode(original, ChannelCount, SampleRate, ScaleFactor);
        EegSample[] decoded = EegChunkEncoder.Decode(blob, ScaleFactor, 0, SampleRate);

        Assert.Equal(original.Length, decoded.Length);

        for (int i = 0; i < original.Length; i++)
        {
            // raw int16 round-trip should be exact (within scale factor precision)
            Assert.Equal(original[i].Ch1Uv, decoded[i].Ch1Uv, precision: 5);
            Assert.Equal(original[i].Ch2Uv, decoded[i].Ch2Uv, precision: 5);
            Assert.Equal(original[i].Ch3Uv, decoded[i].Ch3Uv, precision: 5);
            Assert.Equal(original[i].Ch4Uv, decoded[i].Ch4Uv, precision: 5);
        }
    }

    [Fact]
    public void Encode_Decode_RoundTrip_RawInt16Exact()
    {
        // Verify raw int16 values are preserved exactly
        short rawValue = 1234;
        double uvValue = rawValue * ScaleFactor; // 93.784

        var samples = new[]
        {
            new EegSample
            {
                TimestampUs = 0,
                Ch1Uv = uvValue,
                Ch2Uv = uvValue,
                Ch3Uv = uvValue,
                Ch4Uv = uvValue,
                QualityFlags = QualityFlag.Normal
            }
        };

        byte[] blob = EegChunkEncoder.Encode(samples, ChannelCount, SampleRate, ScaleFactor);
        EegSample[] decoded = EegChunkEncoder.Decode(blob, ScaleFactor, 0, SampleRate);

        // Convert back to raw and verify exact int16
        short recoveredRaw = (short)Math.Round(decoded[0].Ch1Uv / ScaleFactor);
        Assert.Equal(rawValue, recoveredRaw);
    }

    [Fact]
    public void Encode_BlobSize_MatchesExpected()
    {
        int sampleCount = 160;
        var samples = CreateSamples(sampleCount);

        byte[] blob = EegChunkEncoder.Encode(samples, ChannelCount, SampleRate, ScaleFactor);

        int expectedSize = EegChunkEncoder.HeaderSize + (sampleCount * ChannelCount * sizeof(short));
        Assert.Equal(expectedSize, blob.Length);
    }

    [Fact]
    public void Encode_Header_ContainsCorrectVersion()
    {
        var samples = CreateSamples(10);
        byte[] blob = EegChunkEncoder.Encode(samples, ChannelCount, SampleRate, ScaleFactor);

        Assert.Equal(EegChunkEncoder.CurrentVersion, blob[0]);
    }

    [Fact]
    public void Encode_Header_ContainsChannelCount()
    {
        var samples = CreateSamples(10);
        byte[] blob = EegChunkEncoder.Encode(samples, ChannelCount, SampleRate, ScaleFactor);

        Assert.Equal(ChannelCount, blob[3]);
    }

    [Fact]
    public void Encode_QualitySummary_IsOrOfAllFlags()
    {
        var samples = new[]
        {
            new EegSample { Ch1Uv = 1, QualityFlags = QualityFlag.Normal },
            new EegSample { Ch1Uv = 2, QualityFlags = QualityFlag.Missing },
            new EegSample { Ch1Uv = 3, QualityFlags = QualityFlag.Saturated }
        };

        byte[] blob = EegChunkEncoder.Encode(samples, ChannelCount, SampleRate, ScaleFactor);
        byte qualitySummary = blob[4];

        Assert.True((qualitySummary & (byte)QualityFlag.Missing) != 0);
        Assert.True((qualitySummary & (byte)QualityFlag.Saturated) != 0);
    }

    [Fact]
    public void Decode_InvalidVersion_Throws()
    {
        byte[] blob = new byte[EegChunkEncoder.HeaderSize + 8];
        blob[0] = 99; // invalid version

        Assert.Throws<NotSupportedException>(() =>
            EegChunkEncoder.Decode(blob, ScaleFactor, 0, SampleRate));
    }

    [Fact]
    public void Decode_TooSmallBlob_Throws()
    {
        byte[] blob = new byte[4]; // smaller than header
        Assert.Throws<ArgumentException>(() =>
            EegChunkEncoder.Decode(blob, ScaleFactor, 0, SampleRate));
    }

    [Fact]
    public void Encode_Decode_EmptyChunk_ReturnsEmpty()
    {
        var samples = Array.Empty<EegSample>();
        byte[] blob = EegChunkEncoder.Encode(samples, ChannelCount, SampleRate, ScaleFactor);
        EegSample[] decoded = EegChunkEncoder.Decode(blob, ScaleFactor, 0, SampleRate);

        Assert.Empty(decoded);
        Assert.Equal(EegChunkEncoder.HeaderSize, blob.Length);
    }

    [Fact]
    public void Decode_TimestampsReconstructed_AreMonotonic()
    {
        var samples = CreateSamples(160);
        byte[] blob = EegChunkEncoder.Encode(samples, ChannelCount, SampleRate, ScaleFactor);
        EegSample[] decoded = EegChunkEncoder.Decode(blob, ScaleFactor, 0, SampleRate);

        for (int i = 1; i < decoded.Length; i++)
        {
            Assert.True(decoded[i].TimestampUs > decoded[i - 1].TimestampUs,
                $"Non-monotonic at {i}: {decoded[i].TimestampUs} <= {decoded[i - 1].TimestampUs}");
        }
    }

    [Fact]
    public void Encode_Decode_LargeChunk_5Seconds()
    {
        int sampleCount = 5 * SampleRate; // 800 samples
        var samples = CreateSamples(sampleCount);

        byte[] blob = EegChunkEncoder.Encode(samples, ChannelCount, SampleRate, ScaleFactor);
        EegSample[] decoded = EegChunkEncoder.Decode(blob, ScaleFactor, 0, SampleRate);

        Assert.Equal(sampleCount, decoded.Length);

        // Verify first and last
        Assert.Equal(samples[0].Ch1Uv, decoded[0].Ch1Uv, precision: 5);
        Assert.Equal(samples[^1].Ch1Uv, decoded[^1].Ch1Uv, precision: 5);
    }
}
