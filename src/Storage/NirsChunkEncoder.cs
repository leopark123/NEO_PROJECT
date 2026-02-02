// NirsChunkEncoder.cs
// NIRS Chunk BLOB 编码/解码 - S4-01
//
// 状态: 最小实现。NIRS 协议 Blocked (S3-00/ADR-015)。
// 字段来自 NirsSample (6通道 SpO2%)。
// 单位/阈值由设备说明提供，不由软件推断 (CHARTER.md §2.4)。
//
// 格式: [Header 8 bytes] + [float64 channel-interleaved data] + [validMask per sample]
// Header: version(1) + sampleRate(1) + channels(1) + flags(1) + reserved(4)

using Neo.Core.Models;

namespace Neo.Storage;

public static class NirsChunkEncoder
{
    public const int HeaderSize = 8;
    public const byte CurrentVersion = 1;

    public static byte[] Encode(ReadOnlySpan<NirsSample> samples, int channelCount)
    {
        // 每个样本: 6 × float64 + 1 byte validMask = 49 bytes
        int bytesPerSample = channelCount * sizeof(double) + 1;
        int dataSize = samples.Length * bytesPerSample;
        byte[] blob = new byte[HeaderSize + dataSize];

        blob[0] = CurrentVersion;
        blob[1] = 0; // sampleRate placeholder (device-defined, Blocked)
        blob[2] = (byte)channelCount;
        blob[3] = 0; // flags

        int offset = HeaderSize;
        for (int i = 0; i < samples.Length; i++)
        {
            ref readonly var s = ref samples[i];
            WriteDouble(blob, offset, s.Ch1Percent); offset += 8;
            WriteDouble(blob, offset, s.Ch2Percent); offset += 8;
            WriteDouble(blob, offset, s.Ch3Percent); offset += 8;
            WriteDouble(blob, offset, s.Ch4Percent); offset += 8;
            WriteDouble(blob, offset, s.Ch5Percent); offset += 8;
            WriteDouble(blob, offset, s.Ch6Percent); offset += 8;
            blob[offset++] = s.ValidMask;
        }

        return blob;
    }

    public static NirsSample[] Decode(byte[] blob, long startTimeUs, int sampleRate)
    {
        if (blob.Length < HeaderSize)
            throw new ArgumentException("Blob too small");

        if (blob[0] != CurrentVersion)
            throw new NotSupportedException($"Unsupported version: {blob[0]}");

        int channelCount = blob[2];
        int bytesPerSample = channelCount * sizeof(double) + 1;
        int dataLength = blob.Length - HeaderSize;
        int sampleCount = dataLength / bytesPerSample;

        var result = new NirsSample[sampleCount];
        double intervalUs = sampleRate > 0 ? 1_000_000.0 / sampleRate : 1_000_000.0;
        int offset = HeaderSize;

        for (int i = 0; i < sampleCount; i++)
        {
            result[i] = new NirsSample
            {
                TimestampUs = startTimeUs + (long)(i * intervalUs),
                Ch1Percent = ReadDouble(blob, offset + 0),
                Ch2Percent = ReadDouble(blob, offset + 8),
                Ch3Percent = ReadDouble(blob, offset + 16),
                Ch4Percent = ReadDouble(blob, offset + 24),
                Ch5Percent = ReadDouble(blob, offset + 32),
                Ch6Percent = ReadDouble(blob, offset + 40),
                ValidMask = blob[offset + 48]
            };
            offset += bytesPerSample;
        }

        return result;
    }

    private static void WriteDouble(byte[] buf, int offset, double value)
    {
        BitConverter.TryWriteBytes(buf.AsSpan(offset, 8), value);
    }

    private static double ReadDouble(byte[] buf, int offset)
    {
        return BitConverter.ToDouble(buf, offset);
    }
}
