# Handoff: S1-01 Core Interfaces & Time Model

> **Sprint**: S1
> **Task**: S1-01
> **创建日期**: 2026-01-28
> **状态**: 完成

---

## 1. 本 Sprint 定义的接口

| 接口 | 文件路径 | 用途 |
|------|----------|------|
| `ITimeSeriesSource<T>` | `src/Core/Interfaces/ITimeSeriesSource.cs` | 时间序列数据源抽象 |
| `IDataSink<T>` | `src/Core/Interfaces/IDataSink.cs` | 数据接收器抽象 |
| `IFilterChain` | `src/Core/Interfaces/IFilterChain.cs` | 滤波链抽象（空壳） |
| `IRenderTarget` | `src/Core/Interfaces/IRenderTarget.cs` | 渲染目标抽象 |

---

## 2. 每个接口的职责边界

### 2.1 ITimeSeriesSource<TSample>

**成员签名（完整清单）**:
| 成员 | 类型 | 说明 |
|------|------|------|
| `SampleRate` | `int { get; }` | 采样率（Hz） |
| `ChannelCount` | `int { get; }` | 通道数 |
| `SampleReceived` | `event Action<TSample>?` | 样本到达事件 |
| `Start()` | `void` | 启动采集 |
| `Stop()` | `void` | 停止采集 |

**职责（负责）**:
- 提供带时间戳的样本流
- 报告采样率 (`SampleRate`) 和通道数 (`ChannelCount`)
- 提供启动/停止控制 (`Start()` / `Stop()`)
- 触发样本到达事件 (`SampleReceived`)

**职责边界（不负责）**:
- ❌ 不负责 具体协议解析（RS232、USB）→ 由具体 Adapter 实现
- ❌ 不负责 DSP 处理 → 由 `IFilterChain` 负责
- ❌ 不负责 数据存储 → 由 `IDataSink` 负责
- ❌ 不负责 渲染显示 → 由 `IRenderTarget` 负责

---

### 2.2 IDataSink<TSample>

**成员签名（完整清单）**:
| 成员 | 类型 | 说明 |
|------|------|------|
| `Write(in TSample)` | `void` | 写入单个样本 |
| `WriteBatch(ReadOnlySpan<TSample>)` | `void` | 批量写入 |
| `Flush()` | `void` | 刷新缓冲区 |

**职责（负责）**:
- 接收带时间戳的样本 (`Write`)
- 批量接收样本 (`WriteBatch`)
- 刷新缓冲区 (`Flush`)

**职责边界（不负责）**:
- ❌ 不负责 数据采集 → 由 `ITimeSeriesSource` 负责
- ❌ 不负责 DSP 处理 → 由 `IFilterChain` 负责
- ❌ 不负责 持久化存储实现 → 由具体实现决定（S4）
- ❌ 不负责 渲染显示 → 由 `IRenderTarget` 负责

---

### 2.3 IFilterChain

**成员签名（完整清单）**:
| 成员 | 说明 |
|------|------|
| （无成员） | S1 空壳接口 |

**职责（负责）**:
- 定义滤波处理的契约（S2 阶段细化）

**职责边界（不负责）**:
- ❌ 不负责 具体滤波算法实现 → S2-01 实现
- ❌ 不负责 数据采集 → 由 `ITimeSeriesSource` 负责
- ❌ 不负责 数据存储 → 由 `IDataSink` 负责
- ❌ 不负责 渲染显示 → 由 `IRenderTarget` 负责

**注意**: S1 阶段为空壳接口，成员将在 S2-01 定义。

---

### 2.4 IRenderTarget

**成员签名（完整清单）**:
| 成员 | 类型 | 说明 |
|------|------|------|
| `Width` | `int { get; }` | 渲染区域宽度（像素） |
| `Height` | `int { get; }` | 渲染区域高度（像素） |
| `DpiScale` | `float { get; }` | DPI 缩放因子 |
| `IsValid` | `bool { get; }` | 渲染目标是否有效 |
| `BeginDraw()` | `void` | 开始一帧渲染 |
| `EndDraw()` | `void` | 结束一帧渲染 |
| `HandleDeviceLost()` | `void` | 处理设备丢失恢复 |

**职责（负责）**:
- 提供渲染区域尺寸 (`Width`, `Height`)
- 提供 DPI 缩放因子 (`DpiScale`)
- 提供渲染目标有效性检查 (`IsValid`)
- 管理渲染帧生命周期 (`BeginDraw`, `EndDraw`)
- 处理设备丢失恢复 (`HandleDeviceLost`)

