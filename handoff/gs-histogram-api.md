# GS 直方图 API 文档

> **任务**: S2-03
> **创建日期**: 2026-01-28
> **依据**: DSP_SPEC.md §3.3, CONSENSUS_BASELINE.md §6.4

---

## 1. 概述

本文档描述 GS (Grayscale) 直方图数据结构的 API 接口和使用方式。

GS 直方图是 aEEG 的**统计表达**，将 aEEG 数值流转换为 15 秒周期的电压分布直方图。

**这是统计编码，不是信号处理。**

---

## 2. 冻结规格（不可修改）

> ⚠️ 以下规格严格来源于 DSP_SPEC.md §3.3，禁止修改

### 2.1 电压范围

| 参数 | 值 |
|------|-----|
| 输入 | aEEG μV (来自 S2-02) |
| 统计范围 | 0-200 μV |
| 负值处理 | 忽略（不计入任何 bin） |
| 超过 200 μV | 计入 bin 229 |

### 2.2 Bin 结构

| 参数 | 值 |
|------|-----|
| 总 bin 数 | **230** |
| bin index | 0-229 |

### 2.3 分段映射规则

| 区域 | 电压范围 | Bin 范围 | 映射方法 |
|------|----------|----------|----------|
| 线性区 | 0-10 μV | 0-99 (100 bins) | bin = floor(uV × 10) |
| 对数区 | 10-200 μV | 100-229 (130 bins) | bin = 100 + floor((log10(uV) - 1) / 1.301 × 130) |

### 2.4 统计周期

| 参数 | 值 |
|------|-----|
| 周期 | **15 秒** |
| aEEG 输入率 | 1 Hz |
| 每帧样本数 | 15 个 min/max 对 |

### 2.5 Counter 语义

| Counter 值 | 含义 |
|------------|------|
| 0-228 | 累计中 |
| **229** | 本帧结束 (flush) |
| **255** | 忽略（不计入） |

### 2.6 Bin 值

| 参数 | 值 |
|------|-----|
| 类型 | byte (0-255) |
| 最大饱和值 | **249** |
| 饱和行为 | 达到即 clamp，样本计数继续 |

---

## 3. 禁止事项

```
❌ 对 GS 做平滑 / 插值
❌ 改变 bin 数量
❌ 改变 15 秒周期
❌ 对 log / linear 分界点做"优化"
❌ 根据 UI 需要调整 GS
❌ 引入任何"视觉增强"
```

**GS 是事实统计，不是图像算法。**

---

## 4. 核心类型

### 4.1 GsFrame (帧结构)

```csharp
namespace Neo.DSP.GS;

/// <summary>
/// GS 直方图帧（15 秒统计周期）。
/// </summary>
public sealed class GsFrame
{
    // 常量
    public const double PeriodSeconds = 15.0;
    public const int BinCount = 230;
    public const byte MaxBinValue = 249;
    public const byte CounterEndOfCycle = 229;
    public const byte CounterIgnore = 255;

    // 直方图数据
    public byte[] Bins { get; }  // 230 bins

    // 时间戳 (μs)
    public long StartTimestampUs { get; }
    public long EndTimestampUs { get; }
    public long CenterTimestampUs { get; }  // 窗口中心

    // 元数据
    public int ChannelIndex { get; }
    public QualityFlag Quality { get; }
    public int SampleCount { get; }
    public bool IsComplete { get; }
}
```

### 4.2 GsProcessorOutput (输出结构)

```csharp
/// <summary>
/// GS 处理器输出。
/// </summary>
public readonly struct GsProcessorOutput
{
    public GsFrame Frame { get; init; }
    public int ChannelIndex { get; init; }
}
```

---

## 5. GsBinMapper (映射器)

```csharp
namespace Neo.DSP.GS;

/// <summary>
/// GS 直方图 Bin 映射器。
/// </summary>
public static class GsBinMapper
{
    // 常量
    public const int TotalBins = 230;
    public const int LinearBins = 100;
    public const int LogBins = 130;
    public const int InvalidBin = -1;

    /// <summary>
    /// 将电压值映射到 bin 索引。
    /// </summary>
    /// <param name="voltageUv">电压值 (μV)</param>
    /// <returns>bin 索引 (0-229)，或 -1 表示忽略</returns>
    public static int MapToBin(double voltageUv);

    /// <summary>
    /// 获取 bin 索引对应的电压中心值。
    /// </summary>
    public static double GetBinCenterVoltage(int binIndex);

    /// <summary>
    /// 获取 bin 索引对应的电压边界。
    /// </summary>
    public static double GetBinLowerBound(int binIndex);
    public static double GetBinUpperBound(int binIndex);
}
```

---

## 6. GsProcessor (处理器)

### 6.1 创建

```csharp
// 使用默认配置（4通道）
var processor = new GsProcessor();

// 自定义配置
var processor = new GsProcessor(new GsProcessorConfig
{
    ChannelCount = 4
});
```

