# Handoff: S1-05 EEG Waveform Rendering

> **Sprint**: S1
> **Task**: S1-05
> **创建日期**: 2026-01-28
> **状态**: ⚠️ 已被 S2-05 替代

---

## ⚠️ 重要说明

**此 Handoff 已被 S2-05 替代。**

S2-05 实现了新的渲染架构（预处理 + 渲染分离），严格遵循铁律6。

请参阅: [waveform-rendering-api.md](./waveform-rendering-api.md)

已删除文件:
- `src/Rendering/EEG/EegWaveformRenderer.cs` (包含 O(N) 循环，违反铁律6)

替代组件:
- `PolylineBuilder` (预处理阶段)
- `EegPolylineRenderer` (渲染阶段，只做 Draw)

---

## 以下为历史记录（仅供参考）

---

## 1. 概述

本任务实现了 EEG 波形渲染功能，在 Content Layer 中绘制 4 通道 EEG 波形。

核心特性:
- 4 通道 EEG 波形渲染
- μs 时间戳到 X 轴像素映射
- μV 电压值到 Y 轴像素映射
- 时间间隙检测与断线处理
- QualityFlag/NaN 处理
- 饱和标记显示

---

## 2. 文件清单

| 文件 | 用途 |
|------|------|
| `src/Rendering/EEG/EegColorPalette.cs` | 通道颜色定义 |
| `src/Rendering/EEG/EegChannelView.cs` | 通道视图配置 |
| `src/Rendering/EEG/EegWaveformRenderer.cs` | 波形渲染器 |
| `src/Rendering/Layers/ContentLayer.cs` | 内容层（已更新） |
| `tests/Rendering.Tests/EEG/EegColorPaletteTests.cs` | 颜色测试 |
| `tests/Rendering.Tests/EEG/EegChannelViewTests.cs` | 通道视图测试 |
| `tests/Rendering.Tests/EEG/EegWaveformRendererTests.cs` | 渲染器测试 |

---

## 3. 架构设计

```
┌─────────────────────────────────────────────────────────────────┐
│  ContentLayer (Layer 2)                                         │
│  ├─ 检查 RenderContext.Channels 是否有数据                       │
│  │   ├─ 有数据 → EegWaveformRenderer.Render()                   │
│  │   └─ 无数据 → 绘制占位波形                                    │
└─────────────────────────────────────────────────────────────────┘
         │
         ▼
┌─────────────────────────────────────────────────────────────────┐
│  EegWaveformRenderer                                            │
│  ├─ 遍历 4 通道                                                  │
│  │   ├─ DrawChannelBackground() - 绘制通道背景                   │
│  │   ├─ DrawBaseline() - 绘制基线                               │
│  │   └─ DrawWaveform() - 绘制波形                               │
│  │       ├─ 检查 NaN/QualityFlag → 跳过                         │
│  │       ├─ 检查时间间隙 > 25ms → 断线 + 遮罩                     │
│  │       ├─ 检查饱和标志 → 红色标记                              │
│  │       └─ 绘制正常线段                                        │
└─────────────────────────────────────────────────────────────────┘
         │
         ▼
┌─────────────────────────────────────────────────────────────────┐
│  EegChannelView (每通道配置)                                     │
│  ├─ UvToY() - μV 到像素 Y 坐标映射                               │
│  ├─ YToUv() - 像素 Y 坐标到 μV 映射                               │
│  ├─ BaselineY - 基线位置                                        │
│  └─ Color - 通道颜色 (来自 EegColorPalette)                      │
└─────────────────────────────────────────────────────────────────┘
```

---

## 4. 组件 API

### 4.1 EegColorPalette

```csharp
public static class EegColorPalette
{
    // 通道颜色
    static readonly Color4 Channel1;  // 绿色
    static readonly Color4 Channel2;  // 蓝色
    static readonly Color4 Channel3;  // 橙色
    static readonly Color4 Channel4;  // 紫色

    // 特殊标记颜色
    static readonly Color4 GapMask;          // 间隙遮罩（灰色半透明）
    static readonly Color4 SaturationMarker; // 饱和标记（红色）
    static readonly Color4 ChannelBackground;
    static readonly Color4 Baseline;

    // 按索引获取颜色
    static Color4 GetChannelColor(int channelIndex);
}
```

### 4.2 EegChannelView

```csharp
public readonly record struct EegChannelView
{
    int ChannelIndex { get; init; }
    string ChannelName { get; init; }
    Color4 Color { get; init; }
    float YOffset { get; init; }
    float Height { get; init; }
    float UvToPixelScale { get; init; }
    float BaselineY { get; }
    bool IsVisible { get; init; }
    float LineWidth { get; init; }

    // 坐标转换
    float UvToY(double uv);
    double YToUv(float y);
    bool ContainsY(float y);

    // 工厂方法
    static EegChannelView CreateDefault(int channelIndex, float yOffset, float height, float dpiScale = 1.0f);
}
```

### 4.3 EegWaveformRenderer

```csharp
public sealed class EegWaveformRenderer
{
    // 渲染波形
    void Render(ID2D1DeviceContext context, ResourceCache resources, RenderContext renderContext);

    // 重置缓存
    void Invalidate();
}
```

---

## 5. 数据流

```
ITimeSeriesSource<EegSample>
         │
         │ SampleReceived 事件
         ▼
┌─────────────────────┐
│  数据采集线程        │
│  ├─ EegSample       │
│  │   ├─ TimestampUs │
│  │   ├─ Ch1Uv~Ch4Uv │
│  │   └─ QualityFlags│
└─────────────────────┘
         │
         │ (由 DSP 线程预处理，填充到 RenderContext)
         ▼
┌─────────────────────┐
│  RenderContext      │
│  ├─ Channels[]      │
│  │   ├─ DataPoints  │ ← float[] (μV 值)
│  │   ├─ QualityFlags│ ← byte[]
│  │   └─ Timestamps  │ ← 起始时间 + 间隔
│  ├─ VisibleRange    │
│  └─ ViewportSize    │
└─────────────────────┘
         │
         │ (渲染线程读取)
         ▼
┌─────────────────────┐
│  EegWaveformRenderer│
│  └─ DrawLine()      │ ← 只做 Draw 调用（铁律6）
└─────────────────────┘
```

