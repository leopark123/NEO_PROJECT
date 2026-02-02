// INirsChunkStore.cs
// NIRS Chunk 存储读取接口 - S4-01
//
// 状态: 最小接口。NIRS 协议 Blocked (S3-00/ADR-015)。

using Neo.Core.Models;

namespace Neo.Storage;

public sealed record NirsChunkInfo
{
    public required long Id { get; init; }
    public required long SessionId { get; init; }
    public required long StartTimeUs { get; init; }
    public required long EndTimeUs { get; init; }
    public required int SampleCount { get; init; }
    public required int ByteLength { get; init; }
}

public interface INirsChunkStore
{
    IReadOnlyList<NirsChunkInfo> QueryTimeRange(long sessionId, long startUs, long endUs);
    IReadOnlyList<NirsChunkInfo> GetSessionIndex(long sessionId);
    NirsSample[] ReadChunk(long chunkId);
}
