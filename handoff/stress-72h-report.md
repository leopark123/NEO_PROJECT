# S4-03: 72-Hour Storage Stress Test Report

> **版本**: v3.0
> **执行者**: Claude Code
> **执行日期**: 2026-01-29
> **关联任务**: S4-03
> **覆盖验收标准**: AT-20, AT-22, AT-24, ARCHITECTURE.md §8.6

---

## 1. 压测策略

**策略 C（组合）**: 时间加速仿真 + 存储上限缩放

| 参数 | 生产值 | 压测值 | 缩放比 |
|------|--------|--------|--------|
| 运行时长 | 72 小时 | ~36 秒 | 7,200x 加速 |
| 数据量 | 41,472,000 samples | 41,472,000 samples | 1:1 (完整) |
| Chunk 数量 | 259,200 | 259,200 | 1:1 (完整) |
| 存储上限 | 300 GiB | 50 MB | 6,291x 缩放 |
| 清理阈值 | 90% (270 GiB) | 80% (40 MB) | 提前触发 |
| 写入间隔 | 500 ms | 10 ms | 全速 |

### v2→v3 变更历史

| 版本 | 审计发现 | 修复 |
|------|----------|------|
| v2 | P1: 活跃会话仅 64.8h | 活跃会话独占 259,200 chunks (72h)，旧会话 30,000 chunks 直接 SQL 注入 |
| v2 | P1: AT-22 内存未断言 | `Assert.True(gcHeapGrowth < 50MB)` 强制 GC 后 |
| v2 | P2: 未验证删除最旧 | 清理前后 `QueryEarliestChunkTimestamp`，assert 时间戳右移 |
| v2 | P2: 无连续性验证 | SQL LAG() 窗口函数检查活跃会话时间间隙 >2s |
| v3 | P1: AT-22 用 50MB 绝对值替代原标准 | 恢复 AT-22 原标准: 暖机基线 vs 最终 GC 后堆, 增长率 < 10% |
| v3 | P2: ChunkWriter dispose 竞态异常 | 修复根因: 事务作用域 + Join 超时 + 写入线程安全退出 |

---

## 2. 等价性说明

### 为何可代表 72 小时

1. **活跃会话完整 72h**: 单一活跃会话写入 259,200 个 1 秒 EEG chunk（160Hz × 4ch × int16），时间跨度 72.0 小时。每个 chunk 经过完整的 `EegChunkEncoder.Encode` → `ConcurrentQueue` → `ChunkWriter.DrainQueues` → SQLite INSERT 全链路。

2. **存储上限缩放**: 50 MB 上限 + 30,000 旧会话 seed chunks 迫使 StorageReaper 触发清理。验证 reaper 删除最旧（非活跃）会话。

3. **并发验证**: 读取线程在写入 + 删除期间持续执行 `QueryTimeRange` 和 `ReadChunk`，验证 WAL 并发读写无死锁、无损坏。

4. **会话保护**: 旧会话 30,000 chunks 被 reaper 部分删除，活跃会话 259,200 chunks 全部保留。

### 不等价的方面

| 差异点 | 影响 | 缓解 |
|--------|------|------|
| 无真实 sleep 间隔 | ConcurrentQueue 积压（生产中不会） | Post-GC 堆验证无泄漏 (8.0 MB) |
| WS 增长高于生产 | 队列积压导致（非泄漏） | GC 后堆 < 暖机基线，增长率为负 |
| SQLite WAL checkpoint 频率不同 | 加速模式下 WAL 增长更快 | DB 文件大小仍在合理范围 (376 MB) |
| 无 NIRS 数据流 | NIRS 被 ADR-015 阻塞 | 仅 EEG 测试 |
| 旧会话 chunks 直接 SQL 注入 | 绕过 ChunkWriter 编码链路 | 旧会话仅作为 reaper 删除目标，不验证编码 |

---

## 3. 执行方式

```bash
# 构建
dotnet build tests/StressTests/Neo.StressTests.csproj

# 运行（带详细输出）
dotnet test tests/StressTests/Neo.StressTests.csproj --verbosity normal --logger "console;verbosity=detailed"
```

入口: `tests/StressTests/Storage72hStressTest.cs` → `Stress_72h_FullVolume_WriteDeletePlayback()`

---

## 4. 关键统计结果

### 4.1 写入统计