**职责边界（不负责）**:
- ❌ 不负责 具体渲染实现 → 由 Vortice 实现（S1-03）
- ❌ 不负责 数据采集 → 由 `ITimeSeriesSource` 负责
- ❌ 不负责 DSP 处理 → 由 `IFilterChain` 负责
- ❌ 不负责 数据存储 → 由 `IDataSink` 负责

**铁律约束（铁律6）**:
- 渲染线程只做 GPU 绘制调用
- 不做 O(N) 计算
- 不分配大对象
- 不访问 SQLite

---

## 3. 数据模型定义

| 模型 | 文件路径 | 用途 |
|------|----------|------|
| `GlobalTime` | `src/Core/Models/GlobalTime.cs` | 统一时间戳 |
| `EegSample` | `src/Core/Models/EegSample.cs` | EEG 样本 |
| `NirsSample` | `src/Core/Models/NirsSample.cs` | NIRS 样本 |
| `DataQuality` | `src/Core/Models/DataQuality.cs` | 质量信息 |

---

## 4. 枚举定义

| 枚举 | 文件路径 | 用途 |
|------|----------|------|
| `ClockDomain` | `src/Core/Enums/ClockDomain.cs` | 时钟域标识 |
| `ChannelType` | `src/Core/Enums/ChannelType.cs` | 通道类型 |
| `QualityFlag` | `src/Core/Enums/QualityFlag.cs` | 质量标志 |

### 4.1 QualityFlag 成员

| 成员 | 值 | 说明 | 来源 |
|------|-----|------|------|
| `Normal` | 0 | 数据正常 | - |
| `Missing` | 1 << 0 | 数据缺失 | 铁律5 |
| `Saturated` | 1 << 1 | 信号饱和 | 铁律5 |
| `LeadOff` | 1 << 2 | 电极脱落 | 铁律5 |
| `Interpolated` | 1 << 3 | 已插值填充 | 铁律2 |
| `Undocumented` | 1 << 4 | 字段无文档证据 | 证据导向原则 |

> **Undocumented 标志说明**: 当数据字段在原始证据中无明确定义时，
> 必须设为 NaN 并标记此标志。下游处理应忽略或等待新证据。

---

## 5. 时间戳规则

| 规则 | 规格 | 依据 |
|------|------|------|
| 数据类型 | `int64` | CONSENSUS_BASELINE §5.1 |
| 单位 | 微秒 (μs) | CONSENSUS_BASELINE §5.1 |
| 特性 | 单调递增 | CONSENSUS_BASELINE §5.1 |
| 纪元 | 监护开始时刻 = 0 | CONSENSUS_BASELINE §5.1 |
| 时钟来源 | Host 打点 | ADR-012 |
| 语义 | **样本中心时间** | CONSENSUS_BASELINE §5.3 |
| 适用对象 | EEG / NIRS / Video | ADR-012 |

### 时间戳语义详解

```
⚠️ 关键约定：所有时间戳表示"样本或窗口的中心时间"
```

| 数据类型 | 时间戳含义 |
|----------|-----------|
| EEG 样本 | 该样本的采集中心时刻 |
| NIRS 样本 | 该样本的采集中心时刻 |
| aEEG 输出 | 该1秒窗口的中心时间点 |
| LOD Min/Max | 该降采样区间的中心时间 |
| Video 帧 | 该帧的曝光中心时刻 |

---

## 6. 证据引用（审计追溯）

| 定义 | 依据文档 | 章节 |
|------|----------|------|
| 时间模型 | CONSENSUS_BASELINE.md | §5.1, §5.3 |
| ClockDomain 枚举 | CONSENSUS_BASELINE.md | §5.2 |
| EEG 参数 | CONSENSUS_BASELINE.md | §6.1, §6.2 |
| NIRS 参数 | CONSENSUS_BASELINE.md | §6.5 |
| 质量标志 | 00_CONSTITUTION.md | 铁律5 |
| 渲染约束 | 00_CONSTITUTION.md | 铁律6 |
| Raw 数据规则 | 00_CONSTITUTION.md | 铁律1, 铁律12 |
| 接口架构 | ARCHITECTURE.md | §2, §3, §4, §5 |
| 时钟域策略 | CONSENSUS_BASELINE.md | ADR-010, ADR-012 |
| NIRS 约束 | CONSENSUS_BASELINE.md | ADR-013 |

---

## 7. 未实现/延后项

| 项目 | 延后原因 | 计划 Sprint |
|------|----------|------------|
| `IFilterChain` 具体方法 | S1 仅定义接口存在性 | S2-01 |
| 存储实现 | 按 CHARTER §3 延后 | S4 |
| 渲染实现 | 需要 Vortice 底座 | S1-03 |

---

**文档结束**