### 6.2 处理 aEEG 输出

```csharp
// 方式1：处理 AeegProcessorOutput
bool hasOutput = processor.ProcessAeegOutput(
    aeegOutput,
    counter: deviceCounter,  // 设备 data[16]: 0-228=累计, 229=帧结束, 255=忽略
    out GsProcessorOutput gsOutput);

// 方式2：直接处理参数
bool hasOutput = processor.ProcessAeegOutput(
    channelIndex: 0,
    minUv: aeegOutput.AeegOutput.MinUv,
    maxUv: aeegOutput.AeegOutput.MaxUv,
    timestampUs: aeegOutput.AeegOutput.TimestampUs,
    quality: aeegOutput.Quality,
    counter: deviceCounter,  // 设备 data[16]
    out GsProcessorOutput gsOutput);
```

### 6.3 Counter 参数

**重要**: `counter` 参数来自设备数据 `data[16]`，不是软件计算的。

| Counter 值 | 行为 |
|------------|------|
| 0-228 | 累计样本到当前帧，不输出 |
| **229** | 累计样本后完成帧并输出 |
| **255** | 忽略该样本（不计入任何统计） |

### 6.4 输出频率

- **输入**: 1 Hz（aEEG 输出，每秒一对 min/max）
- **输出**: 由设备 counter=229 控制（约 1/15 Hz）
- **输出时机**: `ProcessAeegOutput` 返回 `true` 时（counter=229 触发）

---

## 7. 与 S2-02 集成

```csharp
// EegFilterChain (S2-01) → AeegProcessor (S2-02) → GsProcessor (S2-03)

var filterChain = new EegFilterChain();
var aeegProcessor = new AeegProcessor();
var gsProcessor = new GsProcessor();

// 处理循环
for each sample in eegDataStream
{
    // Step 1: EEG 滤波
    var filtered = filterChain.ProcessSampleUv(ch, rawValueUv, timestampUs);

    // Step 2: aEEG 处理
    if (aeegProcessor.ProcessFilteredSample(ch, filtered, out var aeegOutput))
    {
        // Step 3: GS 直方图累计
        // counter 来自设备数据 data[16]: 0-228=累计, 229=帧结束, 255=忽略
        byte counter = devicePacket.Data[16];

        if (gsProcessor.ProcessAeegOutput(aeegOutput, counter, out var gsOutput))
        {
            // counter=229 时输出 GsFrame
            UpdateGsDisplay(gsOutput.Frame);
        }
    }
}
```

---

## 8. Gap 处理

```csharp
// Gap 阈值: 2 秒（超过 aEEG 1 Hz 输出间隔 2 倍）
// Gap 期间:
//   - 不累计
//   - 不生成伪 bin
//   - 重置累计器
//   - Quality |= QualityFlag.Missing
```

---

## 9. 时间戳语义

依据: CONSENSUS_BASELINE.md §5.3

| 属性 | 含义 |
|------|------|
| StartTimestampUs | 帧第一个样本的时间戳 |
| EndTimestampUs | 帧最后一个样本的时间戳 |
| CenterTimestampUs | 帧中心时间 = Start + (End - Start) / 2 |

---

## 10. 文件清单

```
src/DSP/GS/
├── GsBinMapper.cs           # Bin 映射逻辑
├── GsHistogramAccumulator.cs# 累计器
├── GsFrame.cs               # 帧结构
└── GsProcessor.cs           # 主处理器

tests/DSP.Tests/GS/
├── GsMappingLinearTests.cs  # 线性区域映射测试 (20 tests)
├── GsMappingLogTests.cs     # Log 区域映射测试 (20 tests)
├── GsCounterBehaviorTests.cs# Counter 行为测试 (18 tests)
└── GsSaturationTests.cs     # 饱和测试 (22 tests)
```

---

## 11. 测试覆盖

| 测试类别 | 测试数 | 说明 |
|---------|-------|------|
| 线性映射 | 16+ | 0-10 μV → bin 0-99 |
| Log 映射 | 14+ | 10-200 μV → bin 100-229 |
| Counter 行为 | 26 | counter=229 帧输出、counter=255 忽略 |
| 饱和测试 | 14+ | 249 饱和、边界处理 |
| **总计** | 90 | - |

---

## 12. 已知限制（不做推断）

1. **Bin 分辨率固定**: 230 bins 是设备协议定义，无法调整
2. **周期固定**: 15 秒是 aEEG 显示标准，无法调整
3. **无缩放**: GS 不做任何显示层面的缩放或变换
4. **无平滑**: 即使连续 gap，也不做任何插值或平滑

---

## 13. 下游依赖

本模块被以下任务使用：
- S4+: 显示渲染 - 使用 GsFrame 绘制 aEEG 背景灰度

---

**文档结束**
