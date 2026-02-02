# 📋 TASK-S2-04: aEEG 半对数显示映射（Display Mapping Layer）

> **Sprint**: 2
> **负责方**: Claude Code
> **优先级**: 🟢 P0
> **预估工时**: 1h
> **状态**: ✅ 已完成

---

## ✅ 完成说明

> **完成日期**: 2026-01-28

### 交付物

| 文件 | 说明 |
|------|------|
| `src/Rendering/Mapping/AeegSemiLogMapper.cs` | 半对数 Y 轴映射器 |
| `src/Rendering/Mapping/AeegAxisTicks.cs` | Y 轴刻度定义 |

### 测试覆盖

| 测试文件 | 测试数 |
|----------|--------|
| `SemiLogLinearSegmentTests.cs` | 16 |
| `SemiLogLogSegmentTests.cs` | 14 |
| `BoundaryMappingTests.cs` | 22 |
| `TickPositionTests.cs` | 20 |
| **总计** | 72 |

---

## 1. 目标

将 GS / aEEG 数值（μV）映射为 UI 垂直坐标（Y 像素）。

**⚠️ 这是显示映射，不是信号处理。**

```
职责边界:
- 不产生新数据
- 不修改 GS
- 不解释医学含义
- 这是 View 层职责
```

---

## 2. 输入（必读文件）

| 文件 | 重点章节 |
|------|----------|
| `spec/DSP_SPEC.md` | aEEG Display / Semi-log Y-axis |
| `spec/CONSENSUS_BASELINE.md` | §6.4（aEEG 参数） |
| `handoff/gs-histogram-api.md` | S2-03 输出格式 |

---

## 3. 冻结规格

### 3.1 Y 轴结构（半对数）

| 参数 | 值 |
|------|-----|
| 显示范围 | 0-200 μV |
| 线性段 | 0-10 μV |
| 对数段 | 10-200 μV |
| 分界点 | **10 μV**（医学冻结） |

### 3.2 高度分配

| 段 | 高度占比 |
|-----|---------|
| 线性段 (0-10 μV) | **50%** (下半区) |
| 对数段 (10-200 μV) | **50%** (上半区) |

### 3.3 标准刻度（固定，不可增删）

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

### 3.4 映射规则

- 相同 μV → 相同 Y
- 显示层不可影响统计层
- UI 缩放只改变 pixelHeight，不改变映射函数

### 3.5 无效值处理

| 输入 | 输出 |
|------|------|
| NaN | NaN |
| 负值 | NaN |
| > 200 μV | clamp 到 0 (顶部) |

---

## 4. 禁止事项

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

## 5. 实现要求

### 5.1 映射函数

- 必须是**纯函数**
- 输入：μV
- 输出：Y（double / float）

### 5.2 高度参数化

```csharp
totalHeightPx        // 总高度
linearHeightPx = totalHeightPx * 0.5  // 线性段高度
logHeightPx = totalHeightPx * 0.5     // 对数段高度
```

---

## 6. 验收标准

### 6.1 功能验收

- [x] 严格 0-10 μV 线性映射
- [x] 严格 10-200 μV 对数映射
- [x] 上下各占 50%
- [x] 未改变 GS 数据
- [x] 映射为纯函数
- [x] 11 个标准刻度正确
- [x] 分界点 10 μV 正确

### 6.2 测试验收

- [x] 线性段映射测试 (16 tests)
- [x] 对数段映射测试 (14 tests)
- [x] 边界处理测试 (22 tests)
- [x] 刻度位置测试 (20 tests)
- [x] 所有 72 个测试通过

### 6.3 编译验收

- [x] `dotnet build src/Rendering/Neo.Rendering.csproj` 零错误
- [x] `dotnet test tests/Rendering.Tests/ --filter Mapping` 全部通过

---

## 7. 依赖与被依赖

### 依赖
- S2-03: GS 直方图（GsFrame 输入）

### 被依赖
- S4+: 显示渲染（使用映射器绘制 aEEG 趋势）

---

## 8. 自检清单

- [x] 是否严格 0-10 μV 线性？ → **是**
- [x] 是否严格 10-200 μV 对数？ → **是**
- [x] 是否上下各占 50%？ → **是**
- [x] 是否未改 GS 数据？ → **是，纯映射**
- [x] 是否映射为纯函数？ → **是**
- [x] 是否 handoff 与代码一致？ → **是**

---

## 9. 映射公式

### 9.1 线性段 (0-10 μV)

```
Y = totalHeight - (μV / 10) × linearHeight

坐标:
  0 μV → Y = totalHeight (底部)
  5 μV → Y = totalHeight - linearHeight/2
  10 μV → Y = totalHeight/2 (中间)
```

### 9.2 对数段 (10-200 μV)

```
Y = logHeight × (1 - (log10(μV) - 1) / logRange)

其中:
  logRange = log10(200) - 1 ≈ 1.301

坐标:
  10 μV → Y = totalHeight/2 (中间)
  100 μV → Y ≈ 0.231 × logHeight
  200 μV → Y = 0 (顶部)
```

---

**任务卡结束**
