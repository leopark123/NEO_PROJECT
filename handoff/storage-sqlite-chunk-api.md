# SQLite + Chunk Storage API 交接文档

> **版本**: v1.0
> **负责方**: Claude Code
> **创建日期**: 2026-01-29
> **关联任务**: S4-01

---

## 1. 概述

基于 SQLite WAL 模式的 EEG/NIRS 长程存储模块。EEG 数据以 1 秒 Chunk（160 样本 × 4 通道 raw int16）存入 BLOB，后台单线程批量事务写入，支持容量上限自动淘汰（300 GiB FIFO）。

---

## 2. 公开接口

```csharp
namespace Neo.Storage
{
    // ── 数据库引导 ──
    public sealed class NeoDatabase : IDisposable
    {
        public NeoDatabase(StorageConfiguration config);
        public void Initialize();                               // 创建/迁移 schema
        public SqliteConnection GetWriteConnection();           // 单写连接
        public SqliteConnection CreateReadConnection();         // 多读连接 (WAL)
    }

    // ── EEG Chunk 编解码 ──
    public static class EegChunkEncoder
    {
        public const byte CurrentVersion = 1;
        public const int HeaderSize = 8;
        public static byte[] Encode(ReadOnlySpan<EegSample> samples, int channels, int sampleRate, double scaleFactor);
        public static EegSample[] Decode(byte[] blob, double scaleFactor, long startTimeUs, int sampleRate);
    }

    // ── 写入管道 ──
    public sealed class ChunkWriter : IDisposable
    {
        public ChunkWriter(NeoDatabase db, StorageConfiguration config, AuditLog auditLog, StorageReaper reaper);
        public void SetActiveSession(long sessionId);
        public void AcceptEegSample(in EegSample sample);      // DSP 线程调用
        public void AcceptEegBatch(ReadOnlySpan<EegSample> samples);
        public void FlushRemaining();
        public void Start();
        public void Stop();
        public long TotalEegChunksWritten { get; }
        public long TotalBytesWritten { get; }
    }

    // ── 容量控制 ──
    public sealed class StorageReaper
    {
        public StorageReaper(NeoDatabase db, StorageConfiguration config);
        public void SetAuditLog(AuditLog auditLog);
        public void CheckAndCleanup();
        public long GetCurrentStorageSize();
        public long TotalDeletedChunks { get; }
        public long TotalFreedBytes { get; }
    }

    // ── 读取接口 ──
    public interface IEegChunkStore : IDisposable
    {
        IReadOnlyList<EegChunkInfo> QueryTimeRange(long sessionId, long startUs, long endUs);
        IReadOnlyList<EegChunkInfo> GetSessionIndex(long sessionId);
        EegSample[] ReadChunk(long chunkId);
        EegSample[] ReadTimeRange(long sessionId, long startUs, long endUs);
    }

    // ── 审计日志 ──
    public sealed class AuditLog
    {
        public AuditLog(SqliteConnection conn);
        public void Log(string eventType, long? sessionId = null,
                        string? oldValue = null, string? newValue = null, string? details = null);
    }
}
```

---

## 3. 数据模型

### Schema V1

```sql
-- patients: 患者元数据
CREATE TABLE patients (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    patient_id TEXT NOT NULL UNIQUE,
    created_at_us INTEGER NOT NULL
);

-- sessions: 记录会话
CREATE TABLE sessions (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    patient_id INTEGER NOT NULL REFERENCES patients(id),
    start_time_us INTEGER NOT NULL,
    end_time_us INTEGER,
    is_active INTEGER NOT NULL DEFAULT 1,
    sample_rate INTEGER NOT NULL DEFAULT 160,
    channel_count INTEGER NOT NULL DEFAULT 4,
    scale_factor REAL NOT NULL DEFAULT 0.076
);

-- eeg_chunks: EEG BLOB 数据
CREATE TABLE eeg_chunks (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    session_id INTEGER NOT NULL REFERENCES sessions(id),
    start_time_us INTEGER NOT NULL,
    end_time_us INTEGER NOT NULL,
    sample_count INTEGER NOT NULL,
    channel_count INTEGER NOT NULL,
    encoding_version INTEGER NOT NULL,
    quality_summary INTEGER NOT NULL DEFAULT 0,
    data_blob BLOB NOT NULL,
    byte_length INTEGER NOT NULL
);
-- INDEX: idx_eeg_session_time ON (session_id, start_time_us)
-- INDEX: idx_eeg_end_time ON (session_id, end_time_us)

-- nirs_chunks: NIRS BLOB (Blocked, S3-00/ADR-015)
-- audit_log: 审计追踪
-- storage_state: 容量统计 (单行)
```

