# Handoff: S2-01 EEG Basic Digital Filter Chain

> **Sprint**: S2
> **Task**: S2-01
> **创建日期**: 2026-01-28
> **状态**: 完成

---

## 1. 本 Sprint 定义的类

| 类 | 文件路径 | 用途 |
|------|----------|------|
| `SosSection` | `src/DSP/Filters/SosSection.cs` | SOS 系数结构 |
| `SosSectionState` | `src/DSP/Filters/SosSection.cs` | SOS 滤波器状态 |
| `IirFilterBase` | `src/DSP/Filters/IirFilterBase.cs` | IIR 滤波器基类 |
| `NotchFilter` | `src/DSP/Filters/NotchFilter.cs` | 陷波滤波器 (50/60 Hz) |
| `HighPassFilter` | `src/DSP/Filters/HighPassFilter.cs` | 高通滤波器 (0.3/0.5/1.5 Hz) |
| `LowPassFilter` | `src/DSP/Filters/LowPassFilter.cs` | 低通滤波器 (15/35/50/70 Hz) |
| `EegFilterChain` | `src/DSP/Filters/EegFilterChain.cs` | EEG 滤波链 |
| `EegFilterChainConfig` | `src/DSP/Filters/EegFilterChain.cs` | 滤波链配置 |
| `FilteredSample` | `src/DSP/Filters/EegFilterChain.cs` | 滤波输出样本 |

---

## 2. 每个类的职责边界

### 2.1 IirFilterBase

**成员签名（完整清单）**:
| 成员 | 类型 | 说明 |
|------|------|------|
| `Process(double)` | `double` | 处理单个样本 |
| `Reset()` | `void` | 重置滤波器状态 |
| `Order` | `int { get; }` | 滤波器阶数 |

**职责（负责）**:
- 实现 Direct Form II Transposed IIR 滤波算法
- 管理 SOS section 链
- 提供状态重置功能

**职责边界（不负责）**:
- ❌ 不负责 系数计算 → 由子类提供预计算系数
- ❌ 不负责 多通道管理 → 由 `EegFilterChain` 负责
- ❌ 不负责 Gap 检测 → 由 `EegFilterChain` 负责

---

### 2.2 NotchFilter

**成员签名（完整清单）**:
| 成员 | 类型 | 说明 |
|------|------|------|
| `Create(NotchFrequency)` | `static NotchFilter` | 工厂方法 |
| `WarmupSamples` | `static int { get; }` | 预热样本数 (16) |

**规格**:
| 参数 | 值 | 依据 |
|------|-----|------|
| 类型 | 2nd order IIR (1 SOS) | DSP_SPEC.md §2.4 |
| Q 值 | 30 | DSP_SPEC.md §2.4 |
| 选项 | 50 Hz, 60 Hz | DSP_SPEC.md §2.4 |
| 预热时间 | 0.1 sec (16 samples @ 160Hz) | DSP_SPEC.md §7 |

**职责（负责）**:
- 消除工频干扰 (50/60 Hz)

**职责边界（不负责）**:
- ❌ 不负责 其他频率滤波 → 由 HPF/LPF 负责

---

### 2.3 HighPassFilter

**成员签名（完整清单）**:
| 成员 | 类型 | 说明 |
|------|------|------|
| `Create(HighPassCutoff)` | `static HighPassFilter` | 工厂方法 |
| `GetWarmupSamples(HighPassCutoff)` | `static int` | 获取预热样本数 |

**规格**:
| 参数 | 值 | 依据 |
|------|-----|------|
| 类型 | 2nd order Butterworth (1 SOS) | DSP_SPEC.md §2.2 |
| 选项 | 0.3 Hz, 0.5 Hz, 1.5 Hz | DSP_SPEC.md §2.2 |
| 预热时间 (0.3Hz) | 20 sec (3200 samples) | DSP_SPEC.md §7 |
| 预热时间 (0.5Hz) | 6 sec (960 samples) | DSP_SPEC.md §7 |
| 预热时间 (1.5Hz) | 2 sec (320 samples) | DSP_SPEC.md §7 |

**职责（负责）**:
- 消除直流偏移和低频漂移

**职责边界（不负责）**:
- ❌ 不负责 高频滤波 → 由 LPF 负责

---

### 2.4 LowPassFilter

