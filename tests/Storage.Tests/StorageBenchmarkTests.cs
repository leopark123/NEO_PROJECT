// StorageBenchmarkTests.cs
// 存储性能基准测试 - S4-01
//
// 验证:
// - 写入吞吐: 模拟 160Hz×4ch 连续写入
// - DB 增长: 统计 chunk 数量、文件大小
// - 清理触发: 调小上限验证自动删除
//
// 依据: ARCHITECTURE.md §8.6 (性能基线: 写入 P99 <50ms/批)

using System.Diagnostics;
using Neo.Core.Enums;
using Neo.Core.Models;
using Xunit;
using Xunit.Abstractions;

namespace Neo.Storage.Tests;

public class StorageBenchmarkTests : IDisposable
{
    private readonly string _tempDir;
    private readonly ITestOutputHelper _output;

    public StorageBenchmarkTests(ITestOutputHelper output)
    {
        _output = output;
        _tempDir = Path.Combine(Path.GetTempPath(), $"neo_bench_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        try { Directory.Delete(_tempDir, recursive: true); } catch { }
    }

    private static EegSample CreateSample(int index, long startUs)
    {
        const int sampleRate = 160;
        double t = index / (double)sampleRate;
        short raw = (short)(100 * Math.Sin(2 * Math.PI * 10 * t));
        double uv = raw * 0.076;

        return new EegSample
        {
            TimestampUs = startUs + (long)(index * 1_000_000.0 / sampleRate),
            Ch1Uv = uv,
            Ch2Uv = uv * 0.8,
            Ch3Uv = uv * 0.6,
            Ch4Uv = uv * 0.4,
            QualityFlags = QualityFlag.Normal
        };
    }

    /// <summary>
    /// 写入吞吐基准: 模拟 160Hz×4ch 连续写入 60 秒（缩短版）。
    /// 实际测试可扩展至 600 秒(10分钟)。
    /// </summary>
    [Fact]
    public void Benchmark_WriteThrough_60Seconds()
    {
        var config = new StorageConfiguration
        {
            DbPath = Path.Combine(_tempDir, "bench.db"),
            EegChunkDurationSeconds = 1,
            FlushIntervalMs = 100,
            StorageLimitBytes = 1L * 1024 * 1024 * 1024 // 1 GB (no cleanup)
        };

        using var db = new NeoDatabase(config);
        db.Initialize();

        var conn = db.GetWriteConnection();
        var auditLog = new AuditLog(conn);
        var reaper = new StorageReaper(db, config);

        long patientId = CreatePatient(conn, "BENCH-001");
        long sessionId = CreateSession(conn, patientId, 0);

        using var writer = new ChunkWriter(db, config, auditLog, reaper);
        writer.SetActiveSession(sessionId);
        writer.Start();

        const int durationSeconds = 60;
        const int sampleRate = 160;
        int totalSamples = durationSeconds * sampleRate;

        var sw = Stopwatch.StartNew();
        var batchLatencies = new List<double>();

        // Simulate real-time feeding: 160 samples per second
        for (int sec = 0; sec < durationSeconds; sec++)
        {
            var batchSw = Stopwatch.StartNew();

            for (int i = 0; i < sampleRate; i++)
            {
                int globalIndex = sec * sampleRate + i;
                var sample = CreateSample(globalIndex, 0);
                writer.AcceptEegSample(in sample);
            }

            batchSw.Stop();
            batchLatencies.Add(batchSw.Elapsed.TotalMilliseconds);
        }

        writer.FlushRemaining();
        Thread.Sleep(500); // Let writer drain
        writer.Stop();
        sw.Stop();

        // ── Results ──
        long dbFileSize = new FileInfo(Path.GetFullPath(config.DbPath)).Length;
        long chunksWritten = writer.TotalEegChunksWritten;
        long bytesWritten = writer.TotalBytesWritten;

        batchLatencies.Sort();
        double p50 = batchLatencies[batchLatencies.Count / 2];
        double p99 = batchLatencies[(int)(batchLatencies.Count * 0.99)];
        double maxLatency = batchLatencies[^1];
        double avgLatency = batchLatencies.Average();

        _output.WriteLine("=== WRITE THROUGHPUT BENCHMARK (60s) ===");
        _output.WriteLine($"Duration:         {sw.Elapsed.TotalSeconds:F2} seconds");
        _output.WriteLine($"Total samples:    {totalSamples}");
        _output.WriteLine($"Chunks written:   {chunksWritten}");
        _output.WriteLine($"Bytes written:    {bytesWritten:N0} ({bytesWritten / 1024.0:F1} KB)");
        _output.WriteLine($"DB file size:     {dbFileSize:N0} ({dbFileSize / 1024.0:F1} KB)");
        _output.WriteLine($"Batch latency P50: {p50:F3} ms");
        _output.WriteLine($"Batch latency P99: {p99:F3} ms");
        _output.WriteLine($"Batch latency Max: {maxLatency:F3} ms");
        _output.WriteLine($"Batch latency Avg: {avgLatency:F3} ms");
        _output.WriteLine($"Overhead ratio:   {dbFileSize / (double)bytesWritten:F2}x");
        _output.WriteLine("");

        // AT-20: EEG data for 72h = 160Hz × 4ch × 2bytes × 72h = ~331 MB
        // Per hour: ~4.6 MB. Per minute: ~77 KB.
        double perMinuteKB = bytesWritten / 1024.0; // 60s worth
        double per72hMB = perMinuteKB * 60 * 72 / 1024.0;
        _output.WriteLine($"Estimated 72h EEG storage: {per72hMB:F1} MB");

        // ── Assertions ──
        Assert.True(chunksWritten >= 58, $"Expected >= 58 chunks for 60s, got {chunksWritten}");
        Assert.True(p99 < 50, $"P99 batch latency {p99:F3}ms exceeds 50ms target (ARCHITECTURE.md §8.6)");
        Assert.True(bytesWritten > 0, "Expected non-zero bytes written");
    }

    /// <summary>
    /// DB 增长统计: 验证每小时文件增长 < 50 MB (ARCHITECTURE.md §8.6)。
    /// </summary>
    [Fact]
    public void Benchmark_DbGrowth_PerMinute()
    {
        var config = new StorageConfiguration
        {
            DbPath = Path.Combine(_tempDir, "growth.db"),
            EegChunkDurationSeconds = 1,
            FlushIntervalMs = 50,
            StorageLimitBytes = 1L * 1024 * 1024 * 1024
        };

        using var db = new NeoDatabase(config);
        db.Initialize();

        var conn = db.GetWriteConnection();
        var auditLog = new AuditLog(conn);
        var reaper = new StorageReaper(db, config);

        long patientId = CreatePatient(conn, "GROWTH-001");
        long sessionId = CreateSession(conn, patientId, 0);

        using var writer = new ChunkWriter(db, config, auditLog, reaper);
        writer.SetActiveSession(sessionId);
        writer.Start();

        // Write 5 minutes of data
        const int minutes = 5;
        const int sampleRate = 160;

        for (int min = 0; min < minutes; min++)
        {
            for (int sec = 0; sec < 60; sec++)
            {
                for (int i = 0; i < sampleRate; i++)
                {
                    int globalIdx = (min * 60 + sec) * sampleRate + i;
                    var sample = CreateSample(globalIdx, 0);
                    writer.AcceptEegSample(in sample);
                }
            }
        }

        writer.FlushRemaining();
        Thread.Sleep(500);
        writer.Stop();

        long dbSize = new FileInfo(Path.GetFullPath(config.DbPath)).Length;
        long chunksWritten = writer.TotalEegChunksWritten;
        long bytesWritten = writer.TotalBytesWritten;

        double perMinuteKB = dbSize / (minutes * 1024.0);
        double perHourMB = perMinuteKB * 60 / 1024.0;

        _output.WriteLine("=== DB GROWTH BENCHMARK ===");
        _output.WriteLine($"Duration:         {minutes} minutes");
        _output.WriteLine($"Chunks written:   {chunksWritten}");
        _output.WriteLine($"Data bytes:       {bytesWritten:N0} ({bytesWritten / 1024.0:F1} KB)");
        _output.WriteLine($"DB file size:     {dbSize:N0} ({dbSize / 1024.0:F1} KB)");
        _output.WriteLine($"Per minute:       {perMinuteKB:F1} KB");
        _output.WriteLine($"Per hour (est):   {perHourMB:F1} MB");
        _output.WriteLine($"Per 72h (est):    {perHourMB * 72:F1} MB");

        // ARCHITECTURE.md §8.6: file growth < 50 MB/hour
        Assert.True(perHourMB < 50, $"Per-hour growth {perHourMB:F1} MB exceeds 50 MB target");
    }

    /// <summary>
    /// 清理触发基准: 把上限调小验证自动删除。
    /// </summary>
    [Fact]
    public void Benchmark_CleanupTrigger_SmallLimit()
    {
        var config = new StorageConfiguration
        {
            DbPath = Path.Combine(_tempDir, "cleanup.db"),
            EegChunkDurationSeconds = 1,
            FlushIntervalMs = 50,
            StorageLimitBytes = 15 * 1024, // 15 KB
            CleanupThreshold = 0.8 // trigger at 12 KB
        };

        using var db = new NeoDatabase(config);
        db.Initialize();

        var conn = db.GetWriteConnection();
        var auditLog = new AuditLog(conn);
        var reaper = new StorageReaper(db, config);
        reaper.SetAuditLog(auditLog);

        // Create old session (non-active)
        long patientId = CreatePatient(conn, "CLEANUP-001");
        long oldSessionId = CreateSession(conn, patientId, 0, active: false);
        WriteDataDirect(db, config, oldSessionId, 10); // 10 seconds

        // Create active session
        long activeSessionId = CreateSession(conn, patientId, 20_000_000, active: true);
        WriteDataDirect(db, config, activeSessionId, 5); // 5 seconds

        long sizeBefore = reaper.GetCurrentStorageSize();
        _output.WriteLine($"Size before cleanup: {sizeBefore} bytes");

        // Trigger cleanup
        reaper.CheckAndCleanup();

        long sizeAfter = reaper.GetCurrentStorageSize();
        _output.WriteLine($"Size after cleanup:  {sizeAfter} bytes");
        _output.WriteLine($"Freed:               {sizeBefore - sizeAfter} bytes");
        _output.WriteLine($"Deleted chunks:      {reaper.TotalDeletedChunks}");
        _output.WriteLine($"Freed total:         {reaper.TotalFreedBytes} bytes");

        // Verify cleanup happened
        Assert.True(reaper.TotalDeletedChunks > 0, "Expected chunks to be deleted");
        Assert.True(reaper.TotalFreedBytes > 0, "Expected bytes to be freed");

        // Verify active session data preserved
        using var store = new EegChunkStore(db, config);
        var activeChunks = store.GetSessionIndex(activeSessionId);
        Assert.True(activeChunks.Count > 0, "Active session chunks must survive cleanup");

        // Verify audit log
        using var auditCmd = conn.CreateCommand();
        auditCmd.CommandText = "SELECT details FROM audit_log WHERE event_type = 'STORAGE_CLEANUP' LIMIT 1;";
        string? details = auditCmd.ExecuteScalar()?.ToString();
        Assert.NotNull(details);
        _output.WriteLine($"Audit log: {details}");
    }

    // ── Helpers ──

    private static long CreatePatient(Microsoft.Data.Sqlite.SqliteConnection conn, string pid)
    {
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "INSERT INTO patients (patient_id, created_at_us) VALUES (@pid, 0); SELECT last_insert_rowid();";
        cmd.Parameters.AddWithValue("@pid", pid);
        return Convert.ToInt64(cmd.ExecuteScalar());
    }

    private static long CreateSession(Microsoft.Data.Sqlite.SqliteConnection conn,
        long patientId, long startTimeUs, bool active = true)
    {
        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            INSERT INTO sessions (patient_id, start_time_us, is_active)
            VALUES (@pid, @start, @active);
            SELECT last_insert_rowid();
            """;
        cmd.Parameters.AddWithValue("@pid", patientId);
        cmd.Parameters.AddWithValue("@start", startTimeUs);
        cmd.Parameters.AddWithValue("@active", active ? 1 : 0);
        return Convert.ToInt64(cmd.ExecuteScalar());
    }

    private static void WriteDataDirect(NeoDatabase db, StorageConfiguration config,
        long sessionId, int seconds)
    {
        var conn = db.GetWriteConnection();
        int sampleRate = config.EegSampleRate;
        int samplesPerChunk = sampleRate * config.EegChunkDurationSeconds;

        using var transaction = conn.BeginTransaction();
        using var cmd = conn.CreateCommand();
        cmd.Transaction = transaction;

        long totalBytes = 0;
        int totalChunks = 0;

        for (int sec = 0; sec < seconds; sec++)
        {
            var samples = new EegSample[samplesPerChunk];
            for (int i = 0; i < samplesPerChunk; i++)
            {
                int idx = sec * sampleRate + i;
                double t = idx / (double)sampleRate;
                short raw = (short)(100 * Math.Sin(2 * Math.PI * 10 * t));
                samples[i] = new EegSample
                {
                    TimestampUs = (long)(idx * 1_000_000.0 / sampleRate),
                    Ch1Uv = raw * 0.076,
                    Ch2Uv = raw * 0.076 * 0.8,
                    Ch3Uv = raw * 0.076 * 0.6,
                    Ch4Uv = raw * 0.076 * 0.4,
                    QualityFlags = QualityFlag.Normal
                };
            }

            byte[] blob = EegChunkEncoder.Encode(samples, config.EegChannelCount,
                sampleRate, config.EegScaleFactor);

            cmd.CommandText = """
                INSERT INTO eeg_chunks (session_id, start_time_us, end_time_us, sample_count,
                                        channel_count, encoding_version, quality_summary, data_blob, byte_length)
                VALUES (@sid, @start, @end, @cnt, @ch, 1, 0, @blob, @len);
                """;
            cmd.Parameters.Clear();
            cmd.Parameters.AddWithValue("@sid", sessionId);
            cmd.Parameters.AddWithValue("@start", samples[0].TimestampUs);
            cmd.Parameters.AddWithValue("@end", samples[^1].TimestampUs);
            cmd.Parameters.AddWithValue("@cnt", samplesPerChunk);
            cmd.Parameters.AddWithValue("@ch", config.EegChannelCount);
            cmd.Parameters.AddWithValue("@blob", blob);
            cmd.Parameters.AddWithValue("@len", blob.Length);
            cmd.ExecuteNonQuery();

            totalBytes += blob.Length;
            totalChunks++;
        }

        cmd.CommandText = """
            INSERT INTO storage_state (id, total_bytes, eeg_chunk_count, storage_limit_bytes)
            VALUES (1, @bytes, @cnt, @limit)
            ON CONFLICT(id) DO UPDATE SET
                total_bytes = total_bytes + @bytes,
                eeg_chunk_count = eeg_chunk_count + @cnt;
            """;
        cmd.Parameters.Clear();
        cmd.Parameters.AddWithValue("@bytes", totalBytes);
        cmd.Parameters.AddWithValue("@cnt", totalChunks);
        cmd.Parameters.AddWithValue("@limit", config.StorageLimitBytes);
        cmd.ExecuteNonQuery();

        transaction.Commit();
    }
}
