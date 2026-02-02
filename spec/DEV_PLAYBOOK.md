# 📘 NEO 项目开发手册 DEV_PLAYBOOK

> **版本**: v2.3（工业级最终版）  
> **核心原则**: 会话可以丢，但仓库不丢  
> **真相源**: Claude Code 只能相信仓库里的冻结文件与证据文件，不允许凭空推断  
> **不确定性原则**: 不确定性是第一等公民，未知≠默认值，缺失≠推断  
> **自检原则**: 每个结论必须有工程证据（文件路径+行为证据），禁止文字型自检

---

# 一、仓库文件结构（9类必备文件）

## 1.1 规格文件（spec/ — 冻结，不可修改）

```
spec/
├── 00_CONSTITUTION.md      # 15条铁律（绝对不可违反）
├── CONSENSUS_BASELINE.md   # 冻结基线 v1.6
├── ARCHITECTURE.md         # 系统架构 v2.5
├── DSP_SPEC.md             # DSP规格 v2.2
├── DB_SCHEMA.sql           # SQLite schema（单一真相源）
├── ACCEPTANCE_TESTS.md     # 验收测试 v2.1
├── DECISIONS.md            # ADR记录 v1.6
├── DEV_PLAYBOOK.md         # 本文档（开发手册）
└── tasks/                  # 任务卡
    ├── TASK-S1-01.md
    └── ...
```

## 1.2 状态文件（status/ — 持续更新，续航关键）

```
status/
├── PROGRESS.md             # 进度看板（✅/🟡/🔴）
├── CHECKPOINT.md           # 续航摘要（三段式：FACTS/CONTEXT/NEXT ACTIONS）
├── OPEN_QUESTIONS.md       # 待确认问题清单（避免重复问）
└── UNKNOWN_ASSUMPTIONS.md  # ⚠️ 未知假设登记（防止隐性默认值）
```

## 1.3 证据文件（evidence/ — 每个功能必须产出）

```
evidence/
├── S1-01-CoreInterfaces/
│   ├── PROOF.md            # 证据汇总（必须）
│   ├── ARCH_CHECK.md       # 架构一致性检查
│   └── test_output.txt     # 测试输出粘贴
├── S1-02-DoubleBuffer/
│   ├── PROOF.md
│   └── stress_test.log     # 压测日志
└── Sprint1/
    └── E2E_PROOF.md        # 端到端验收证据
```

## 1.4 检查点文件（checkpoints/ — 每个模块必须产出）⚠️ 新增

```
checkpoints/
├── README.md               # 模板和说明
├── CP-01-core-interfaces.md
├── CP-02-double-buffer.md
├── CP-03-vortice-renderer.md
└── ...
```

**Checkpoint 是工程证据型自检报告，必须包含：**
- 具体文件路径和行为证据
- 未实现项显式标注为 UNIMPLEMENTED
- 不确定项显式标注为 UNKNOWN
- 禁止使用"合理/通常/一般认为"等模糊表述

## 1.5 项目状态文件（根目录）⚠️ 新增

```
PROJECT_STATUS.md           # 进度总览（给人看，非代码）
```

---

# 二、Claude Code 行为规则（System Prompt）

每次会话开头加载以下约束：

