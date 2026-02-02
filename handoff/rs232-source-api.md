# Handoff: RS232 EEG TimeSeries Source (S1-02a)

> **Sprint**: S1
> **Task**: S1-02a (EEG-only)
> **创建日期**: 2026-01-28
> **状态**: ✅ 完成（ADR-015 范围裁决）
> **范围**: EEG + aEEG(GS) 解析，**不含 NIRS**

---

## 1. 支持的设备类型

| 设备类型 | 实现状态 | 备注 |
|----------|----------|------|
| EEG (Cerebralogik 5.0) | ✅ 已实现 | 协议来自 clogik_50_ser.cpp |
| NIRS | ⛔ 超出范围 | 协议格式未定义（见下方说明） |

### 1.1 NIRS 超出范围说明（ADR-015）

**裁决**: ADR-015 — S1-02 范围裁决与 NIRS 拆分

**原因**: CONSENSUS_BASELINE §12.3 仅定义 NIRS 接口参数（RS232/6通道/1-4Hz），**未定义协议格式**：

| 缺失项 | 说明 |
|--------|------|
| 帧头 | 未定义（EEG 为 0xAA 0x55） |
| 数据长度 | 未定义（EEG 为 36 字节） |
| 字节序 | 未定义（EEG 为大端序） |
| CRC 算法 | 未定义（EEG 为累加和） |
| 字段映射 | 未定义 |

**ADR-015 约束**:
- ❌ 不得实现
- ❌ 不得推断
- ❌ 不得写占位解析逻辑

**后续任务**: S3-00 NIRS RS232 Protocol Spec & Parser（Blocked）

**当前处理**: `Rs232NirsSource.Start()` 抛出 `NotImplementedException`。

---

## 2. 串口参数

| 参数 | 值 | 来源 |
|------|-----|------|
| 波特率 | 115200 bps | CONSENSUS_BASELINE §12.2, clogik_50_ser.cpp L120 |
| 数据位 | 8 | CONSENSUS_BASELINE §12.2 |
| 停止位 | 1 | CONSENSUS_BASELINE §12.2 |
| 校验位 | None | CONSENSUS_BASELINE §12.2 |

---

## 3. 协议格式（EEG）

### 3.1 帧结构

| 字段 | 偏移 | 长度 | 说明 |
|------|------|------|------|
| Header | 0 | 2 | 0xAA 0x55 |
| Data | 2 | 36 | 18 个 int16 (大端序) |
| CRC | 38 | 2 | 累加和 (大端序) |
| **总长** | - | **40** | bytes |

### 3.2 数据字段含义

| 索引 | 字段 | 说明 | 来源 |
|------|------|------|------|
| data[0] | EEG CH1 | 原始值 (int16) | DSP_SPEC.md L54 |
| data[1] | EEG CH2 | 原始值 (int16) | DSP_SPEC.md L55 |
| data[2] | EEG CH3 | 原始值 (int16) | DSP_SPEC.md L56 |
| data[3] | GS Bin (CH1) | aEEG 直方图 bin 值 | DSP_SPEC.md L57 |
| data[4] | GS Bin (CH2) | aEEG 直方图 bin 值 | DSP_SPEC.md L58 |
| data[9] | Config | 配置信息 | DSP_SPEC.md L59 |
| data[16] | GS Counter | 0-229 循环, 255=无效 | DSP_SPEC.md L60 |

### 3.3 CH4 计算通道

| 项目 | 规格 | 来源 |
|------|------|------|
| 公式 | CH4 = CH1 - CH2 | ACCEPTANCE_TESTS.md L477 |
| 含义 | C3-C4 (A-D) | CONSENSUS_BASELINE.md §6.2 |

### 3.4 数值转换

```
μV = raw_value * 0.076
```

来源: clogik_50_ser.cpp L84

---

## 4. CRC / 丢包处理策略

### 4.1 CRC 校验

| 项目 | 规格 |
|------|------|
| 算法 | 累加和 (sum of all bytes) |
| 范围 | Header + Data 所有字节 |
| 字节序 | 大端序 |
| 来源 | clogik_50_ser.cpp L31, L41, L54, L70 |

### 4.2 处理策略

| 情况 | 处理 |
|------|------|
| CRC 校验通过 | 发布 EegSample，QualityFlag = Normal |
| CRC 校验失败 | 丢弃数据包，触发 CrcErrorOccurred 事件 |
| 帧头不匹配 | 重置状态机，继续搜索帧头 |
| 丢包 | 由时间戳间隔检测（下游处理） |

