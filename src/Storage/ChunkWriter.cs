// ChunkWriter.cs
// 后台 Chunk 批量写入线程 - S4-01
//
// 依据: ARCHITECTURE.md §8.3 (单写线程, 批量事务, 固定提交周期)
//       00_CONSTITUTION.md 铁律6 (不在渲染/DSP关键路径中访问SQLite)
//       00_CONSTITUTION.md 铁律12 (append-only)
//       00_CONSTITUTION.md 铁律13 (所有记录带时间戳)

using System.Collections.Concurrent;
using System.Diagnostics;
using Microsoft.Data.Sqlite;
using Neo.Core.Models;

namespace Neo.Storage;

public sealed class ChunkWriter : IDisposable
{
    private readonly NeoDatabase _db;
    private readonly StorageConfiguration _config;
    private readonly AuditLog _auditLog;
    private readonly StorageReaper _reaper;

    // 待写入的 chunk 队列（无锁，DSP 线程生产，写入线程消费）
    private readonly ConcurrentQueue<PendingEegChunk> _eegQueue = new();
    // NIRS queue: Reserved for future NIRS implementation (S3-00 Blocked)

    // EEG 样本累积器
    private readonly List<EegSample> _eegAccumulator = [];
    private long _eegChunkStartUs = -1;
    private long _currentSessionId = -1;

    // 写入线程
    private Thread? _writerThread;
    private volatile bool _stopRequested;
    private bool _disposed;
    private int _drainInProgress;

    // Prepared statements
    private SqliteCommand? _insertEegCmd;
    private SqliteCommand? _updateStorageStateCmd;

    // 统计
    private long _totalEegChunksWritten;
    private long _totalBytesWritten;

    public long TotalEegChunksWritten => Interlocked.Read(ref _totalEegChunksWritten);
    public long TotalBytesWritten => Interlocked.Read(ref _totalBytesWritten);

    public ChunkWriter(NeoDatabase db, StorageConfiguration config, AuditLog auditLog, StorageReaper reaper)
    {
        _db = db;
        _config = config;
        _auditLog = auditLog;
        _reaper = reaper;
    }

    /// <summary>
    /// 设置当前活跃会话 ID。
    /// </summary>
    public void SetActiveSession(long sessionId)
    {
        _currentSessionId = sessionId;
        _eegAccumulator.Clear();
        _eegChunkStartUs = -1;
    }

    /// <summary>
    /// 接收 EEG 样本（由 DSP 线程调用，非阻塞）。
    /// 累积到 chunk 大小后加入写入队列。
    /// </summary>
    public void AcceptEegSample(in EegSample sample)
    {
        if (_currentSessionId < 0) return;

        if (_eegChunkStartUs < 0)
            _eegChunkStartUs = sample.TimestampUs;

        _eegAccumulator.Add(sample);

        int samplesPerChunk = _config.EegSampleRate * _config.EegChunkDurationSeconds;
        if (_eegAccumulator.Count >= samplesPerChunk)
        {
            FlushEegChunk();
        }
    }

    /// <summary>
    /// 批量接收 EEG 样本。
    /// </summary>
    public void AcceptEegBatch(ReadOnlySpan<EegSample> samples)
    {
        for (int i = 0; i < samples.Length; i++)
            AcceptEegSample(in samples[i]);
    }

    /// <summary>
    /// 将累积的样本打包为 chunk 并加入写入队列。
    /// </summary>
    private void FlushEegChunk()
    {
        if (_eegAccumulator.Count == 0) return;

        var samples = _eegAccumulator.ToArray();
        byte[] blob = EegChunkEncoder.Encode(
            samples,
            _config.EegChannelCount,
            _config.EegSampleRate,
            _config.EegScaleFactor);

        long endTimeUs = samples[^1].TimestampUs;

        _eegQueue.Enqueue(new PendingEegChunk
        {
            SessionId = _currentSessionId,
            StartTimeUs = _eegChunkStartUs,
            EndTimeUs = endTimeUs,
            SampleCount = samples.Length,
            ChannelCount = _config.EegChannelCount,
            Blob = blob,
            QualitySummary = blob[4]  // from header
        });

        _eegAccumulator.Clear();
        _eegChunkStartUs = -1;
    }

