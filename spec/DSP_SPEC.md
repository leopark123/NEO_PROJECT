# DSP_SPEC.md - 数字信号处理规格

> **版本**: 2.3
> **更新日期**: 2026-01-28
> **变更说明**: v2.3 **§3.2 aEEG带通阶数修正为6阶**（HPF 2阶 + LPF 4阶），复用§2.3已验证LPF系数，新增§3.2.1 HPF_2Hz系数占位；v2.2 新增§3.0医学意义澄清（aEEG≠RMS冻结）；v2.1 LOD选择函数参数统一为μs；v2.0 基于项目实际资料更新采样率(160Hz)、通道数(4+6)、多档位滤波器

---

## 1. 信号采集参数

### 1.1 EEG 采集参数

```yaml
eeg:
  sample_rate: 160 Hz              # 采样率（从示例代码确认）
  channels: 4                       # 4通道（3物理+1计算）
  resolution: 16 bit               # ADC分辨率
  scale_factor: 0.076              # μV/LSB
  input_range: ±2500 μV            # 输入范围
  serial_baud: 115200              # 波特率
  packet_rate: 160 Hz              # 数据包速率
```

### 1.2 通道配置

| 通道 | 名称 | 导联 | 类型 |
|------|------|------|------|
| CH1 | 通道1 | C3-P3 (A-B) | 物理 |
| CH2 | 通道2 | C4-P4 (C-D) | 物理 |
| CH3 | 通道3 | P3-P4 (B-C) | 物理 |
| CH4 | 通道4 | C3-C4 (A-D) | **计算** |

### 1.3 NIRS 采集参数

```yaml
nirs:
  channels: 6                       # 组织氧饱和度通道数
  sample_rate: 1-4 Hz              # 低速采样
  value_range: [0, 100]            # 百分比
  display_groups: 3                 # 显示分组
```

### 1.4 数据包结构

```c
// 数据包格式 (40 bytes)
struct EEGPacket {
    uint8_t  header[2];     // 0xAA 0x55
    int16_t  data[18];      // 36 bytes payload
    uint16_t crc;           // 校验和
};

// data[] 字段映射
// data[0]  = EEG CH1 raw
// data[1]  = EEG CH2 raw  
// data[2]  = EEG CH3 raw
// data[3]  = aEEG GS histogram bin (CH1)
// data[4]  = aEEG GS histogram bin (CH2)
// data[9]  = Config word
// data[16] = GS counter (0-229有效, 255=忽略)
```

---

## 2. 滤波器规格（多档位）

### 2.1 滤波器选项总览

| 滤波器类型 | 可选值 | 默认值 | 阶数 |
|-----------|--------|--------|------|
| 高通 (HPF) | 0.3, 0.5, 1.5 Hz | 0.5 Hz | 2 |
| 低通 (LPF) | 15, 35, 50, 70 Hz | 35 Hz | 4 |
| 陷波 (Notch) | 50, 60 Hz | 50 Hz | 2 |

### 2.2 高通滤波器（Butterworth IIR）

**设计参数**：
- 类型: Butterworth
- 阶数: 2
- 采样率: 160 Hz

**系数表（SOS格式，double精度）**：

```yaml
HPF_0.3Hz:
  # Normalized frequency: 0.3 / 80 = 0.00375
  sos:
    - [1.0, -2.0, 1.0, 1.0, -1.98222644, 0.98237771]
  gain: 0.99117852

HPF_0.5Hz:
  # Normalized frequency: 0.5 / 80 = 0.00625  
  sos:
    - [1.0, -2.0, 1.0, 1.0, -1.97037449, 0.97072991]
  gain: 0.98526618

HPF_1.5Hz:
  # Normalized frequency: 1.5 / 80 = 0.01875
  sos:
    - [1.0, -2.0, 1.0, 1.0, -1.91119707, 0.91497583]
  gain: 0.95573826
```

### 2.3 低通滤波器（Butterworth IIR）

**设计参数**：
- 类型: Butterworth
- 阶数: 4
- 采样率: 160 Hz

