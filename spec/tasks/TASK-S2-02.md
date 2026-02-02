# 📋 TASK-S2-02: aEEG 处理链（Medical Frozen）

> **Sprint**: 2
> **负责方**: Claude Code
> **优先级**: 🟢 P0
> **预估工时**: 4h
> **状态**: ✅ 已完成

---

## ✅ 完成说明

> **完成日期**: 2026-01-28

### 规格变更 (DSP_SPEC.md v2.3)

- **§3.2 阶数**: 4 → **6**（HPF 2阶 + LPF 4阶）
- **§3.2.1 HPF 2Hz 系数**: 已补充
  ```yaml
  sos: [1.0, -2.0, 1.0, 1.0, -1.88910739, 0.89490251]
  gain: 0.94597746
  ```
- **LPF 15Hz**: 复用 §2.3 已验证系数

### 代码修复

| 文件 | 修复内容 |
|------|----------|
| `AeegBandpassFilter.cs` | HPF 2Hz 使用 §3.2.1 官方系数 |
| `AeegEnvelopeCalculator.cs` | 时间戳改为窗口中心 (+500,000 μs) |

---

## 1. 目标

实现 aEEG（振幅整合脑电图）处理链，用于从滤波后 EEG 生成 aEEG 趋势数据。

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

## 2. 输入（必读文件）

| 文件 | 重点章节 |
|------|----------|
| `spec/DSP_SPEC.md` | §3（aEEG算法规格）、§3.0（医学约束） |
| `spec/00_CONSTITUTION.md` | 铁律4（double精度）、铁律5（质量标志） |
| `spec/CONSENSUS_BASELINE.md` | §6.4（aEEG参数） |
| `handoff/eeg-filter-chain-api.md` | FilteredSample 输入格式 |

---

## 3. 输出

### 3.1 代码文件

```
src/DSP/AEEG/
├── AeegBandpassFilter.cs      # 2-15Hz 带通滤波器
├── AeegRectifier.cs           # 半波整流器
├── AeegEnvelopeCalculator.cs  # 包络计算器
└── AeegProcessor.cs           # aEEG 主处理器

tests/DSP.Tests/AEEG/
├── AeegBandpassFilterTests.cs
├── AeegEnvelopeTests.cs
└── AeegProcessorTests.cs
```

### 3.2 交接文档

```
handoff/aeeg-chain-api.md
```

---

## 4. 处理规格（来源: DSP_SPEC.md §3）

### 4.1 带通滤波器 (§3.2)

| 参数 | 值 |
|------|-----|
| 类型 | Butterworth |
| 低截止 | 2 Hz |
| 高截止 | 15 Hz |
| 实现 | HPF(2Hz) 串联 LPF(15Hz) |

### 4.2 整流器 (§3.1)

- **类型**: 半波整流
- **公式**: y = |x|

### 4.3 包络计算 (§3.1)

| 阶段 | 参数 |
|------|------|
| 峰值检测 | 0.5秒窗口最大值 |
| 平滑 | 15秒移动平均 |
| 输出 | 每秒 min/max 对 |

### 4.4 输出格式 (§3.3)

```yaml
aeeg_output:
  rate: 1 Hz
  values:
    - min_uV: double
    - max_uV: double
```

---

## 5. 医学约束（不可违反）

> ⚠️ 来源: DSP_SPEC.md §3.0 医学意义澄清

```
❌ aEEG ≠ RMS（禁止 RMS 替代）
❌ 禁止自定义变换
❌ 禁止自行计算 SOS 系数
✅ 临床判读一致性优先
✅ 15秒周期是核心
```

---

## 6. 验收标准

### 6.1 功能验收

- [x] 带通滤波器 2-15 Hz (6阶: HPF 2Hz + LPF 15Hz)
- [x] 半波整流 |x|
- [x] 峰值检测 0.5秒窗口
- [x] 15秒移动平均
- [x] 每秒 min/max 输出（窗口中心时间戳）
- [x] Per-channel 独立状态
- [x] Gap 处理和状态重置
- [x] 瞬态期间标记 QualityFlag.Transient

### 6.2 测试验收

- [x] 带通频响测试
- [x] 整流器测试
- [x] 包络输出率测试（1 Hz）
- [x] 72h 稳定性测试
- [x] 所有 91 个 DSP 测试通过

### 6.3 编译验收

- [x] `dotnet build src/DSP/Neo.DSP.csproj` 零警告
- [x] `dotnet test tests/DSP.Tests/` 全部通过

---

## 7. 依赖与被依赖

### 依赖
- S2-01: EEG 滤波链（FilteredSample 输入）

### 被依赖
- S2-03: GS 直方图（使用 aEEG 输出）
- S4+: 显示渲染（aEEG 趋势显示）

---

## 8. 预热时间

| 组件 | 预热时间 | 预热样本 (160Hz) |
|------|---------|-----------------|
| Bandpass | 1.5 秒 | 240 |
| Envelope | 15 秒 | 2400 |
| **总计** | ~16.5 秒 | 2640 |

---

**任务卡结束**
