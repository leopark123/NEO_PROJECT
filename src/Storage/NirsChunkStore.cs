// NirsChunkStore.cs
// NIRS Chunk 读取实现 - S4-01
//
// 状态: 最小实现。NIRS 协议 Blocked (S3-00/ADR-015)。

using Microsoft.Data.Sqlite;
using Neo.Core.Models;

namespace Neo.Storage;

public sealed class NirsChunkStore : INirsChunkStore, IDisposable
{
    private readonly NeoDatabase _db;
    private SqliteConnection? _readConn;

    public NirsChunkStore(NeoDatabase db)
    {
        _db = db;
    }

    private SqliteConnection GetReadConnection()
    {
        if (_readConn == null || _readConn.State != System.Data.ConnectionState.Open)
        {
            _readConn?.Dispose();
            _readConn = _db.CreateReadConnection();
        }
        return _readConn;
    }

    public IReadOnlyList<NirsChunkInfo> QueryTimeRange(long sessionId, long startUs, long endUs)
    {
        var conn = GetReadConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            SELECT id, session_id, start_time_us, end_time_us, sample_count, byte_length
            FROM nirs_chunks
            WHERE session_id = @sid AND end_time_us >= @start AND start_time_us <= @end
            ORDER BY start_time_us;
            """;
        cmd.Parameters.AddWithValue("@sid", sessionId);
        cmd.Parameters.AddWithValue("@start", startUs);
        cmd.Parameters.AddWithValue("@end", endUs);

        var result = new List<NirsChunkInfo>();
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            result.Add(new NirsChunkInfo
            {
                Id = reader.GetInt64(0),
                SessionId = reader.GetInt64(1),
                StartTimeUs = reader.GetInt64(2),
                EndTimeUs = reader.GetInt64(3),
                SampleCount = reader.GetInt32(4),
                ByteLength = reader.GetInt32(5)
            });
        }
        return result;
    }

    public IReadOnlyList<NirsChunkInfo> GetSessionIndex(long sessionId)
    {
        var conn = GetReadConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            SELECT id, session_id, start_time_us, end_time_us, sample_count, byte_length
            FROM nirs_chunks WHERE session_id = @sid ORDER BY start_time_us;
            """;
        cmd.Parameters.AddWithValue("@sid", sessionId);

        var result = new List<NirsChunkInfo>();
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            result.Add(new NirsChunkInfo
            {
                Id = reader.GetInt64(0),
                SessionId = reader.GetInt64(1),
                StartTimeUs = reader.GetInt64(2),
                EndTimeUs = reader.GetInt64(3),
                SampleCount = reader.GetInt32(4),
                ByteLength = reader.GetInt32(5)
            });
        }
        return result;
    }

    public NirsSample[] ReadChunk(long chunkId)
    {
        var conn = GetReadConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT data_blob, start_time_us FROM nirs_chunks WHERE id = @id;";
        cmd.Parameters.AddWithValue("@id", chunkId);

        using var reader = cmd.ExecuteReader();
        if (!reader.Read())
            return [];

        byte[] blob = (byte[])reader["data_blob"];
        long startTimeUs = reader.GetInt64(1);
        return NirsChunkEncoder.Decode(blob, startTimeUs, 4); // default 4Hz
    }

    public void Dispose()
    {
        _readConn?.Dispose();
        _readConn = null;
    }
}