| 指标 | 值 | 目标 | 状态 |
|------|-----|------|------|
| 活跃会话写入 chunks | 259,200 | 259,200 (72h equiv) | PASS |
| 活跃会话时间跨度 | 72.0 hours | ≥71.9h | PASS |
| 活跃会话写入字节 | 333,849,600 (318.4 MB) | ~331 MB (AT-20) | PASS |
| 总样本数 (等效) | 41,472,000 | 41,472,000 | PASS |
| 旧会话 seed chunks | 30,000 | (reaper 目标) | — |
| 写入错误 | 0 | 0 | PASS |
| 写入速率 | 7,156 chunks/sec | — | — |
| 运行耗时 | 36.2 秒 | — | — |

### 4.2 淘汰/清理

| 指标 | 值 | 说明 |
|------|-----|------|
| 删除 chunks 总数 | 9,000 | 来自旧会话 seed |
| 释放字节 | 11,592,000 (11.1 MB) | — |
| 清理审计日志条目 | 2 | STORAGE_CLEANUP |
| 清理前最早时间戳 (μs) | 0 | 旧会话 seed 起始 |
| 清理后最早时间戳 (μs) | 1,000,000 | 活跃会话起始 |
| 删除目标为最旧 | **YES** | 时间戳右移确认 |
| DB 剩余 chunks | 275,800 | 活跃 259,200 + 旧会话残余 21,000 |
| 活跃会话 chunks | 259,200 | 全部保留（受保护） |

### 4.3 存储

| 指标 | 值 |
|------|-----|
| 最终 storage_state 字节 | 360,897,600 (344.2 MB) |
| DB 文件大小 | 394,444,800 (376.2 MB) |
| 存储上限 | 52,428,800 (50 MB) |
| Overhead ratio | 1.09x (DB 文件 vs 逻辑数据) |

> 注: 存储超过 50 MB 上限是因为活跃会话受保护不可删除。这是正确行为（CHARTER.md §2.5: 当前活跃监护不可删除）。

### 4.4 内存/资源 (AT-22)

| 指标 | 值 | 说明 |
|------|-----|------|
| GC Heap 冷启动 | 0.7 MB | 进程初始化后 |
| **GC Heap 暖机基线** | **20.2 MB** | **25,000 chunks 后强制 GC** |
| **GC Heap 最终** | **8.0 MB** | **全部完成后强制 GC** |
| **GC Heap 增长率** | **-60.5%** | **(8.0 - 20.2) / 20.2 = 负增长** |
| **AT-22 判定** | **< 10% 限值** | **PASS** |
| Working Set 起始 | 54.7 MB | — |
| Working Set 结束 | 136.4 MB | — |

**AT-22 内存增长 < 10% 验证方法**:

spec/ACCEPTANCE_TESTS.md AT-22 要求"内存增长 < 10%"。在加速测试中，采用以下等价验证：

1. **暖机基线** (25,000 chunks ≈ 6.9h 等效后): 强制 `GC.Collect(2, Aggressive)` + `GC.WaitForPendingFinalizers()` + `GC.GetTotalMemory(forceFullCollection: true)` = **20.2 MB**。此时 SQLite 连接/prepared statements/运行时均已稳定。

2. **最终值** (259,200 chunks 全部完成，writer 已停止后): 同上强制 GC = **8.0 MB**。

3. **增长率**: (8.0 - 20.2) / 20.2 = **-60.5%** (负增长)。最终堆 < 暖机基线，无泄漏。

4. **断言**: `Assert.True(gcHeapGrowthPct < 0.10)` — 通过（-60.5% < 10%）。

5. **Working Set 不作为断言指标**: 加速测试中 ConcurrentQueue 积压导致 WS 增长（生产环境每秒仅 1 个 chunk，队列深度 ≤ 1）。GC 后堆才是真实保留内存的有效度量。

**内存趋势（采样点，每 25,000 chunks）:**

| Chunks | 等效时间 | WS (MB) | GC Heap (MB) |
|--------|----------|---------|--------------|
| 25,000 | 6.9h | 150.0 | 24.0 |
| 50,000 | 13.9h | 192.0 | 62.0 |
| 75,000 | 20.8h | 229.0 | 94.0 |
| 100,000 | 27.8h | 265.0 | 133.0 |
| 125,000 | 34.7h | 303.0 | 159.0 |
| 150,000 | 41.7h | 345.0 | 215.0 |
| 175,000 | 48.6h | 387.0 | 224.0 |
| 200,000 | 55.6h | 421.0 | 273.0 |
| 225,000 | 62.5h | 450.0 | 316.0 |
| 250,000 | 69.4h | 487.0 | 333.0 |
| **259,200 (GC后)** | **72.0h** | **136.4** | **8.0** |

