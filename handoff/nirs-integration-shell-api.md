# Handoff: S3-01 NIRS Integration Shell

> **Sprint**: S3
> **Task**: S3-01
> **创建日期**: 2026-01-29
> **状态**: 完成
> **前置依赖**: S3-00 (Blocked)

---

## 1. 概述

本任务实现了 NIRS 模块的"集成壳（Integration Shell）"。

**核心原则: 只做系统层集成位，不实现任何协议或算法。**

系统启动时，NIRS 模块注册并标记为 Blocked，不产生任何数据，不显示伪数据。

---

## 2. 本模块明确不实现的内容

| 项目 | 状态 | 原因 |
|------|------|------|
| NIRS 数据解析器 | 不实现 | S3-00 Blocked |
| 模拟数据 / 假数据 | 不实现 | ADR-015 禁止 |
| 数值推断 | 不实现 | ADR-013 禁止 |
| DSP / aEEG / EEG 修改 | 不实现 | 已冻结模块 |

---

## 3. 当前阻塞原因

来源: PROJECT_STATE.md S3-00 Blocked, ADR-015

NIRS 模块的协议证据缺失，详见 PROJECT_STATE.md 中 S3-00 阻塞项。
证据到位前禁止实现、禁止推断、禁止写占位逻辑。

---

## 4. 文件清单

| 文件 | 用途 |
|------|------|
| `src/NIRS/Neo.NIRS.csproj` | NIRS 模块项目文件 |
| `src/NIRS/NirsIntegrationShell.cs` | 集成壳（阻塞状态管理） |
| `src/Host/NirsWiring.cs` | 装配/DI/生命周期 |
| `src/Core/Enums/QualityFlag.cs` | 新增 BlockedBySpec 标志 |

---

## 5. 组件 API

### 5.1 NirsIntegrationShell

```csharp
public sealed class NirsIntegrationShell : IDisposable
{
    public NirsShellStatus Status { get; }        // BlockedByMissingEvidence
    public string BlockReason { get; }             // 阻塞原因描述
    public bool IsAvailable { get; }               // false (当前)

    public void Start();                           // 记录阻塞状态
    public void Stop();                            // 无操作

    public static NirsSample CreateBlockedSample(long timestampUs);
    // → 所有通道 NaN, ValidMask = 0

    public static QualityFlag BlockedQualityFlags { get; }
    // → Undocumented | BlockedBySpec
}
```

### 5.2 NirsWiring

```csharp
public sealed class NirsWiring : IDisposable
{
    public NirsIntegrationShell Shell { get; }
    public bool IsNirsAvailable { get; }           // false (当前)
    public void Start();
    public void Stop();
}
```

### 5.3 QualityFlag 新增

```csharp
BlockedBySpec = 1 << 6  // 模块被规格证据阻塞
```

---

## 6. 行为规格

### 系统启动

```
MainForm.OnFormLoad()
  → NirsWiring.Start()
    → NirsIntegrationShell.Start()
      → Status = BlockedByMissingEvidence
      → Trace: "NIRS is blocked by missing protocol evidence (S3-00)."
```

### 运行时

- NIRS 模块不产生任何数据
- UI / 渲染层不报错、不显示伪数据
- 系统正常运行（仅 EEG 功能）

### 系统关闭

```
MainForm.OnFormClosing()
  → NirsWiring.Dispose()
```

---

## 7. 证据声明

- 本模块不含任何协议级信息
- 阻塞详情见 PROJECT_STATE.md S3-00
- 数值语义来自设备，不由软件推断（ADR-013）

---

**文档结束**