### 4.3 统计信息

- `PacketsReceived`: 累计接收有效包数
- `CrcErrors`: 累计 CRC 错误数

---

## 5. 时间戳来源说明

| 项目 | 规格 | 来源 |
|------|------|------|
| 打点方式 | **主机打点** | ADR-012 |
| 时钟源 | `Stopwatch.GetTimestamp()` | ARCHITECTURE.md §13.2 |
| 单位 | 微秒 (μs) | CONSENSUS_BASELINE §5.1 |
| 语义 | **样本中心时间** | CONSENSUS_BASELINE §5.3 |
| 打点位置 | `SerialPort.DataReceived` 事件触发时 | Rs232TimeSeriesSource.cs |

### 5.1 时间戳精度

| 平台 | 典型精度 |
|------|----------|
| Windows | ~100 ns (Stopwatch) |
| 实际精度 | ±10-15 ms (受串口缓冲影响) |

---

## 6. 接口定义

### 6.1 Rs232EegSource

实现: `ITimeSeriesSource<EegSample>`

| 成员 | 类型 | 说明 |
|------|------|------|
| `SampleRate` | `int { get; }` | 固定 160 Hz |
| `ChannelCount` | `int { get; }` | 固定 4 |
| `SampleReceived` | `event Action<EegSample>?` | 样本到达事件 |
| `RawPacketReceived` | `event Action<EegSample, short[]>?` | 原始数据包事件 |
| `CrcErrorOccurred` | `event Action<long>?` | CRC 错误事件 |
| `SerialErrorOccurred` | `event Action<Exception>?` | 串口错误事件（状态上报） |
| `Start()` | `void` | 启动采集 |
| `Stop()` | `void` | 停止采集 |
| `Dispose()` | `void` | 释放资源 |

### 6.2 Rs232NirsSource

实现: `ITimeSeriesSource<NirsSample>`

**状态**: ⚠️ 预留接口，未实现

| 成员 | 说明 |
|------|------|
| `Start()` | 抛出 `NotImplementedException` |

---

## 7. 未实现内容

| 项目 | 原因 | 当前状态 | 计划 |
|------|------|----------|------|
| NIRS 协议解析 | 协议格式未定义 | NotImplementedException | 等待协议文档 |
| 命令发送 | 超出本任务范围 | 未实现 | 可扩展 |
| 设备重连 | 超出本任务范围 | 未实现 | S4 |

---

## 8. 文件清单

| 文件 | 用途 |
|------|------|
| `src/DataSources/Rs232/Rs232Config.cs` | 串口配置 |
| `src/DataSources/Rs232/Rs232ProtocolParser.cs` | 协议解析器 |
| `src/DataSources/Rs232/Rs232TimeSeriesSource.cs` | 数据源实现 |
| `tests/DataSources.Tests/Rs232ProtocolParserTests.cs` | 协议解析器单元测试 |

---

## 9. 证据引用

| 实现 | 依据文档 | 章节/行 |
|------|----------|---------|
| **ICD 字段表** | ICD_EEG_RS232_Protocol_Fields.md | evidence/sources/icd/ |
| 串口参数 | CONSENSUS_BASELINE.md | §12.2 |
| 帧格式 | clogik_50_ser.cpp | L27-91 |
| 字段映射 (data[0-16]) | DSP_SPEC.md | L53-60 |
| CH4 计算公式 | ACCEPTANCE_TESTS.md | L477 |
| CRC 算法 | clogik_50_ser.cpp | L31, L41, L54, L70 |
| 转换系数 | clogik_50_ser.cpp | L84 |
| 时间戳策略 | ADR-012, ARCHITECTURE.md | §13.2 |
| EEG 参数 | CONSENSUS_BASELINE.md | §6.1, §6.2 |
| NIRS 约束 | CONSENSUS_BASELINE.md | §12.3 (协议格式 TBD) |

---

## 10. 使用示例

```csharp
// 创建配置
var config = new Rs232Config
{
    PortName = "COM1",
    BaudRate = 115200
};

// 创建数据源
using var source = new Rs232EegSource(config);

// 订阅事件
source.SampleReceived += sample =>
{
    Console.WriteLine($"T={sample.TimestampUs} CH1={sample.Ch1Uv:F2}μV");
};

source.CrcErrorOccurred += count =>
{
    Console.WriteLine($"CRC errors: {count}");
};

// 启动采集
source.Start();

// ... 运行 ...

// 停止采集
source.Stop();
```

---

**文档结束**
