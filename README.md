# 🏥 NEO - 新生儿脑功能监护系统

> **版本**: v1.0 (Sprint 1)  
> **状态**: 🚀 开发中  
> **冻结日期**: 2025-01-21

---

## 项目概述

NEO 是一套新生儿多模态脑功能监护系统，支持：

| 模块 | 说明 | 参数 |
|------|------|------|
| **EEG** | 4通道脑电波形 | 160Hz |
| **aEEG** | 振幅整合脑电趋势 | 1Hz |
| **NIRS** | 6通道组织氧饱和度 | 1-4Hz |
| **Video** | 患者视频同步 | 30fps |
| **长程监护** | 连续记录 | 72小时 |

---

## 快速开始

### 1. 解压项目

```bash
unzip NEO_PROJECT_FULL_v1.1.zip
cd NEO
```

### 2. 创建 .NET 解决方案

```powershell
# 运行初始化脚本（见 PROJECT_STRUCTURE.md）
dotnet new sln -n NEO
# ... 详见 PROJECT_STRUCTURE.md
```

### 3. 构建与测试

```powershell
.\scripts\build.ps1
.\scripts\test.ps1
```

---

## AI 协作开发

本项目采用 **双 AI 协作模式**：

| 角色 | 工具 | 负责领域 | 提示词 |
|------|------|----------|--------|
| **工程架构师** | Codex | 接口、渲染、缓冲 | `CODEX_SYSTEM_PROMPT.md` |
| **DSP 工程师** | Claude Code | 滤波、检测、处理 | `CLAUDE_CODE_SYSTEM_PROMPT.md` |

### 交接机制

```
Codex 完成 S1-01
       │
       ▼
handoff/interfaces-api.md  ──→  Claude Code 读取
       │                              │
       ▼                              ▼
Codex 继续 S1-02~04          Claude 执行 S1-05
```

---

## 目录结构

```
NEO/
├── spec/           # 📋 规格文档（冻结）
├── handoff/        # 🤝 AI交接目录
├── src/            # 💻 源代码
│   ├── Core/       #    [Codex] 核心接口
│   ├── DSP/        #    [Claude] DSP算法
│   ├── Mock/       #    [Claude] 模拟数据
│   ├── Rendering/  #    [Codex] 渲染层
│   └── App/        #    [Codex] WPF应用
├── tests/          # 🧪 测试
└── scripts/        # 📜 构建脚本
```

详见 [PROJECT_STRUCTURE.md](PROJECT_STRUCTURE.md)

---

## Sprint 1 任务

| 任务ID | 名称 | 负责 | 状态 |
|--------|------|------|------|
| S1-01 | 核心接口定义 | Codex | ⏳ |
| S1-02 | SafeDoubleBuffer | Codex | ⏳ |
| S1-03 | Vortice渲染底座 | Codex | ⏳ |
| S1-04 | 三层渲染+WPF | Codex | ⏳ |
| S1-05 | 模拟数据源 | Claude | ⏳ |

**验收目标**：
- AT-05: FPS ≥ 120
- AT-07: DPI 切换无崩溃
- AT-08: DeviceLost 3秒恢复

---

## 核心参数（冻结）

| 参数 | 值 | 状态 |
|------|-----|------|
| EEG 采样率 | 160 Hz | 🔒 |
| EEG 通道数 | 4 | 🔒 |
| NIRS 通道数 | 6 | 🔒 |
| 时间轴单位 | int64 μs | 🔒 |
| 存储方案 | SQLite + Chunk | 🔒 |

---

## 文档索引

| 文档 | 说明 |
|------|------|
| [spec/00_CONSTITUTION.md](spec/00_CONSTITUTION.md) | 15条铁律 |
| [spec/CONSENSUS_BASELINE.md](spec/CONSENSUS_BASELINE.md) | 共识基线 |
| [spec/ARCHITECTURE.md](spec/ARCHITECTURE.md) | 系统架构 |
| [spec/DSP_SPEC.md](spec/DSP_SPEC.md) | DSP规格 |
| [spec/DECISIONS.md](spec/DECISIONS.md) | 架构决策 |

---

**NEO Project © 2025**
