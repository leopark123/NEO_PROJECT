// NeoDatabase.cs
// 数据库初始化、Schema 定义、PRAGMA 配置 - S4-01
//
// 依据: ARCHITECTURE.md §8.2 (WAL, synchronous=NORMAL, cache_size, etc.)
//       ARCHITECTURE.md §8.7 (表结构，本实现改用 Chunk BLOB 方案)
//       00_CONSTITUTION.md 铁律12 (Raw append-only)
//       00_CONSTITUTION.md 铁律13 (所有记录带时间戳)

using System.Diagnostics;
using Microsoft.Data.Sqlite;

namespace Neo.Storage;

public sealed class NeoDatabase : IDisposable
{
    private const int CurrentSchemaVersion = 1;

    private readonly StorageConfiguration _config;
    private readonly string _connectionString;
    private SqliteConnection? _writeConnection;
    private bool _disposed;

    public NeoDatabase(StorageConfiguration config)
    {
        _config = config ?? throw new ArgumentNullException(nameof(config));

        string dir = Path.GetDirectoryName(Path.GetFullPath(config.DbPath))!;
        Directory.CreateDirectory(dir);

        _connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = Path.GetFullPath(config.DbPath),
            Mode = SqliteOpenMode.ReadWriteCreate,
            Cache = SqliteCacheMode.Shared
        }.ToString();
    }

    /// <summary>
    /// 初始化数据库：创建表、设置 PRAGMAs、执行迁移。
    /// </summary>
    public void Initialize()
    {
        _writeConnection = CreateConnection();
        _writeConnection.Open();

        ApplyPragmas(_writeConnection);
        EnsureSchema(_writeConnection);

        Trace.TraceInformation("[NeoDatabase] Initialized: {0}", _config.DbPath);
    }

    /// <summary>
    /// 获取写入连接（单一写入连接，ARCHITECTURE.md §8.3）。
    /// </summary>
    public SqliteConnection GetWriteConnection()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        return _writeConnection ?? throw new InvalidOperationException("Database not initialized");
    }

    /// <summary>
    /// 创建只读连接（用于查询，不阻塞写入）。
    /// </summary>
    public SqliteConnection CreateReadConnection()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        var csb = new SqliteConnectionStringBuilder
        {
            DataSource = Path.GetFullPath(_config.DbPath),
            Mode = SqliteOpenMode.ReadOnly,
            Cache = SqliteCacheMode.Shared
        };

        var conn = new SqliteConnection(csb.ToString());
        conn.Open();
        ApplyReadPragmas(conn);
        return conn;
    }

    /// <summary>
    /// 应用写入连接 PRAGMAs。
    /// 依据: ARCHITECTURE.md §8.2
    /// </summary>
    private void ApplyPragmas(SqliteConnection conn)
    {
        using var cmd = conn.CreateCommand();

        // WAL 模式（必须开启）- 允许并发读写
        cmd.CommandText = "PRAGMA journal_mode=WAL;";
        cmd.ExecuteNonQuery();

        // 同步级别: NORMAL（平衡安全与性能）
        cmd.CommandText = "PRAGMA synchronous=NORMAL;";
        cmd.ExecuteNonQuery();

        // 缓存大小: 64MB
        cmd.CommandText = $"PRAGMA cache_size={_config.SqliteCacheSize};";
        cmd.ExecuteNonQuery();

        // 临时存储: 内存
        cmd.CommandText = "PRAGMA temp_store=MEMORY;";
        cmd.ExecuteNonQuery();

        // 外键约束
        cmd.CommandText = "PRAGMA foreign_keys=ON;";
        cmd.ExecuteNonQuery();

        // 忙等待超时
        cmd.CommandText = $"PRAGMA busy_timeout={_config.SqliteBusyTimeout};";
        cmd.ExecuteNonQuery();

        // WAL 自动 checkpoint: 每 1000 页
        cmd.CommandText = "PRAGMA wal_autocheckpoint=1000;";
        cmd.ExecuteNonQuery();

        // mmap: 256MB（加速读取）
        cmd.CommandText = "PRAGMA mmap_size=268435456;";
        cmd.ExecuteNonQuery();
    }

    private static void ApplyReadPragmas(SqliteConnection conn)
    {
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "PRAGMA query_only=ON;";
        cmd.ExecuteNonQuery();
    }

    /// <summary>
    /// 创建/迁移 Schema。
    /// 使用简易 schema_version 表管理版本。
    /// </summary>
    private static void EnsureSchema(SqliteConnection conn)
    {
        using var transaction = conn.BeginTransaction();

        try
        {
            using var cmd = conn.CreateCommand();
            cmd.Transaction = transaction;

            // schema_version 表
            cmd.CommandText = """
                CREATE TABLE IF NOT EXISTS schema_version (
                    version INTEGER NOT NULL,
                    applied_at TEXT NOT NULL DEFAULT (datetime('now'))
                );
                """;
            cmd.ExecuteNonQuery();

            // 检查当前版本
            cmd.CommandText = "SELECT COALESCE(MAX(version), 0) FROM schema_version;";
            int currentVersion = Convert.ToInt32(cmd.ExecuteScalar());

            if (currentVersion < 1)
                ApplyV1(cmd);

            transaction.Commit();
        }
        catch
        {
            transaction.Rollback();
            throw;
        }
    }

    /// <summary>
    /// V1 Schema: Patients, Sessions, EegChunks, NirsChunks, AuditLog, StorageState
    /// </summary>
    private static void ApplyV1(SqliteCommand cmd)
    {
        // ── Patients ──────────────────────────────────────────────
        cmd.CommandText = """
            CREATE TABLE patients (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                patient_id TEXT NOT NULL UNIQUE,
                name TEXT,
                birth_date TEXT,
                created_at_us INTEGER NOT NULL,
                notes TEXT
            );
            CREATE INDEX idx_patients_pid ON patients(patient_id);
            """;
        cmd.ExecuteNonQuery();

        // ── Sessions ──────────────────────────────────────────────
        cmd.CommandText = """
            CREATE TABLE sessions (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                patient_id INTEGER NOT NULL REFERENCES patients(id),
                start_time_us INTEGER NOT NULL,
                end_time_us INTEGER,
                is_active INTEGER NOT NULL DEFAULT 1,
                eeg_sample_rate INTEGER NOT NULL DEFAULT 160,
                eeg_channel_count INTEGER NOT NULL DEFAULT 4,
                eeg_scale_factor REAL NOT NULL DEFAULT 0.076,
                device_info TEXT,
                config_json TEXT,
                created_at TEXT NOT NULL DEFAULT (datetime('now'))
            );
            CREATE INDEX idx_sessions_patient ON sessions(patient_id);
            CREATE INDEX idx_sessions_time ON sessions(start_time_us);
            CREATE INDEX idx_sessions_active ON sessions(is_active);
            """;
        cmd.ExecuteNonQuery();

        // ── EegChunks ─────────────────────────────────────────────
        // Chunk BLOB 方案：每行存储固定时长（默认1秒=160样本）的 raw int16 数据
        // BLOB 格式: [header 8 bytes] + [channel-interleaved int16 data]
        // 铁律12: append-only，不允许 UPDATE/DELETE raw 数据
        cmd.CommandText = """
            CREATE TABLE eeg_chunks (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                session_id INTEGER NOT NULL REFERENCES sessions(id),
                start_time_us INTEGER NOT NULL,
                end_time_us INTEGER NOT NULL,
                sample_count INTEGER NOT NULL,
                channel_count INTEGER NOT NULL DEFAULT 4,
                encoding_version INTEGER NOT NULL DEFAULT 1,
                quality_summary INTEGER NOT NULL DEFAULT 0,
                data_blob BLOB NOT NULL,
                byte_length INTEGER NOT NULL,
                created_at TEXT NOT NULL DEFAULT (datetime('now'))
            );
            CREATE INDEX idx_eeg_session_time ON eeg_chunks(session_id, start_time_us);
            CREATE INDEX idx_eeg_end_time ON eeg_chunks(session_id, end_time_us);
            """;
        cmd.ExecuteNonQuery();

        // ── NirsChunks ────────────────────────────────────────────
        // NIRS 协议 Blocked (S3-00/ADR-015)，表结构预留。
        // 字段定义来自 NirsSample (6通道 SpO2%)。
        // 单位/阈值由设备说明提供，不由软件推断 (CHARTER.md §2.4)。
        cmd.CommandText = """
            CREATE TABLE nirs_chunks (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                session_id INTEGER NOT NULL REFERENCES sessions(id),
                start_time_us INTEGER NOT NULL,
                end_time_us INTEGER NOT NULL,
                sample_count INTEGER NOT NULL,
                channel_count INTEGER NOT NULL DEFAULT 6,
                encoding_version INTEGER NOT NULL DEFAULT 1,
                quality_summary INTEGER NOT NULL DEFAULT 0,
                data_blob BLOB NOT NULL,
                byte_length INTEGER NOT NULL,
                created_at TEXT NOT NULL DEFAULT (datetime('now'))
            );
            CREATE INDEX idx_nirs_session_time ON nirs_chunks(session_id, start_time_us);
            """;
        cmd.ExecuteNonQuery();

        // ── AuditLog ──────────────────────────────────────────────
        // 铁律7: 全链路可审计
        // 铁律12: append-only
        cmd.CommandText = """
            CREATE TABLE audit_log (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                timestamp_us INTEGER NOT NULL,
                event_type TEXT NOT NULL,
                session_id INTEGER,
                old_value TEXT,
                new_value TEXT,
                details TEXT
            );
            CREATE INDEX idx_audit_time ON audit_log(timestamp_us);
            CREATE INDEX idx_audit_type ON audit_log(event_type);
            """;
        cmd.ExecuteNonQuery();

        // ── StorageState ──────────────────────────────────────────
        cmd.CommandText = """
            CREATE TABLE storage_state (
                id INTEGER PRIMARY KEY CHECK (id = 1),
                total_bytes INTEGER NOT NULL DEFAULT 0,
                eeg_chunk_count INTEGER NOT NULL DEFAULT 0,
                nirs_chunk_count INTEGER NOT NULL DEFAULT 0,
                storage_limit_bytes INTEGER NOT NULL,
                last_cleanup_time_us INTEGER,
                last_cleanup_freed_bytes INTEGER,
                updated_at TEXT NOT NULL DEFAULT (datetime('now'))
            );
            """;
        cmd.ExecuteNonQuery();

        // ── Version 记录 ─────────────────────────────────────────
        cmd.CommandText = "INSERT INTO schema_version (version) VALUES (1);";
        cmd.ExecuteNonQuery();
    }

    private SqliteConnection CreateConnection()
    {
        return new SqliteConnection(_connectionString);
    }

    public void Dispose()
    {
        if (_disposed) return;
        _writeConnection?.Dispose();
        _writeConnection = null;
        _disposed = true;
    }
}
