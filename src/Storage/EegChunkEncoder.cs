// EegChunkEncoder.cs
// EEG Chunk BLOB 编码/解码 - S4-01
//
// 格式: [Header 8 bytes] + [int16 channel-interleaved data]
// Header: version(1) + sampleRate_hi(1) + sampleRate_lo(1) + channels(1) + flags(1) + reserved(3)
// Data: sample0_ch1(int16) sample0_ch2(int16) ... sample0_ch4(int16) sample1_ch1(int16) ...
//
// 依据: 00_CONSTITUTION.md 铁律1 (Raw不改), 铁律12 (append-only)
//       handoff/rs232-source-api.md (scale_factor = 0.076 μV/LSB)

using Neo.Core.Enums;
using Neo.Core.Models;

namespace Neo.Storage;

public static class EegChunkEncoder
{
    public const int HeaderSize = 8;
    public const byte CurrentVersion = 1;

    /// <summary>
    /// 将 EegSample 数组编码为 BLOB。
    /// 通道值从 double μV 转回 raw int16: raw = (short)Round(μV / scaleFactor)
    /// </summary>
    /// <remarks>
    /// 转换是可逆的：原始设备发送 int16，经 Rs232Parser 转换为 double μV (raw * 0.076)。
    /// 此处反向转换恢复 raw int16。由于原始值为整数，Round 确保无精度损失。
    /// </remarks>
    public static byte[] Encode(ReadOnlySpan<EegSample> samples, int channelCount, int sampleRate, double scaleFactor)
    {
        int dataSize = samples.Length * channelCount * sizeof(short);
        byte[] blob = new byte[HeaderSize + dataSize];

        // Header
        blob[0] = CurrentVersion;
        blob[1] = (byte)(sampleRate >> 8);
        blob[2] = (byte)(sampleRate & 0xFF);
        blob[3] = (byte)channelCount;
        blob[4] = ComputeQualitySummary(samples);
        blob[5] = 0; // reserved
        blob[6] = 0; // reserved
        blob[7] = 0; // reserved

        // Data: channel-interleaved int16
        double invScale = 1.0 / scaleFactor;
        int offset = HeaderSize;

        for (int i = 0; i < samples.Length; i++)
        {
            ref readonly var s = ref samples[i];

            WriteInt16(blob, offset, (short)Math.Round(s.Ch1Uv * invScale));
            offset += 2;
            WriteInt16(blob, offset, (short)Math.Round(s.Ch2Uv * invScale));
            offset += 2;
            if (channelCount >= 3)
            {
                WriteInt16(blob, offset, (short)Math.Round(s.Ch3Uv * invScale));
                offset += 2;
            }
            if (channelCount >= 4)
            {
                WriteInt16(blob, offset, (short)Math.Round(s.Ch4Uv * invScale));
                offset += 2;
            }
        }

        return blob;
    }

    /// <summary>
    /// 从 BLOB 解码为 EegSample 数组。
    /// </summary>
    public static EegSample[] Decode(byte[] blob, double scaleFactor, long startTimeUs, int sampleRate)
    {
        if (blob.Length < HeaderSize)
            throw new ArgumentException("Blob too small for header");

        byte version = blob[0];
        if (version != CurrentVersion)
            throw new NotSupportedException($"Unsupported encoding version: {version}");

        int channelCount = blob[3];
        byte qualitySummary = blob[4];
        int bytesPerSample = channelCount * sizeof(short);
        int dataLength = blob.Length - HeaderSize;

        if (dataLength % bytesPerSample != 0)
            throw new ArgumentException("Data length not aligned to sample size");

        int sampleCount = dataLength / bytesPerSample;
        var result = new EegSample[sampleCount];
        double sampleIntervalUs = 1_000_000.0 / sampleRate;
        int offset = HeaderSize;

        for (int i = 0; i < sampleCount; i++)
        {
            double ch1 = ReadInt16(blob, offset) * scaleFactor; offset += 2;
            double ch2 = ReadInt16(blob, offset) * scaleFactor; offset += 2;
            double ch3 = channelCount >= 3 ? ReadInt16(blob, offset) * scaleFactor : 0; if (channelCount >= 3) offset += 2;
            double ch4 = channelCount >= 4 ? ReadInt16(blob, offset) * scaleFactor : 0; if (channelCount >= 4) offset += 2;

            result[i] = new EegSample
            {
                TimestampUs = startTimeUs + (long)(i * sampleIntervalUs),
                Ch1Uv = ch1,
                Ch2Uv = ch2,
                Ch3Uv = ch3,
                Ch4Uv = ch4,
                QualityFlags = (QualityFlag)qualitySummary
            };
        }

        return result;
    }

    /// <summary>
    /// 计算 chunk 的质量摘要标志（所有样本的 QualityFlags 的 OR）。
    /// </summary>
    private static byte ComputeQualitySummary(ReadOnlySpan<EegSample> samples)
    {
        byte flags = 0;
        for (int i = 0; i < samples.Length; i++)
            flags |= (byte)samples[i].QualityFlags;
        return flags;
    }

    private static void WriteInt16(byte[] buf, int offset, short value)
    {
        buf[offset] = (byte)(value & 0xFF);
        buf[offset + 1] = (byte)((value >> 8) & 0xFF);
    }

    private static short ReadInt16(byte[] buf, int offset)
    {
        return (short)(buf[offset] | (buf[offset + 1] << 8));
    }
}
