# Handoff: S1-06 System Integration

> **Sprint**: S1
> **Task**: S1-06
> **创建日期**: 2026-01-28
> **状态**: 完成

---

## 1. 概述

本任务完成了 Sprint 1 所有模块的系统集成，实现了最小可运行的 EEG 监护系统闭环。

集成链路:
```
EEG 数据源 → EegRingBuffer → RenderContext → LayeredRenderer → 窗口显示
```

---

## 2. 文件清单

| 文件 | 用途 |
|------|------|
| `src/Host/Neo.Host.csproj` | Host 项目配置 |
| `src/Host/Program.cs` | 应用程序入口点 |
| `src/Host/MainForm.cs` | 主窗口 + 模块接线 |
| `src/Mock/Neo.Mock.csproj` | Mock 项目配置 |
| `src/Mock/MockEegSource.cs` | EEG 模拟数据源 (TASK-S1-05) |

---

## 3. 集成结构

```
┌─────────────────────────────────────────────────────────────────────────────┐
│                              MainForm (主窗口)                               │
├─────────────────────────────────────────────────────────────────────────────┤
│                                                                             │
│   ┌──────────────┐     ┌───────────────────┐     ┌──────────────────────┐  │
│   │ MockEegSource │────►│   EegRingBuffer   │────►│  RenderContext       │  │
│   │   (160 Hz)   │     │  (10秒滑动窗口)    │     │  (通道数据)          │  │
│   └──────────────┘     └───────────────────┘     └───────────┬──────────┘  │
│                                                               │             │
│   ┌───────────────────────────────────────────────────────────▼───────────┐│
│   │                        LayeredRenderer                                 ││
│   │  ┌─────────────┐  ┌─────────────┐  ┌─────────────┐                    ││
│   │  │  GridLayer  │  │ContentLayer │  │OverlayLayer │                    ││
│   │  │  (Order=0)  │  │  (Order=1)  │  │  (Order=2)  │                    ││
│   │  │             │  │ EegWaveform │  │             │                    ││
│   │  │             │  │  Renderer   │  │             │                    ││
│   │  └─────────────┘  └─────────────┘  └─────────────┘                    ││
│   └───────────────────────────────────────────────────────────────────────┘│
│                                    │                                        │
│   ┌────────────────────────────────▼────────────────────────────────────┐  │
│   │                        D2DRenderTarget                               │  │
│   │  ┌─────────────────┐  ┌─────────────────┐  ┌──────────────────┐     │  │
│   │  │ GraphicsDevice  │  │ SwapChainManager│  │ D2DDeviceManager │     │  │
│   │  │   (D3D11)       │  │   (DXGI)        │  │   (D2D)          │     │  │
│   │  └─────────────────┘  └─────────────────┘  └──────────────────┘     │  │
│   └─────────────────────────────────────────────────────────────────────┘  │
│                                                                             │
└─────────────────────────────────────────────────────────────────────────────┘
```

---

## 4. 初始化顺序

```
1. Clock 初始化
   └─ Stopwatch.Start()
   └─ 记录 sessionStartUs

2. Buffer 初始化
   └─ EegRingBuffer.CreateForSeconds(10)  // 10秒 @ 160Hz

3. DataSource 初始化
   └─ MockEegSource 创建
   └─ 订阅 SampleReceived 事件

4. Renderer 初始化 (OnFormLoad)
   └─ D2DRenderTarget.Initialize(hwnd, size)
   └─ LayeredRenderer.CreateDefault()

5. 启动
   └─ eegSource.Start()
   └─ renderTimer.Start()
```

---

## 5. 数据流

```
┌─────────────────────────────────────────────────────────────────────────────┐
│                              数据流（每帧）                                  │
├─────────────────────────────────────────────────────────────────────────────┤
│                                                                             │
│  [模拟线程 @ 160Hz]                                                          │
│       │                                                                     │
│       │ OnTimerCallback()                                                   │
│       │   - 生成 EegSample（4通道正弦波）                                     │
│       │   - 时间戳 = Stopwatch 微秒                                          │
│       ▼                                                                     │
│  SampleReceived 事件                                                         │
│       │                                                                     │
│       │ OnEegSampleReceived()                                               │
│       │   - buffer.Write(sample)                                            │
│       ▼                                                                     │
│  EegRingBuffer (10秒滑动窗口)                                                │
│       │                                                                     │
│       │                                                                     │
│  [渲染线程 @ 60Hz]                                                           │
│       │                                                                     │
│       │ OnRenderTick()                                                      │
│       │   - buffer.GetRange(startUs, endUs, output)                         │
│       │   - BuildChannelRenderData()                                        │
│       │   - 创建 RenderContext                                              │
│       ▼                                                                     │
│  LayeredRenderer.RenderFrame()                                              │
│       │                                                                     │
│       │ GridLayer.Render()      → 网格背景                                   │
│       │ ContentLayer.Render()   → EegWaveformRenderer                       │
│       │ OverlayLayer.Render()   → 时间轴/光标                                │
│       ▼                                                                     │
│  D2DRenderTarget.EndDraw()                                                  │
│       │                                                                     │
│       │ SwapChain.Present()                                                 │
│       ▼                                                                     │
│  屏幕显示                                                                    │
│                                                                             │
└─────────────────────────────────────────────────────────────────────────────┘
```

