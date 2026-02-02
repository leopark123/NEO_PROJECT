// IEegChunkStore.cs
// EEG Chunk 存储读取接口 - S4-01
//
// 依据: ARCHITECTURE.md §8.3 (读写分离)

using Neo.Core.Models;

namespace Neo.Storage;

public sealed record EegChunkInfo
{
    public required long Id { get; init; }
    public required long SessionId { get; init; }
    public required long StartTimeUs { get; init; }
    public required long EndTimeUs { get; init; }
    public required int SampleCount { get; init; }
    public required int ByteLength { get; init; }
}

public interface IEegChunkStore
{
    /// <summary>
    /// 按时间范围查询 EEG chunk 列表。
    /// </summary>
    IReadOnlyList<EegChunkInfo> QueryTimeRange(long sessionId, long startUs, long endUs);

    /// <summary>
    /// 获取会话的 chunk 时间索引（所有 chunk 的起止时间）。
    /// </summary>
    IReadOnlyList<EegChunkInfo> GetSessionIndex(long sessionId);

    /// <summary>
    /// 读取并解码单个 chunk 的样本数据。
    /// </summary>
    EegSample[] ReadChunk(long chunkId);

    /// <summary>
    /// 按时间范围读取并解码所有匹配 chunk 的样本数据。
    /// </summary>
    EegSample[] ReadTimeRange(long sessionId, long startUs, long endUs);
}
