# 📦 存储策略冻结文档

> **版本**: v1.1（整合ChatGPT方案）  
> **状态**: 🔒 已冻结  
> **目标**: 72小时以上连续运行、300GB上限、零维护

---

## 核心原则

```
SQLite ≠ 存原始大数据
SQLite = 索引 + 元数据
大数据 = 文件 Chunk
```

---

## 数据分层结构（必须这样做）

```
/data/
 ├── patient_001/
 │    ├── meta.db               ← SQLite（索引+元数据）
 │    ├── eeg/
 │    │    ├── eeg_20240101_1000.bin
 │    │    ├── eeg_20240101_1010.bin
 │    │    └── ...
 │    ├── aeeg/
 │    │    └── aeeg_20240101_1000.bin
 │    ├── nirs/
 │    │    └── nirs_20240101_1000.bin
 │    └── video/
 │         └── cam_20240101_1000.mp4
```

---

## Chunk 规则（冻结值）

| 项目 | 冻结值 |
|------|--------|
| **Chunk 粒度** | **10 分钟/文件** |
| EEG 格式 | 原始 int16 |
| aEEG 格式 | GS 直方图 |
| NIRS 格式 | 原始设备值 |
| Video 格式 | mp4 / avi |
| 命名规则 | `{type}_{date}_{time}.{ext}` |

---

## SQLite 只负责什么（明确边界）

### ✅ SQLite 存储
- Chunk 文件路径
- 起止时间（μs）
- 数据类型（EEG / aEEG / NIRS / Video）
- 校验信息（CRC/SHA256）
- 删除状态
- 患者元数据
- 会话信息
- 事件标记

### ❌ SQLite 禁止
- **禁止**把 EEG 波形点直接写进 SQLite
- **禁止**存储大二进制数据（BLOB > 1MB）

---

## SQLite Schema（冻结）

```sql
-- 患者表
CREATE TABLE patients (
    id TEXT PRIMARY KEY,
    name TEXT,
    birth_date TEXT,
    created_at INTEGER NOT NULL,  -- 微秒时间戳
    metadata TEXT  -- JSON
);

-- 监护会话表
CREATE TABLE sessions (
    id TEXT PRIMARY KEY,
    patient_id TEXT NOT NULL REFERENCES patients(id),
    start_time INTEGER NOT NULL,  -- 微秒时间戳
    end_time INTEGER,             -- 微秒时间戳，NULL表示进行中
    status TEXT NOT NULL,         -- 'active', 'completed', 'deleted'
    metadata TEXT  -- JSON
);

-- Chunk 索引表
CREATE TABLE chunks (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    session_id TEXT NOT NULL REFERENCES sessions(id),
    data_type TEXT NOT NULL,      -- 'eeg', 'nirs'
    start_time INTEGER NOT NULL,  -- 微秒时间戳
    end_time INTEGER NOT NULL,    -- 微秒时间戳
    file_path TEXT NOT NULL,      -- 相对路径
    file_size INTEGER NOT NULL,   -- 字节
    sample_count INTEGER NOT NULL,
    checksum TEXT                 -- SHA256
);

-- aEEG 趋势表（直接存SQLite）
CREATE TABLE aeeg_trends (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    session_id TEXT NOT NULL REFERENCES sessions(id),
    timestamp INTEGER NOT NULL,   -- 微秒时间戳
    channel INTEGER NOT NULL,     -- 通道号
    upper_margin REAL NOT NULL,   -- 上边界 μV
    lower_margin REAL NOT NULL,   -- 下边界 μV
    bandwidth REAL NOT NULL       -- 带宽 μV
);

-- 事件标记表
CREATE TABLE events (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    session_id TEXT NOT NULL REFERENCES sessions(id),
    timestamp INTEGER NOT NULL,   -- 微秒时间戳
    event_type TEXT NOT NULL,     -- 'seizure', 'artifact', 'marker', etc.
    description TEXT,
    created_by TEXT               -- 'system', 'user'
);

-- 删除日志表（审计用，不可删除）
CREATE TABLE deletion_log (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    deleted_at INTEGER NOT NULL,  -- 微秒时间戳
    session_id TEXT NOT NULL,
    patient_id TEXT NOT NULL,
    reason TEXT NOT NULL,         -- 'storage_limit', 'manual'
    freed_bytes INTEGER NOT NULL
);

-- 索引
CREATE INDEX idx_chunks_session ON chunks(session_id);
CREATE INDEX idx_chunks_time ON chunks(start_time, end_time);
CREATE INDEX idx_aeeg_session_time ON aeeg_trends(session_id, timestamp);
CREATE INDEX idx_events_session ON events(session_id);
```

---

## Chunk 文件格式（冻结）

```
┌─────────────────────────────────────────────────────────────┐
│                    Chunk 文件结构                            │
├─────────────────────────────────────────────────────────────┤
│  Header (64 bytes)                                          │
│  ├── Magic: "NEOC" (4 bytes)                               │
│  ├── Version: uint16 (2 bytes)                             │
│  ├── DataType: uint8 (1 byte) - 0=EEG, 1=NIRS              │
│  ├── ChannelCount: uint8 (1 byte)                          │
│  ├── SampleRate: float32 (4 bytes)                         │
│  ├── StartTime: int64 μs (8 bytes)                         │
│  ├── EndTime: int64 μs (8 bytes)                           │
│  ├── SampleCount: uint32 (4 bytes)                         │
│  ├── Checksum: uint32 CRC32 (4 bytes)                      │
│  └── Reserved (28 bytes)                                    │
├─────────────────────────────────────────────────────────────┤
│  Data (variable)                                            │
│  └── Samples: [timestamp:int64, ch0:float32, ch1:float32...]│
│      重复 SampleCount 次                                    │
└─────────────────────────────────────────────────────────────┘
```

---

## 禁止事项

```
❌ 不得将原始波形数据存入 SQLite BLOB
❌ 不得修改已写入的 Chunk 文件（只读）
❌ 不得部分删除会话数据
❌ 不得关闭删除日志
❌ 不得删除当前活跃会话
❌ 不得自定义 Chunk 粒度（固定10分钟）
```

---

## 验收测试（AT-24）

```
1. 写入数据直到达到 300 GiB
2. 验证自动删除最旧会话
3. 验证删除日志记录完整
4. 验证当前活跃会话未被删除
5. 验证删除后空间正确释放
```

---

**🔒 END OF STORAGE_POLICY v1.0**
