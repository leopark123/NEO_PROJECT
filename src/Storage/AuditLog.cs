// AuditLog.cs
// 审计日志写入 - S4-01
//
// 依据: 00_CONSTITUTION.md 铁律7 (全链路可审计)
//       00_CONSTITUTION.md 铁律12 (append-only)
//       CHARTER.md §2.5 (删除操作必记录日志)

using System.Diagnostics;
using Microsoft.Data.Sqlite;

namespace Neo.Storage;

public sealed class AuditLog
{
    private readonly SqliteConnection _conn;
    private readonly SqliteCommand _insertCmd;

    public AuditLog(SqliteConnection writeConnection)
    {
        _conn = writeConnection;

        // Prepared statement（避免重复解析）
        _insertCmd = _conn.CreateCommand();
        _insertCmd.CommandText = """
            INSERT INTO audit_log (timestamp_us, event_type, session_id, old_value, new_value, details)
            VALUES (@ts, @type, @sid, @old, @new, @details);
            """;
        _insertCmd.Parameters.Add("@ts", SqliteType.Integer);
        _insertCmd.Parameters.Add("@type", SqliteType.Text);
        _insertCmd.Parameters.Add("@sid", SqliteType.Integer);
        _insertCmd.Parameters.Add("@old", SqliteType.Text);
        _insertCmd.Parameters.Add("@new", SqliteType.Text);
        _insertCmd.Parameters.Add("@details", SqliteType.Text);
        _insertCmd.Prepare();
    }

    public void Log(string eventType, long? sessionId = null,
        string? oldValue = null, string? newValue = null, string? details = null)
    {
        long timestampUs = Stopwatch.GetTimestamp() * 1_000_000 / Stopwatch.Frequency;

        _insertCmd.Parameters["@ts"].Value = timestampUs;
        _insertCmd.Parameters["@type"].Value = eventType;
        _insertCmd.Parameters["@sid"].Value = sessionId.HasValue ? sessionId.Value : DBNull.Value;
        _insertCmd.Parameters["@old"].Value = oldValue ?? (object)DBNull.Value;
        _insertCmd.Parameters["@new"].Value = newValue ?? (object)DBNull.Value;
        _insertCmd.Parameters["@details"].Value = details ?? (object)DBNull.Value;

        _insertCmd.ExecuteNonQuery();

        Trace.TraceInformation("[AuditLog] {0}: session={1} {2}",
            eventType, sessionId, details ?? "");
    }
}