    /// <summary>
    /// 强制刷新残余样本（会话结束时调用）。
    /// </summary>
    public void FlushRemaining()
    {
        FlushEegChunk();
    }

    /// <summary>
    /// 启动后台写入线程。
    /// </summary>
    public void Start()
    {
        if (_writerThread != null) return;

        var conn = _db.GetWriteConnection();
        PrepareStatements(conn);

        _stopRequested = false;
        _writerThread = new Thread(WriterLoop)
        {
            Name = "ChunkWriter",
            IsBackground = true,
            Priority = ThreadPriority.BelowNormal
        };
        _writerThread.Start();

        Trace.TraceInformation("[ChunkWriter] Started (flush interval: {0}ms)", _config.FlushIntervalMs);
    }

    /// <summary>
    /// 停止写入线程并刷新残余数据。
    /// </summary>
    public void Stop()
    {
        if (_writerThread == null) return;

        FlushRemaining();
        _stopRequested = true;

        // 等待写入线程完成最终刷新（线程退出循环后会自行 DrainQueues）
        if (!_writerThread.Join(timeout: TimeSpan.FromSeconds(30)))
            Trace.TraceWarning("[ChunkWriter] Writer thread did not terminate within 30s");

        _writerThread = null;

        // 写入线程已终止，安全地执行最终刷新（处理 FlushRemaining 后可能新增的 chunks）
        DrainQueues();

        Trace.TraceInformation("[ChunkWriter] Stopped. Total chunks: {0}, bytes: {1}",
            _totalEegChunksWritten, _totalBytesWritten);
    }

    private void PrepareStatements(SqliteConnection conn)
    {
        _insertEegCmd = conn.CreateCommand();
        _insertEegCmd.CommandText = """
            INSERT INTO eeg_chunks (session_id, start_time_us, end_time_us, sample_count,
                                    channel_count, encoding_version, quality_summary, data_blob, byte_length)
            VALUES (@sid, @start, @end, @cnt, @ch, @ver, @qual, @blob, @len);
            """;
        _insertEegCmd.Parameters.Add("@sid", SqliteType.Integer);
        _insertEegCmd.Parameters.Add("@start", SqliteType.Integer);
        _insertEegCmd.Parameters.Add("@end", SqliteType.Integer);
        _insertEegCmd.Parameters.Add("@cnt", SqliteType.Integer);
        _insertEegCmd.Parameters.Add("@ch", SqliteType.Integer);
        _insertEegCmd.Parameters.Add("@ver", SqliteType.Integer);
        _insertEegCmd.Parameters.Add("@qual", SqliteType.Integer);
        _insertEegCmd.Parameters.Add("@blob", SqliteType.Blob);
        _insertEegCmd.Parameters.Add("@len", SqliteType.Integer);
        _insertEegCmd.Prepare();

        _updateStorageStateCmd = conn.CreateCommand();
        _updateStorageStateCmd.CommandText = """
            INSERT INTO storage_state (id, total_bytes, eeg_chunk_count, storage_limit_bytes, updated_at)
            VALUES (1, @bytes, @cnt, @limit, datetime('now'))
            ON CONFLICT(id) DO UPDATE SET
                total_bytes = total_bytes + @bytes,
                eeg_chunk_count = eeg_chunk_count + @cnt,
                updated_at = datetime('now');
            """;
        _updateStorageStateCmd.Parameters.Add("@bytes", SqliteType.Integer);
        _updateStorageStateCmd.Parameters.Add("@cnt", SqliteType.Integer);
        _updateStorageStateCmd.Parameters.Add("@limit", SqliteType.Integer);
        _updateStorageStateCmd.Prepare();
    }

