# 📋 NEO 项目上下文摘要（Context Brief）

> **版本**: v1.2  
> **最后更新**: 2025-01-21  
> **作用**: Sprint 级上下文，每个 Sprint 更新一次

---

## 当前状态

| 属性 | 值 |
|------|-----|
| **当前 Sprint** | Sprint 1 - 工程底座 |
| **Sprint 目标** | 渲染底座先跑起来（接口+双缓冲+Vortice+WPF壳） |
| **开始日期** | 待定 |
| **预计结束** | 待定 |

---

## Sprint 1 范围（收敛版）

### 目标
> 工程底座先跑起来：验证 FPS 基线、DPI 切换、DeviceLost 恢复

### 任务清单

| 任务ID | 任务名称 | 负责方 | 状态 | 依赖 |
|--------|----------|--------|------|------|
| S1-01 | 核心接口定义 | Codex | ⏳ 待开始 | - |
| S1-02 | SafeDoubleBuffer | Codex | ⏳ 待开始 | S1-01 |
| S1-03 | Vortice渲染底座 | Codex | ⏳ 待开始 | S1-01 |
| S1-04 | 三层渲染框架+WPF壳 | Codex | ⏳ 待开始 | S1-02, S1-03 |
| S1-05 | 模拟数据源 | Claude | ⏳ 待开始 | S1-01 |

### 必过验收用例

| AT编号 | 用例 | 验收标准 |
|--------|------|----------|
| AT-05 | FPS基线 | 空网格+模拟数据，FPS ≥ 120 |
| AT-07 | DPI切换 | 100%↔150%↔200% 切换无崩溃 |
| AT-08 | DeviceLost | 3秒内自动恢复 |

### 必需 handoff 产出

- [ ] `handoff/interfaces-api.md` (S1-01)
- [ ] `handoff/double-buffer-api.md` (S1-02)
- [ ] `handoff/renderer-device-api.md` (S1-03)
- [ ] `handoff/renderer-api.md` (S1-04)
- [ ] `handoff/mock-data-api.md` (S1-05)

---

## 系统参数速查

### EEG 参数
| 参数 | 值 |
|------|-----|
| 采样率 | **160 Hz** |
| 通道数 | **4** (CH1-CH4) |
| 数据格式 | int16, 0.076 μV/LSB |

### NIRS 参数
| 参数 | 值 |
|------|-----|
| 通道数 | **6** |
| 显示分组 | 3 |
| 采样率 | 1-4 Hz |

### 时间轴
| 参数 | 值 |
|------|-----|
| 数据类型 | int64 |
| 单位 | **微秒 (μs)** |
| 时钟域 | Host（临时，见ADR-010）|

---

## 本 Sprint 禁止事项

```
🚫 不得实现 Sprint 2+ 功能：
   - IIR 滤波器
   - LOD 金字塔
   - 伪迹检测处理
   - aEEG 处理链
   - Zero-Phase 回放
   - 视频同步
   - 4通道 EEG 实际波形渲染（S2）
   - 6通道 NIRS 实际趋势渲染（S2）
```

---

## DoD（完成定义）

每个任务完成必须满足：

```
□ dotnet build 通过
□ dotnet test 通过（该任务相关测试）
□ 通过该任务指定的 AT 用例
□ 生成 handoff/xxx-api.md
□ 更新本文件状态
□ Git 提交
```

---

## 上下文加载指引

### Codex 启动时加载：
```
spec/00_CONSTITUTION.md
spec/CODEX_SYSTEM_PROMPT.md
spec/ARCHITECTURE.md
spec/CONTEXT_BRIEF.md (本文件)
spec/tasks/TASK-S1-xx.md (当前任务)
handoff/*.md (依赖模块)
```

### Claude Code 启动时加载：
```
spec/00_CONSTITUTION.md
spec/CLAUDE_SYSTEM_PROMPT.md
spec/DSP_SPEC.md
spec/CONTEXT_BRIEF.md (本文件)
spec/tasks/TASK-S1-xx.md (当前任务)
handoff/*.md (依赖模块)
```

---

## 变更记录

| 版本 | 日期 | 变更 |
|------|------|------|
| v1.0 | 2025-01-21 | 初始版本 |
| v1.1 | 2025-01-21 | 更新参数：160Hz/4ch EEG/6ch NIRS/μs时间轴 |
| v1.2 | 2025-01-21 | Sprint 1 范围收敛（5任务），添加DoD |