```
【Claude Code 强约束 - 必须执行】

═══════════════════════════════════════════════════
 真相源规则
═══════════════════════════════════════════════════
1. 只以 spec/ 和 evidence/ 为真相源
2. 任何没有证据支撑的结论必须标注"未知/待确认"
3. 不得猜测、不得推断、不得凭空假设

═══════════════════════════════════════════════════
 实现前规则
═══════════════════════════════════════════════════
4. 任何实现前，先输出"证据引用清单"
   格式：spec/文档名 → §章节号 → 具体条款
5. 检查是否违反 00_CONSTITUTION.md（15条铁律）

═══════════════════════════════════════════════════
 实现后规则（证据必须具体，禁止泛化）
═══════════════════════════════════════════════════
6. 每完成一个功能，执行"自检三件套"：
   - dotnet build（编译通过）
   - dotnet test（相关测试通过）
   - 生成 evidence/<feature>/PROOF.md
   
7. ⚠️ PROOF.md 证据强制要求（禁止形式化）：
   - 必须包含可执行命令（原文）
   - 必须包含真实输出（不少于10行原文粘贴）
   - 必须包含至少1条边界/失败case说明
   - ❌ 禁止："测试通过，功能正常"
   - ✅ 要求："运行 dotnet test X，输出如下[粘贴]，其中 case Y 覆盖了 Z 边界"

═══════════════════════════════════════════════════
 强制自检规则（工程证据型，禁止文字型）⚠️ 关键
═══════════════════════════════════════════════════
8. 每完成一个功能模块，必须生成 Checkpoint 文档（checkpoints/CP-xx.md）
9. 每一条结论必须引用具体文件路径或实现证据
   - ✅ 合格："src/Core/Time/GlobalClock.cs:L45 使用 Stopwatch.GetTimestamp()"
   - ❌ 不合格："时间戳设计合理，满足要求"
10. 未实现或不确定内容必须显式标注：
    - UNIMPLEMENTED：明确未实现，说明原因和影响
    - UNKNOWN：不确定，需要确认
11. 禁止使用"合理/通常/一般认为"等模糊表述

═══════════════════════════════════════════════════
 文件修改规则
═══════════════════════════════════════════════════
12. 不得修改 spec/ 冻结文件
   （除非指挥官明确下达"解冻/修订"指令）
13. 新增假设必须写入 status/OPEN_QUESTIONS.md
   并给出"需要用户提供的最小信息"

═══════════════════════════════════════════════════
 未知假设规则（防止隐性默认值）⚠️ 关键
═══════════════════════════════════════════════════
14. 任何代码中的默认值/阈值/策略，如果无法在 spec/evidence 中找到来源：
    - 必须登记在 status/UNKNOWN_ASSUMPTIONS.md
    - 必须标注：位置、原因、风险等级
    - 🔴高风险项：必须停止，等待指挥官确认
    - ❌ 禁止"合理推断后直接实现"

═══════════════════════════════════════════════════
 续航规则（三段式Checkpoint）
═══════════════════════════════════════════════════
15. 上下文接近压缩时，必须先写 status/CHECKPOINT.md
16. CHECKPOINT 必须包含固定三段（顺序不可变）：
    - FACTS：已实现模块、接口签名、已产出证据
    - CONTEXT：当前目标、为什么做到这一步
    - NEXT ACTIONS：下一步具体文件、禁止修改的文件
17. 每个任务完成后，更新 status/PROGRESS.md 和 PROJECT_STATUS.md
```

---

# 三、Checkpoint 续航协议（解决上下文压缩问题）

## 3.1 触发时机

- Claude Code 提示上下文将要压缩
- 完成一个子模块后
- 当天收工前
- 对话超过 50 轮

## 3.2 CHECKPOINT.md 模板

```markdown
# Checkpoint - 2026-01-26 15:30

## 已完成清单（含文件路径）
- [x] S1-01 核心接口 → evidence/S1-01/PROOF.md
- [x] S1-02 无锁缓冲 → evidence/S1-02/PROOF.md

## 当前正在进行（具体到文件/行范围）
- 任务：S1-03 Vortice渲染底座
- 文件：src/Rendering/Device/VorticeDeviceManager.cs
- 行范围：150-220
- 进度：Device初始化完成，DPI切换进行中（约70%）

## 下一步 TODO（可执行粒度）
1. 完成 DPI 切换逻辑（OnDpiChanged方法）
2. 实现 DeviceLost 恢复（RecreateDevice方法）
3. 添加 120 FPS 稳定性测试

## 本次变更的证据链接
- evidence/S1-03/PROOF.md（部分完成）

## 未决问题
- 见 status/OPEN_QUESTIONS.md #Q003
```

## 3.3 续航启动指令

新会话只需发送：

```
【续航模式】

读取以下文件恢复上下文：
1. status/CHECKPOINT.md（上次进度）
2. status/PROGRESS.md（整体看板）
3. status/OPEN_QUESTIONS.md（待确认问题）
4. spec/00_CONSTITUTION.md（铁律）
5. spec/CONSENSUS_BASELINE.md（基线）

从 CHECKPOINT 记录的位置继续执行。
```

---

# 四、证据化自检框架

## 4.1 PROOF.md 必须包含 5 件事（禁止形式化！）

```markdown
# [功能名称] 证据报告

## 1. 需求引用（来自哪个spec，具体到章节）
- spec/CONSENSUS_BASELINE.md §5.1 时间戳定义
- spec/ARCHITECTURE.md §2.1 核心接口
- 要求：int64微秒，主机单调时钟

## 2. 实现范围（改了哪些文件，具体行数）
- 新增：src/Core/Models/GlobalTime.cs (85行)
- 新增：src/Core/Interfaces/ITimeSeriesSource.cs (42行)
- 新增：tests/Core.Tests/GlobalTimeTests.cs (120行)

## 3. 可重复验证步骤（必须可直接执行）
```bash
cd NEO_PROJECT
dotnet build src/Core/
dotnet test tests/Core.Tests/ --filter "GlobalTimeTests" -v n
```

## 4. 结果证据（必须粘贴真实输出，不少于10行）

⚠️ 以下为真实终端输出，非模拟：

```
$ dotnet test tests/Core.Tests/ --filter "GlobalTimeTests" -v n
  Determining projects to restore...
  All projects are up-to-date for restore.
  Neo.Core -> /home/user/NEO_PROJECT/src/Core/bin/Debug/net8.0/Neo.Core.dll
  Neo.Core.Tests -> /home/user/NEO_PROJECT/tests/Core.Tests/bin/Debug/net8.0/Neo.Core.Tests.dll