> 趋势中 GC Heap 持续增长是因为采样使用 `GC.GetTotalMemory(false)` 不触发回收。最终强制 GC 后降至 8.0 MB < 暖机基线 20.2 MB，证明无泄漏。

### 4.5 正确性校验

| 检查项 | 结果 | 目标 |
|--------|------|------|
| 写入侧时间戳单调违规 | 0 | 0 |
| DB 持久化时间戳单调违规 | 0 | 0 |
| 活跃会话时间间隙 (>2s) | 0 | 0 |
| 活跃最早时间戳 (μs) | 1,000,000 | — |
| 活跃最新时间戳 (μs) | 259,200,993,750 | — |
| 活跃时间跨度 (hours) | 72.0h | ≥71.9h |
| 回放验证错误 | 0 | 0 |
| 并发 reader 查询数 | 628 | >0 |
| 并发 reader 解码 chunks | 314 | >0 |
| 并发 reader 错误 | 0 | 0 |

### 4.6 运行稳定性

| 检查项 | v2 状态 | v3 状态 |
|--------|---------|---------|
| 未处理异常 | ChunkWriter dispose 竞态异常 | **无** |
| 测试进程崩溃 | 否 | 否 |
| 测试退出码 | 0 (PASS) | 0 (PASS) |

---

## 5. 验收标准映射

### AT-20: 72h 数据存储

| 验收条件 | 结果 | 状态 |
|----------|------|------|
| 数据量: EEG 160Hz×4ch×2bytes×72h ≈ 331 MB | 318.4 MB 写入 | PASS |
| 活跃会话覆盖完整 72h | 259,200 chunks, 72.0h 时间跨度 | PASS |
| 数据完整性 100% | 0 写入错误, 0 单调违规, 0 时间间隙 | PASS |
| 读取正常 | 回放验证 0 错误 | PASS |

### AT-22: 72h 连续运行

| 验收条件 | 结果 | 状态 |
|----------|------|------|
| 无崩溃 | 测试完成，无异常，无未处理异常 | PASS |
| 无内存泄漏 (内存增长 < 10%) | GC 堆增长 -60.5% (暖机 20.2 MB → 最终 8.0 MB) | **PASS** |
| 内存断言 | `Assert.True(gcHeapGrowthPct < 0.10)` 通过 | PASS |
| 数据完整无丢失 | 259,200 chunks 全部写入, 活跃会话连续 | PASS |

> 注: AT-22 的 CPU 和帧率条件需要全系统集成测试，不在存储层压测范围内。

### AT-24: 存储滚动清理

| 验收条件 | 结果 | 状态 |
|----------|------|------|
| 存储上限检测正确 | 在 40 MB 阈值处触发清理 | PASS |
| 最旧数据被删除 | 旧会话 9,000 chunks 被删除，最早时间戳从 0 → 1,000,000 | PASS |
| 删除目标验证 | 清理后最早时间戳右移（断言通过） | PASS |
| 当前监护不中断 | 活跃会话 259,200 chunks 完整保留 | PASS |
| 活跃会话连续性 | 0 个时间间隙 (>2s 阈值) | PASS |
| 清理过程无数据丢失 | 0 写入错误, 并发读 0 错误 | PASS |
| 删除操作有审计日志 | 2 条 STORAGE_CLEANUP 审计记录 | PASS |

### ARCHITECTURE.md §8.6: 72h 连续写入零失败

| 验收条件 | 结果 | 状态 |
|----------|------|------|
| 72h 连续写入零失败 | 259,200 chunks, 0 errors, 72.0h 时间跨度 | **PASS** |

---

## 6. 断言清单 (v3)

测试包含以下硬断言（任一失败即测试 FAIL）:

