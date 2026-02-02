# Checkpoint - 初始化

> 创建时间: 2026-01-25 00:00  
> **格式要求**：必须包含 FACTS / CONTEXT / NEXT ACTIONS 三段，顺序固定

---

## 1. FACTS（不可争议事实）

### 已实现模块
无（项目刚初始化）

### 当前数据结构/接口签名
无

### 已产出证据
无

---

## 2. CONTEXT（背景）

### 当前 Sprint 目标
Sprint 1：渲染底座 + 模拟数据

### 为什么做到这一步
项目刚初始化，尚未开始开发

### 已知风险/阻塞
无

---

## 3. NEXT ACTIONS（可直接执行）

### 下一步必须修改的具体文件
1. 创建 `src/Core/Models/GlobalTime.cs`
2. 创建 `src/Core/Interfaces/ITimeSeriesSource.cs`
3. 创建 `tests/Core.Tests/GlobalTimeTests.cs`

### 明确禁止修改的文件
- `spec/` 目录下所有文件（冻结）

### 执行命令
```bash
# 开始 S1-01 任务
dotnet new classlib -n Neo.Core -o src/Core
```

---

## 续航指令（新会话粘贴）

```
【续航模式】

读取以下文件：
1. status/CHECKPOINT.md（本文件）
2. status/PROGRESS.md
3. status/OPEN_QUESTIONS.md
4. status/UNKNOWN_ASSUMPTIONS.md
5. spec/00_CONSTITUTION.md
6. spec/DEV_PLAYBOOK.md

从 NEXT ACTIONS 开始执行。
遵循 DEV_PLAYBOOK 中的所有规则。
```