    /// <summary>
    /// 写入线程主循环。
    /// </summary>
    private void WriterLoop()
    {
        while (!_stopRequested)
        {
            try
            {
                DrainQueues();
                Thread.Sleep(_config.FlushIntervalMs);
            }
            catch (Exception ex)
            {
                if (!_stopRequested)
                    Trace.TraceError("[ChunkWriter] Writer loop error: {0}", ex.Message);
            }
        }

        // 退出前最终刷新（Stop() 也会调用 DrainQueues，此处为保险）
        try { DrainQueues(); }
        catch (ObjectDisposedException) { /* DB may be disposed during shutdown */ }
    }

    /// <summary>
    /// 消费队列中所有待写入的 chunk，以事务批量写入。
    /// </summary>
    private void DrainQueues()
    {
        if (Interlocked.Exchange(ref _drainInProgress, 1) == 1)
            return;

        try
        {
        if (_eegQueue.IsEmpty)
            return;

        var conn = _db.GetWriteConnection();
        int eegCount = 0;
        long batchBytes = 0;

        // 使用 using 块（非 using var）确保事务在 CheckAndCleanup 前完全释放
        using (var transaction = conn.BeginTransaction())
        {
            try
            {
                while (_eegQueue.TryDequeue(out var chunk))
                {
                    _insertEegCmd!.Transaction = transaction;
                    _insertEegCmd.Parameters["@sid"].Value = chunk.SessionId;
                    _insertEegCmd.Parameters["@start"].Value = chunk.StartTimeUs;
                    _insertEegCmd.Parameters["@end"].Value = chunk.EndTimeUs;
                    _insertEegCmd.Parameters["@cnt"].Value = chunk.SampleCount;
                    _insertEegCmd.Parameters["@ch"].Value = chunk.ChannelCount;
                    _insertEegCmd.Parameters["@ver"].Value = (int)EegChunkEncoder.CurrentVersion;
                    _insertEegCmd.Parameters["@qual"].Value = (int)chunk.QualitySummary;
                    _insertEegCmd.Parameters["@blob"].Value = chunk.Blob;
                    _insertEegCmd.Parameters["@len"].Value = chunk.Blob.Length;
                    _insertEegCmd.ExecuteNonQuery();

                    eegCount++;
                    batchBytes += chunk.Blob.Length;
                }

                // 更新 storage_state
                if (eegCount > 0)
                {
                    _updateStorageStateCmd!.Transaction = transaction;
                    _updateStorageStateCmd.Parameters["@bytes"].Value = batchBytes;
                    _updateStorageStateCmd.Parameters["@cnt"].Value = eegCount;
                    _updateStorageStateCmd.Parameters["@limit"].Value = _config.StorageLimitBytes;
                    _updateStorageStateCmd.ExecuteNonQuery();
                }

                transaction.Commit();
            }
            catch
            {
                transaction.Rollback();
                throw;
            }
        }
        // transaction 已完全释放

        Interlocked.Add(ref _totalEegChunksWritten, eegCount);
        Interlocked.Add(ref _totalBytesWritten, batchBytes);

        // 检查容量并触发清理（在事务完全释放后执行）
        if (eegCount > 0)
            _reaper.CheckAndCleanup();
        }
        finally
        {
            Interlocked.Exchange(ref _drainInProgress, 0);
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        Stop();
        _insertEegCmd?.Dispose();
        _updateStorageStateCmd?.Dispose();
        _disposed = true;
    }

    private struct PendingEegChunk
    {
        public long SessionId;
        public long StartTimeUs;
        public long EndTimeUs;
        public int SampleCount;
        public int ChannelCount;
        public byte[] Blob;
        public byte QualitySummary;
    }

    // PendingNirsChunk: Reserved for future NIRS implementation (S3-00 Blocked)
}