**成员签名（完整清单）**:
| 成员 | 类型 | 说明 |
|------|------|------|
| `Create(LowPassCutoff)` | `static LowPassFilter` | 工厂方法 |
| `GetWarmupSamples(LowPassCutoff)` | `static int` | 获取预热样本数 |

**规格**:
| 参数 | 值 | 依据 |
|------|-----|------|
| 类型 | 4th order Butterworth (2 SOS) | DSP_SPEC.md §2.3 |
| 选项 | 15 Hz, 35 Hz, 50 Hz, 70 Hz | DSP_SPEC.md §2.3 |
| 预热时间 | 32-160 samples | DSP_SPEC.md §7 |

**职责（负责）**:
- 限制信号带宽
- 抗混叠保护

**职责边界（不负责）**:
- ❌ 不负责 低频滤波 → 由 HPF 负责

---

### 2.5 EegFilterChain

**成员签名（完整清单）**:
| 成员 | 类型 | 说明 |
|------|------|------|
| `ProcessSample(int, short, long, double)` | `FilteredSample` | 处理原始样本 |
| `ProcessSampleUv(int, double, long)` | `FilteredSample` | 处理 μV 样本 |
| `IsWarmedUp(int)` | `bool` | 检查通道预热状态 |
| `GetSamplesProcessed(int)` | `long` | 获取已处理样本数 |
| `ResetChannel(int)` | `void` | 重置单通道 |
| `ResetAll()` | `void` | 重置所有通道 |
| `Dispose()` | `void` | 释放资源 |
| `Config` | `EegFilterChainConfig { get; }` | 配置 |
| `ChannelCount` | `int { get; }` | 通道数 |
| `WarmupSamples` | `int { get; }` | 预热样本数 |

**处理链**:
```
Raw EEG (int16) → Scale → Notch → High-Pass → Low-Pass → Filtered EEG (double, μV)
```

**职责（负责）**:
- 管理每通道独立滤波状态
- 检测并处理数据 Gap（>4 样本触发重置）
- 标记预热期间的瞬态数据 (`QualityFlag.Transient`)
- 保持输入/输出时间戳一致

**职责边界（不负责）**:
- ❌ 不负责 数据采集 → 由 `ITimeSeriesSource` 负责
- ❌ 不负责 aEEG 计算 → S2-02 实现
- ❌ 不负责 数据存储 → 由 `IDataSink` 负责
- ❌ 不负责 渲染显示 → 由渲染模块负责

---

### 2.6 EegFilterChainConfig

**成员签名（完整清单）**:
| 成员 | 类型 | 默认值 | 说明 |
|------|------|--------|------|
| `NotchFrequency` | `NotchFrequency?` | `Hz50` | 陷波频率 (null=禁用) |
| `HighPassCutoff` | `HighPassCutoff` | `Hz0_5` | 高通截止 |
| `LowPassCutoff` | `LowPassCutoff` | `Hz35` | 低通截止 |
| `ChannelCount` | `int` | `4` | 通道数 |
| `SampleRate` | `int` | `160` | 采样率 (Hz) |

---

### 2.7 FilteredSample

**成员签名（完整清单）**:
| 成员 | 类型 | 说明 |
|------|------|------|
| `Value` | `double` | 滤波后的值 (μV) |
| `TimestampUs` | `long` | 时间戳 (μs)，与输入一致 |
| `Quality` | `QualityFlag` | 质量标志 |

---

## 3. 枚举定义

| 枚举 | 文件路径 | 用途 |
|------|----------|------|
| `NotchFrequency` | `src/DSP/Filters/NotchFilter.cs` | 陷波频率选项 |
| `HighPassCutoff` | `src/DSP/Filters/HighPassFilter.cs` | 高通截止选项 |
| `LowPassCutoff` | `src/DSP/Filters/LowPassFilter.cs` | 低通截止选项 |

### 3.1 NotchFrequency 成员

| 成员 | 说明 |
|------|------|
| `Hz50` | 50 Hz (欧洲/中国工频) |
| `Hz60` | 60 Hz (美洲/日本工频) |

### 3.2 HighPassCutoff 成员

| 成员 | 说明 | 预热时间 |
|------|------|----------|
| `Hz0_3` | 0.3 Hz | 20 sec |
| `Hz0_5` | 0.5 Hz (默认) | 6 sec |
| `Hz1_5` | 1.5 Hz | 2 sec |

