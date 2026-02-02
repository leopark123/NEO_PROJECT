# aEEG 显示映射 API 文档

> **任务**: S2-04
> **创建日期**: 2026-01-28
> **依据**: DSP_SPEC.md, CONSENSUS_BASELINE.md §6.4

---

## 1. 概述

本文档描述 aEEG 显示映射层的 API 接口和使用方式。

**这是显示映射，不是信号处理。**

```
⚠️ 重要声明：
- 不产生新数据
- 不修改 GS
- 不解释医学含义
- 这是 View 层职责
```

---

## 2. 冻结规格（不可修改）

> ⚠️ 以下规格严格来源于医学标准，禁止修改

### 2.1 Y 轴结构（半对数）

| 参数 | 值 |
|------|-----|
| 显示范围 | 0-200 μV |
| 线性段 | 0-10 μV |
| 对数段 | 10-200 μV |
| 分界点 | **10 μV**（医学冻结） |

### 2.2 高度分配

| 段 | 高度占比 |
|-----|---------|
| 线性段 (0-10 μV) | **50%** (下半区) |
| 对数段 (10-200 μV) | **50%** (上半区) |

### 2.3 标准刻度（固定，不可增删）

| 刻度值 (μV) | 类型 |
|------------|------|
| 0 | 主刻度 |
| 1 | 次刻度 |
| 2 | 次刻度 |
| 3 | 次刻度 |
| 4 | 次刻度 |
| 5 | 主刻度 |
| 10 | **主刻度（分界点）** |
| 25 | 次刻度 |
| 50 | 主刻度 |
| 100 | 主刻度 |
| 200 | 主刻度 |

---

## 3. 禁止事项

```
❌ 对 GS bin 做任何"视觉平滑"
❌ 做 anti-alias 数据插值
❌ 根据屏幕比例改变线性/对数分界
❌ "自动适配"不同设备
❌ 修改 GS 直方图
❌ 新增或删除刻度
```

**S2-04 不允许"设计感"。**

---

## 4. 核心类型

### 4.1 AeegSemiLogMapper (映射器)

```csharp
namespace Neo.Rendering.Mapping;

/// <summary>
/// aEEG 半对数 Y 轴映射器。
/// </summary>
public sealed class AeegSemiLogMapper
{
    // 冻结常量
    public const double MinVoltageUv = 0.0;
    public const double MaxVoltageUv = 200.0;
    public const double LinearLogBoundaryUv = 10.0;
    public const double LinearHeightRatio = 0.5;
    public const double LogHeightRatio = 0.5;

    // 属性
    public double TotalHeightPx { get; }
    public double LinearHeightPx { get; }
    public double LogHeightPx { get; }

    // 构造函数
    public AeegSemiLogMapper(double totalHeightPx);

    // 映射方法（纯函数）
    public double MapVoltageToY(double voltageUv);
    public double MapYToVoltage(double y);

    // 静态便捷方法
    public static double GetY(double voltageUv, double totalHeightPx);
}
```

### 4.2 AeegAxisTick (刻度结构)

```csharp
/// <summary>
/// aEEG Y 轴刻度信息。
/// </summary>
public readonly struct AeegAxisTick
{
    public double VoltageUv { get; init; }
    public double Y { get; init; }
    public string Label { get; init; }
    public bool IsMajor { get; init; }
}
```

### 4.3 AeegAxisTicks (刻度生成器)

```csharp
/// <summary>
/// aEEG Y 轴刻度生成器。
/// </summary>
public static class AeegAxisTicks
{
    // 固定刻度值
    public static readonly double[] StandardTicksUv;  // 11 个刻度
    public static readonly double[] MajorTicksUv;     // 6 个主刻度
    public const int TickCount = 11;

    // 生成方法
    public static AeegAxisTick[] GetTicks(double totalHeightPx);
    public static double GetTickY(double voltageUv, double totalHeightPx);
    public static double GetBoundaryY(double totalHeightPx);
    public static string FormatTickLabel(double voltageUv);
    public static bool IsMajorTick(double voltageUv);
}
```

---

## 5. 映射规则

### 5.1 线性段映射 (0-10 μV)

```
Y = totalHeight - (μV / 10) × linearHeight
```

| μV | Y (totalHeight=1000) |
|----|----------------------|
| 0 | 1000 (底部) |
| 5 | 750 |
| 10 | 500 (中间) |

### 5.2 对数段映射 (10-200 μV)

```
Y = logHeight × (1 - (log10(μV) - 1) / logRange)

其中:
- logRange = log10(200) - 1 ≈ 1.301
```

| μV | Y (totalHeight=1000) |
|----|----------------------|
| 10 | 500 (中间) |
| 25 | ~346 |
| 50 | ~231 |
| 100 | ~115 |
| 200 | 0 (顶部) |

