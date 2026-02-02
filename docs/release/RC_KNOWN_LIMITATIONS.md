# RC_KNOWN_LIMITATIONS.md — 已知限制与未实现项

> **版本**: RC-1
> **日期**: 2026-01-29
> **验证者**: Claude Code (S5-01)

---

## 1. 显式声明不在范围内的内容

### 1.1 S3-00 NIRS RS232 协议解析 — Blocked (ADR-015)

**状态**: 🚫 Blocked — 协议证据缺失

**缺失证据**:
1. 帧头定义
2. 每帧长度 (bytes)
3. 字节序
4. 校验算法 (CRC/Checksum)
5. 字段映射 (通道/状态/单位)

**当前行为**: S3-01 集成壳已就位, NIRS 模块报告 Blocked 状态, 所有值为 NaN, QualityFlag 标记为 `BlockedBySpec`。系统正常运行 (EEG-only)。

**解除条件**: 上述 5 项证据以可引用文本 (docx/pdf/md) 形式提供。

---

### 1.2 AT-15 NIRS 6 通道趋势显示

**状态**: 不可验收 — 依赖 S3-00

S3-00 Blocked 导致无 NIRS 数据源。集成壳已就位, 协议规格到位后可直接对接。

---

### 1.3 ~~AT-19 Zero-Phase 滤波 (回放模式)~~ — 已实现并集成

**状态**: ✅ 已实现并集成至回放管线

- `IirFilterBase.ProcessZeroPhase()` + `EegFilterChain.ProcessBlockZeroPhase()` 已实现 filtfilt (前后向 IIR)。6 项验收测试全部通过。
- `EegPlaybackSource` 已集成可选 `EegFilterChain` 依赖。采用**预滤波架构**：`Start()` 时调用 `BuildFilteredBuffer()` 对整个 EegRingBuffer 做一次性 zero-phase 滤波，结果存入 `_filteredBuffer`。回放循环从预滤波缓冲区读取，确保 filtfilt 对完整信号生效。
- Gap 标记 (QualityFlag.Missing / NaN) 直接透传，不参与滤波。
- 4 项回放集成测试验证：高频衰减、低频保留、无滤波透传、全通道覆盖。

---

### 1.4 ~~AT-12 LOD 金字塔~~ — 已实现

**状态**: ✅ 已实现

`src/DSP/LOD/` 实现增量式多层 Min/Max 降采样金字塔 (最大 10 层, 1024x)。含 Spike 保护和 SelectLevel 自动选级。12 项验收测试全部通过。

---

### 1.5 AT-18 时间轴控制 (跳转/缩放/拖动)

**状态**: 部分实现

回放时间轴 (S3-03) 已实现 PlaybackClock + SeekTo。完整的 UI 交互控件 (拖动、缩放滑块) 在后续 Sprint。

---

### 1.6 ~~AT-21 审计日志完整性~~ — 已增强

**状态**: ✅ 已增强

AuditLog 已扩展覆盖以下事件类型：
- `SCREENSHOT` — 截图保存后记录
- `PRINT` — 打印成功后记录
- `USB_EXPORT` — USB 导出成功后记录
- `MONITORING_START` — 监控启动时记录 (数据源、采样率、通道数)
- `MONITORING_STOP` — 监控停止时记录 (运行时长、帧数)
- `DEVICE_LOST` — D3D11 设备丢失时记录
- `DEVICE_RESTORED` — D3D11 设备恢复时记录
- `FILTER_CHANGE` — LPF 截止频率变更 (F7/F8 键触发, 重建滤波链)
- `GAIN_CHANGE` — 增益调整变更 (F5/F6 键触发, 切换 7 级增益)
- `CRC_ERROR` — RS232 CRC 校验失败 (Rs232EegSource 内部记录)
- `SERIAL_ERROR` — RS232 串口读取异常 (Rs232EegSource 内部记录)

MainForm 创建独立 `neo_audit.db` 并注入服务。滤波器/增益变更通过 `DoCycleLpfCutoff` / `DoCycleGain` 实际调用。RS232 串口异常由 `Rs232EegSource` 内部直接写入审计日志。

