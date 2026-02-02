# 📋 TASK-S1-01: 核心接口定义

> **Sprint**: 1  
> **负责方**: Codex  
> **优先级**: 🔴 P0（阻塞后续所有任务）  
> **预估工时**: 4h  
> **状态**: ⏳ 待开始

---

## 1. 目标

定义系统核心接口，作为 Codex 和 Claude 协作的契约边界。这些接口将被所有后续模块依赖。

---

## 2. 输入（必读文件）

| 文件 | 重点章节 |
|------|----------|
| `spec/00_CONSTITUTION.md` | 铁律11（时间轴）、铁律4（double精度） |
| `spec/CONSENSUS_BASELINE.md` | §5（时间轴规范）、§6（参数事实） |
| `spec/ARCHITECTURE.md` | §3（数据交换层）、§4（DSP处理层） |
| `spec/TIME_SYNC.md` | §2（ClockDomain定义） |
| `spec/API_STYLE.md` | 全文（命名规范） |

---

## 3. 输出

### 3.1 代码文件

```
src/Core/
├── Interfaces/
│   ├── ITimeSeriesSource.cs      # 数据源接口
│   ├── IDataSink.cs              # 数据接收接口
│   ├── IFilterChain.cs           # 滤波器链接口
│   └── IRenderTarget.cs          # 渲染目标接口
├── Models/
│   ├── GlobalTime.cs             # 全局时间结构
│   ├── EegSample.cs              # EEG样本结构
│   ├── NirsSample.cs             # NIRS样本结构
│   └── DataQuality.cs            # 数据质量标记
└── Enums/
    ├── ClockDomain.cs            # 时钟域枚举
    ├── ChannelType.cs            # 通道类型枚举
    └── QualityFlag.cs            # 质量标志枚举
```

### 3.2 交接文档

```
handoff/interfaces-api.md
```

---

## 4. 接口规格要求

### 4.1 ITimeSeriesSource（数据源）

```csharp
public interface ITimeSeriesSource
{
    string Name { get; }
    int SampleRateHz { get; }
    int ChannelCount { get; }
    ClockDomain ClockDomain { get; }
    int EstimatedPrecisionUs { get; }
    
    event EventHandler<DataReceivedEventArgs> DataReceived;
    
    void Start();
    void Stop();
}
```

### 4.2 GlobalTime（全局时间）

```csharp
public readonly struct GlobalTime
{
    public long MonotonicUs { get; }      // 单调时间（微秒）
    public ClockDomain ClockDomain { get; }
    public int PrecisionUs { get; }
    
    // 转换方法
    public DateTime ToUtc(long utcOffsetUs);
}
```

### 4.3 EegSample（EEG样本）

```csharp
public readonly struct EegSample
{
    public long TimestampUs { get; }      // 样本中心时间
    public double Ch1 { get; }            // μV
    public double Ch2 { get; }
    public double Ch3 { get; }
    public double Ch4 { get; }
    public QualityFlags Quality { get; }  // Gap/Clip/Outlier 标记
}
```

### 4.4 QualityFlags（质量标记 - 重要！）

```csharp
[Flags]
public enum QualityFlags : byte
{
    None = 0,
    Gap = 1 << 0,           // 数据缺失
    Clipped = 1 << 1,       // 信号饱和
    Outlier = 1 << 2,       // 离群值
    Interpolated = 1 << 3,  // 已插值
    LowQuality = 1 << 4,    // 低质量（电极接触不良等）
}
```

---

## 5. 验收标准

### 5.1 功能验收

- [ ] 所有接口使用 `long` 类型 + `Us` 后缀表示微秒时间戳
- [ ] `ClockDomain` 枚举包含 Device/Host/Unknown
- [ ] `QualityFlags` 支持 Gap/Clip/Outlier 标记
- [ ] EegSample 支持 4 通道
- [ ] NirsSample 支持 6 通道
- [ ] 所有公开成员有 XML 文档注释

### 5.2 编译验收

- [ ] `dotnet build` 通过，无 warning
- [ ] 符合 `API_STYLE.md` 命名规范

### 5.3 文档验收

- [ ] `handoff/interfaces-api.md` 包含所有公开接口
- [ ] 线程模型说明完整
- [ ] 时间戳语义说明完整

---

## 6. 约束（不可违反）

```
❌ 禁止使用 DateTime 作为时间戳类型
❌ 禁止省略时间戳单位后缀（必须用 Us/Ms/Sec）
❌ 禁止在接口中硬编码通道数（使用 ChannelCount 属性）
✅ 必须预留 ClockDomain 字段（即使当前只用 Host）
✅ 必须预留 QualityFlags 字段（即使当前不处理伪迹）
```

---

## 7. 依赖与被依赖

### 依赖
- 无（首个任务）

### 被依赖
- S1-02: SafeDoubleBuffer（需要数据结构定义）
- S1-03: Vortice渲染底座（需要 IRenderTarget）
- S1-05: 模拟数据源（需要 ITimeSeriesSource）
- S2-xx: 所有 DSP 任务

---

## 8. 启动指令（给 Codex）

```
请先阅读以下文件：
1. spec/00_CONSTITUTION.md
2. spec/CONSENSUS_BASELINE.md §5-§6
3. spec/ARCHITECTURE.md §3-§4
4. spec/TIME_SYNC.md §2
5. spec/API_STYLE.md

然后执行任务 TASK-S1-01：
- 在 src/Core/ 下创建接口和数据结构
- 所有时间戳使用 long + Us 后缀
- 预留 ClockDomain 和 QualityFlags
- 完成后生成 handoff/interfaces-api.md
```

---

**任务卡结束**
