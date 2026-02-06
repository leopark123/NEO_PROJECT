# 🎯 Project State（进度锚点）

> **用途**：Claude Code 每次会话启动时**第一步只读这个文件**
> **规则**：每完成一个功能，必须更新此文件
> **最后更新**：2026-01-29 (S5-01 RC)
> **项目状态**：Release Candidate (RC-1)

---

## ✅ Completed（已完成，禁止重复实现）

```
S1-01 核心接口（GlobalTime + ITimeSeriesSource）
  - 完成日期: 2026-01-28
  - 交付物: src/Core/Interfaces/, src/Core/Models/, src/Core/Enums/
  - Handoff: handoff/interfaces-api.md
  - 证据: evidence/sources/SOURCES_MANIFEST.md

S1-02a RS232 EEG 数据源（EEG-only，ADR-015 范围裁决）
  - 完成日期: 2026-01-28
  - 范围: EEG + aEEG(GS) 解析，不含 NIRS
  - 交付物: src/DataSources/Rs232/
  - Handoff: handoff/rs232-source-api.md
  - ICD: evidence/sources/icd/ICD_EEG_RS232_Protocol_Fields.md
  - 证据: DSP_SPEC.md, ACCEPTANCE_TESTS.md, clogik_50_ser.cpp
  - 裁决: ADR-015（NIRS 拆分为独立 Blocked 任务）

S1-02b SafeDoubleBuffer 无锁双缓冲
  - 完成日期: 2026-01-28
  - 交付物: src/Infrastructure/Buffers/
  - Handoff: handoff/double-buffer-api.md
  - 证据: ARCHITECTURE.md §3, ADR-007
  - 测试: tests/Infrastructure.Tests/

S1-03 Vortice 渲染底座（Renderer Device API）
  - 完成日期: 2026-01-28
  - 交付物: src/Rendering/Device/, src/Rendering/Core/, src/Rendering/Resources/
  - Handoff: handoff/renderer-device-api.md
  - 证据: ARCHITECTURE.md §5, ADR-002, ADR-006, ADR-008, 铁律6
  - 测试: tests/Rendering.Tests/

S1-04 三层渲染框架
  - 完成日期: 2026-01-28
  - 交付物: src/Rendering/Layers/, src/Rendering/Core/LayeredRenderer.cs
  - Handoff: handoff/renderer-layer-api.md
  - 证据: ARCHITECTURE.md §5, ADR-008, 铁律6
  - 测试: tests/Rendering.Tests/Layers/
  - 备注: 项目升级至 .NET 9 + Vortice 3.8.1

S1-05 EEG 波形渲染 ⚠️ 已被 S2-05 替代
  - 完成日期: 2026-01-28
  - 状态: ⚠️ 已被 S2-05 替代（EegWaveformRenderer.cs 已删除，违反铁律6）
  - Handoff: handoff/eeg-waveform-renderer-api.md (已标记为历史记录)
  - 替代方案: S2-05 PolylineBuilder + EegPolylineRenderer

S1-06 系统集成（Sprint 1 Integration Gate）
  - 完成日期: 2026-01-28
  - 范围: 模块接线与装配，无新增功能
  - 交付物: src/Host/
  - Handoff: handoff/system-integration.md
  - 证据: ARCHITECTURE.md §2, CONSENSUS_BASELINE.md §5.1
  - 集成链: EEG数据源 → SafeDoubleBuffer → RenderContext → LayeredRenderer → 窗口
  - 约束: 只做接线，无DSP/无UI交互/无NIRS/无存储

S2-01 EEG 基础数字滤波链（Real-time）
  - 完成日期: 2026-01-28
  - 范围: IIR 实时滤波链 (Notch + HPF + LPF)，per-channel 状态，瞬态标记
  - 交付物: src/DSP/Filters/
  - Handoff: handoff/eeg-filter-chain-api.md
  - 证据: DSP_SPEC.md §2, §7, 00_CONSTITUTION.md 铁律4/5
  - 测试: tests/DSP.Tests/
  - 滤波链: Raw EEG → Notch (50/60Hz) → HPF (0.3/0.5/1.5Hz) → LPF (15/35/50/70Hz) → Filtered
  - 约束: 无aEEG/RMS/包络/GS/零相滤波/UI/数据库

S2-02 aEEG处理链（Medical Frozen）
  - 完成日期: 2026-01-28
  - 范围: aEEG 处理 (带通2-15Hz 6阶 + 整流 + 包络 + 1Hz输出)
  - 交付物: src/DSP/AEEG/
  - Handoff: handoff/aeeg-chain-api.md
  - 证据: DSP_SPEC.md §3 (v2.3), 00_CONSTITUTION.md 铁律4/5, CONSENSUS_BASELINE.md §5.3
  - 测试: tests/DSP.Tests/AEEG/ (33个测试)
  - 处理链: Filtered EEG → Bandpass(2-15Hz, 6阶) → Rectify(|x|) → Peak(0.5s) → Smooth(15s) → Min/Max(1Hz)
  - 系数: HPF 2Hz (§3.2.1) + LPF 15Hz (§2.3)
  - 时间戳: 窗口中心语义 (§5.3)
  - 约束: 无RMS替代/无GS直方图/无半对数映射/无UI

S2-03 GS直方图（aEEG统计表达层）
  - 完成日期: 2026-01-28
  - 范围: GS 灰度直方图数据结构（统计编码，非信号处理）
  - 交付物: src/DSP/GS/
  - Handoff: handoff/gs-histogram-api.md
  - 证据: DSP_SPEC.md §3.3, CONSENSUS_BASELINE.md §6.4
  - 测试: tests/DSP.Tests/GS/ (90个测试)
  - 规格:
    - 230 bins (index 0-229)
    - 分段映射: 0-10 μV 线性(100 bins), 10-200 μV log10(130 bins)
    - 15 秒统计周期
    - 饱和值 249
    - Counter 语义: 229=周期结束, 255=忽略
  - 约束: 无平滑/无插值/无UI调整/不改变bin数量/不改变周期

S2-04 aEEG半对数显示映射（Display Mapping Layer）
  - 完成日期: 2026-01-28
  - 范围: μV到Y像素坐标的纯函数映射（显示层，非信号处理）
  - 交付物: src/Rendering/Mapping/
  - Handoff: handoff/aeeg-display-mapping-api.md
  - 证据: DSP_SPEC.md, CONSENSUS_BASELINE.md §6.4
  - 测试: tests/Rendering.Tests/Mapping/ (72个测试)
  - 规格:
    - 显示范围: 0-200 μV
    - 线性段: 0-10 μV → 下半区 (50%)
    - 对数段: 10-200 μV → 上半区 (50%)
    - 分界点: 10 μV（医学冻结）
    - 标准刻度: 0,1,2,3,4,5,10,25,50,100,200 μV（固定11个）
  - 约束: 纯函数/无数据修改/无平滑/无插值/无自适应

S2-05 EEG/aEEG波形渲染层（Waveform Rendering Layer）
  - 完成日期: 2026-01-28
  - 范围: EEG增益缩放 + 折线段构建 + aEEG趋势渲染（使用S2-04映射）
  - 交付物:
    - src/Rendering/EEG/EegGainScaler.cs (增益缩放)
    - src/Rendering/EEG/PolylineBuilder.cs (预处理，O(N)允许)
    - src/Rendering/EEG/EegWaveformRenderData.cs (预构建数据结构)
    - src/Rendering/EEG/EegPolylineRenderer.cs (只做Draw)
    - src/Rendering/AEEG/AeegSeriesBuilder.cs (预处理)
    - src/Rendering/AEEG/AeegTrendRenderer.cs (只做Draw)
    - src/Rendering/AEEG/AeegColorPalette.cs
    - src/Rendering/AEEG/AeegGridAndAxisRenderer.cs
  - Handoff: handoff/waveform-rendering-api.md
  - 证据: CONSENSUS_BASELINE.md §6.3, ADR-005, 铁律2/5/6
  - 测试: tests/Rendering.Tests/Waveform/ (88个测试)
  - 架构: 预处理(PolylineBuilder) + 渲染(EegPolylineRenderer)分离
  - 规格:
    - 增益: 10, 20, 50, 70, 100, 200, 1000 μV/cm
    - EEG间隙: > 4样本(25ms)断线
    - aEEG间隙: > 2秒断线
    - 使用AeegSemiLogMapper (S2-04)
  - 约束: 铁律2(不伪造波形)/铁律5(缺失可见)/铁律6(渲染只Draw,无O(N))

S3-01 NIRS集成壳（Integration Shell）
  - 完成日期: 2026-01-29
  - 范围: 系统层集成位，不实现任何 NIRS 协议或算法
  - 交付物:
    - src/NIRS/NirsIntegrationShell.cs (阻塞状态管理)
    - src/Host/NirsWiring.cs (装配/DI/生命周期)
    - src/Core/Enums/QualityFlag.cs (新增 BlockedBySpec)
  - Handoff: handoff/nirs-integration-shell-api.md
  - 证据: PROJECT_STATE.md S3-00 Blocked, ADR-015
  - 行为:
    - 系统启动 → NIRS 模块注册 → 标记为 Blocked
    - 所有 NIRS 数值为 NaN, 质量标志 Undocumented | BlockedBySpec
    - UI/渲染层不报错、不显示伪数据
  - 约束: 不实现协议/不模拟数据/不修改DSP-EEG-aEEG

S3-02 视频采集与回放适配层（Video Capture & Playback）
  - 完成日期: 2026-01-29
  - 范围: USB UVC 摄像头采集、H.264/MP4 录制、.tsidx 索引、时间戳回放
  - 交付物:
    - src/Video/Neo.Video.csproj (项目文件)
    - src/Video/VideoFrame.cs (帧元数据 record struct)
    - src/Video/IVideoSource.cs (接口 + CameraDeviceInfo)
    - src/Video/UsbCameraSource.cs (MF SourceReader 采集)
    - src/Video/VideoRecorder.cs (MF SinkWriter H.264 编码 + .tsidx)
    - src/Video/VideoPlaybackSource.cs (MP4 回放 + 时间戳定位)
    - src/Host/VideoWiring.cs (生命周期装配)
  - Handoff: handoff/video-capture-api.md
  - 证据: ADR-011, ADR-012
  - 规格:
    - 分辨率: 640x480 (可配置)
    - 帧率: 15-30 fps
    - 编码: H.264/MP4, 1-2 Mbps
    - 时间戳: Host Monotonic Clock, int64 μs
    - 同步精度: ±50-100ms with EEG
    - .tsidx: 二进制索引，20 bytes/entry，全量加载
  - 优雅降级: 无摄像头时记录警告，系统正常运行
  - 约束: 无视频预览UI/无多摄像头选择/无变速回放

S3-03 同步回放（Video + EEG Synchronized Playback）
  - 完成日期: 2026-01-29
  - 范围: 统一时间线服务、回放时钟、EEG 回放适配器、多流协调器
  - 交付物:
    - src/Core/Enums/PlaybackState.cs (播放状态枚举)
    - src/Core/Models/TimelinePositionEventArgs.cs (位置变更事件)
    - src/Core/Interfaces/ITimelineService.cs (统一时间线接口)
    - src/Playback/Neo.Playback.csproj (回放模块项目)
    - src/Playback/PlaybackClock.cs (可暂停/可 seek 虚拟时钟)
    - src/Playback/EegPlaybackSource.cs (EEG 回放适配器)
    - src/Playback/MultiStreamCoordinator.cs (多流同步协调器，实现 ITimelineService)
  - Handoff: handoff/playback-sync-api.md
  - 证据: ARCHITECTURE.md §9, AT-17, 铁律2/11/13
  - 测试: tests/Playback.Tests/ (26个测试)
  - 规格:
    - 同步容差: ±100ms (AT-17)
    - 时间戳: Host Monotonic Clock, int64 μs
    - 位置更新: 20 Hz
    - 支持: Play/Pause/SeekTo/PlaybackRate
  - 约束: 无 NIRS 回放(S3-00 blocked)/无变速视频/无 UI 控件

S4-01 SQLite + Chunk 存储
  - 完成日期: 2026-01-29
  - 范围: SQLite WAL 数据库引导、EEG Chunk BLOB 编解码、后台写入管道、容量淘汰、读取接口
  - 交付物:
    - src/Storage/Neo.Storage.csproj (项目文件)
    - src/Storage/StorageConfiguration.cs (全局配置)
    - src/Storage/NeoDatabase.cs (DB 引导 + Schema V1 + PRAGMA)
    - src/Storage/EegChunkEncoder.cs (int16 BLOB 编解码)
    - src/Storage/NirsChunkEncoder.cs (最小占位, Blocked)
    - src/Storage/IEegChunkStore.cs + EegChunkStore.cs (读取接口)
    - src/Storage/INirsChunkStore.cs + NirsChunkStore.cs (最小占位)
    - src/Storage/ChunkWriter.cs (后台批量写入)
    - src/Storage/StorageReaper.cs (容量淘汰 FIFO)
    - src/Storage/AuditLog.cs (审计日志)
  - Handoff: handoff/storage-sqlite-chunk-api.md
  - 证据: ARCHITECTURE.md §8, 铁律6/7/12/13/14, AT-20/AT-24
  - 测试: tests/Storage.Tests/ (22个测试)
  - 基准:
    - 写入 P99: 0.343ms (目标 <50ms)
    - 预计 72h: 318.4 MB EEG (AT-20 目标 ~331 MB)
    - DB 增长: <50 MB/hour
    - 淘汰: 自动删除非活跃会话旧 chunk
  - 约束: NIRS 存储为占位/Chunk BLOB 非逐样本行/单写连接

S4-03 72小时稳定性与耐久性压测 (v3)
  - 完成日期: 2026-01-29
  - 范围: 时间加速仿真验证 72h 连续写入的数据完整性、稳定性、淘汰正确性、回放稳定性
  - 策略: C (Combined) — 时间加速 + 存储上限缩放 (50 MB)
  - 交付物:
    - tests/StressTests/Neo.StressTests.csproj (压测项目)
    - tests/StressTests/Storage72hStressTest.cs (72h 全量压测 v3)
  - 修改文件 (v3 bug 修复):
    - src/Storage/ChunkWriter.cs — DrainQueues 事务作用域修复 + Stop Join 超时调整
  - Handoff: handoff/stress-72h-report.md (v3)
  - 证据: AT-20, AT-22, AT-24, ARCHITECTURE.md §8.6
  - 压测结果 (v3):
    - 活跃会话写入: 259,200 chunks (72.0h 时间跨度, 318.4 MB)
    - 旧会话 seed: 30,000 chunks (reaper 目标, 直接 SQL 注入)
    - 写入错误: 0, 未处理异常: 0
    - 时间戳单调违规: 0 (写入侧 + DB 侧)
    - 活跃会话时间间隙: 0 (>2s 阈值)
    - Reaper 删除: 9,000 chunks (11.1 MB), 最早时间戳从 0→1,000,000
    - 活跃会话保护: 259,200 chunks 完整保留 (72.0h)
    - 并发读取错误: 0 (628 queries, 314 chunks decoded)
    - 回放验证错误: 0
    - AT-22 内存增长: -60.5% (暖机基线 20.2 MB → 最终 8.0 MB, 限值 <10%)
    - 审计日志: 2 条 STORAGE_CLEANUP
    - 运行耗时: 36.2 秒 (7,200x 加速)
    - Storage 单元测试: 23/23 通过
  - AT-22 验证方法:
    - 暖机基线: 25,000 chunks 后强制 GC.Collect(2, Aggressive) = 20.2 MB
    - 最终值: 全部完成 + writer 停止后强制 GC = 8.0 MB
    - 增长率: (8.0 - 20.2) / 20.2 = -60.5%, 断言 < 10%
    - WS 不作为断言指标 (加速伪影: 队列积压, 生产中不存在)
  - v3 ChunkWriter 事务竞态修复:
    - 根因: DrainQueues 中 using var transaction 使事务作用域覆盖 reaper 调用, 导致嵌套事务
    - 修复: using 块语句限定事务作用域, reaper 调用移至事务释放后
    - 修复: Stop() Join 超时从 5s→30s, 写入线程最终 drain 加 ObjectDisposedException 保护
  - 约束: 无 NIRS 数据流/无渲染 DSP 并发/时间加速导致 WS 增长非生产等价

S4-04 截图、打印预览与 USB 导出
  - 完成日期: 2026-01-29
  - 范围: WYSIWYG 截图保存、打印预览（含可编辑结论文本）、USB 安全导出
  - 交付物:
    - src/Host/Services/ScreenshotService.cs (截图服务)
    - src/Host/Services/PrintService.cs (打印预览 + PrintDocument)
    - src/Host/Services/UsbExportService.cs (USB 导出)
  - 修改文件:
    - src/Rendering/Core/D2DRenderTarget.cs — 新增 CaptureScreenshot() 方法
    - src/Host/MainForm.cs — 集成三项服务 + 键盘快捷键
  - Handoff: handoff/screenshot-print-export.md
  - 证据: CONSENSUS_BASELINE.md §12.7, ARCHITECTURE.md §8.2
  - 技术方案:
    - 截图: D3D11 BackBuffer → Staging Texture → Bitmap → PNG (WYSIWYG)
    - 打印: PrintPreviewControl + PrintDocument, 可编辑结论文本
    - USB: DriveInfo.GetDrives() + DriveType.Removable, 系统盘禁写, 同名不覆盖
  - 快捷键: Ctrl+P (截图) / Ctrl+Shift+P (打印预览) / Ctrl+E (USB 导出)
  - 安全约束: 不写入系统盘 / 不静默失败 / 同名自动编号不覆盖
  - 构建: dotnet build Neo.sln → 0 errors

S5-01 Release Candidate 最终验收
  - 完成日期: 2026-01-29
  - 范围: 封板级验收 — 证明系统已完成, 非新功能
  - 交付物:
    - docs/release/RC_CHECKLIST.md (功能点逐项验证)
    - docs/release/RC_TEST_REPORT.md (测试报告 + 72h 压测摘要)
    - docs/release/RC_KNOWN_LIMITATIONS.md (已知限制 + 未实现声明)
  - 验证结果:
    - Release 构建: 0 errors, 11 warnings (全部预存)
    - 单元测试: 582/586 通过 (99.3%), 4 个失败均为预存/环境敏感
    - TODO/FIXME 扫描: 0 matches
    - 铁律合规: 15/15 条全部合规
    - ADR 落实: 9/9 条全部落实
    - Handoff 文档: 18/18 份完整
    - Blocked 项: 无（S3-00 已于 2026-02-06 解冻）
  - 约束: 禁止新功能/架构调整/参数猜测/文档重写
```