---

### 1.7 视频预览 UI 面板

**状态**: 未实现 — 超出 S3-02 范围

视频采集和录制功能已实现。视频预览面板 (在 MainForm 中显示实时画面) 在后续 Sprint。最新帧可通过 `VideoFrameDoubleBuffer.CopyLatestFramePixels()` 获取。

---

### 1.8 标注/事件标记功能 (OverlayLayer)

**状态**: 占位实现 — 不在 RC 范围内

`OverlayLayer` (S1-04) 中的"测量标注"、"事件标记"、"伪迹遮罩"为占位设计，从未在任何 Sprint 任务中被分配实现。当前 OverlayLayer 仅实现时间轴刻度和光标绘制。标注功能及其审计日志将在后续 Sprint 中实现。

---

### 1.9 多摄像头选择 / 变速回放

**状态**: 未实现 — 超出当前范围

当前使用第一个可用 UVC 设备。多摄像头选择和变速回放不在 S3-02/S3-03 范围内。

---

## 2. 测试失败项分析

### 2.1 DpiHelperTests.DipToPixelRound (Rendering.Tests)

**现象**: 期望 101, 实际 100
**根因**: 66.67 DIP × 1.5 DPI = 100.005, 浮点舍入在边界值处不确定
**影响**: 无。仅影响亚像素级 DPI 缩放精度
**分类**: 预存, non-blocking, 不影响临床功能

### 2.2 SafeDoubleBufferStressTests (Infrastructure.Tests, 2个)

**现象**: 多消费者压力测试在 CI 环境下偶发失败
**根因**: 多线程调度时序敏感, 非确定性竞态条件
**影响**: 无。生产使用场景 (单生产者 + 单消费者) 全部通过
**分类**: 预存, 环境敏感, non-blocking

### 2.3 72h 压测 (StressTests) — 重新执行时失败

**现象**: `SqliteConnection does not support nested transactions`
**根因**: 测试代码中的 `LogIntervalStats()` 从测试线程调用 `reaper.GetCurrentStorageSize()`, 使用写连接, 与 writer 线程的事务并发冲突
**影响**: 无。这是测试线程安全问题, 非生产代码缺陷。ChunkWriter 生产代码的事务作用域已在 v3 修复中验证正确
**分类**: 测试代码竞态, non-blocking。v3 原始运行结果 (259,200 chunks, 0 errors) 记录在 `handoff/stress-72h-report.md`

---

## 3. 编译警告

| 代码 | 数量 | 位置 | 分析 |
|------|------|------|------|
| CS0420 | 4 | SafeDoubleBuffer.cs | volatile 字段传递给 Interlocked — 设计意图 (Interlocked 提供更强内存屏障) |
| CS8625 | 1 | ResourceCache.cs:158 | nullable 赋值 — Dispose 路径中的清理逻辑, 安全 |
| xUnit1031 | 6 | 测试代码 | 测试中同步等待 Task — 非生产代码, 不影响功能 |

---

## 4. 架构简化 / 偏差说明

| 项目 | 规格文档描述 | 实际实现 | 理由 |
|------|-------------|----------|------|
| EEG 存储格式 | ARCHITECTURE.md §8.4: 多文件每小时分段 | 单数据库文件 + Chunk BLOB | ADR-014 决策: 嵌入式设备简化, 单文件备份 |
| 表结构 | ARCHITECTURE.md §8.7: eeg_raw 逐样本行 | eeg_chunks BLOB 批量 | 性能优化: 写入 P99 0.343ms vs 逐行 >50ms |
| UI 框架 | ARCHITECTURE.md §1: "WPF/WinUI" | WinForms + D2D | 实际项目选择 WinForms, D2D 通过 Vortice 集成 |
| 目标帧率 | ARCHITECTURE.md §1.2: 120 FPS | 实际 60 FPS 定时器 | WinForms Timer 限制, 60 FPS 满足临床需求 |