---

## 6. 间隙处理规则

依据 ADR-005:

| 间隙大小 | 处理方式 | 说明 |
|---------|---------|------|
| ≤ 4 样本 (25ms) | 可选插值 | 必须标记 `Interpolated` |
| > 4 样本 (25ms) | 强制断线 | 绘制灰色遮罩 |

```csharp
// 间隙检测逻辑
const long MaxGapUs = 4 * 6250;  // 25000 μs (25ms)

bool hasGap = (currentTimestampUs - lastTimestampUs) > MaxGapUs;
if (hasGap) {
    DrawGapMask(...);  // 灰色遮罩
    lastPoint = null;  // 断开线段
}
```

---

## 7. 质量标志处理

| QualityFlag | 行为 |
|-------------|------|
| `Normal` | 正常渲染 |
| `Missing` | 不渲染，绘制缺失标记 |
| `Saturated` | 渲染但用红色标记 |
| `LeadOff` | 不渲染 |
| `Interpolated` | 正常渲染（已插值数据有效） |
| `Undocumented` | 不渲染 |

```csharp
// 质量检查逻辑
bool isValidSample = !float.IsNaN(value) &&
    (quality & (QualityFlag.Missing | QualityFlag.LeadOff | QualityFlag.Undocumented)) == 0;

bool isSaturated = (quality & QualityFlag.Saturated) != 0;
```

---

## 8. 约束

| 约束 | 实现 |
|------|------|
| ✅ 读取 ITimeSeriesSource | 通过 RenderContext.Channels |
| ✅ μs 时间戳映射 X | RenderContext.TimestampToX() |
| ✅ μV 值映射 Y | EegChannelView.UvToY() |
| ✅ 4 通道独立渲染 | 遍历 channels[0..3] |
| ✅ 间隙断线 | > 25ms 断开 |
| ✅ NaN/QualityFlag 处理 | 跳过无效样本 |
| ❌ 无 DSP/滤波 | 未实现任何滤波 |
| ❌ 无缩放/LOD | 未实现 LOD |
| ❌ 无包络/RMS | 未实现统计 |
| ❌ 无 UI 交互 | 未实现交互 |

---

## 9. 测试覆盖

| 测试文件 | 覆盖内容 |
|----------|----------|
| `EegColorPaletteTests.cs` | 颜色定义、通道区分、半透明度 |
| `EegChannelViewTests.cs` | 坐标转换、通道名称、DPI 缩放 |
| `EegWaveformRendererTests.cs` | 间隙检测、质量标志、4通道独立 |

测试结果: **51 通过, 0 失败**

---

## 10. 证据引用

| 实现 | 依据文档 | 章节 |
|------|----------|------|
| EEG 参数 | CONSENSUS_BASELINE.md | §6.1, §6.2 |
| 时间戳规则 | CONSENSUS_BASELINE.md | §5.1, §5.3 |
| 间隙处理 | DECISIONS.md | ADR-005 |
| 质量标志 | 00_CONSTITUTION.md | 铁律5 |
| 渲染约束 | 00_CONSTITUTION.md | 铁律6 |
| 不伪造波形 | 00_CONSTITUTION.md | 铁律2 |
| 三层架构 | ARCHITECTURE.md | §5 |

---

## 11. 依赖关系

### 前置依赖
- S1-01: 核心接口（ITimeSeriesSource, EegSample, QualityFlag）
- S1-03: Vortice 渲染底座（D2DRenderTarget, ResourceCache）
- S1-04: 三层渲染框架（ContentLayer, RenderContext）

### 被依赖
- S1-06: 系统集成（连接真实数据源）
- S2-xx: DSP 滤波链（滤波后数据传递给渲染器）

---

## 12. 使用示例

### 12.1 自动渲染（通过 ContentLayer）

```csharp
// 当 RenderContext 包含 EEG 数据时，ContentLayer 自动使用 EegWaveformRenderer
var renderContext = new RenderContext
{
    ViewportWidth = 1920,
    ViewportHeight = 1080,
    VisibleRange = new TimeRange(0, 10_000_000),  // 10秒
    Channels = eegChannelData  // 由数据管道填充
};

// LayeredRenderer 会调用 ContentLayer.Render()
// ContentLayer 检测到 Channels 非空，使用 EegWaveformRenderer
layeredRenderer.RenderFrame(context, resources, renderContext);
```

### 12.2 直接使用渲染器

```csharp
// 直接使用 EegWaveformRenderer（如需自定义场景）
var renderer = new EegWaveformRenderer();

// 在渲染循环中
renderer.Render(d2dContext, resourceCache, renderContext);

// 视口变化时
renderer.Invalidate();
```

---

## 13. 自检清单

- [x] 连续时间戳 → 绘制连续线段
- [x] 时间间隙 > 25ms → 断开线段 + 灰色遮罩
- [x] NaN 值 → 不绘制
- [x] QualityFlag.Missing → 不绘制
- [x] QualityFlag.LeadOff → 不绘制
- [x] QualityFlag.Saturated → 红色标记
- [x] 4 通道独立显示
- [x] 无 DSP/滤波代码
- [x] 无 LOD/缩放代码
- [x] 无包络/RMS 代码
- [x] 无 UI 交互代码

---

**文档结束**
