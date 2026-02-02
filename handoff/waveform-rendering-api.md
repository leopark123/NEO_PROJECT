# Handoff: S2-05 EEG/aEEG Waveform Rendering Layer

> **Sprint**: S2
> **Task**: S2-05
> **创建日期**: 2026-01-28
> **状态**: 完成

---

## 1. 概述

本任务实现了 EEG/aEEG 波形渲染层，严格遵循铁律6（渲染线程只做 Draw）。

**核心架构：预处理 + 渲染分离**

```
预处理线程                          渲染线程
    │                                  │
    ├─ PolylineBuilder.Build()         │
    │      ↓                           │
    │  PolylineBuildResult             │
    │      ↓                           │
    ├─ 封装为 EegWaveformRenderData ───→ EegPolylineRenderer.Render()
    │                                  │    只做 Draw 调用
    │                                  │    无 O(N) 计算
    ├─ AeegSeriesBuilder.Build()       │    无大分配
    │      ↓                           │
    │  AeegSeriesBuildResult           │
    │      ↓                           │
    └─ 封装为 AeegTrendRenderData ────→ AeegTrendRenderer.Render()
                                       │    只做 Draw 调用
                                       │    无 O(N) 计算
                                       │    无大分配
```

---

## 2. 铁律6 合规性

### 2.1 预处理阶段（非渲染线程）

| 组件 | 职责 | O(N) 计算 |
|------|------|-----------|
| `PolylineBuilder` | 构建 EEG 折线数据 | ✅ 允许 |
| `AeegSeriesBuilder` | 构建 aEEG 趋势数据 | ✅ 允许 |

### 2.2 渲染阶段（渲染线程）

| 组件 | 职责 | O(N) 计算 | 大分配 |
|------|------|-----------|--------|
| `EegPolylineRenderer` | 绘制 EEG 波形 | ❌ 禁止 | ❌ 禁止 |
| `AeegTrendRenderer` | 绘制 aEEG 趋势 | ❌ 禁止 | ❌ 禁止 |

**渲染器只做：**
- 迭代预构建的段（Segments）
- 调用 `context.DrawLine()` / `context.FillRectangle()`
- 使用 `ResourceCache` 获取画刷（O(1)）

**渲染器禁止：**
- 遍历原始数据数组
- 创建新对象（如 HashSet、List）
- 计算坐标转换

---

## 3. 文件清单

### 3.1 EEG 渲染

| 文件 | 用途 |
|------|------|
| `src/Rendering/EEG/EegGainScaler.cs` | 增益缩放器 |
| `src/Rendering/EEG/PolylineBuilder.cs` | 折线段构建器（预处理阶段） |
| `src/Rendering/EEG/EegWaveformRenderData.cs` | 预构建渲染数据结构 |
| `src/Rendering/EEG/EegPolylineRenderer.cs` | 折线渲染器（只做 Draw） |

### 3.2 aEEG 渲染

| 文件 | 用途 |
|------|------|
| `src/Rendering/AEEG/AeegColorPalette.cs` | aEEG 颜色定义 |
| `src/Rendering/AEEG/AeegSeriesBuilder.cs` | aEEG 序列构建器（预处理阶段） |
| `src/Rendering/AEEG/AeegTrendRenderer.cs` | aEEG 趋势渲染器（只做 Draw） |
| `src/Rendering/AEEG/AeegGridAndAxisRenderer.cs` | aEEG 网格和轴线渲染器 |

### 3.3 测试

| 文件 | 用途 |
|------|------|
| `tests/Rendering.Tests/Waveform/EegGainScalerTests.cs` | 增益缩放器测试 |
| `tests/Rendering.Tests/Waveform/PolylineBuilderTests.cs` | 折线构建器测试 |
| `tests/Rendering.Tests/Waveform/AeegSeriesBuilderTests.cs` | aEEG 序列构建器测试 |
| `tests/Rendering.Tests/Waveform/AeegColorPaletteTests.cs` | aEEG 颜色测试 |

---

## 4. 组件 API

### 4.1 PolylineBuilder (预处理阶段)

```csharp
public sealed class PolylineBuilder
{
    // 在预处理线程调用
    public PolylineBuildResult Build(
        ReadOnlySpan<float> dataPoints,
        ReadOnlySpan<byte> qualityFlags,
        long startTimestampUs,
        long sampleIntervalUs,
        Func<long, float> timestampToX,
        Func<double, float> uvToY,
        long visibleStartUs,
        long visibleEndUs);
}

public sealed class PolylineBuildResult
{
    public Vector2[] Points { get; }        // 预计算的坐标
    public PolylineSegment[] Segments { get; } // 连续线段
    public GapInfo[] Gaps { get; }          // 间隙区域
    public int[] SaturationIndices { get; } // 饱和点索引（已排序）
}
```

### 4.2 EegPolylineRenderer (渲染阶段)

```csharp
public sealed class EegPolylineRenderer
{
    // 在渲染线程调用，只做 Draw
    public void Render(
        ID2D1DeviceContext context,
        ResourceCache resources,
        in EegWaveformRenderData renderData);
}

public readonly struct EegWaveformRenderData
{
    public EegChannelRenderData[] Channels { get; init; }
}
```

### 4.3 AeegSeriesBuilder (预处理阶段)

```csharp
public sealed class AeegSeriesBuilder
{
    // 在预处理线程调用
    public AeegSeriesBuildResult Build(
        ReadOnlySpan<float> minValues,
        ReadOnlySpan<float> maxValues,
        ReadOnlySpan<long> timestamps,
        ReadOnlySpan<byte> qualityFlags,
        AeegSemiLogMapper mapper,
        float renderAreaTop,
        Func<long, float> timestampToX,
        long visibleStartUs,
        long visibleEndUs);
}
```

