# 📋 TASK-S1-06: 系统集成

> **Sprint**: 1
> **负责方**: Claude Code
> **优先级**: 🟢 P0
> **预估工时**: 2h
> **状态**: ⏳ 待开始

---

## 1. 目标

完成 Sprint 1 所有模块的系统集成，实现最小可运行的 EEG 监护系统闭环。

**本任务只允许"接线"和"装配"，严禁新增功能。**

---

## 2. 输入（必读文件）

| 文件 | 重点章节 |
|------|----------|
| `spec/ARCHITECTURE.md` | §2（初始化顺序） |
| `spec/CONSENSUS_BASELINE.md` | §5.1（时间戳规则）、§6.2（默认量程） |
| `handoff/interfaces-api.md` | ITimeSeriesSource 接口 |
| `handoff/double-buffer-api.md` | EegRingBuffer API |
| `handoff/renderer-device-api.md` | D2DRenderTarget API |
| `handoff/renderer-layer-api.md` | LayeredRenderer API |
| `handoff/eeg-waveform-renderer-api.md` | EegWaveformRenderer API |

---

## 3. 输出

### 3.1 代码文件

```
src/Host/
├── Neo.Host.csproj        # Host 项目配置
├── Program.cs             # 应用程序入口点
└── MainForm.cs            # 主窗口 + 模块接线
```

### 3.2 交接文档

```
handoff/system-integration.md
```

---

## 4. 集成规格

### 4.1 初始化顺序

```
1. Clock 初始化
   └─ Stopwatch.Start()
   └─ 记录 sessionStartUs

2. Buffer 初始化
   └─ EegRingBuffer.CreateForSeconds(10)

3. DataSource 初始化
   └─ 从 src/Mock/ 引用 MockEegSource
   └─ 注入 Host 时间戳提供者
   └─ 订阅 SampleReceived 事件

4. Renderer 初始化 (OnFormLoad)
   └─ D2DRenderTarget.Initialize(hwnd, size)
   └─ LayeredRenderer.CreateDefault()

5. 启动
   └─ eegSource.Start()
   └─ renderTimer.Start()
```

### 4.2 数据流

```
MockEegSource (160Hz)
    → EegRingBuffer (10秒滑动窗口)
    → RenderContext
    → LayeredRenderer
    → 屏幕显示
```

### 4.3 数据源依赖

本任务使用 `src/Mock/MockEegSource.cs` 作为数据源：
- 该模块由 TASK-S1-05 定义
- 遵循 TASK-S1-05 波形规格（AlphaFrequency=10Hz, BaseAmplitude=50μV）
- 切换到真实硬件时，替换为 Rs232EegSource 即可

---

## 5. 验收标准

### 5.1 功能验收

- [ ] 程序启动，窗口显示
- [ ] 4 通道 EEG 波形可见
- [ ] 波形随时间滚动（10秒/屏）
- [ ] 时间戳使用 int64 μs
- [ ] 时间戳单调递增

### 5.2 编译验收

- [ ] `dotnet build src/Host/Neo.Host.csproj` 通过
- [ ] `dotnet run --project src/Host/Neo.Host.csproj` 正常启动

---

## 6. 约束（不可违反）

```
❌ 新增 DSP / 滤波 / RMS / aEEG
❌ 新增 UI 交互（缩放、配置、按钮）
❌ 新增 NIRS / 视频 / 数据存储
❌ 新增配置系统 / 设置文件
❌ 修改已冻结接口
❌ 顺手优化 / 重构 / 改名
✅ 只做模块接线和装配
✅ 使用 src/Mock/MockEegSource 作为测试数据源
```

---

## 7. 依赖与被依赖

### 依赖
- S1-01: 核心接口（ITimeSeriesSource, EegSample）
- S1-02b: SafeDoubleBuffer / EegRingBuffer
- S1-03: D2DRenderTarget
- S1-04: LayeredRenderer
- S1-05: EegWaveformRenderer
- TASK-S1-05: MockEegSource（src/Mock/）

### 被依赖
- S2-xx: DSP 链路集成

---

## 8. 启动指令（给 Claude Code）

```
请先阅读以下文件：
1. spec/ARCHITECTURE.md §2
2. spec/CONSENSUS_BASELINE.md §5.1, §6.2
3. handoff/interfaces-api.md
4. handoff/double-buffer-api.md
5. handoff/renderer-device-api.md
6. handoff/renderer-layer-api.md

然后执行任务 TASK-S1-06：
- 创建 Host 项目（src/Host/）
- 接线所有 S1 模块
- 使用 src/Mock/MockEegSource 作为数据源
- 验证系统闭环运行
- 完成后生成 handoff/system-integration.md
```

---

**任务卡结束**