### 5.3 无效值处理

| 输入 | 输出 |
|------|------|
| NaN | NaN |
| 负值 | NaN |
| > 200 μV | clamp 到 0 (顶部) |

---

## 6. 使用示例

### 6.1 基本映射

```csharp
// 创建映射器
var mapper = new AeegSemiLogMapper(totalHeightPx: 1000);

// 映射电压到 Y 坐标
double y = mapper.MapVoltageToY(50.0);  // ~231

// 逆映射
double voltage = mapper.MapYToVoltage(500.0);  // 10.0
```

### 6.2 获取刻度

```csharp
// 获取所有刻度
AeegAxisTick[] ticks = AeegAxisTicks.GetTicks(totalHeightPx: 1000);

// 绘制刻度
foreach (var tick in ticks)
{
    DrawTickLine(tick.Y, tick.Label, tick.IsMajor);
}
```

### 6.3 与 GS 显示集成

```csharp
// GsFrame 来自 S2-03
GsFrame gsFrame = ...;

// 为每个 bin 计算 Y 位置
for (int bin = 0; bin < GsFrame.BinCount; bin++)
{
    // 获取 bin 对应的电压
    double voltage = GsBinMapper.GetBinCenterVoltage(bin);

    // 映射到 Y 坐标
    double y = mapper.MapVoltageToY(voltage);

    // 绘制（灰度由 bin 值决定）
    byte intensity = gsFrame.Bins[bin];
    DrawGsPixel(x, y, intensity);
}
```

---

## 7. 坐标系约定

```
Y = 0        → 200 μV (顶部)
Y = height/2 → 10 μV (分界点)
Y = height   → 0 μV (底部)

  ┌─────────────────────┐  Y = 0 (200 μV)
  │                     │
  │    对数段           │
  │    10-200 μV        │
  │    (上半区)         │
  │                     │
  ├─────────────────────┤  Y = height/2 (10 μV)
  │                     │
  │    线性段           │
  │    0-10 μV          │
  │    (下半区)         │
  │                     │
  └─────────────────────┘  Y = height (0 μV)
```

---

## 8. 纯函数保证

```
⚠️ 映射函数是纯函数：

1. 相同输入 → 相同输出
2. 无副作用
3. 无状态依赖（除构造时的 totalHeightPx）

这意味着：
- 可并行调用
- 可缓存结果
- UI 缩放只改变 totalHeightPx，不改变映射逻辑
```

---

## 9. 文件清单

```
src/Rendering/Mapping/
├── AeegSemiLogMapper.cs   # 半对数映射器
└── AeegAxisTicks.cs       # Y 轴刻度定义

tests/Rendering.Tests/Mapping/
├── SemiLogLinearSegmentTests.cs  # 线性段测试
├── SemiLogLogSegmentTests.cs     # 对数段测试
├── BoundaryMappingTests.cs       # 边界测试
└── TickPositionTests.cs          # 刻度测试
```

---

## 10. 测试覆盖

| 测试类别 | 测试数 | 说明 |
|---------|-------|------|
| 线性段映射 | 16 | 0-10 μV → 下半区 |
| 对数段映射 | 14 | 10-200 μV → 上半区 |
| 边界处理 | 22 | 分界点、无效值 |
| 刻度位置 | 20 | 固定刻度验证 |
| **总计** | 72 | - |

---

## 11. 与其他模块关系

```
                        ┌─────────────────┐
                        │ S2-02 aEEG处理链 │
                        └────────┬────────┘
                                 │ AeegOutput (1Hz min/max)
                                 ▼
                        ┌─────────────────┐
                        │ S2-03 GS直方图   │
                        └────────┬────────┘
                                 │ GsFrame (15s bins)
                                 ▼
┌─────────────────────────────────────────────────────────┐
│                    S2-04 显示映射                        │
│                                                         │
│  输入: μV 值 (来自 GsFrame 或 AeegOutput)               │
│  输出: Y 像素坐标                                       │
│                                                         │
│  ⚠️ 纯映射，不修改数据                                  │
└─────────────────────────────────────────────────────────┘
                                 │
                                 ▼
                        ┌─────────────────┐
                        │ 渲染层 (S4+)     │
                        │ 绘制 aEEG 趋势   │
                        └─────────────────┘
```

---

## 12. 已知限制（不做推断）

1. **分界点固定**: 10 μV 是医学冻结值，不可调整
2. **比例固定**: 上下各 50% 是标准，不可调整
3. **刻度固定**: 11 个刻度点不可增删
4. **无自适应**: 不根据屏幕/设备自动调整映射

---

**文档结束**