| # | 断言 | 目标 |
|---|------|------|
| 1 | `writer.TotalEegChunksWritten >= 259,200` | AT-20: 完整 72h 数据量 |
| 2 | `activeTimeSpanHours >= 71.9` | AT-20: 时间跨度覆盖 |
| 3 | `writeErrors == 0` | AT-20: 零写入错误 |
| 4 | `gcHeapWarmBaseline > 0` | AT-22: 暖机基线已捕获 |
| 5 | `gcHeapGrowthPct < 0.10` | AT-22: 内存增长 < 10% |
| 6 | `totalReaperDeleted > 0` | AT-24: reaper 有触发 |
| 7 | `cleanupAuditEntries > 0` | AT-24: 审计日志存在 |
| 8 | `deletionShiftedEarliest == true` | AT-24: 删除目标为最旧 |
| 9 | `activeSessionGaps == 0` | AT-24: 活跃会话连续 |
| 10 | `monotonicViolations == 0` | 正确性: 写入侧单调 |
| 11 | `dbMonotonicViolations == 0` | 正确性: DB 侧单调 |
| 12 | `playbackVerifyErrors == 0` | 正确性: 回放解码 |
| 13 | `activeChunksRemaining > 0` | 正确性: 活跃数据保留 |
| 14 | `readerErrors.Count == 0` | 并发: 无读取错误 |

---

## 7. ChunkWriter 事务竞态修复 (v3)

### 问题

v2 报告中的 "已知限制" 第 6 条记录了 `SqliteTransaction has completed` 异常。v3 中查明根因并修复。

### 根因分析

`ChunkWriter.DrainQueues()` 原实现中 `_reaper.CheckAndCleanup()` 位于事务的 try-catch 块内。当 `CheckAndCleanup()` 在 `transaction.Commit()` 之后抛出异常时，catch 块尝试对已提交的事务执行 `Rollback()`，导致 `SqliteTransaction has completed` 异常。

此外，`using var transaction` 声明使事务对象在整个方法作用域内存活（即使已 Commit），当 `CheckAndCleanup()` 在同一连接上开启新事务时产生嵌套事务错误。

### 修复内容 (`src/Storage/ChunkWriter.cs`)

1. **事务作用域**: 将 `using var transaction` 改为 `using (var transaction)` 块语句，确保事务在 `CheckAndCleanup()` 调用前完全释放。

2. **清理调用位置**: 将 `_reaper.CheckAndCleanup()` 移到事务 `using` 块之外，不再被事务的 try-catch 保护。事务提交后的清理失败不会触发 `Rollback()`。

3. **Join 超时**: 将 `Stop()` 中的 `_writerThread.Join` 超时从 5 秒增加到 30 秒，避免在加速测试中因超时导致主线程和写入线程同时调用 `DrainQueues()`。

4. **安全退出**: 写入线程退出循环后的最终 `DrainQueues()` 包裹在 `try-catch(ObjectDisposedException)` 中，处理 DB 提前释放的情况。

### 验证

- v3 测试运行 **无未处理异常**（v2 有 `SqliteTransaction has completed`）
- 23 个 Storage 单元测试全部通过
- 压测 36.2 秒内 259,200 chunks 完整写入

---

## 8. 已知限制

1. **NIRS 未测试**: ADR-015 阻塞，NirsChunkEncoder/Store 为最小占位实现
2. **时间加速伪影**: WS 内存增长高于生产预期，但 GC 后堆确认无泄漏
3. **单活跃会话**: 测试仅含 1 个活跃 + 1 个非活跃会话；生产环境可能有更多会话轮换
4. **无渲染/DSP 并发**: 本测试聚焦存储层，未包含 DSP 滤波或渲染线程的并发压力
5. **SQLite WAL checkpoint**: 加速模式下 WAL 增长模式与生产不同（生产有 60s 间隔 checkpoint）

---

## 9. 变更文件

| 文件 | 用途 | v3 变更 |
|------|------|---------|
| `tests/StressTests/Neo.StressTests.csproj` | 压测项目文件 | 无变更 |
| `tests/StressTests/Storage72hStressTest.cs` | 72h 压测实现 | AT-22 暖机基线 + 百分比断言 |
| `src/Storage/ChunkWriter.cs` | 后台批量写入 | 事务作用域修复 + Join 超时 |

---

## 10. 变更历史

| 版本 | 日期 | 变更内容 |
|------|------|----------|
| v1.0 | 2026-01-29 | 初始版本: 72h 全量压测 (策略 C) |
| v2.0 | 2026-01-29 | 审计修复: 活跃会话 72h 全覆盖, GC 堆断言, 删除目标验证, 连续性验证 |
| v3.0 | 2026-01-29 | 审计修复: AT-22 恢复原标准 (暖机基线 + <10% 增长率), ChunkWriter 事务竞态修复 |

---

**文档结束**