**系数表（SOS格式，double精度）**：

```yaml
LPF_15Hz:
  # Normalized frequency: 15 / 80 = 0.1875
  sos:
    - [1.0, 2.0, 1.0, 1.0, -0.87727063, 0.42650599]
    - [1.0, 2.0, 1.0, 1.0, -0.63208028, 0.17953611]
  gain: 0.02952402

LPF_35Hz:
  # Normalized frequency: 35 / 80 = 0.4375
  sos:
    - [1.0, 2.0, 1.0, 1.0, -0.17016047, 0.39433849]
    - [1.0, 2.0, 1.0, 1.0, 0.08621025, 0.03716562]
  gain: 0.09515069

LPF_50Hz:
  # Normalized frequency: 50 / 80 = 0.625
  sos:
    - [1.0, 2.0, 1.0, 1.0, 0.29618463, 0.55408880]
    - [1.0, 2.0, 1.0, 1.0, 0.63218069, 0.06278633]
  gain: 0.21324752

LPF_70Hz:
  # Normalized frequency: 70 / 80 = 0.875
  sos:
    - [1.0, 2.0, 1.0, 1.0, 1.18063222, 0.79591209]
    - [1.0, 2.0, 1.0, 1.0, 1.49417377, 0.53914284]
  gain: 0.53784561
```

### 2.4 陷波滤波器（IIR Notch）

**设计参数**：
- 类型: IIR Notch (二阶)
- Q 因子: 30
- 采样率: 160 Hz

**系数表（SOS格式，double精度）**：

```yaml
Notch_50Hz:
  # Center frequency: 50 Hz
  # Bandwidth: 50/30 = 1.67 Hz
  sos:
    - [0.98968574, -0.22460795, 0.98968574, 1.0, -0.22460795, 0.97937149]
  gain: 1.0

Notch_60Hz:
  # Center frequency: 60 Hz
  # Bandwidth: 60/30 = 2.0 Hz
  sos:
    - [0.98968574, 0.22460795, 0.98968574, 1.0, 0.22460795, 0.97937149]
  gain: 1.0
```

### 2.5 滤波器处理链

```
Raw EEG (160 Hz)
     │
     ▼
┌─────────────┐
│ Notch Filter│  ← 50/60 Hz 可选
│  (IIR 2阶)  │
└─────────────┘
     │
     ▼
┌─────────────┐
│  Highpass   │  ← 0.3/0.5/1.5 Hz 可选
│  (IIR 2阶)  │
└─────────────┘
     │
     ▼
┌─────────────┐
│  Lowpass    │  ← 15/35/50/70 Hz 可选
│  (IIR 4阶)  │
└─────────────┘
     │
     ▼
 Filtered EEG
```

---

## 3. aEEG 算法规格

### 3.0 医学意义澄清（v1.2 冻结）

> ⚠️ **重要声明**：aEEG 处理流程严格遵循医学定义，不允许以工程优化为目的替换或简化。

**标准处理流程**：
```
带通滤波(2-15Hz) → 整流/绝对值 → 统计/包络/直方图(15s GS) → 半对数映射显示
```

**关键约束**：

| 规则 | 说明 |
|------|------|
| ❌ **aEEG ≠ RMS** | RMS是能量度量，aEEG是振幅分布包络，二者**不等价** |
| ❌ 禁止RMS替代 | 禁止用RMS替代或近似aEEG主显示 |
| ❌ 禁止自定义变换 | 不允许引入未在医学文献或设备规格中定义的变换 |
| ✅ 临床优先 | 输出结果必须以**临床判读一致性**优先于计算效率 |
| ✅ 15秒GS直方图 | 15秒GS直方图是aEEG趋势显示的**核心数据源** |

**违规处理**：任何违反上述医学定义的实现，即使技术上可行，也**必须被拒绝**。

### 3.1 aEEG 处理流程