Test run for /home/user/NEO_PROJECT/tests/Core.Tests/bin/Debug/net8.0/Neo.Core.Tests.dll (.NETCoreApp,Version=v8.0)
Microsoft (R) Test Execution Command Line Tool Version 17.8.0
Starting test execution, please wait...
A total of 1 test files matched the specified pattern.

Passed!  - Failed:     0, Passed:     8, Skipped:     0, Total:     8, Duration: 156 ms
```

## 5. 边界/失败case说明（必须至少1条）

### 已覆盖的边界情况：
- ✅ `GlobalTime_Overflow_ThrowsException`: 测试 int64 溢出边界
- ✅ `GlobalTime_Monotonic_NeverDecreases`: 连续调用1000次验证单调性
- ✅ `GlobalTime_Precision_Microsecond`: 验证精度不低于微秒

### 已知限制：
- ⚠️ 未测试跨进程同步场景（不在当前 Sprint 范围）

## 6. 结论
✅ 通过 — 8个测试全部绿色，符合 spec/CONSENSUS_BASELINE.md §5.1 要求

---
❌ 禁止的写法示例：
- "测试通过，功能正常" ← 这不是证据
- "经验证符合预期" ← 没有具体输出
- "参见测试文件" ← 必须粘贴输出
```

## 4.2 证据强制要求清单

| 要求 | 必须 | 禁止 |
|------|------|------|
| 可执行命令 | ✅ 完整命令原文 | ❌ "运行测试" |
| 输出证据 | ✅ 不少于10行原文 | ❌ "输出正常" |
| 边界case | ✅ 至少1条具体说明 | ❌ "覆盖了边界" |
| 结论 | ✅ 引用spec章节 | ❌ "符合预期" |

---

# 五、三道闸门（防止跑偏）

## 闸门 1：架构一致性检查（每个功能前）

必须输出 `ARCH_CHECK.md`：

```markdown
# 架构一致性检查 - OwnedDoubleBuffer

## 模块归属
- 层级：Infrastructure（基础设施层）
- 命名空间：Neo.Infrastructure.Buffers

## 线程归属
- 主要线程：Worker线程
- 线程安全：是（无锁设计）

## 数据所有权
- 只读：否
- 复制策略：引用传递 + Swap
- 共享范围：生产者/消费者两方

## 铁律检查
- [x] #6 渲染线程只Draw → 不违反（非渲染模块）
- [x] #7 时间戳int64微秒 → 不涉及
- [x] #10 必须有单元测试 → 已添加

## 结论
✅ 架构一致性检查通过
```

## 闸门 2：数据库一致性检查

- 所有表结构必须来自 `spec/DB_SCHEMA.sql`
- 启动时校验 schema hash
- 每次改表必须追加 ADR

产出：`evidence/<feature>/DB_CHECK.md`

## 闸门 3：端到端验收（每个Sprint）

最小闭环：数据输入 → DSP → UI → 存储 → 回放

产出：`evidence/SprintX/E2E_PROOF.md`

---

# 六、进度看板（PROGRESS.md）

```markdown
# NEO 项目进度看板

> 最后更新：2026-01-26 15:30

## Sprint 1 - 渲染底座 + 模拟数据

| 任务 | 状态 | 证据 | 更新时间 |
|------|------|------|----------|
| S1-01 核心接口 | ✅ | [PROOF](../evidence/S1-01/PROOF.md) | 01-25 10:00 |
| S1-02 无锁缓冲 | ✅ | [PROOF](../evidence/S1-02/PROOF.md) | 01-25 14:00 |
| S1-03 渲染底座 | 🟡 | 进行中 | 01-26 15:00 |
| S1-04 模拟数据 | ⬜ | - | - |
| S1-05 系统集成 | ⬜ | - | - |

## 状态图例
- ✅ 完成（有证据）
- 🟡 进行中
- 🔴 阻塞（见 OPEN_QUESTIONS）
- ⬜ 待开始

## 阻塞项
- 无

## 下一里程碑
- Sprint 1 验收：2026-01-28
```

---

# 七、问题清单（OPEN_QUESTIONS.md）

