# 📁 NEO 项目目录结构与文件放置说明

> **版本**: v1.0  
> **冻结日期**: 2025-01-21

---

## 完整目录树

```
NEO/
│
├── 📋 spec/                              # 规格文档（只读）
│   ├── 00_CONSTITUTION.md                # 15条铁律
│   ├── CONSENSUS_BASELINE.md             # 共识基线 v1.4
│   ├── ARCHITECTURE.md                   # 系统架构 v2.3 [Codex参考]
│   ├── DSP_SPEC.md                       # DSP规格 v2.1 [Claude参考]
│   ├── ACCEPTANCE_TESTS.md               # 验收测试 v2.0
│   ├── DECISIONS.md                      # ADR决策 v1.4
│   ├── CONTEXT_BRIEF.md                  # Sprint上下文 v1.2
│   ├── TIME_SYNC.md                      # 时间同步策略 v1.1
│   ├── API_STYLE.md                      # 代码风格
│   ├── CHECKLIST.md                      # Sprint核对清单
│   └── tasks/                            # 任务卡
│       ├── TASK-S1-01.md                 # [Codex] 核心接口
│       ├── TASK-S1-02.md                 # [Codex] 双缓冲
│       ├── TASK-S1-03.md                 # [Codex] Vortice
│       ├── TASK-S1-04.md                 # [Codex] 渲染框架
│       └── TASK-S1-05.md                 # [Claude] 模拟数据
│
├── 🤝 handoff/                           # 交接目录（AI读写）
│   ├── TEMPLATE.md                       # 交接文档模板
│   ├── interfaces-api.md                 # [Codex→Claude] 接口定义
│   ├── double-buffer-api.md              # [Codex] 双缓冲API
│   ├── renderer-device-api.md            # [Codex] 渲染设备API
│   ├── renderer-api.md                   # [Codex] 渲染框架API
│   └── mock-data-api.md                  # [Claude→Codex] 模拟数据API
│
├── 💻 src/                               # 源代码
│   ├── Core/                             # [Codex] 核心层
│   │   ├── Interfaces/                   # 接口定义
│   │   ├── Models/                       # 数据模型
│   │   └── Enums/                        # 枚举定义
│   │
│   ├── Infrastructure/                   # [Codex] 基础设施
│   │   ├── Buffers/                      # 缓冲区实现
│   │   └── Threading/                    # 线程工具
│   │
│   ├── Rendering/                        # [Codex] 渲染层
│   │   ├── Device/                       # D3D设备管理
│   │   ├── Layers/                       # 渲染层实现
│   │   ├── Composition/                  # 层合成
│   │   └── Resources/                    # GPU资源
│   │
│   ├── DSP/                              # [Claude] DSP算法
│   │   ├── Filters/                      # 滤波器
│   │   ├── Processing/                   # 处理链
│   │   └── Detection/                    # 检测算法
│   │
│   ├── Mock/                             # [Claude] 模拟数据
│   │   ├── WaveformGenerators/           # 波形生成器
│   │   └── ArtifactInjectors/            # 伪迹注入器
│   │
│   └── App/                              # [Codex] WPF应用
│       └── Controls/                     # 自定义控件
│
├── 🧪 tests/                             # 测试
│   ├── Core.Tests/                       # [Codex]
│   ├── Infrastructure.Tests/             # [Codex]
│   │   └── Buffers/
│   ├── Rendering.Tests/                  # [Codex]
│   │   ├── Device/
│   │   └── Performance/
│   ├── DSP.Tests/                        # [Claude]
│   │   ├── Filters/
│   │   └── Processing/
│   └── Mock.Tests/                       # [Claude]
│
├── 📜 scripts/                           # 构建脚本
│   ├── build.ps1
│   └── test.ps1
│
├── 📚 docs/                              # 文档
│
├── CLAUDE_CODE_SYSTEM_PROMPT.md          # ⭐ Claude Code 提示词
├── CODEX_SYSTEM_PROMPT.md                # ⭐ Codex 提示词
├── PROJECT_STRUCTURE.md                  # 本文件
└── README.md                             # 项目说明
```

---

## 分工表