### 3.3 LowPassCutoff 成员

| 成员 | 说明 |
|------|------|
| `Hz15` | 15 Hz |
| `Hz35` | 35 Hz (默认) |
| `Hz50` | 50 Hz |
| `Hz70` | 70 Hz |

---

## 4. QualityFlag 更新

S2-01 新增标志:

| 成员 | 值 | 说明 | 来源 |
|------|-----|------|------|
| `Transient` | 1 << 5 | 滤波器预热期间的瞬态数据 | DSP_SPEC.md §7 |

---

## 5. 关键约束

### 5.1 时间戳保持规则

```
⚠️ 关键约定：滤波器输出时间戳必须与输入完全一致
```

| 规则 | 依据 |
|------|------|
| 输入时间戳 = 输出时间戳 | DSP_SPEC.md §2.5 |
| 不引入 Group Delay 补偿 | 实时系统要求 |

### 5.2 Gap 处理规则

| 规则 | 依据 |
|------|------|
| Gap 阈值 | > 4 样本 (>25ms @ 160Hz) |
| Gap 行为 | 重置滤波器状态 |
| Gap 标记 | `QualityFlag.Missing` |

### 5.3 精度规则（铁律4）

| 规则 | 依据 |
|------|------|
| 所有系数使用 double | 00_CONSTITUTION.md 铁律4 |
| 所有状态使用 double | 00_CONSTITUTION.md 铁律4 |
| 不使用 float | 防止精度损失 |

---

## 6. 使用示例

### 6.1 基本使用

```csharp
// 创建滤波链（使用默认配置）
var chain = new EegFilterChain();

// 处理原始样本
short rawValue = 1234;
long timestampUs = 6250;  // 第一个样本时间
var result = chain.ProcessSample(
    channelIndex: 0,
    rawValue: rawValue,
    timestampUs: timestampUs,
    scaleFactor: 0.076);

// 检查输出
if (result.Quality.HasFlag(QualityFlag.Transient))
{
    // 预热期间，显示可降低优先级
}
double filteredUv = result.Value;
```

### 6.2 自定义配置

```csharp
var config = new EegFilterChainConfig
{
    NotchFrequency = NotchFrequency.Hz60,  // 美国工频
    HighPassCutoff = HighPassCutoff.Hz0_3, // 更低截止
    LowPassCutoff = LowPassCutoff.Hz50,    // 更高带宽
    ChannelCount = 8
};
var chain = new EegFilterChain(config);
```

### 6.3 单独使用滤波器

```csharp
// 创建单个滤波器
var hpf = HighPassFilter.Create(HighPassCutoff.Hz0_5);
var lpf = LowPassFilter.Create(LowPassCutoff.Hz35);
var notch = NotchFilter.Create(NotchFrequency.Hz50);

// 手动处理
double filtered = hpf.Process(input);
filtered = lpf.Process(filtered);
filtered = notch.Process(filtered);

// 重置
hpf.Reset();
```

---

## 7. 测试覆盖

| 测试文件 | 测试内容 |
|----------|----------|
| `FilterFrequencyResponseTests.cs` | 频率响应 (-3dB @ 截止频率) |
| `FilterStabilityTests.cs` | 长时间运行稳定性 (72h 模拟) |
| `TransientBehaviorTests.cs` | 预热期间瞬态行为 |

---

## 8. 证据引用（审计追溯）

| 定义 | 依据文档 | 章节 |
|------|----------|------|
| 滤波器系数 | DSP_SPEC.md | §2.2, §2.3, §2.4 |
| 预热时间 | DSP_SPEC.md | §7 |
| 处理链顺序 | DSP_SPEC.md | §2.5 |
| Gap 检测阈值 | DSP_SPEC.md | §6.1 |
| 精度要求 | 00_CONSTITUTION.md | 铁律4 |
| 质量标志 | 00_CONSTITUTION.md | 铁律5 |
| 采样率/通道数 | CONSENSUS_BASELINE.md | §6.1 |

---

## 9. 未实现/延后项

| 项目 | 延后原因 | 计划 Sprint |
|------|----------|------------|
| aEEG 计算 | 本 Sprint 仅实现基础滤波 | S2-02 |
| 零相滤波 (filtfilt) | 非实时，离线分析用 | 按需 |
| UI 集成 | 按 CHARTER 分离 | S3+ |

---

**文档结束**
