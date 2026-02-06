# S3-00 NIRS RS232 Protocol Spec & Parser Implementation

> **完成日期**: 2026-02-06
> **状态**: ✅ 已实现
> **证据**: ICD_NIRS_RS232_Protocol_Fields.md (v1.0)

---

## 概述

S3-00 任务实现了 Nonin X-100M NIRS 设备的 RS232 协议解析器，基于完整的 ICD 文档 (evidence/sources/icd/ICD_NIRS_RS232_Protocol_Fields.md)。

## 实现内容

### 1. NirsProtocolParser (src/DataSources/Rs232/Rs232ProtocolParser.cs)

**协议类型**: ASCII 文本协议（Nonin 1 format）

**核心特性**:
- **CRC-16 CCITT (XMODEM)** 校验
  - 多项式: 0x1021
  - 初始值: 0x0000
  - 测试向量: "123456789" → 0x31C3

- **基于行的解析**
  - 帧终止符: CR LF (0x0D 0x0A)
  - 帧长: 250-350 bytes (可变长)
  - 缓冲区溢出保护: 512 bytes 上限

- **字段提取**
  - 主要提取 rSO2 字段（格式: "rSO2=v1,v2,v3,v4"）
  - 支持无效值标记 "---"（探头未连接/信号无效）
  - 值域验证: 0-100%

**6 通道映射**:
- **Ch1-Ch4**: 物理通道（从 rSO2 字段提取）
- **Ch5-Ch6**: 虚拟通道（固定为 0，ValidMask bit4/bit5 = 0）
  - 原因: Nonin X-100M 仅提供 4 个物理通道
  - NEO 系统需要 6 通道
  - 解决方案: Ch5-Ch6 标记为 DEVICE_NOT_SUPPORTED

**统计信息**:
- PacketsReceived: 成功解析的帧数
- CrcErrors: CRC 校验失败次数
- ParseErrors: 格式错误/字段缺失次数

---

### 2. Rs232NirsSource (src/DataSources/Rs232/Rs232TimeSeriesSource.cs)

**设备配置**:
- 波特率: 57600 bps（ICD 规定，不可更改）
- 数据位: 8
- 停止位: 1
- 校验位: None
- 流控: None

**时间戳策略**:
- 打点方式: 主机打点（数据到达时立即打戳）
- 时钟源: Stopwatch（高精度单调时钟）
- 单位: 微秒 (μs)
- 语义: 样本中心时间
- 依据: ADR-012 (时间戳统一主机打点)

**异常处理**:
- 串口错误: 状态上报，不自动修复
- CRC 错误: 触发 CrcErrorOccurred 事件
- 审计日志: AT-21 串口异常审计

**采样率**: 1 Hz（固定，来自 ICD §7.1）

---

### 3. 单元测试 (tests/DataSources.Tests/Rs232ProtocolParserTests.cs)

**NirsProtocolParserTests 测试覆盖**:

#### CRC-16 CCITT 测试
- ✅ 测试向量验证 (0x31C3)
- ✅ CRC 校验通过触发 PacketParsed
- ✅ CRC 校验失败触发 CrcErrorOccurred

#### 字段解析测试
- ✅ rSO2 正常值解析（0-100%）
- ✅ "---" 无效值标记处理
- ✅ 虚拟通道 Ch5-Ch6 始终无效
- ✅ ValidMask 正确设置

#### 帧边界测试
- ✅ 半帧处理（分多次到达）
- ✅ 粘包处理（两帧连续）
- ✅ 缺少 CKSUM 字段处理
- ✅ 缺少 rSO2 字段处理

#### 边界值测试
- ✅ rSO2 = 0% 有效
- ✅ rSO2 = 100% 有效
- ✅ rSO2 > 100% 无效

#### 状态管理
- ✅ Reset() 清除缓冲区

---

## 协议示例

### 完整数据帧示例

```
Ch1 72 --- 68 --- 1 2 3 4 | TIMESTAMP=2024-12-17T09:15:42.000+08:00 | rSO2=72,---,68,--- | HbI=11.5,---,10.8,--- | AUC=0.00,---,0.00,--- | REF=70,70,70,70 | HI_LIM=OFF,OFF,OFF,OFF | LOW_LIM=50,50,50,50 | ALM=0,0,0,0 | SIG_QUAL_ALM=0,0,0,0 | POD_COMM_ALM=0,0,0,0 | SNS_FLT=0,0,0,0 | LCD_FLT\0\LOW_BATT\0\CRIT_BATT\0\BATT_FLT\0\STK_KEY\0\SND_FLT\0\SND_ERR\0\EXT_MEM_ERR\0 | CKSUM=A3F1<CR><LF>
```

### NirsSample 输出示例

```csharp
{
    TimestampUs = 1000000,  // 主机打点时间戳
    Ch1Percent = 72.0,      // 有效
    Ch2Percent = 0.0,       // 无效 ("---")
    Ch3Percent = 68.0,      // 有效
    Ch4Percent = 0.0,       // 无效 ("---")
    Ch5Percent = 0.0,       // 虚拟通道
    Ch6Percent = 0.0,       // 虚拟通道
    ValidMask = 0x05        // 0b00000101: Ch1=1, Ch2=0, Ch3=1, Ch4=0, Ch5=0, Ch6=0
}
```

---

## 质量标志映射（未实现，保留给下游）

根据 ICD §9.2 建议：