### EEG Chunk BLOB 格式

```
Offset  Size  Field
0       1     version (=1)
1       2     sampleRate (LE uint16)
3       1     channelCount
4       1     qualitySummary (OR of all sample QualityFlags)
5       3     reserved (0x00)
8       N     channel-interleaved int16 data (LE)
              N = sampleCount × channelCount × 2
```

1 秒 chunk: 8 + 160 × 4 × 2 = **1,288 bytes**

### EegChunkInfo

```csharp
public readonly record struct EegChunkInfo
{
    public long Id { get; init; }
    public long SessionId { get; init; }
    public long StartTimeUs { get; init; }
    public long EndTimeUs { get; init; }
    public int SampleCount { get; init; }
    public int ByteLength { get; init; }
}
```

---

## 4. 线程模型

| 角色 | 线程 | 说明 |
|------|------|------|
| DSP/采集 | DSP 线程 | 调用 `AcceptEegSample()` 将样本推入 ConcurrentQueue |
| 写入者 | ChunkWriter 后台线程 | 按 FlushIntervalMs 周期消费队列，批量事务写入 |
| 读取者 | 任意线程 | 通过 `CreateReadConnection()` 获取独立只读连接 |
| 淘汰者 | ChunkWriter 后台线程 | `DrainQueues()` 提交后调用 `StorageReaper.CheckAndCleanup()` |

**线程安全性**: 读写分离（WAL 模式，单写多读）

**锁策略**: ConcurrentQueue (lock-free) + SQLite WAL 内置并发控制

---

## 5. 时间戳语义

| 输入/输出 | 时间戳含义 | 单位 |
|-----------|-----------|------|
| EegSample.TimestampUs | 样本采样时刻 | μs |
| eeg_chunks.start_time_us | Chunk 首样本时间戳 | μs |
| eeg_chunks.end_time_us | Chunk 末样本时间戳 | μs |
| audit_log.timestamp_us | 事件发生时刻 (Host Clock) | μs |

**时钟域**: Host Monotonic Clock

**精度**: 与 EEG 采集精度一致 (μs 级)

---

## 6. 数据契约

| 属性 | 值 | 说明 |
|------|-----|------|
| 采样率 | 160 Hz | 可配置 (StorageConfiguration.EegSampleRate) |
| 通道数 | 4 | 可配置 (StorageConfiguration.EegChannelCount) |
| 存储数据类型 | int16 (raw) | 原始 ADC 值，μV = raw × 0.076 |
| 读取数据类型 | double (μV) | EegSample.Ch1Uv~Ch4Uv |
| 时间戳单位 | μs | int64 |
| Chunk 时长 | 1 秒 (默认) | 可配置 1-5 秒 |
| Quality 表达 | QualityFlag enum | OR 汇总存入 quality_summary |

---

## 7. 使用示例

```csharp
// ── 初始化 ──
var config = new StorageConfiguration
{
    DbPath = "neo_data.db",
    StorageLimitBytes = 300L * 1024 * 1024 * 1024, // 300 GiB
    EegChunkDurationSeconds = 1,
    FlushIntervalMs = 500
};

using var db = new NeoDatabase(config);
db.Initialize();

var conn = db.GetWriteConnection();
var auditLog = new AuditLog(conn);
var reaper = new StorageReaper(db, config);
reaper.SetAuditLog(auditLog);

// ── 写入 ──
using var writer = new ChunkWriter(db, config, auditLog, reaper);
writer.SetActiveSession(sessionId);
writer.Start();

// DSP 线程中:
writer.AcceptEegSample(in sample);

// 会话结束:
writer.FlushRemaining();
writer.Stop();

// ── 读取 ──
using var store = new EegChunkStore(db, config);
var index = store.GetSessionIndex(sessionId);
var samples = store.ReadTimeRange(sessionId, startUs, endUs);
```

