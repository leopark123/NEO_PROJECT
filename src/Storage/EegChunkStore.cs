// EegChunkStore.cs
// EEG Chunk 读取实现 - S4-01
//
// 依据: ARCHITECTURE.md §8.3 (读写分离, 只读连接)

using Microsoft.Data.Sqlite;
using Neo.Core.Models;

namespace Neo.Storage;

public sealed class EegChunkStore : IEegChunkStore, IDisposable
{
    private readonly NeoDatabase _db;
    private readonly StorageConfiguration _config;
    private SqliteConnection? _readConn;

    public EegChunkStore(NeoDatabase db, StorageConfiguration config)
    {
        _db = db;
        _config = config;
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

    public IReadOnlyList<EegChunkInfo> QueryTimeRange(long sessionId, long startUs, long endUs)
    {
        var conn = GetReadConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            SELECT id, session_id, start_time_us, end_time_us, sample_count, byte_length
            FROM eeg_chunks
            WHERE session_id = @sid AND end_time_us >= @start AND start_time_us <= @end
            ORDER BY start_time_us;
            """;
        cmd.Parameters.AddWithValue("@sid", sessionId);
        cmd.Parameters.AddWithValue("@start", startUs);
        cmd.Parameters.AddWithValue("@end", endUs);

        var result = new List<EegChunkInfo>();
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            result.Add(new EegChunkInfo
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

    public IReadOnlyList<EegChunkInfo> GetSessionIndex(long sessionId)
    {
        var conn = GetReadConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            SELECT id, session_id, start_time_us, end_time_us, sample_count, byte_length
            FROM eeg_chunks
            WHERE session_id = @sid
            ORDER BY start_time_us;
            """;
        cmd.Parameters.AddWithValue("@sid", sessionId);

        var result = new List<EegChunkInfo>();
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            result.Add(new EegChunkInfo
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

    public EegSample[] ReadChunk(long chunkId)
    {
        var conn = GetReadConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            SELECT c.data_blob, c.start_time_us, s.eeg_sample_rate, s.eeg_scale_factor
            FROM eeg_chunks c
            JOIN sessions s ON c.session_id = s.id
            WHERE c.id = @id;
            """;
        cmd.Parameters.AddWithValue("@id", chunkId);

        using var reader = cmd.ExecuteReader();
        if (!reader.Read())
            return [];

        byte[] blob = (byte[])reader["data_blob"];
        long startTimeUs = reader.GetInt64(1);
        int sampleRate = reader.GetInt32(2);
        double scaleFactor = reader.GetDouble(3);

        return EegChunkEncoder.Decode(blob, scaleFactor, startTimeUs, sampleRate);
    }

    public EegSample[] ReadTimeRange(long sessionId, long startUs, long endUs)
    {
        var chunks = QueryTimeRange(sessionId, startUs, endUs);
        if (chunks.Count == 0)
            return [];

        var allSamples = new List<EegSample>();
        foreach (var chunk in chunks)
        {
            var samples = ReadChunk(chunk.Id);
            foreach (var s in samples)
            {
                if (s.TimestampUs >= startUs && s.TimestampUs <= endUs)
                    allSamples.Add(s);
            }
        }
        return allSamples.ToArray();
    }

    public void Dispose()
    {
        _readConn?.Dispose();
        _readConn = null;
    }
}
