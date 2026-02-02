// StorageConfiguration.cs
// 存储配置 - S4-01 SQLite + Chunk
//
// 依据: ARCHITECTURE.md §8.2 (SQLite PRAGMAs), §13.5 (300GiB limit)
//       CHARTER.md §2.5 (FIFO, no prompt, must log)

namespace Neo.Storage;

public sealed class StorageConfiguration
{
    /// <summary>
    /// 数据库文件路径。
    /// 默认: 当前目录下 neo_data/neo.db
    /// </summary>
    public string DbPath { get; set; } = Path.Combine("neo_data", "neo.db");

    /// <summary>
    /// 存储上限（字节）。
    /// 默认: 300 GiB (ARCHITECTURE.md §13.5)
    /// 可调范围: 1 GiB - 2 TiB
    /// </summary>
    public long StorageLimitBytes { get; set; } = 300L * 1024 * 1024 * 1024;

    /// <summary>
    /// EEG Chunk 时长（秒）。
    /// 默认: 1 秒 (160 samples @ 160Hz)
    /// 可选: 1, 5, 10
    /// 理由: 1秒是最小可寻址单位，平衡查询粒度与写入批次大小。
    /// </summary>
    public int EegChunkDurationSeconds { get; set; } = 1;

    /// <summary>
    /// NIRS Chunk 时长（秒）。
    /// 默认: 10 秒
    /// 理由: NIRS 采样率低(1-4Hz)，10秒聚合减少行数。
    /// </summary>
    public int NirsChunkDurationSeconds { get; set; } = 10;

    /// <summary>
    /// 写入刷新间隔（毫秒）。
    /// 默认: 500ms
    /// 可调范围: 100 - 5000
    /// 理由: 平衡写入延迟与事务批次大小（ARCHITECTURE.md §8.3: 100-500ms）
    /// </summary>
    public int FlushIntervalMs { get; set; } = 500;

    /// <summary>
    /// 容量清理触发阈值（占 StorageLimitBytes 的比例）。
    /// 默认: 0.9 (90%)，即达到 270GiB 时触发清理 (ARCHITECTURE.md §13.5)
    /// </summary>
    public double CleanupThreshold { get; set; } = 0.9;

    /// <summary>
    /// EEG 采样率 (Hz)。
    /// 固定: 160 (ARCHITECTURE.md §1.2)
    /// </summary>
    public int EegSampleRate { get; set; } = 160;

    /// <summary>
    /// EEG 通道数。
    /// 固定: 4 (ARCHITECTURE.md §1.2)
    /// </summary>
    public int EegChannelCount { get; set; } = 4;

    /// <summary>
    /// EEG raw → μV 缩放因子。
    /// 固定: 0.076 μV/LSB (handoff/rs232-source-api.md)
    /// </summary>
    public double EegScaleFactor { get; set; } = 0.076;

    /// <summary>
    /// SQLite cache_size (PRAGMA)。
    /// 默认: -64000 (64MB, ARCHITECTURE.md §8.2)
    /// </summary>
    public int SqliteCacheSize { get; set; } = -64000;

    /// <summary>
    /// SQLite busy_timeout (ms)。
    /// 默认: 5000 (ARCHITECTURE.md §8.2)
    /// </summary>
    public int SqliteBusyTimeout { get; set; } = 5000;
}
