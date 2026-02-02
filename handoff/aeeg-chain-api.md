# aEEG 处理链 API 文档

> **任务**: S2-02
> **创建日期**: 2026-01-28
> **依据**: DSP_SPEC.md §3, 00_CONSTITUTION.md 铁律4/5

---

## 1. 概述

本文档描述 aEEG (振幅整合脑电图) 处理链的 API 接口和使用方式。

**处理链**:
```
Filtered EEG (160 Hz)
    ↓
Bandpass Filter (2-15 Hz)
    ↓
Half-Wave Rectification (y = |x|)
    ↓
Peak Detection (0.5秒窗口最大值)
    ↓
Smoothing (15秒移动平均)
    ↓
Min/Max Extraction (每秒上下边界)
    ↓
aEEG Output (1 Hz)
```

---

## 2. 核心类型

### 2.1 AeegOutput (输出结构)

```csharp
namespace Neo.DSP.AEEG;

/// <summary>
/// aEEG 输出数据（每秒一对 min/max）。
/// </summary>
public readonly struct AeegOutput
{
    /// <summary>下边界 (μV)</summary>
    public double MinUv { get; init; }

    /// <summary>上边界 (μV)</summary>
    public double MaxUv { get; init; }

    /// <summary>时间戳 (μs)，窗口中心时间 (CONSENSUS_BASELINE.md §5.3)</summary>
    public long TimestampUs { get; init; }

    /// <summary>是否有效（已完成预热）</summary>
    public bool IsValid { get; init; }
}
```

### 2.2 AeegProcessorOutput (完整输出)

```csharp
/// <summary>
/// aEEG 处理器输出（含质量标志）。
/// </summary>
public readonly struct AeegProcessorOutput
{
    /// <summary>aEEG 输出（min/max 对）</summary>
    public AeegOutput AeegOutput { get; init; }

    /// <summary>通道索引</summary>
    public int ChannelIndex { get; init; }

    /// <summary>质量标志</summary>
    public QualityFlag Quality { get; init; }
}
```

---

## 3. AeegProcessor (主处理器)

### 3.1 创建

```csharp
// 使用默认配置（4通道，160Hz）
var processor = new AeegProcessor();

// 自定义配置
var processor = new AeegProcessor(new AeegProcessorConfig
{
    ChannelCount = 4,
    SampleRate = 160
});
```

### 3.2 处理样本

```csharp
// 方式1：直接处理
bool hasOutput = processor.ProcessSample(
    channelIndex: 0,
    filteredValue: sample.Value,
    timestampUs: sample.TimestampUs,
    inputQuality: QualityFlag.Normal,
    out AeegProcessorOutput output);

// 方式2：处理 FilteredSample（来自 EegFilterChain）
bool hasOutput = processor.ProcessFilteredSample(
    channelIndex: 0,
    sample: filteredSample,
    out AeegProcessorOutput output);
```

### 3.3 输出频率

- **输入**: 160 Hz（每通道）
- **输出**: 1 Hz（每通道每秒输出一对 min/max）
- **输出时机**: `ProcessSample` 返回 `true` 时

### 3.4 预热行为

```csharp
// 预热样本数 = 带通滤波器(240) + 包络计算器(2400) = 2640 样本
// 约 16.5 秒 @ 160Hz

// 预热期间：
// - IsValid = false
// - Quality |= QualityFlag.Transient

if (!processor.IsWarmedUp(channelIndex))
{
    // 数据在预热期间，显示时应降低优先级
}
```

### 3.5 Gap 处理

```csharp
// Gap > 4 样本（>25ms @ 160Hz）会触发状态重置
// - 滤波器状态清零
// - 包络统计重置
// - Quality |= QualityFlag.Missing
```

---

## 4. 组件 API

### 4.1 AeegBandpassFilter