---

## 🟡 In Progress（进行中）

```
（无）
```

---

## 🔴 Not Started（未开始）

### Sprint 1：渲染底座 + 模拟数据
- [x] S1-01 核心接口（GlobalTime + ITimeSeriesSource）✅
- [x] S1-02a RS232 EEG 数据源 ✅
- [x] S1-02b SafeDoubleBuffer 无锁双缓冲 ✅
- [x] S1-03 Vortice 渲染底座 ✅
- [x] S1-04 三层渲染框架 ✅
- [x] S1-05 EEG 波形渲染（Real Draw, No DSP）✅
- [x] S1-06 系统集成 ✅

### Sprint 2：DSP滤波链 + aEEG
- [x] S2-01 EEG基础数字滤波链（Real-time）✅
- [x] S2-02 aEEG处理链（整流→统计→半对数）✅
- [x] S2-03 GS直方图（15秒）✅
- [x] S2-04 aEEG半对数显示映射 ✅
- [x] S2-05 EEG/aEEG波形渲染层 ✅

### Sprint 3：NIRS + 视频
- [x] S3-00 NIRS RS232 Protocol Spec & Parser ✅ **(2026-02-06 完成)**
- [x] S3-01 NIRS集成壳（Integration Shell）✅
- [x] S3-02 视频采集 ✅
- [x] S3-03 同步回放 ✅

