# Checkpoint CP-12: S2-05 EEG/aEEG 波形渲染层

> **创建日期**: 2026-01-28
> **任务**: S2-05 EEG/aEEG Waveform Rendering Layer
> **状态**: ✅ 已完成

---

## 1. 完成内容

### 1.1 交付物

```
src/Rendering/EEG/
├── EegGainScaler.cs          # EEG 增益缩放器
├── PolylineBuilder.cs        # 折线段构建器（预处理阶段）
├── EegWaveformRenderData.cs  # 预构建渲染数据结构
└── EegPolylineRenderer.cs    # 折线渲染器（只做 Draw）

src/Rendering/AEEG/
├── AeegColorPalette.cs       # aEEG 颜色定义
├── AeegTrendRenderer.cs      # aEEG 趋势渲染器（只做 Draw）
├── AeegGridAndAxisRenderer.cs# aEEG 网格轴线渲染器
└── AeegSeriesBuilder.cs      # aEEG 序列构建器（预处理阶段）

tests/Rendering.Tests/Waveform/
├── EegGainScalerTests.cs      # 增益缩放器测试
├── PolylineBuilderTests.cs    # 折线构建器测试
├── AeegSeriesBuilderTests.cs  # aEEG 序列构建器测试
└── AeegColorPaletteTests.cs   # aEEG 颜色测试

handoff/waveform-rendering-api.md  # API 文档
```

### 1.2 测试结果

- 波形渲染测试: **109 个全部通过**
- 渲染测试总数: 341 通过

---

## 2. 规格遵循

| 规格项 | 规格值 | 实现值 | 状态 |
|--------|--------|--------|------|
| 增益选项 | 10,20,50,70,100,200,1000 | 7 个选项 | ✅ |
| 1000 μV/cm | 必选 | 已实现 | ✅ |
| EEG 间隙阈值 | > 4 样本 (25ms) | 25000 μs | ✅ |
| aEEG 间隙阈值 | > 2 秒 | 2000000 μs | ✅ |
| Y 轴映射 | 使用 S2-04 | AeegSemiLogMapper | ✅ |

---

## 3. 铁律约束检查

- [x] 铁律2: 不伪造波形 → **未违反**
  - 间隙 > 4 样本断线
  - 无跨间隙插值

- [x] 铁律5: 缺失/饱和可见 → **未违反**
  - 间隙遮罩（灰色半透明）
  - 饱和标记（红色）
  - 质量标志处理

- [x] 铁律6: 渲染只 Draw → **未违反**
  - 构建器在预处理阶段调用
  - 渲染器只做 Draw 调用
  - 无 O(N) 计算
  - 无大分配（无 HashSet/List/Dictionary）
  - 饱和检查使用 Array.BinarySearch（O(log n)，无分配）

---

## 4. Self-Check (Mandatory)

- [x] 实现是否完全来自文档？
  - CONSENSUS_BASELINE.md §6.3 (增益设置)
  - ADR-005 (间隙处理)
  - DSP_SPEC.md §3 (aEEG 规格)
  - 00_CONSTITUTION.md 铁律2/5/6

- [x] 是否引入任何推测？ → **否**

- [x] 是否改变已有接口/数据结构？ → **否，新增模块**

- [x] 是否影响时间戳一致性？ → **否，保持 int64 μs**

- [x] 是否可被回放复现？ → **是，纯渲染无状态**

- [x] 是否更新了 PROJECT_STATE.md？ → **是**

---

## 5. 组件详情

### 5.1 EegGainScaler

```csharp
// 支持的增益设置
enum EegGainSetting { 10, 20, 50, 70, 100, 200, 1000 } // μV/cm

// 核心功能
- UvToPixels(uv): μV → 像素偏移
- PixelsToUv(pixels): 像素 → μV
- GetDisplayRangeUv(heightPx): 计算显示范围
```

### 5.2 PolylineBuilder

```csharp
// 间隙处理
- MaxInterpolatableGapSamples = 4
- MaxInterpolatableGapUs = 25000 (25ms)

// 输出结构
- Points[]: Vector2 坐标数组
- Segments[]: 连续线段
- Gaps[]: 间隙区域
- SaturationIndices[]: 饱和点索引
```

### 5.3 EegPolylineRenderer

```csharp
// 铁律6: 只做 Draw 调用
- 接收 EegWaveformRenderData（预构建）
- 迭代预构建的 Segments
- 调用 context.DrawLine()
- 无 O(N) 计算，无分配
```

### 5.4 AeegSeriesBuilder

```csharp
// 预处理阶段（非渲染线程）
- 使用 S2-04 AeegSemiLogMapper
- 构建 AeegTrendRenderData
- 间隙处理: MaxGapUs = 2_000_000 (2秒)
```

### 5.5 AeegTrendRenderer

```csharp
// 铁律6: 只做 Draw 调用
- 接收 AeegTrendRenderData（预构建）
- 迭代预构建的 Segments
- 调用 context.DrawLine() / FillRectangle()
- 无 O(N) 计算，无分配
```

---

## 6. 与其他模块关系

```
S2-02 aEEG处理链 → AeegOutput (1Hz min/max)
        ↓
S2-03 GS直方图 → GsFrame (15s bins)
        ↓
S2-04 显示映射 → AeegSemiLogMapper
        ↓
S2-05 波形渲染层 (铁律6: 预处理 + 渲染分离)

预处理线程:                      渲染线程:
├─ EegGainScaler                 │
├─ PolylineBuilder.Build()       │
│      ↓                         │
│  EegWaveformRenderData ───────→ EegPolylineRenderer.Render()
│                                │   (只做 Draw)
├─ AeegSeriesBuilder.Build()     │
│      ↓                         │
│  AeegTrendRenderData ─────────→ AeegTrendRenderer.Render()
│                                │   (只做 Draw)
└─ AeegGridAndAxisRenderer ─────→ (只做 Draw)
```

---

## 7. 下一步

- Sprint 3: NIRS + 视频
  - S3-00 NIRS RS232 Protocol Spec & Parser → **🚫 Blocked (ADR-015)**
  - S3-01 NIRS 集成 → 依赖 S3-00
  - S3-02 视频采集
  - S3-03 同步回放

---

**Checkpoint 结束**
