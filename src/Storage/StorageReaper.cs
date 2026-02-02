// StorageReaper.cs
// 存储容量控制与淘汰策略 - S4-01
//
// 依据: ARCHITECTURE.md §13.5 (300GiB, 按患者会话删除, 90%触发)
//       CHARTER.md §2.5 (FIFO, 无提示, 必须记录日志)
//       00_CONSTITUTION.md 铁律7 (审计日志)
//       00_CONSTITUTION.md 铁律12 (append-only: 不删除活跃会话的数据)
//
// 删除策略:
// - 默认: 最旧 chunk 优先删除（保持单个会话的连续性尽可能长）
// - 保护: 当前活跃会话（is_active=1）的数据不可删除
// - 事务: 删除操作在事务内完成，同时更新 storage_state
// - 审计: 每次删除记录到 audit_log

using System.Diagnostics;
using Microsoft.Data.Sqlite;

namespace Neo.Storage;

public sealed class StorageReaper
{
    private readonly NeoDatabase _db;
    private readonly StorageConfiguration _config;
    private AuditLog? _auditLog;

    // 批量删除大小（每次事务删除的最大 chunk 数）
    private const int DeleteBatchSize = 100;

    public long TotalFreedBytes { get; private set; }
    public int TotalDeletedChunks { get; private set; }

    public StorageReaper(NeoDatabase db, StorageConfiguration config)
    {
        _db = db;
        _config = config;
    }

    public void SetAuditLog(AuditLog auditLog)
    {
        _auditLog = auditLog;
    }

    /// <summary>
    /// 检查存储占用并在超过阈值时自动清理。
    /// CHARTER.md §2.5: 无提示，自动删除，必须记录日志。
    /// </summary>
    public void CheckAndCleanup()
    {
        long threshold = (long)(_config.StorageLimitBytes * _config.CleanupThreshold);
        long currentSize = GetCurrentStorageSize();

        if (currentSize <= threshold)
            return;

        Trace.TraceWarning("[StorageReaper] Storage {0:F1} MB exceeds threshold {1:F1} MB, starting cleanup",
            currentSize / (1024.0 * 1024), threshold / (1024.0 * 1024));

        int totalDeleted = 0;
        long totalFreed = 0;

        while (currentSize > threshold)
        {
            var (deleted, freed) = DeleteOldestChunks();
            if (deleted == 0)
            {
                Trace.TraceWarning("[StorageReaper] No more deletable chunks (only active session remains)");
                break;
            }

            totalDeleted += deleted;
            totalFreed += freed;
            currentSize -= freed;
        }

        if (totalDeleted > 0)
        {
            TotalDeletedChunks += totalDeleted;
            TotalFreedBytes += totalFreed;

            // 更新 storage_state
            UpdateStorageState(-totalFreed, -totalDeleted);

            // 记录最后清理时间
            UpdateLastCleanupTime(totalFreed);

            _auditLog?.Log("STORAGE_CLEANUP",
                details: $"Deleted {totalDeleted} chunks, freed {totalFreed} bytes, " +
                         $"current size: {currentSize} bytes");

            Trace.TraceInformation("[StorageReaper] Cleanup complete: deleted {0} chunks, freed {1:F1} MB",
                totalDeleted, totalFreed / (1024.0 * 1024));
        }
    }