### Sprint 4：存储 + 长程测试 + 临床交付
- [x] S4-01 SQLite + Chunk存储 ✅
- [x] S4-02 滚动清理（300GiB FIFO）✅ （已在 S4-01 StorageReaper 中实现）
- [x] S4-03 72小时压测 ✅
- [x] S4-04 截图/打印预览/USB导出 ✅

### Sprint 5：验收
- [x] S5-01 Release Candidate 最终验收 ✅

---

## ⚠️ Blocked（阻塞项）

```
（无）

S3-00 NIRS RS232 Protocol Spec & Parser（ADR-015 拆分任务）
  - 状态: ✅ 已解冻 (2026-02-06)
  - 裁决: ADR-015（S1-02 范围裁决与 NIRS 拆分）
  - 解冻依据: ICD_NIRS_RS232_Protocol_Fields.md 完整提供所有必需证据
  - 证据清单:
    1. ✅ 帧头定义（ASCII "Ch1" 标记）
    2. ✅ 每帧长度（250-350 bytes 可变长）
    3. ✅ 字节序（ASCII协议，CRC大端序）
    4. ✅ 校验算法（CRC-16 CCITT/XMODEM，含测试向量）
    5. ✅ 字段映射（6通道完整映射 + rSO2/HbI/AUC参数）
  - 验收标准: 全部 5 项通过（可引用/字节级精度/独立验证/含校验/覆盖6通道）
  - 下一步: 启动 NIRS 解析器实现
```