```markdown
# 待确认问题清单

> 最后更新：2026-01-26

## Q001 [已解决]
- **问题**：NIRS 阈值具体数值？
- **状态**：✅ 已解决
- **答案**：通过配置加载，当前标注TBD（ADR-013）
- **写入**：spec/CONSENSUS_BASELINE.md §12.3

## Q002 [待确认]
- **问题**：打印页眉需要哪些字段？
- **状态**：🟡 待指挥官确认
- **需要的最小信息**：字段列表 + 是否必填
- **默认方案**：预留常见字段，不强制必填

## Q003 [待确认]
- **问题**：WPF 集成方式？HwndHost 还是 D3DImage？
- **状态**：🟡 待确认
- **需要的信息**：是否需要透明度支持
- **默认方案**：HwndHost（性能优先）
```

---

# 八、启动指令模板

## 8.1 首次启动（完整版）

```
你是 NEO 项目的全栈工程师（Claude Code）。

【加载约束文件】
1. spec/00_CONSTITUTION.md（15条铁律）
2. spec/CONSENSUS_BASELINE.md（冻结基线）
3. spec/ARCHITECTURE.md（系统架构）
4. spec/DSP_SPEC.md（DSP规格）
5. spec/DEV_PLAYBOOK.md（本手册）

【加载进度文件】
6. status/PROGRESS.md（进度看板）
7. status/CHECKPOINT.md（续航摘要）
8. status/OPEN_QUESTIONS.md（待确认问题）

【执行任务】
Sprint 1 全部任务：S1-01 → S1-02 → S1-03 → S1-04 → S1-05

【输出要求】
- 每个功能完成后，执行自检三件套（build/test/PROOF.md）
- 每个任务完成后，更新 status/PROGRESS.md
- 上下文接近压缩时，先写 status/CHECKPOINT.md
- 遇到不确定问题，写入 status/OPEN_QUESTIONS.md
```

## 8.2 续航启动（简化版）

```
【续航模式】

读取：
1. status/CHECKPOINT.md
2. status/PROGRESS.md
3. status/OPEN_QUESTIONS.md

从 CHECKPOINT 记录的位置继续执行。
规则同首次启动。
```

---

# 九、Sprint 1 任务规格

| 任务 | 核心产出 | 关键约束 | 证据要求 |
|------|----------|----------|----------|
| S1-01 | GlobalTime + ITimeSeriesSource | int64μs, Stopwatch | PROOF + ARCH_CHECK |
| S1-02 | OwnedDoubleBuffer | 禁止lock, 100万次无撕裂 | PROOF + 压测日志 |
| S1-03 | VorticeDeviceManager | DPI/DeviceLost, 120FPS | PROOF + FPS日志 |
| S1-04 | EegMock + NirsMock | 160Hz/4ch + 4Hz/6ch | PROOF + 波形截图 |
| S1-05 | 系统集成 | 4+6通道闭环 | E2E_PROOF |

## 边界（不可越界）

```
❌ 不接真实 RS232 设备
❌ 不实现滤波器 / aEEG / GS
❌ 不做业务 UI
❌ 不改 spec/ 冻结文件
❌ 不实现存储/视频模块
```

---

# 十、默认决策（写入DECISIONS.md）

| 问题 | 默认决策 | 可配置 |
|------|----------|--------|
| 打印页眉字段 | 预留常见字段，不强制必填 | ✅ 是 |
| 存储上限 | 300 GiB | ✅ 是 |
| 删除策略 | 自动滚动删除无提示 | ✅ 可选开关 |
| 视频格式 | MP4(H.264)，退化为AVI | ✅ 是 |
| 增益档位 | 必须含 1000 μV/cm | ❌ 固定 |

---

# 附录：15条铁律速查

| # | 铁律 |
|---|------|
| 1 | Raw数据只读，采集后不可修改 |
| 2 | 不伪造波形，测试数据必须标记 |
| 3 | Zoom Out 必须 Min/Max，不可均值 |
| 4 | 滤波器必须 double 精度 |
| 5 | 缺失/饱和数据必须有标记 |
| 6 | 渲染线程只做 Draw |
| 7 | 时间戳使用 int64 微秒 |
| 8 | 所有参数必须可追溯 |
| 9 | 协议实现必须与 ICD 一致 |
| 10 | 每个模块必须有单元测试 |
| 11 | 异常必须记录，不可吞没 |
| 12 | 数据完整性必须验证 |
| 13 | 存储格式必须前向兼容 |
| 14 | 患者数据必须加密 |
| 15 | 审计日志必须防篡改 |

---

**🔒 END OF DEV_PLAYBOOK v2.1**
