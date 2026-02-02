# 📋 TASK-S2-01: EEG 基础数字滤波链（Real-time）

> **Sprint**: 2
> **负责方**: Claude Code
> **优先级**: 🟢 P0
> **预估工时**: 4h
> **状态**: ✅ 已完成

---

## 1. 目标

实现实时 IIR 数字滤波链，用于 EEG 信号预处理。

**滤波链**:
```
Raw EEG (int16) → Scale → Notch → High-Pass → Low-Pass → Filtered EEG (double, μV)
```

---

## 2. 输入（必读文件）

| 文件 | 重点章节 |
|------|----------|
| `spec/DSP_SPEC.md` | §2（滤波器规格）、§7（预热时间） |
| `spec/00_CONSTITUTION.md` | 铁律4（double精度）、铁律5（质量标志） |
| `spec/CONSENSUS_BASELINE.md` | §5.1（时间戳规则）、§6.1（采样参数） |
| `handoff/interfaces-api.md` | QualityFlag 枚举 |

---

## 3. 输出

### 3.1 代码文件

```
src/DSP/
├── Neo.DSP.csproj                  # DSP 项目配置
└── Filters/
    ├── SosSection.cs               # SOS 系数结构
    ├── IirFilterBase.cs            # IIR 滤波器基类
    ├── NotchFilter.cs              # 陷波滤波器 (50/60 Hz)
    ├── HighPassFilter.cs           # 高通滤波器 (0.3/0.5/1.5 Hz)
    ├── LowPassFilter.cs            # 低通滤波器 (15/35/50/70 Hz)
    └── EegFilterChain.cs           # EEG 滤波链

tests/DSP.Tests/
├── Neo.DSP.Tests.csproj
├── FilterFrequencyResponseTests.cs  # 频率响应测试
├── FilterStabilityTests.cs          # 稳定性测试 (含 72h)
└── TransientBehaviorTests.cs        # 瞬态行为测试
```

### 3.2 交接文档

```
handoff/eeg-filter-chain-api.md
```

---

## 4. 滤波器规格（来源: DSP_SPEC.md）

### 4.1 滤波器类型

| 滤波器 | 类型 | 阶数 | 可选值 | 默认值 |
|--------|------|------|--------|--------|
| Notch | IIR | 2 | 50, 60 Hz | 50 Hz |
| HPF | Butterworth IIR | 2 | 0.3, 0.5, 1.5 Hz | 0.5 Hz |
| LPF | Butterworth IIR | 4 | 15, 35, 50, 70 Hz | 35 Hz |

### 4.2 SOS 系数（必须使用 DSP_SPEC.md §2.2-2.4 固定值）

```yaml
# 禁止自行计算系数，必须使用规格文档中的固定值
# 如发现频率响应异常，应提交规格修订请求
```

### 4.3 预热时间（来源: DSP_SPEC.md §7）

| 滤波器 | 预热时间 | 预热样本数 (160Hz) |
|--------|----------|-------------------|
| HPF 0.3Hz | 10 sec | 1600 |
| HPF 0.5Hz | 6 sec | 960 |
| HPF 1.5Hz | 2 sec | 320 |
| LPF | < 1 sec | 7-32 |
| Notch | 0.1 sec | 16 |

---

## 5. 实现要求

### 5.1 数值精度（铁律4）

```csharp
// ✅ 正确
double[] sosCoefficients;
double z1, z2;  // 状态变量

// ❌ 禁止
float[] sosCoefficients;  // 精度不足
```

### 5.2 Per-Channel 状态

```csharp
// 每通道独立滤波状态
private readonly ChannelFilterState[] _channelStates;
```

### 5.3 Gap 处理

```csharp
// Gap > 4 样本 (>25ms @ 160Hz) → 重置滤波器状态
if (delta > _maxGapUs)
{
    state.Reset();
    quality |= QualityFlag.Missing;
}
```

### 5.4 瞬态标记（铁律5）

```csharp
// 预热期间标记 QualityFlag.Transient
if (state.SamplesProcessed < _warmupSamples)
{
    quality |= QualityFlag.Transient;
}
```

### 5.5 时间戳保持

```csharp
// 输入时间戳 = 输出时间戳（不引入延迟补偿）
return new FilteredSample
{
    Value = filtered,
    TimestampUs = timestampUs,  // 保持不变
    Quality = quality
};
```

---

## 6. 验收标准

### 6.1 功能验收

- [x] 滤波器系数与 DSP_SPEC.md §2.2-2.4 完全一致
- [x] 所有系数和状态使用 double 精度
- [x] Per-channel 独立滤波状态
- [x] Gap 检测和滤波器重置
- [x] 瞬态期间标记 QualityFlag.Transient
- [x] 时间戳保持不变

### 6.2 测试验收

- [x] 频率响应测试（通带/阻带验证）
- [x] 稳定性测试（72h 模拟）
- [x] 瞬态行为测试
- [x] 所有测试通过

### 6.3 编译验收

- [x] `dotnet build src/DSP/Neo.DSP.csproj` 零警告
- [x] `dotnet test tests/DSP.Tests/` 全部通过

---

## 7. 约束（不可违反）

```
❌ 实现 aEEG / RMS / 包络 / GS 直方图
❌ 实现零相滤波 (filtfilt)
❌ 实现 UI 逻辑 / 数据库存储
❌ 自行计算/修改 DSP_SPEC 系数
❌ 使用 float 精度
✅ 仅实现基础实时滤波链
✅ 使用 DSP_SPEC.md 固定系数
✅ 使用 double 精度
```

---

## 8. 依赖与被依赖

### 依赖
- S1-01: 核心接口（QualityFlag）

### 被依赖
- S2-02: aEEG 处理链
- S1-06+: 系统集成（滤波后数据）

---

## 9. 启动指令（给 Claude Code）

```
请先阅读以下文件：
1. spec/DSP_SPEC.md §2, §7
2. spec/00_CONSTITUTION.md 铁律4, 铁律5
3. spec/CONSENSUS_BASELINE.md §5.1, §6.1
4. handoff/interfaces-api.md (QualityFlag)

然后执行任务 TASK-S2-01：
- 创建 src/DSP/ 项目
- 实现滤波器（使用 DSP_SPEC.md 固定系数）
- 实现 EegFilterChain
- 创建测试（含 72h 稳定性测试）
- 完成后生成 handoff/eeg-filter-chain-api.md
- 更新 PROJECT_STATE.md
```

---

**任务卡结束**