---

## 6. 模块使用摘要

| 模块 | 来源 | 用途 |
|------|------|------|
| `ITimeSeriesSource<EegSample>` | S1-01 | 数据源接口 |
| `EegSample` | S1-01 | EEG 样本模型 |
| `QualityFlag` | S1-01 | 质量标志 |
| `EegRingBuffer` | S1-02b | EEG 环形缓冲（10秒滑动窗口） |
| `D2DRenderTarget` | S1-03 | 渲染目标 |
| `RenderContext` | S1-03 | 渲染状态 |
| `ResourceCache` | S1-03 | 资源缓存 |
| `LayeredRenderer` | S1-04 | 分层渲染器 |
| `GridLayer` | S1-04 | 网格层 |
| `ContentLayer` | S1-04 | 内容层 |
| `OverlayLayer` | S1-04 | 覆盖层 |
| `EegWaveformRenderer` | S1-05 | EEG 波形渲染 |

---

## 7. 运行说明

### 7.1 构建

```bash
cd F:\NEO_PROJECT
dotnet build src/Host/Neo.Host.csproj
```

### 7.2 运行

```bash
dotnet run --project src/Host/Neo.Host.csproj
```

### 7.3 预期行为

1. 启动 1280x720 窗口，标题 "NEO EEG Monitor - S1-06 Integration"
2. 显示 4 通道 EEG 波形（模拟正弦波）
3. 波形随时间滚动（10秒/屏）
4. 背景网格和时间轴可见

---

## 8. 测试桩说明

### MockEegSource (src/Mock/MockEegSource.cs)

| 属性 | 说明 |
|------|------|
| 定位 | 测试桩（Test Stub），非生产模块 |
| 来源 | TASK-S1-05 (spec/tasks/TASK-S1-05.md) |
| 用途 | 无 RS232 硬件时验证渲染管线 |
| 时间基准 | 注入 Host 时间戳提供者，确保统一 |
| 波形参数 | AlphaFrequency=10Hz, AlphaAmplitude=30μV (TASK-S1-05 §4.1) |
| 噪声 | NoiseStdDev=5μV, 高斯分布 (TASK-S1-05 §4.2) |
| 通道差异 | ChannelFactors=[1.0, 0.9, 1.1, 0.95] (TASK-S1-05 §4.1) |

---

## 9. 已知限制

| 限制 | 原因 | 计划 |
|------|------|------|
| 使用模拟数据源 | 无真实 RS232 硬件 | 切换到 Rs232EegSource 即可 |
| 无 DSP/滤波 | S1 范围外 | S2-01 实现 |
| 无 UI 交互 | S1 范围外 | S2+ |
| 无数据存储 | S1 范围外 | S4 |
| 固定 10秒/屏 | S1 范围外 | S2+ 添加缩放 |

---

## 10. 切换到真实硬件

要切换到真实 RS232 硬件，只需替换数据源：

```csharp
// 替换 MockEegSource 为 Rs232EegSource
var config = new Rs232Config
{
    PortName = "COM3",
    BaudRate = 115200
};
_eegSource = new Rs232EegSource(config);
```

---

## 11. 自检清单

- [x] 是否只做了集成，没有新增功能？ ✅ 仅接线 + 测试桩
- [x] 是否所有模块均来自 S1-01 ~ S1-05？ ✅ 生产模块全部来自既有任务
- [x] MockEegSource 是否明确标注为测试桩？ ✅ 注释已说明定位和切换方式
- [x] 时间基准是否统一？ ✅ MockEegSource 注入 Host 时间戳提供者
- [x] 是否未触碰任何 Blocked 项？ ✅ 未触碰 NIRS
- [x] 是否能完整启动并显示 EEG 波形？ ✅ 已验证
- [x] 是否已更新 PROJECT_STATE.md？ ✅ 已更新

---

## 12. 证据引用

| 实现 | 依据文档 | 章节 |
|------|----------|------|
| 任务定义 | spec/tasks/TASK-S1-06.md | 全文 |
| 初始化顺序 | ARCHITECTURE.md | §2 |
| 时间戳规则 | CONSENSUS_BASELINE.md | §5.1 |
| 环形缓冲设计 | handoff/double-buffer-api.md | §3.3 (EegRingBuffer) |
| 渲染架构 | handoff/renderer-device-api.md | §3 |
| 三层渲染 | handoff/renderer-layer-api.md | §3 |
| EEG 波形 | handoff/eeg-waveform-renderer-api.md | §3 |
| MockEegSource | spec/tasks/TASK-S1-05.md | §4.1, §4.2 |

---

**文档结束**