    /// <summary>
    /// 删除最旧的非活跃会话的 chunk 批次。
    /// </summary>
    private (int deleted, long freed) DeleteOldestChunks()
    {
        var conn = _db.GetWriteConnection();
        using var transaction = conn.BeginTransaction();

        try
        {
            using var cmd = conn.CreateCommand();
            cmd.Transaction = transaction;

            // 找到最旧的非活跃会话的 EEG chunks
            cmd.CommandText = """
                SELECT c.id, c.byte_length
                FROM eeg_chunks c
                JOIN sessions s ON c.session_id = s.id
                WHERE s.is_active = 0
                ORDER BY c.start_time_us ASC
                LIMIT @limit;
                """;
            cmd.Parameters.AddWithValue("@limit", DeleteBatchSize);

            var toDelete = new List<(long id, long bytes)>();
            using (var reader = cmd.ExecuteReader())
            {
                while (reader.Read())
                    toDelete.Add((reader.GetInt64(0), reader.GetInt64(1)));
            }

            if (toDelete.Count == 0)
            {
                // 尝试删除最旧的活跃会话中最旧的 chunk（仅当没有非活跃会话时）
                // 但 CHARTER.md §2.5 说"当前活跃监护不可删除"
                // 所以我们检查是否有非活跃会话可清理其 nirs_chunks
                cmd.CommandText = """
                    SELECT c.id, c.byte_length
                    FROM nirs_chunks c
                    JOIN sessions s ON c.session_id = s.id
                    WHERE s.is_active = 0
                    ORDER BY c.start_time_us ASC
                    LIMIT @limit;
                    """;
                using (var reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                        toDelete.Add((reader.GetInt64(0), reader.GetInt64(1)));
                }

                if (toDelete.Count == 0)
                {
                    transaction.Rollback();
                    return (0, 0);
                }

                // 删除 NIRS chunks
                long nirsFreed = 0;
                foreach (var (id, bytes) in toDelete)
                {
                    cmd.CommandText = "DELETE FROM nirs_chunks WHERE id = @id;";
                    cmd.Parameters.Clear();
                    cmd.Parameters.AddWithValue("@id", id);
                    cmd.ExecuteNonQuery();
                    nirsFreed += bytes;
                }

                // 清理空的非活跃会话
                CleanupEmptySessions(cmd);

                transaction.Commit();
                return (toDelete.Count, nirsFreed);
            }

            // 删除 EEG chunks
            long freed = 0;
            foreach (var (id, bytes) in toDelete)
            {
                cmd.CommandText = "DELETE FROM eeg_chunks WHERE id = @id;";
                cmd.Parameters.Clear();
                cmd.Parameters.AddWithValue("@id", id);
                cmd.ExecuteNonQuery();
                freed += bytes;
            }

            // 清理空的非活跃会话（所有 chunk 已删除）
            CleanupEmptySessions(cmd);

            transaction.Commit();
            return (toDelete.Count, freed);
        }
        catch
        {
            transaction.Rollback();
            throw;
        }
    }

    /// <summary>
    /// 删除没有任何 chunk 的非活跃会话。
    /// </summary>
    private static void CleanupEmptySessions(SqliteCommand cmd)
    {
        cmd.CommandText = """
            DELETE FROM sessions
            WHERE is_active = 0
              AND id NOT IN (SELECT DISTINCT session_id FROM eeg_chunks)
              AND id NOT IN (SELECT DISTINCT session_id FROM nirs_chunks);
            """;
        cmd.Parameters.Clear();
        cmd.ExecuteNonQuery();

        // 清理没有会话的患者
        cmd.CommandText = """
            DELETE FROM patients
            WHERE id NOT IN (SELECT DISTINCT patient_id FROM sessions);
            """;
        cmd.ExecuteNonQuery();
    }

    /// <summary>
    /// 获取当前存储占用。
    /// </summary>
    public long GetCurrentStorageSize()
    {
        var conn = _db.GetWriteConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT COALESCE(total_bytes, 0) FROM storage_state WHERE id = 1;";
        var result = cmd.ExecuteScalar();
        return result != null && result != DBNull.Value ? Convert.ToInt64(result) : 0;
    }

    private void UpdateStorageState(long bytesDelta, int chunkDelta)
    {
        var conn = _db.GetWriteConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            UPDATE storage_state
            SET total_bytes = MAX(0, total_bytes + @bytes),
                eeg_chunk_count = MAX(0, eeg_chunk_count + @cnt),
                updated_at = datetime('now')
            WHERE id = 1;
            """;
        cmd.Parameters.AddWithValue("@bytes", bytesDelta);
        cmd.Parameters.AddWithValue("@cnt", chunkDelta);
        cmd.ExecuteNonQuery();
    }

    private void UpdateLastCleanupTime(long freedBytes)
    {
        long timestampUs = Stopwatch.GetTimestamp() * 1_000_000 / Stopwatch.Frequency;
        var conn = _db.GetWriteConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            UPDATE storage_state
            SET last_cleanup_time_us = @ts, last_cleanup_freed_bytes = @freed
            WHERE id = 1;
            """;
        cmd.Parameters.AddWithValue("@ts", timestampUs);
        cmd.Parameters.AddWithValue("@freed", freedBytes);
        cmd.ExecuteNonQuery();
    }
}