### 4.4 AeegTrendRenderer (渲染阶段)

```csharp
public sealed class AeegTrendRenderer
{
    // 在渲染线程调用，只做 Draw
    public void Render(
        ID2D1DeviceContext context,
        ResourceCache resources,
        in AeegTrendRenderData renderData);
}

public readonly struct AeegTrendRenderData
{
    public AeegTrendPoint[] Points { get; init; }
    public AeegTrendSegment[] Segments { get; init; }
    public AeegGapInfo[] Gaps { get; init; }
    public Rect RenderArea { get; init; }
}
```

---

## 5. 使用示例

### 5.1 EEG 波形渲染

```csharp
// === 预处理线程 ===
var builder = new PolylineBuilder();

// 构建每个通道的数据
var channel0Data = builder.Build(
    dataPoints, qualityFlags,
    startTimestampUs, sampleIntervalUs,
    timestampToX, uvToY,
    visibleRange.StartUs, visibleRange.EndUs);

// 封装为渲染数据
var renderData = new EegWaveformRenderData
{
    Channels = [
        new EegChannelRenderData
        {
            ChannelIndex = 0,
            Points = channel0Data.Points,
            Segments = channel0Data.Segments,
            Gaps = channel0Data.Gaps,
            SaturationIndices = channel0Data.SaturationIndices,
            ChannelArea = channelArea,
            Color = EegColorPalette.Channel1,
            LineWidth = 1.5f,
            BaselineY = baselineY
        }
    ]
};

// === 渲染线程 ===
var renderer = new EegPolylineRenderer();
renderer.Render(context, resources, renderData);
// 只做 Draw，无 O(N) 计算
```

### 5.2 aEEG 趋势渲染

```csharp
// === 预处理线程 ===
var builder = new AeegSeriesBuilder();
var mapper = new AeegSemiLogMapper(areaHeight);

var buildResult = builder.Build(
    minValues, maxValues, timestamps, qualityFlags,
    mapper, renderAreaTop, timestampToX,
    visibleRange.StartUs, visibleRange.EndUs);

var renderData = new AeegTrendRenderData
{
    Points = buildResult.Points,
    Segments = buildResult.Segments,
    Gaps = buildResult.Gaps,
    RenderArea = renderArea
};

// === 渲染线程 ===
var renderer = new AeegTrendRenderer();
renderer.Render(context, resources, renderData);
// 只做 Draw，无 O(N) 计算
```

---

## 6. 增益设置规格

| 增益 | 值 (μV/cm) | 说明 |
|------|-----------|------|
| Gain10 | 10 | 最高灵敏度 |
| Gain20 | 20 | 高灵敏度 |
| Gain50 | 50 | **默认值** |
| Gain70 | 70 | 标准 |
| Gain100 | 100 | 标准 |
| Gain200 | 200 | 低灵敏度 |
| Gain1000 | 1000 | **S2-05 必选** |

---

## 7. 间隙处理规则

### 7.1 EEG 间隙 (ADR-005)

| 间隙大小 | 处理方式 | 说明 |
|---------|---------|------|
| ≤ 4 样本 (≤25ms) | 可选插值 | 必须标记 `Interpolated` |
| > 4 样本 (>25ms) | **强制断线** | 绘制灰色遮罩 |

### 7.2 aEEG 间隙

| 间隙大小 | 处理方式 | 说明 |
|---------|---------|------|
| ≤ 2 秒 | 连续 | 正常渲染 |
| > 2 秒 | **强制断线** | 绘制灰色遮罩 |

---

## 8. 铁律约束

| 铁律 | 约束 | 实现 |
|------|------|------|
| 铁律2 | 不伪造波形 | 间隙必须断线，不跨间隙插值 |
| 铁律5 | 缺失/饱和可见 | 灰色遮罩 + 红色标记 |
| 铁律6 | 渲染只 Draw | 构建器预处理，渲染器只调用 Draw |

---

## 9. 证据引用

| 实现 | 依据文档 | 章节 |
|------|----------|------|
| 增益设置 | CONSENSUS_BASELINE.md | §6.3 |
| 间隙处理 | DECISIONS.md | ADR-005 |
| 时间戳规则 | CONSENSUS_BASELINE.md | §5.1, §5.3 |
| aEEG 映射 | DSP_SPEC.md | §3 |
| 渲染约束 | 00_CONSTITUTION.md | 铁律2/5/6 |
| 三层架构 | ARCHITECTURE.md | §5 |

---

## 10. 依赖关系

### 前置依赖
- S1-05: EEG 波形渲染（原始 EegWaveformRenderer）
- S2-04: aEEG 半对数映射（AeegSemiLogMapper, AeegAxisTicks）
- S1-03: Vortice 渲染底座（D2DRenderTarget, ResourceCache）

### 被依赖
- S4+: 完整 UI 集成

---

## 11. 自检清单

- [x] 增益支持 10, 20, 50, 70, 100, 200, 1000 μV/cm
- [x] 1000 μV/cm 必选项已实现
- [x] EEG 间隙 > 4 样本断线 (ADR-005)
- [x] aEEG 间隙 > 2 秒断线
- [x] 使用 AeegSemiLogMapper (S2-04)
- [x] 铁律2: 不伪造波形
- [x] 铁律5: 缺失/饱和可见
- [x] **铁律6: 渲染只 Draw，无 O(N) 计算，无大分配**
- [x] 构建器在预处理阶段调用
- [x] 渲染器只接收预构建数据
- [x] 无跨间隙插值

---

**文档结束**