---

## 8. 性能特征

| 指标 | 值 | 测试条件 |
|------|-----|----------|
| 写入延迟 P50 | 0.017 ms | 160 samples/batch, 60s 持续写入 |
| 写入延迟 P99 | 0.343 ms | 同上 (目标 <50ms, ARCHITECTURE.md §8.6) |
| 写入延迟 Max | 0.343 ms | 同上 |
| 写入延迟 Avg | 0.026 ms | 同上 |
| 每分钟数据量 | 75.5 KB | 160Hz × 4ch × int16 + header |
| 预计 72h 存储 | 318.4 MB | EEG only (AT-20 目标 ~331 MB) |
| 每小时 DB 增长 | < 50 MB | ARCHITECTURE.md §8.6 目标 |
| 淘汰清理 | 10 chunks/op | 50KB 限制测试，清理 12,880 bytes |

---

## 9. 已知限制

1. **NIRS 存储为占位**: S3-00/ADR-015 阻塞，NirsChunkEncoder/NirsChunkStore 为最小实现
2. **Chunk BLOB vs 逐样本行**: 实现采用 BLOB 编码（而非 ARCHITECTURE.md §8.7 的 `eeg_raw` 逐样本表），以满足性能目标
3. **单写连接**: 写入瓶颈在 SQLite 单写互斥，但 160Hz×4ch 吞吐远低于 SQLite 写入上限
4. **schema_version 迁移**: 当前仅 V1，未来版本需在 NeoDatabase.MigrateSchema() 添加迁移逻辑
5. **EegChunkStore.ReadTimeRange**: 解码后逐样本过滤，大范围查询可能产生多余解码开销

---

## 10. 依赖项

| 依赖模块 | 接口/类 | 用途 |
|----------|---------|------|
| Neo.Core | EegSample | EEG 样本数据结构 |
| Neo.Core | QualityFlag | 质量标志枚举 |
| Microsoft.Data.Sqlite | SqliteConnection | SQLite 数据库访问 |

---

## 11. 测试覆盖

| 测试类 | 测试项 | 数量 | 状态 |
|--------|--------|------|------|
| EegChunkEncoderTests | 编解码往返、Header、边界、异常 | 11 | ✅ |
| StorageIntegrationTests | DB 初始化、写入读取、淘汰、审计、并发 | 8 | ✅ |
| StorageBenchmarkTests | 写入吞吐 60s、DB 增长、清理触发 | 3 | ✅ |
| **合计** | | **22** | ✅ |

---

## 12. SQLite PRAGMA 配置

| PRAGMA | 值 | 依据 |
|--------|-----|------|
| journal_mode | WAL | ARCHITECTURE.md §8.2 |
| synchronous | NORMAL | ARCHITECTURE.md §8.2 |
| cache_size | -64000 (64 MB) | ARCHITECTURE.md §8.2 |
| temp_store | MEMORY | ARCHITECTURE.md §8.2 |
| busy_timeout | 5000 | ARCHITECTURE.md §8.2 |
| foreign_keys | ON | 数据完整性 |
| mmap_size | 268435456 (256 MB) | ARCHITECTURE.md §8.2 |

---

## 13. 容量控制策略

1. **阈值触发**: 当 `storage_state.total_bytes > StorageLimitBytes × CleanupThreshold` (默认 90%) 时触发清理
2. **淘汰顺序**: 按 `start_time_us ASC` 删除非活跃会话 (is_active=0) 的最旧 chunk
3. **保护活跃会话**: `is_active=1` 的会话 chunk 不被删除
4. **批量删除**: 每批 100 chunks，循环直到低于阈值
5. **自动清理空会话**: 删除无 chunk 的非活跃会话及其孤儿患者记录
6. **审计追踪**: 每次清理写入 `STORAGE_CLEANUP` 审计日志条目

---

## 14. 变更历史

| 版本 | 日期 | 变更内容 |
|------|------|----------|
| v1.0 | 2026-01-29 | 初始版本: Schema V1, ChunkWriter, StorageReaper, EegChunkStore |

---

**文档结束**