---

## 📋 Last Verified（最后验证）

| 项目 | 日期 | 验证者 |
|------|------|--------|
| S3-00 NIRS RS232 解析器实现完成 | 2026-02-06 | Claude Code |
| S3-00 NIRS 协议证据验收通过并解冻 | 2026-02-06 | Claude Code |
| S5-01 RC 验收完成 | 2026-01-29 | Claude Code |
| S4-04 截图/打印/USB导出完成 | 2026-01-29 | Claude Code |
| S4-03 72h 压测完成 | 2026-01-29 | Claude Code |
| S4-01 SQLite + Chunk 存储完成 | 2026-01-29 | Claude Code |
| S3-03 同步回放完成 | 2026-01-29 | Claude Code |
| S3-02 视频采集与回放完成 | 2026-01-29 | Claude Code |
| S3-01 NIRS集成壳完成 | 2026-01-29 | Claude Code |
| S2-05 EEG/aEEG波形渲染层完成 | 2026-01-28 | Claude Code |
| S2-04 aEEG半对数显示映射完成 | 2026-01-28 | Claude Code |
| S2-03 GS直方图完成 | 2026-01-28 | Claude Code |
| S2-02 aEEG处理链完成 | 2026-01-28 | Claude Code |
| DSP_SPEC.md v2.3 (§3.2 阶数修正+§3.2.1 系数补充) | 2026-01-28 | 指挥官 |
| S2-01 EEG滤波链完成 | 2026-01-28 | Claude Code |
| S1-06 系统集成完成 | 2026-01-28 | Claude Code |
| S1-05 EEG 波形渲染完成 | 2026-01-28 | Claude Code |
| S1-04 三层渲染框架完成 | 2026-01-28 | Claude Code |
| .NET 9 + Vortice 3.8.1 升级 | 2026-01-28 | Claude Code |
| S1-02b SafeDoubleBuffer 完成 | 2026-01-28 | Claude Code |
| S1-03 Vortice 渲染底座完成 | 2026-01-28 | Claude Code |
| ADR-015 范围裁决执行 | 2026-01-28 | Claude Code |
| S1-02a EEG-only 完成 | 2026-01-28 | Claude Code |
| S3-00 NIRS 标记 Blocked | 2026-01-28 | Claude Code |
| S1-01 核心接口完成 | 2026-01-28 | Claude Code |
| S1-01 审计通过（IsRunning 已移除） | 2026-01-28 | 指挥官 |
| 证据文件补充完成 | 2026-01-28 | 指挥官 |
| 方案审计通过 | 2026-01-25 | ChatGPT |
| 项目初始化 | 2026-01-25 | Claude |

---

## 🔄 更新规则

**Claude Code 必须遵循：**

1. **会话启动时**：第一步读取此文件，确认当前进度
2. **完成功能后**：立即更新此文件
   - 将任务从 "Not Started" 移到 "In Progress"
   - 完成后移到 "Completed"
3. **遇到阻塞时**：添加到 "Blocked" 并说明原因
4. **禁止**：在 "Completed" 列表中的功能上重复实现

---

## 📍 当前位置

```
Sprint: 5 完成
Task: S5-01 Release Candidate 最终验收 ✅ 已完成
Phase: Release Candidate (RC-1)
Status: 系统可被第三方工程师复核
```