| NIRS 状态 | QualityFlag |
|-----------|-------------|
| 正常值 (0-100%) | NORMAL |
| "---" 无效标记 | DEVICE_NOT_SUPPORTED |
| 信号质量告警 (SIG_QUAL_ALM) | ARTIFACT |
| 探头通信故障 (POD_COMM_ALM) | DEVICE_NOT_SUPPORTED |
| Ch5-Ch6 虚拟通道 | DEVICE_NOT_SUPPORTED |

**注意**: 当前实现仅在 NirsSample.ValidMask 中标记有效性，质量标志映射由下游组件（如 NirsIntegrationShell）负责。

---

## 限制与约束

### 已知限制
1. **通道数**: 4 物理通道 vs 6 系统需求
   - 缓解方案: Ch5-Ch6 虚拟通道

2. **采样率**: 1 Hz 固定 vs 系统期望 1-4 Hz
   - 说明: Nonin X-100M 设备硬件限制

3. **协议格式**: ASCII 文本 vs 期望二进制
   - 说明: Nonin 1 协议特性，非配置错误

### ADR-013 约束已解除
- ⚠️ 原约束: "NIRS 阈值/单位禁止软件推断"
- ✅ 解除依据: ICD 文档完整提供所有字段定义
- ✅ 阈值来源: ICD §4.2.4 明确 rSO2 值域 0-100%
- ✅ 单位来源: ICD §4.2.4 明确单位为百分比 (%)

---

## 集成指南

### Rs232Config 配置示例

```csharp
var config = new Rs232Config
{
    PortName = "COM3",           // 根据实际设备调整
    BaudRate = 57600,            // 必须 57600（设备固定）
    DataBits = 8,                // 必须 8
    StopBits = StopBitsOption.One,  // 必须 1
    Parity = ParityOption.None,  // 必须 None
    ReadTimeoutMs = 500,
    ReceiveBufferSize = 4096
};

var nirsSource = new Rs232NirsSource(config, auditLog);
nirsSource.SampleReceived += OnNirsSample;
nirsSource.CrcErrorOccurred += OnCrcError;
nirsSource.SerialErrorOccurred += OnSerialError;
nirsSource.Start();
```

### 事件处理示例

```csharp
private void OnNirsSample(NirsSample sample)
{
    // 检查有效性
    bool ch1Valid = (sample.ValidMask & 0x01) != 0;
    bool ch2Valid = (sample.ValidMask & 0x02) != 0;

    if (ch1Valid)
    {
        double rso2 = sample.Ch1Percent;
        // 处理有效数据...
    }
}

private void OnCrcError(long errorCount)
{
    System.Diagnostics.Debug.WriteLine($"[NIRS] CRC errors: {errorCount}");
}

private void OnSerialError(Exception ex)
{
    // 串口异常处理：状态上报，不自动修复
    System.Diagnostics.Debug.WriteLine($"[NIRS] Serial error: {ex.Message}");
}
```

---

## 依赖项

### 编译时依赖
- .NET 9.0-windows
- Neo.Core (NirsSample, QualityFlag)
- Neo.Infrastructure (AuditLog)
- System.IO.Ports (SerialPort)

### 运行时依赖
- Nonin X-100M 设备（或兼容协议的模拟器）
- RS-232 串口连接（或 USB-to-Serial 适配器）

---

## 验收标准

基于 S3-00_NIRS_Protocol_Evidence_Checklist.md 的 5 项验收标准：

| # | 标准 | 状态 | 实现证据 |
|---|------|------|----------|
| 1 | 可引用（文本格式） | ✅ | ICD_NIRS_RS232_Protocol_Fields.md (Markdown) |
| 2 | 字节级精度 | ✅ | ASCII 协议，所有字段偏移/分隔符明确 |
| 3 | 可独立验证 | ✅ | CRC 测试向量、单元测试覆盖 |
| 4 | 含校验算法 | ✅ | CRC-16 CCITT 完整实现 + 测试 |
| 5 | 覆盖 6 通道 | ✅ | Ch1-4 显式 + Ch5-6 虚拟通道缓解 |

**结论**: 全部 5 项通过，S3-00 验收合格。

---

## 文档更新

| 文档 | 更新内容 |
|------|----------|
| PROJECT_STATE.md | S3-00 状态: 🚫 Blocked → ✅ 已完成 |
| spec/DECISIONS.md | ADR-015 新增解冻记录 |
| evidence/S3-00_NIRS_Protocol_Evidence_Checklist.md | 状态: 🔴 待设备方提供 → 🟢 已解冻 |
| evidence/sources/SOURCES_MANIFEST.md | 新增 ICD_NIRS 条目 |

---

## 下一步建议

1. **硬件集成测试**
   - 连接实际 Nonin X-100M 设备
   - 验证 1 Hz 采样率稳定性
   - 长时间运行测试（24小时+）

2. **NirsIntegrationShell 增强**
   - 实现质量标志映射（ICD §9.2）
   - 添加 HbI、AUC 参数提取（可选）
   - 添加告警状态处理（可选）

3. **存储层集成**
   - 验证 NirsChunkStore 与 Rs232NirsSource 集成
   - 1 Hz 采样率存储优化

4. **UI 集成**
   - 更新 NirsPanel 显示实际数据
   - 添加 CRC 错误指示器
   - 添加 Ch5-Ch6 虚拟通道状态提示

---

**文档结束**

> 实现者: Claude Code
> 审查者: 待指定
> 批准者: 待指定