```
Filtered EEG (160 Hz)
     │
     ▼
┌─────────────────┐
│ Bandpass Filter │  2-15 Hz (Butterworth 4阶)
│   (IIR 4阶)     │
└─────────────────┘
     │
     ▼
┌─────────────────┐
│  Half-Wave      │  y = |x|
│  Rectification  │
└─────────────────┘
     │
     ▼
┌─────────────────┐
│ Peak Detection  │  0.5秒窗口内最大值
│  (Envelope)     │
└─────────────────┘
     │
     ▼
┌─────────────────┐
│   Smoothing     │  15秒移动平均
│   (Moving Avg)  │
└─────────────────┘
     │
     ▼
┌─────────────────┐
│  Min/Max        │  每秒输出上下边界
│  Extraction     │
└─────────────────┘
     │
     ▼
 aEEG Output (1 Hz)
```

### 3.2 aEEG 带通滤波器

> ⚠️ **v2.3 变更**: 阶数从 4 修正为 **6**（HPF 2阶 + LPF 4阶）
>
> **修正原因**:
> - §2.2 所有 HPF 均为 2 阶设计
> - §2.3 所有 LPF 均为 4 阶设计
> - 复用已验证的 §2.3 LPF_15Hz 系数，确保高频抑制一致性
> - 原 "order: 4" 与设计模式冲突，实际级联为 2+4=6 阶

```yaml
AEEG_Bandpass:
  type: Butterworth
  order: 6                        # ⚠️ v2.3 修正: 原为4，改为6
  low_cutoff: 2 Hz
  high_cutoff: 15 Hz
  sample_rate: 160 Hz

  # 级联实现:
  #   HPF(2Hz, 2阶) → LPF(15Hz, 4阶)
  #   总阶数: 2 + 4 = 6

  components:
    hpf:
      cutoff: 2 Hz
      order: 2                    # 1 个 SOS 节
      coefficients: §3.2.1        # 见下方
    lpf:
      cutoff: 15 Hz
      order: 4                    # 2 个 SOS 节
      coefficients: §2.3 LPF_15Hz # 复用已有系数
```

### 3.2.1 HPF 2Hz 系数（aEEG 专用）

> ✅ **状态**: 已补充 (2026-01-28)

```yaml
HPF_2Hz:
  # 设计参数: Butterworth 2阶, fc=2Hz, fs=160Hz
  # Normalized frequency: 2.0 / 80 = 0.025
  sos:
    - [1.0, -2.0, 1.0, 1.0, -1.88910739, 0.89490251]
  gain: 0.94597746
```

### 3.3 aEEG 输出格式

```yaml
aeeg_output:
  rate: 1 Hz                    # 每秒输出一对值
  values:
    - min_uV: float            # 下边界 (μV)
    - max_uV: float            # 上边界 (μV)
  
  # GS 直方图格式 (15秒周期)
  histogram:
    bins: 230                   # 0-229
    range: [0, 200]            # μV
    interval: 15               # 秒
    counter_values:
      ignore: 255              # 忽略此包
      end_of_cycle: 229        # 15秒周期结束
```

---

## 4. 回放与离线滤波

### 4.1 Zero-Phase 滤波

```yaml
zerophase_filter:
  method: forward-backward (filtfilt)
  latency: 0                    # 零延迟
  use_case: 历史回放模式
  
  # 注意：需要完整数据块，不能实时使用
  minimum_block_size: 1600     # 10秒 @ 160Hz
```

### 4.2 处理模式切换

| 模式 | 滤波方法 | 延迟 | 适用场景 |
|------|---------|------|---------|
| 实时监测 | Causal IIR | 有 | 在线显示 |
| 历史回放 | Zero-Phase | 无 | 回放分析 |
| 段落导出 | Zero-Phase | 无 | 数据导出 |

---

## 5. LOD 金字塔（160Hz 版本）

### 5.1 基本参数

```yaml
lod:
  sample_rate: 160 Hz
  max_duration: 72 hours
  max_samples: 41,472,000       # 160 × 3600 × 72
  max_levels: 26                # ceil(log2(41472000))
  
  downsampling:
    method: min_max_preserve    # 保留极值
    factor: 2                   # 每层2倍降采样
```

