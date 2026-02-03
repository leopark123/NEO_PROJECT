// StorageIntegrationTests.cs
// 集成测试: DB 初始化、写入、读取、淘汰 - S4-01
//
// 验证: Schema 创建、ChunkWriter 写入、EegChunkStore 读取、StorageReaper 淘汰

using Neo.Core.Enums;
using Neo.Core.Models;
using Xunit;

namespace Neo.Storage.Tests;

public class StorageIntegrationTests : IDisposable
{
    private readonly string _tempDir;
    private readonly StorageConfiguration _config;

    public StorageIntegrationTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"neo_storage_test_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);

        _config = new StorageConfiguration
        {
            DbPath = Path.Combine(_tempDir, "test.db"),
            StorageLimitBytes = 10 * 1024 * 1024, // 10 MB for testing
            EegChunkDurationSeconds = 1,
            FlushIntervalMs = 50 // fast for tests
        };
    }

    public void Dispose()
    {
        try { Directory.Delete(_tempDir, recursive: true); } catch { }
    }

    private static EegSample[] CreateEegData(int seconds, long startOffsetUs = 0)
    {
        const int sampleRate = 160;
        int total = seconds * sampleRate;
        var samples = new EegSample[total];

        for (int i = 0; i < total; i++)
        {
            short raw = (short)(100 * Math.Sin(2 * Math.PI * 10 * i / sampleRate));
            double uv = raw * 0.076;

            samples[i] = new EegSample
            {
                TimestampUs = startOffsetUs + (long)(i * 1_000_000.0 / sampleRate),
                Ch1Uv = uv,
                Ch2Uv = uv * 0.8,
                Ch3Uv = uv * 0.6,
                Ch4Uv = uv * 0.4,
                QualityFlags = QualityFlag.Normal
            };
        }
        return samples;
    }

    [Fact]
    public void Database_Initialize_CreatesTablesAndPragmas()
    {
        using var db = new NeoDatabase(_config);
        db.Initialize();

        // Verify WAL mode
        var conn = db.GetWriteConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "PRAGMA journal_mode;";
        string journalMode = cmd.ExecuteScalar()?.ToString() ?? "";
        Assert.Equal("wal", journalMode);

        // Verify tables exist
        cmd.CommandText = "SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name='eeg_chunks';";
        Assert.Equal(1L, Convert.ToInt64(cmd.ExecuteScalar()));

        cmd.CommandText = "SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name='sessions';";
        Assert.Equal(1L, Convert.ToInt64(cmd.ExecuteScalar()));

        cmd.CommandText = "SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name='patients';";
        Assert.Equal(1L, Convert.ToInt64(cmd.ExecuteScalar()));

        cmd.CommandText = "SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name='audit_log';";
        Assert.Equal(1L, Convert.ToInt64(cmd.ExecuteScalar()));
    }

    [Fact]
    public void Database_DoubleInitialize_IsIdempotent()
    {
        using var db = new NeoDatabase(_config);
        db.Initialize();
        // Should not throw
        db.Dispose();

        using var db2 = new NeoDatabase(_config);
        db2.Initialize();
    }

    [Fact]
    public void ChunkWriter_WritesAndReads_EegData()
    {
        using var db = new NeoDatabase(_config);
        db.Initialize();

        var conn = db.GetWriteConnection();
        var auditLog = new AuditLog(conn);
        var reaper = new StorageReaper(db, _config);
        reaper.SetAuditLog(auditLog);

        // Create patient + session
        long patientId = CreatePatient(conn, "TEST-001");
        long sessionId = CreateSession(conn, patientId, 0);

        using var writer = new ChunkWriter(db, _config, auditLog, reaper);
        writer.SetActiveSession(sessionId);
        writer.Start();

        // Write 5 seconds of EEG data
        var data = CreateEegData(5);
        writer.AcceptEegBatch(data);
        writer.FlushRemaining();

        // Wait for writer to drain
        Thread.Sleep(300);
        writer.Stop();

        // Read back via EegChunkStore
        using var store = new EegChunkStore(db, _config);
        var index = store.GetSessionIndex(sessionId);

        Assert.True(index.Count >= 4, $"Expected >= 4 chunks for 5s data, got {index.Count}");

        // Read all data back
        long lastTs = data[^1].TimestampUs;
        var readBack = store.ReadTimeRange(sessionId, 0, lastTs);
        Assert.True(readBack.Length > 0, "Expected read-back samples");
    }

    [Fact]
    public void ChunkWriter_QueryTimeRange_ReturnsCorrectChunks()
    {
        using var db = new NeoDatabase(_config);
        db.Initialize();

        var conn = db.GetWriteConnection();
        var auditLog = new AuditLog(conn);
        var reaper = new StorageReaper(db, _config);

        long patientId = CreatePatient(conn, "TEST-002");
        long sessionId = CreateSession(conn, patientId, 0);

        using var writer = new ChunkWriter(db, _config, auditLog, reaper);
        writer.SetActiveSession(sessionId);
        writer.Start();

        var data = CreateEegData(10);
        writer.AcceptEegBatch(data);
        writer.FlushRemaining();
        Thread.Sleep(300);
        writer.Stop();

        using var store = new EegChunkStore(db, _config);

        // Query first 3 seconds
        var chunks = store.QueryTimeRange(sessionId, 0, 3_000_000);
        Assert.True(chunks.Count >= 3, $"Expected >= 3 chunks for 0-3s range, got {chunks.Count}");

        // Query middle range (5-7 seconds)
        var midChunks = store.QueryTimeRange(sessionId, 5_000_000, 7_000_000);
        Assert.True(midChunks.Count >= 2, $"Expected >= 2 chunks for 5-7s range, got {midChunks.Count}");
    }

    [Fact]
    public void StorageReaper_DeletesOldestChunks_WhenOverLimit()
    {
        // Set very small storage limit to trigger cleanup
        _config.StorageLimitBytes = 5000; // 5 KB - will trigger after a few chunks
        _config.CleanupThreshold = 0.5; // 50% = 2.5 KB

        using var db = new NeoDatabase(_config);
        db.Initialize();

        var conn = db.GetWriteConnection();
        var auditLog = new AuditLog(conn);
        var reaper = new StorageReaper(db, _config);
        reaper.SetAuditLog(auditLog);

        // Create old (non-active) session
        long patientId = CreatePatient(conn, "TEST-OLD");
        long oldSessionId = CreateSession(conn, patientId, 0, active: false);

        // Create current active session
        long activeSessionId = CreateSession(conn, patientId, 10_000_000, active: true);

        // Write data to old session directly
        var oldData = CreateEegData(3);
        WriteChunksDirect(conn, oldSessionId, oldData);

        // Write data to active session
        var activeData = CreateEegData(3, startOffsetUs: 10_000_000);
        WriteChunksDirect(conn, activeSessionId, activeData);

        // Trigger cleanup
        reaper.CheckAndCleanup();

        // Old session chunks should be deleted, active should remain
        using var store = new EegChunkStore(db, _config);
        var activeChunks = store.GetSessionIndex(activeSessionId);
        Assert.True(activeChunks.Count > 0, "Active session chunks should not be deleted");

        // Verify audit log has cleanup record
        using var auditCmd = conn.CreateCommand();
        auditCmd.CommandText = "SELECT COUNT(*) FROM audit_log WHERE event_type = 'STORAGE_CLEANUP';";
        long cleanupLogs = Convert.ToInt64(auditCmd.ExecuteScalar());
        Assert.True(cleanupLogs > 0, "Expected cleanup audit log entry");
    }

    [Fact]
    public void StorageReaper_ProtectsActiveSession()
    {
        _config.StorageLimitBytes = 1000; // Very small
        _config.CleanupThreshold = 0.1;

        using var db = new NeoDatabase(_config);
        db.Initialize();

        var conn = db.GetWriteConnection();
        var reaper = new StorageReaper(db, _config);

        // Only active session exists
        long patientId = CreatePatient(conn, "TEST-ACTIVE");
        long sessionId = CreateSession(conn, patientId, 0, active: true);

        var data = CreateEegData(2);
        WriteChunksDirect(conn, sessionId, data);

        // Cleanup should not delete active session
        reaper.CheckAndCleanup();

        using var store = new EegChunkStore(db, _config);
        var chunks = store.GetSessionIndex(sessionId);
        Assert.True(chunks.Count > 0, "Active session chunks must not be deleted");
    }

    [Fact]
    public void AuditLog_RecordsEvents()
    {
        using var db = new NeoDatabase(_config);
        db.Initialize();

        var conn = db.GetWriteConnection();
        var auditLog = new AuditLog(conn);

        auditLog.Log("RECORDING_START", sessionId: 1, details: "Test session started");
        auditLog.Log("RECORDING_STOP", sessionId: 1, details: "Test session stopped");
        auditLog.Log("PATIENT_SWITCH", oldValue: "P001", newValue: "P002");

        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT COUNT(*) FROM audit_log;";
        long count = Convert.ToInt64(cmd.ExecuteScalar());
        Assert.Equal(3, count);

        cmd.CommandText = "SELECT event_type FROM audit_log ORDER BY id;";
        using var reader = cmd.ExecuteReader();
        var events = new List<string>();
        while (reader.Read())
            events.Add(reader.GetString(0));

        Assert.Equal(["RECORDING_START", "RECORDING_STOP", "PATIENT_SWITCH"], events);
    }

    [Fact]
    public async Task ConcurrentReadWrite_NoDeadlock()
    {
        using var db = new NeoDatabase(_config);
        db.Initialize();

        var conn = db.GetWriteConnection();
        var auditLog = new AuditLog(conn);
        var reaper = new StorageReaper(db, _config);

        long patientId = CreatePatient(conn, "TEST-CONCURRENT");
        long sessionId = CreateSession(conn, patientId, 0);

        using var writer = new ChunkWriter(db, _config, auditLog, reaper);
        writer.SetActiveSession(sessionId);
        writer.Start();

        // Write on producer thread
        var writeTask = Task.Run(() =>
        {
            var data = CreateEegData(5);
            writer.AcceptEegBatch(data);
            writer.FlushRemaining();
        });

        // Read on consumer thread
        var readTask = Task.Run(() =>
        {
            using var store = new EegChunkStore(db, _config);
            for (int i = 0; i < 10; i++)
            {
                var index = store.GetSessionIndex(sessionId);
                Thread.Sleep(50);
            }
        });

        // Both should complete without deadlock (timeout = 10s)
        var timeout = Task.Delay(TimeSpan.FromSeconds(10));
        var completed = await Task.WhenAny(Task.WhenAll(writeTask, readTask), timeout);
        Assert.True(completed != timeout, "Concurrent read/write timed out - possible deadlock");

        writer.Stop();
    }

    [Fact]
    public void ReadChunk_ReturnsDecodedSamples()
    {
        using var db = new NeoDatabase(_config);
        db.Initialize();

        var conn = db.GetWriteConnection();
        var auditLog = new AuditLog(conn);
        var reaper = new StorageReaper(db, _config);

        long patientId = CreatePatient(conn, "TEST-READCHUNK");
        long sessionId = CreateSession(conn, patientId, 0);

        var data = CreateEegData(2);
        WriteChunksDirect(conn, sessionId, data);

        using var store = new EegChunkStore(db, _config);
        var index = store.GetSessionIndex(sessionId);
        Assert.True(index.Count > 0);

        var chunk = store.ReadChunk(index[0].Id);
        Assert.True(chunk.Length > 0);
        Assert.True(chunk[0].TimestampUs >= 0);
    }

    // ── Helper methods ──────────────────────────────────────

    private static long CreatePatient(Microsoft.Data.Sqlite.SqliteConnection conn, string pid)
    {
        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            INSERT INTO patients (patient_id, created_at_us) VALUES (@pid, 0);
            SELECT last_insert_rowid();
            """;
        cmd.Parameters.AddWithValue("@pid", pid);
        return Convert.ToInt64(cmd.ExecuteScalar());
    }

    private static long CreateSession(Microsoft.Data.Sqlite.SqliteConnection conn, long patientId,
        long startTimeUs, bool active = true)
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

    private void WriteChunksDirect(Microsoft.Data.Sqlite.SqliteConnection conn,
        long sessionId, EegSample[] data)
    {
        int samplesPerChunk = _config.EegSampleRate * _config.EegChunkDurationSeconds;

        using var transaction = conn.BeginTransaction();
        using var cmd = conn.CreateCommand();
        cmd.Transaction = transaction;

        long totalBytes = 0;
        int totalChunks = 0;

        for (int offset = 0; offset < data.Length; offset += samplesPerChunk)
        {
            int count = Math.Min(samplesPerChunk, data.Length - offset);
            var chunk = data.AsSpan(offset, count);
            byte[] blob = EegChunkEncoder.Encode(chunk, _config.EegChannelCount,
                _config.EegSampleRate, _config.EegScaleFactor);

            cmd.CommandText = """
                INSERT INTO eeg_chunks (session_id, start_time_us, end_time_us, sample_count,
                                        channel_count, encoding_version, quality_summary, data_blob, byte_length)
                VALUES (@sid, @start, @end, @cnt, @ch, 1, 0, @blob, @len);
                """;
            cmd.Parameters.Clear();
            cmd.Parameters.AddWithValue("@sid", sessionId);
            cmd.Parameters.AddWithValue("@start", chunk[0].TimestampUs);
            cmd.Parameters.AddWithValue("@end", chunk[^1].TimestampUs);
            cmd.Parameters.AddWithValue("@cnt", count);
            cmd.Parameters.AddWithValue("@ch", _config.EegChannelCount);
            cmd.Parameters.AddWithValue("@blob", blob);
            cmd.Parameters.AddWithValue("@len", blob.Length);
            cmd.ExecuteNonQuery();

            totalBytes += blob.Length;
            totalChunks++;
        }

        // Update storage state
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
        cmd.Parameters.AddWithValue("@limit", _config.StorageLimitBytes);
        cmd.ExecuteNonQuery();

        transaction.Commit();
    }
}