| 目录/文件 | 负责方 | Sprint 1 任务 |
|-----------|--------|---------------|
| `src/Core/` | **Codex** | S1-01 |
| `src/Infrastructure/` | **Codex** | S1-02 |
| `src/Rendering/` | **Codex** | S1-03, S1-04 |
| `src/App/` | **Codex** | S1-04 |
| `src/DSP/` | **Claude** | (S2+) |
| `src/Mock/` | **Claude** | S1-05 |
| `handoff/interfaces-api.md` | **Codex** → Claude | S1-01 |
| `handoff/mock-data-api.md` | **Claude** → Codex | S1-05 |

---

## 创建 .NET 解决方案

```powershell
# 进入项目目录
cd NEO

# 1. 创建解决方案
dotnet new sln -n NEO

# 2. 创建项目
dotnet new classlib -n NEO.Core -o src/Core
dotnet new classlib -n NEO.Infrastructure -o src/Infrastructure
dotnet new classlib -n NEO.Rendering -o src/Rendering
dotnet new classlib -n NEO.DSP -o src/DSP
dotnet new classlib -n NEO.Mock -o src/Mock
dotnet new wpf -n NEO.App -o src/App

# 3. 创建测试项目
dotnet new xunit -n NEO.Core.Tests -o tests/Core.Tests
dotnet new xunit -n NEO.Infrastructure.Tests -o tests/Infrastructure.Tests
dotnet new xunit -n NEO.Rendering.Tests -o tests/Rendering.Tests
dotnet new xunit -n NEO.DSP.Tests -o tests/DSP.Tests
dotnet new xunit -n NEO.Mock.Tests -o tests/Mock.Tests

# 4. 添加到解决方案
dotnet sln add src/Core src/Infrastructure src/Rendering src/DSP src/Mock src/App
dotnet sln add tests/Core.Tests tests/Infrastructure.Tests tests/Rendering.Tests tests/DSP.Tests tests/Mock.Tests

# 5. 添加项目引用
dotnet add src/Infrastructure reference src/Core
dotnet add src/Rendering reference src/Core src/Infrastructure
dotnet add src/DSP reference src/Core
dotnet add src/Mock reference src/Core
dotnet add src/App reference src/Core src/Infrastructure src/Rendering src/DSP src/Mock

# 6. 添加 Vortice NuGet 包
dotnet add src/Rendering package Vortice.Direct3D11
dotnet add src/Rendering package Vortice.DXGI
dotnet add src/Rendering package Vortice.Mathematics
```

---

## Sprint 1 执行流程

```
┌─────────────────────────────────────────┐
│  步骤1: Codex 执行 S1-01               │
│  产出: handoff/interfaces-api.md       │
└─────────────────┬───────────────────────┘
                  │
    ┌─────────────┼─────────────┐
    │             │             │
    ▼             ▼             ▼
┌─────────┐ ┌─────────┐ ┌─────────────────┐
│ S1-02   │ │ S1-03   │ │ S1-05 (Claude)  │
│ (Codex) │ │ (Codex) │ │ 等待接口定义后   │
└────┬────┘ └────┬────┘ │ 开始执行        │
     │           │      └─────────────────┘
     └─────┬─────┘
           │
           ▼
┌─────────────────────────────────────────┐
│  步骤4: Codex 执行 S1-04               │
│  验收: AT-05 FPS≥120                   │
└─────────────────────────────────────────┘
```

---

## 关键文件说明

### 给 Codex 的文件

| 文件 | 用途 |
|------|------|
| `CODEX_SYSTEM_PROMPT.md` | **启动时加载此提示词** |
| `spec/ARCHITECTURE.md` | 架构设计参考 |
| `spec/DECISIONS.md` | ADR 决策记录 |
| `spec/tasks/TASK-S1-01~04.md` | 任务卡 |

### 给 Claude Code 的文件

| 文件 | 用途 |
|------|------|
| `CLAUDE_CODE_SYSTEM_PROMPT.md` | **启动时加载此提示词** |
| `spec/DSP_SPEC.md` | DSP 算法规格 |
| `spec/tasks/TASK-S1-05.md` | 任务卡 |
| `handoff/interfaces-api.md` | **等待 Codex 产出后读取** |

---

**文档结束**