### 5.2 层级定义

| 层级 | 分辨率 | 样本数/72h | 用途 |
|------|--------|-----------|------|
| L0 | 160 Hz | 41,472,000 | 原始数据 |
| L1 | 80 Hz | 20,736,000 | 高倍放大 |
| L2 | 40 Hz | 10,368,000 | 分钟级查看 |
| L3 | 20 Hz | 5,184,000 | 5分钟查看 |
| L4 | 10 Hz | 2,592,000 | 15分钟查看 |
| L5 | 5 Hz | 1,296,000 | 30分钟查看 |
| L6 | 2.5 Hz | 648,000 | 1小时查看 |
| L7 | 1.25 Hz | 324,000 | 3小时查看 |
| L8 | 0.625 Hz | 162,000 | 全程概览 |

### 5.3 LOD 选择算法

```python
def select_lod_level(view_width_px, time_range_us, sample_rate=160):
    """
    选择合适的LOD层级
    
    Args:
        view_width_px: 视口宽度（像素）
        time_range_us: 时间范围（微秒）
        sample_rate: 采样率（Hz）
    """
    time_range_sec = time_range_us / 1_000_000
    samples_needed = time_range_sec * sample_rate
    samples_per_pixel = samples_needed / view_width_px
    
    # 目标：每像素2-4个样本
    level = 0
    while samples_per_pixel > 4 and level < 8:
        samples_per_pixel /= 2
        level += 1
    
    return level
```

---

## 6. 伪迹检测与处理（160Hz 版本）

### 6.1 数据间隙 (Gap)

```yaml
gap_detection:
  sample_rate: 160 Hz
  
  # 可插值的最大间隙
  interpolate_max_samples: 4    # 25ms (160 × 0.025)
  interpolate_max_ms: 25
  interpolate_method: linear
  
  # 强制遮罩的最小间隙
  mask_min_samples: 5           # >25ms
  mask_display: broken_line     # 断裂显示
```

### 6.2 信号饱和 (Clipping)

```yaml
clipping_detection:
  threshold_uV: 2400            # 接近满量程
  threshold_ratio: 0.96         # 96% 满量程
  
  # 忽略的最大饱和
  ignore_max_samples: 3         # ~19ms
  
  # 标记的最小饱和
  mark_min_samples: 4           # ~25ms
  mark_display: red_bar         # 红色标记
```

### 6.3 离群值 (Outlier)

```yaml
outlier_detection:
  # 基于 MAD 的阈值
  mad_threshold: 10
  window_samples: 160           # 1秒窗口
  
  # 可替换的最大离群
  replace_max_samples: 1        # 单点
  replace_method: neighbor_avg
  
  # 遮罩的最小离群
  mask_min_samples: 8           # ~50ms
  mask_display: gray_shade
```

### 6.4 伪迹处理优先级

```
1. Gap ≤ 4样本 → 线性插值
2. Gap > 4样本 → 断裂显示 + 遮罩
3. Clip ≤ 3样本 → 忽略
4. Clip > 3样本 → 红色标记
5. Outlier = 1样本 → 邻近平均替换
6. Outlier ≥ 8样本 → 灰色遮罩
```

---

## 7. 滤波器预热

### 7.1 预热时间计算（160Hz）

```yaml
warmup:
  # 基于截止频率的预热时间
  HPF_0.3Hz: 10.0 sec           # 3 / 0.3 = 10s
  HPF_0.5Hz: 6.0 sec            # 3 / 0.5 = 6s
  HPF_1.5Hz: 2.0 sec            # 3 / 1.5 = 2s
  
  LPF_15Hz: 0.2 sec             # 3 / 15 = 0.2s
  LPF_35Hz: 0.086 sec           # 3 / 35 = 0.086s
  LPF_50Hz: 0.06 sec            # 3 / 50 = 0.06s
  LPF_70Hz: 0.043 sec           # 3 / 70 = 0.043s
  
  Notch: 0.1 sec                # 固定
  
  # 组合滤波器预热（取最大值）
  default_chain: 6.0 sec        # HPF_0.5 + LPF_35 + Notch
```

