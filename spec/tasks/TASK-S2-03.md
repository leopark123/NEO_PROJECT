# 📋 TASK-S2-03: GS 直方图构建（aEEG 统计表达层）

> **Sprint**: 2
> **负责方**: Claude Code
> **优先级**: 🟢 P0
> **预估工时**: 2h
> **状态**: ✅ 已完成

---

## ✅ 完成说明

> **完成日期**: 2026-01-28

### 交付物

| 文件 | 说明 |
|------|------|
| `src/DSP/GS/GsBinMapper.cs` | Bin 映射逻辑 |
| `src/DSP/GS/GsFrame.cs` | 帧结构 |
| `src/DSP/GS/GsHistogramAccumulator.cs` | 累计器 |
| `src/DSP/GS/GsProcessor.cs` | 主处理器 |

### 测试覆盖

| 测试文件 | 测试数 |
|----------|--------|
| `GsMappingLinearTests.cs` | 20 |
| `GsMappingLogTests.cs` | 20 |
| `GsCounterBehaviorTests.cs` | 18 |
| `GsSaturationTests.cs` | 22 |
| **总计** | 80 |

### DSP 测试总数

- 之前: 91 个测试
- 之后: 171 个测试 (增加 80 个 GS 测试)

---

## 1. 目标

将 S2-02 输出的 aEEG 数值流构建为 GS 灰度直方图数据结构。

**这是 aEEG 的"统计表达"，不是信号处理。**

---

## 2. 输入（必读文件）

| 文件 | 重点章节 |
|------|----------|
| `spec/DSP_SPEC.md` | §3.3（GS/Histogram 规格） |
| `spec/CONSENSUS_BASELINE.md` | §6.4（aEEG 参数） |
| `handoff/aeeg-chain-api.md` | S2-02 输出格式 |

---

## 3. 冻结规格

### 3.1 电压范围

| 参数 | 值 |
|------|-----|
| 统计范围 | 0-200 μV |
| 负值 | 忽略（不计入） |
| 超过 200 μV | 计入 bin 229 |

### 3.2 Bin 结构

| 参数 | 值 |
|------|-----|
| 总 bin 数 | 230 |
| bin index | 0-229 |

### 3.3 分段映射规则

| 区域 | 电压范围 | Bin 范围 | 映射方法 |
|------|----------|----------|----------|
| 线性区 | 0-10 μV | 0-99 | bin = floor(uV × 10) |
| 对数区 | 10-200 μV | 100-229 | bin = 100 + floor((log10(uV) - 1) / 1.301 × 130) |

### 3.4 统计周期

- 固定 15 秒
- 每 15 秒形成一个 GsFrame

### 3.5 Counter 语义

| Counter 值 | 含义 |
|------------|------|
| 0-228 | 累计中 |
| 229 | 本帧结束 (flush) |
| 255 | 忽略（不计入） |

### 3.6 Bin 值

- 每 bin 为计数值
- 最大饱和值: 249

---

## 4. 禁止事项

```
❌ 对 GS 做平滑 / 插值
❌ 改变 bin 数量
❌ 改变 15 秒周期
❌ 对 log / linear 分界点做"优化"
❌ 根据 UI 需要调整 GS
❌ 引入任何"视觉增强"
```

**GS 是事实统计，不是图像算法。**

---

## 5. 验收标准

### 5.1 功能验收

- [x] 严格 230 bins
- [x] 严格 15 秒周期
- [x] 严格线性 + log 分段映射
- [x] 未引入任何平滑/插值
- [x] 正确处理 counter=255/229
- [x] 每通道独立累计
- [x] Gap 区间不累计，不生成伪 bin
- [x] GsFrame 附带时间戳和 QualityFlag

### 5.2 测试验收

- [x] 线性区域映射测试 (20 tests)
- [x] Log 区域映射测试 (20 tests)
- [x] Counter 行为测试 (18 tests)
- [x] 饱和测试 (22 tests)
- [x] 所有 171 个 DSP 测试通过

### 5.3 编译验收

- [x] `dotnet build src/DSP/Neo.DSP.csproj` 零警告
- [x] `dotnet test tests/DSP.Tests/` 全部通过

---

## 6. 依赖与被依赖

### 依赖
- S2-02: aEEG 处理链（AeegProcessorOutput 输入）

### 被依赖
- S4+: 显示渲染（使用 GsFrame 绘制 aEEG 背景灰度）

---

## 7. 自检清单

- [x] 是否严格 230 bins？ → **是**
- [x] 是否严格 15 秒？ → **是**
- [x] 是否严格线性 + log 分段？ → **是**
- [x] 是否未引入任何平滑？ → **是，无任何平滑/插值**
- [x] 是否正确处理 counter=255/229？ → **是**
- [x] 是否 handoff 与代码一致？ → **是**

---

**任务卡结束**