```csharp
namespace Neo.DSP.AEEG;

/// <summary>
/// aEEG 带通滤波器 (2-15 Hz)。
/// </summary>
public sealed class AeegBandpassFilter
{
    // 设计参数
    public const double LowCutoffHz = 2.0;
    public const double HighCutoffHz = 15.0;
    public const int SampleRate = 160;

    // 预热
    public static int WarmupSamples => 240;  // 1.5秒

    // 处理
    public double Process(double input);
    public void Reset();
}
```

### 4.2 AeegRectifier

```csharp
/// <summary>
/// 半波整流器。
/// </summary>
public static class AeegRectifier
{
    /// <summary>半波整流 y = |x|</summary>
    public static double Rectify(double input);

    /// <summary>批量整流</summary>
    public static void RectifyBatch(double[] input, double[] output, int count);
}
```

### 4.3 AeegEnvelopeCalculator

```csharp
/// <summary>
/// 包络计算器（峰值检测 + 平滑 + Min/Max）。
/// </summary>
public sealed class AeegEnvelopeCalculator
{
    // 预热
    public static int WarmupSamples => 2400;  // 15秒

    // 状态
    public bool IsWarmedUp { get; }
    public long SamplesProcessed { get; }

    // 处理
    public bool ProcessSample(double rectifiedValue, long timestampUs, out AeegOutput output);
    public void Reset();
}
```

---

## 5. 医学约束

> ⚠️ **来源**: DSP_SPEC.md §3.0 医学意义澄清

| 规则 | 说明 |
|------|------|
| ❌ aEEG ≠ RMS | RMS 是能量度量，aEEG 是振幅分布包络，**不等价** |
| ❌ 禁止 RMS 替代 | 禁止用 RMS 替代或近似 aEEG 主显示 |
| ✅ 临床优先 | 输出结果以临床判读一致性优先 |
| ✅ 15秒周期 | 15 秒平滑窗口是 aEEG 趋势的核心 |

---

## 6. 与 S2-01 集成

```csharp
// EegFilterChain (S2-01) → AeegProcessor (S2-02)

var filterChain = new EegFilterChain();
var aeegProcessor = new AeegProcessor();

// 处理循环
for each sample in eegDataStream
{
    // Step 1: EEG 滤波 (Notch + HPF + LPF)
    var filtered = filterChain.ProcessSampleUv(
        channelIndex,
        rawValueUv,
        timestampUs);

    // Step 2: aEEG 处理
    if (aeegProcessor.ProcessFilteredSample(channelIndex, filtered, out var aeegOutput))
    {
        // 每秒输出一对 min/max
        UpdateAeegDisplay(aeegOutput);
    }
}
```

---

## 7. 预热时间表

| 组件 | 预热时间 | 预热样本 (160Hz) |
|------|---------|-----------------|
| Bandpass 2-15Hz | 1.5 秒 | 240 |
| Envelope (15s avg) | 15 秒 | 2400 |
| **总计** | ~16.5 秒 | 2640 |

---

## 8. 文件清单

```
src/DSP/AEEG/
├── AeegBandpassFilter.cs    # 2-15Hz 带通滤波器
├── AeegRectifier.cs         # 半波整流器
├── AeegEnvelopeCalculator.cs# 包络计算器
└── AeegProcessor.cs         # aEEG 主处理器

tests/DSP.Tests/AEEG/
├── AeegBandpassFilterTests.cs
├── AeegEnvelopeTests.cs
└── AeegProcessorTests.cs
```

---

## 9. 测试覆盖

| 测试类别 | 测试数 | 说明 |
|---------|-------|------|
| Bandpass 频响 | 7 | 通带/阻带验证 |
| Rectifier | 6 | 半波整流验证 |
| Envelope | 7 | 输出率/预热/稳定性 |
| Processor | 13 | 集成/Gap/72h稳定性 |
| **总计** | 33 | - |

---

## 10. 下游依赖

本模块被以下任务使用：
- S2-03: GS 直方图（15秒）- 使用 aEEG 输出
- S4+: 显示渲染 - 使用 min/max 对绘制 aEEG 趋势

---

**文档结束**