### 7.2 预热样本数

```yaml
warmup_samples:
  HPF_0.3Hz: 1600               # 10.0 × 160
  HPF_0.5Hz: 960                # 6.0 × 160
  HPF_1.5Hz: 320                # 2.0 × 160
  default_chain: 960            # 6秒 × 160Hz
```

---

## 8. 接口定义

### 8.1 滤波器设置接口

```csharp
public interface IFilterSettings
{
    // 高通滤波器
    FilterOption Highpass { get; set; }  // Hz_0_3, Hz_0_5, Hz_1_5
    
    // 低通滤波器
    FilterOption Lowpass { get; set; }   // Hz_15, Hz_35, Hz_50, Hz_70
    
    // 陷波滤波器
    FilterOption Notch { get; set; }     // Hz_50, Hz_60, Off
    
    event EventHandler<FilterChangedEventArgs> FilterChanged;
}

public enum FilterOption
{
    // Highpass
    Hz_0_3,
    Hz_0_5,
    Hz_1_5,
    
    // Lowpass
    Hz_15,
    Hz_35,
    Hz_50,
    Hz_70,
    
    // Notch
    Hz_50,
    Hz_60,
    Off
}
```

### 8.2 DSP 处理接口

```csharp
public interface IDspProcessor
{
    /// <summary>
    /// 处理单个样本（实时模式）
    /// </summary>
    double ProcessSample(int channelIndex, double sample);
    
    /// <summary>
    /// 处理数据块（批量/回放模式）
    /// </summary>
    Span<double> ProcessBlock(int channelIndex, ReadOnlySpan<double> samples, bool zeroPhase);
    
    /// <summary>
    /// 重置滤波器状态
    /// </summary>
    void Reset(int channelIndex);
    
    /// <summary>
    /// 获取预热完成状态
    /// </summary>
    bool IsWarmedUp(int channelIndex);
    
    /// <summary>
    /// 当前滤波器设置
    /// </summary>
    IFilterSettings Settings { get; }
}
```

---

## 9. 验证要求

### 9.1 滤波器验证

| 测试项 | 方法 | 通过标准 |
|--------|------|---------|
| 频率响应 | 扫频信号 | -3dB @ 截止频率 ±5% |
| 相位线性度 | 脉冲响应 | Zero-Phase 模式延迟 < 1样本 |
| 稳定性 | 72h 连续运行 | 无溢出、无漂移 |
| 系数精度 | double vs float | 相对误差 < 1e-10 |

### 9.2 aEEG 验证

| 测试项 | 方法 | 通过标准 |
|--------|------|---------|
| 频带响应 | 2-15Hz 正弦波 | 通带内平坦 ±1dB |
| 包络提取 | 已知波形 | 峰值误差 < 5% |
| 时间对齐 | 标记事件 | 对齐误差 < 100ms |

---

## 附录：滤波器系数计算代码

```python
from scipy.signal import butter, iirnotch, sos2tf
import numpy as np

def design_filters(fs=160):
    """计算所有滤波器系数"""
    filters = {}
    
    # 高通滤波器
    for fc in [0.3, 0.5, 1.5]:
        sos = butter(2, fc, btype='high', fs=fs, output='sos')
        filters[f'HPF_{fc}Hz'] = sos
    
    # 低通滤波器
    for fc in [15, 35, 50, 70]:
        sos = butter(4, fc, btype='low', fs=fs, output='sos')
        filters[f'LPF_{fc}Hz'] = sos
    
    # 陷波滤波器
    for fc in [50, 60]:
        b, a = iirnotch(fc, Q=30, fs=fs)
        # 转换为SOS格式
        filters[f'Notch_{fc}Hz'] = np.array([[b[0], b[1], b[2], 1.0, a[1], a[2]]])
    
    return filters

# 使用示例
# filters = design_filters(160)
# print(filters['HPF_0.5Hz'])
```

---

**文档结束**
