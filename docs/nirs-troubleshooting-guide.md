# NIRS 故障排查与调试指南

> **版本**: 1.0
> **更新日期**: 2026-02-06
> **适用设备**: Nonin X-100M Cerebral/Somatic Oximeter
> **协议**: Nonin 1 (ASCII Text Format)

---

## 目录

1. [设备连接故障排查](#1-设备连接故障排查)
2. [协议调试指南](#2-协议调试指南)
3. [串口抓包分析](#3-串口抓包分析)
4. [常见问题 FAQ](#4-常见问题-faq)
5. [集成测试指南](#5-集成测试指南)
6. [性能调优](#6-性能调优)

---

## 1. 设备连接故障排查

### 1.1 检查清单

在报告问题前，请依次检查：

| # | 检查项 | 预期结果 | 故障排除 |
|---|--------|---------|---------|
| 1 | 设备电源 | LED 指示灯亮起 | 检查电池/电源适配器 |
| 2 | RS-232 连接 | DB9 插头牢固连接 | 重新插拔连接器 |
| 3 | 串口识别 | 设备管理器显示 COM 端口 | 安装/更新串口驱动 |
| 4 | 端口占用 | 无其他程序占用串口 | 关闭串口监控工具 |
| 5 | 波特率配置 | 57600 bps | 检查软件配置 |
| 6 | 探头连接 | 探头连接至患者 | 重新贴合探头 |

### 1.2 设备管理器检查（Windows）

**步骤**:
1. Win + X → 设备管理器
2. 展开"端口 (COM 和 LPT)"
3. 查找 USB Serial Port 或 Prolific USB-to-Serial

**正常显示**:
```
端口 (COM 和 LPT)
  └─ USB Serial Port (COM3)  [或其他 COM 号]
```

**异常情况**:
- 黄色感叹号: 驱动程序问题
- 未显示设备: USB 连接问题或硬件故障
- 显示但无法打开: 权限问题或端口被占用

**解决方案**:
```bash
# 查看端口占用（PowerShell）
Get-Process | Where-Object {$_.StartInfo.FileName -like "*COM3*"}

# 强制关闭占用进程
taskkill /F /PID <PID>
```

### 1.3 串口配置验证

**NEO 配置** (`appsettings.json`):
```json
{
  "Nirs": {
    "PortName": "COM3",
    "BaudRate": 57600,
    "DataBits": 8,
    "StopBits": "One",
    "Parity": "None",
    "ReadTimeoutMs": 500,
    "ReceiveBufferSize": 4096
  }
}
```

**关键要点**:
- ⚠️ 波特率**必须为 57600**（Nonin X-100M 固定值，不可更改）
- ⚠️ 数据位: 8, 停止位: 1, 校验位: None (8N1)
- ⚠️ 无流控（No Hardware/Software Flow Control）

---

## 2. 协议调试指南

### 2.1 使用串口监控工具

推荐工具：
1. **RealTerm** (Windows)
2. **Serial Port Monitor** (专业工具)
3. **Termite** (轻量级)
4. **Python pyserial** (编程调试)

#### 2.1.1 RealTerm 配置

**步骤**:
1. 下载: https://sourceforge.net/projects/realterm/
2. Port 标签页:
   - Port: COM3
   - Baud: 57600
   - Data Bits: 8
   - Stop Bits: 1
   - Parity: None
   - Hardware Flow Control: None
3. Display 标签页:
   - Display As: Ascii
   - Newline: CR LF
4. 点击 "Change" 打开端口

**预期输出示例**:
```
Ch1 72 --- 68 --- 1 2 3 4 | TIMESTAMP=2024-12-17T09:15:42.000+08:00 | rSO2=72,---,68,--- | HbI=11.5,---,10.8,--- | CKSUM=A3F1<CR><LF>
Ch1 73 --- 69 --- 1 2 3 4 | TIMESTAMP=2024-12-17T09:15:43.000+08:00 | rSO2=73,---,69,--- | HbI=11.6,---,10.9,--- | CKSUM=B4C2<CR><LF>
```

**每秒应收到 1 帧**（1 Hz 采样率）。

#### 2.1.2 Python 调试脚本

```python
# nirs_debug.py - NIRS 串口调试工具
import serial
import time

def main():
    port = "COM3"  # 修改为实际端口
    baudrate = 57600

    try:
        ser = serial.Serial(port, baudrate, timeout=2)
        print(f"[OK] 已打开端口 {port} @ {baudrate} bps")
        print("正在接收数据 (Ctrl+C 退出)...\n")

        frame_count = 0
        while True:
            line = ser.readline()
            if line:
                frame_count += 1
                # 解码并显示
                try:
                    decoded = line.decode('ascii').strip()
                    print(f"[{frame_count}] {decoded}")
                except UnicodeDecodeError:
                    print(f"[{frame_count}] <解码错误> {line.hex()}")
            else:
                print("[WARN] 超时，未收到数据")

    except serial.SerialException as e:
        print(f"[ERROR] 串口错误: {e}")
    except KeyboardInterrupt:
        print(f"\n共接收 {frame_count} 帧")
    finally:
        if 'ser' in locals():
            ser.close()

if __name__ == "__main__":
    main()
```

**运行**:
```bash
python nirs_debug.py
```

### 2.2 CRC 校验验证

#### 2.2.1 手动计算 CRC-16 CCITT

**在线工具**: http://www.sunshine2k.de/coding/javascript/crc/crc_js.html

**参数设置**:
- Algorithm: CRC-16 CCITT (XMODEM)
- Polynomial: 0x1021
- Initial Value: 0x0000
- Final XOR: 0x0000
- Reverse Data: No
- Reverse CRC Result: No

**测试向量**:
```
输入: "123456789" (ASCII)
期望输出: 0x31C3
```

#### 2.2.2 NEO 内置 CRC 验证

NEO 解析器会自动验证 CRC。如果 CRC 错误频繁，检查：

```
[NIRS] CRC errors: 1    → 偶发：可能噪声干扰，可接受
[NIRS] CRC errors: 100  → 频繁：检查连接线/波特率/电磁干扰
```

**调试日志**:
```csharp
// 在 Rs232NirsSource.cs 中添加日志
private void OnCrcError(long errorCount)
{
    System.Diagnostics.Debug.WriteLine($"[NIRS] CRC error #{errorCount}");
    CrcErrorOccurred?.Invoke(errorCount);
}
```

---

## 3. 串口抓包分析

### 3.1 捕获串口数据

#### 3.1.1 使用 Serial Port Monitor

**步骤**:
1. 安装 Serial Port Monitor (HHD Software)
2. Session → New Session
3. 选择 COM 端口
4. Start Monitoring
5. 捕获 100+ 帧后停止
6. Export → Text File

**导出格式示例**:
```
[2024-12-17 09:15:42.123] RX (250 bytes):
Ch1 72 --- 68 --- 1 2 3 4 | TIMESTAMP=2024-12-17T09:15:42.000+08:00 | rSO2=72,---,68,--- | ...

[2024-12-17 09:15:43.125] RX (248 bytes):
Ch1 73 --- 69 --- 1 2 3 4 | TIMESTAMP=2024-12-17T09:15:43.000+08:00 | rSO2=73,---,69,--- | ...
```

#### 3.1.2 分析帧结构

**工具**: NEO 项目自带解析器测试工具

```bash
# 使用单元测试验证抓包数据
dotnet test tests/DataSources.Tests --filter "NirsProtocolParserTests"
```

**自定义验证脚本**:
```python
# analyze_nirs_capture.py
import sys

def parse_nirs_frame(line):
    # 提取 CKSUM
    if "CKSUM=" not in line:
        return False, "Missing CKSUM"

    parts = line.split("CKSUM=")
    data_part = parts[0] + "CKSUM="
    cksum_part = parts[1].strip()

    # 验证 CKSUM 长度
    if len(cksum_part) < 4:
        return False, f"Invalid CKSUM length: {len(cksum_part)}"

    cksum_hex = cksum_part[:4]

    # 提取 rSO2 字段
    if "rSO2=" not in line:
        return False, "Missing rSO2 field"

    rso2_start = line.index("rSO2=") + 5
    rso2_end = line.index("|", rso2_start)
    rso2_values = line[rso2_start:rso2_end].strip().split(",")

    return True, {
        "cksum": cksum_hex,
        "rso2": rso2_values
    }

# 使用
with open("nirs_capture.txt", "r") as f:
    for i, line in enumerate(f, 1):
        success, result = parse_nirs_frame(line)
        if success:
            print(f"[{i}] ✓ CRC={result['cksum']} rSO2={result['rso2']}")
        else:
            print(f"[{i}] ✗ {result}")
```

### 3.2 常见抓包问题

| 症状 | 可能原因 | 解决方案 |
|------|---------|---------|
| 无数据 | 设备未启动/线缆断开 | 检查设备电源和连接 |
| 乱码 | 波特率错误 | 确认 57600 bps |
| 帧不完整 | 缓冲区太小 | 增加 ReceiveBufferSize |
| CRC 全错 | 字节序错误/波特率错误 | 检查配置 |
| 帧率异常 | 非 1 Hz | 检查设备配置 |

---

## 4. 常见问题 FAQ

### Q1: 为什么只有 4 个通道有数据，Ch5/Ch6 一直显示无效？

**A**: Nonin X-100M 设备只提供 4 个物理通道 (Ch1-Ch4)。NEO 系统需要 6 通道，因此 Ch5-Ch6 被实现为**虚拟通道**，始终标记为 `LeadOff`（探头断开）状态。这是正常行为。

**来源**: ICD_NIRS_RS232_Protocol_Fields.md §6.2

---

### Q2: rSO2 值显示为 "---"，这是错误吗？

**A**: 不是错误。"---" 是 Nonin 协议的标准**无效值标记**，表示：
- 探头未连接至患者
- 探头贴合不良
- 信号质量不足

**排查步骤**:
1. 检查探头是否正确贴合
2. 清洁探头传感器表面
3. 重新定位探头
4. 检查设备报警指示

---

### Q3: CRC 错误率多高算正常？

**A**: 经验值：
- **< 0.1%**: 正常（偶发噪声）
- **0.1% - 1%**: 警告（检查连接）
- **> 1%**: 严重（必须处理）

**计算公式**:
```
CRC 错误率 = (CRC错误数 / 总帧数) × 100%
```

**改善措施**:
- 使用屏蔽串口线
- 远离强电磁干扰源（如电机、变频器）
- 检查接地

---

### Q4: 为什么采样率只有 1 Hz，不能提高吗？

**A**: 1 Hz 是 Nonin X-100M 设备的**硬件限制**，无法通过软件配置更改。这是 NIRS 设备的典型采样率（组织氧饱和度变化缓慢）。

**对比**:
- EEG: 160 Hz（脑电信号快速变化）
- NIRS: 1 Hz（组织氧代谢缓慢）
- 视频: 30 fps（可见运动）

---

### Q5: 串口打开失败 "Access Denied"，怎么办？

**A**: 端口被其他程序占用。

**排查**:
```bash
# Windows - 查找占用进程
# 使用 Sysinternals Process Explorer:
# 1. 下载 Process Explorer
# 2. Find → Find Handle or DLL
# 3. 搜索 "COM3"

# 或使用 PowerShell
Get-WmiObject Win32_SerialPort | Where-Object {$_.DeviceID -eq "COM3"}
```

**常见占用者**:
- Arduino IDE Serial Monitor
- PuTTY / Tera Term
- 其他 NEO 实例
- 驱动测试工具

---

### Q6: 时间戳不准确，与系统时间相差几秒？

**A**: NEO 使用**主机打点时间戳**（Stopwatch），而非设备时间戳。

**时间戳来源**:
```csharp
// Rs232NirsSource.cs
private static long GetHostTimestampUs()
{
    long ticks = Stopwatch.GetTimestamp();
    return ticks / TicksPerMicrosecond;
}
```

**对比**:
- 设备时间戳 (TIMESTAMP=...): 仅用于调试和日志关联
- 主机时间戳 (TimestampUs): 用于多流同步（EEG + NIRS + Video）

**同步策略**: ADR-012 (时间戳统一主机打点)

---

### Q7: 模拟数据源 (MockNirsSource) 如何使用？

**A**: 用于无硬件测试。

**配置示例**:
```csharp
// 在 NirsWiring.cs 或测试代码中
var timestampProvider = () => Stopwatch.GetTimestamp() / TicksPerMicrosecond;

var mockConfig = new MockNirsConfig
{
    BaseRso2 = 75.0,              // 基准 75%
    OscillationAmplitude = 10.0,  // ±10% 波动
    Ch2FailureProbability = 0.05  // Ch2 5% 概率断开
};

var mockSource = new MockNirsSource(timestampProvider, mockConfig);
var shell = new NirsIntegrationShell(mockSource);
shell.Start();
```

---

## 5. 集成测试指南

### 5.1 单元测试

**运行所有 NIRS 测试**:
```bash
cd F:\NEO_PROJECT
dotnet test --filter "Nirs"
```

**测试覆盖**:
- ✅ CRC-16 CCITT 测试向量 (0x31C3)
- ✅ 正常帧解析
- ✅ 无效值 "---" 处理
- ✅ 虚拟通道 Ch5-Ch6
- ✅ 半帧/粘包边界测试
- ✅ 并发访问测试

### 5.2 集成测试流程

**测试步骤**:
1. **准备环境**
   - 连接 Nonin X-100M 设备
   - 配置 COM 端口
   - 贴合探头至模拟假人或志愿者

2. **启动 NEO**
   ```bash
   dotnet run --project src/UI/Neo.UI.csproj
   ```

3. **验证 NIRS 面板**
   - Ch1-Ch4 显示 rSO2 值（60-90% 正常范围）
   - Ch5-Ch6 显示 "Lead Off"
   - 质量指示器正常

4. **模拟探头断开**
   - 移除 Ch2 探头
   - 观察 Ch2 显示 "---"
   - 质量标志变为 LeadOff

5. **长时间测试**
   - 运行 24 小时
   - 监控 CRC 错误率
   - 检查内存泄漏

### 5.3 性能基准

| 指标 | 预期值 | 测试方法 |
|------|-------|---------|
| 帧率 | 1 Hz ± 50 ms | 计时 100 帧 |
| CRC 错误率 | < 0.1% | 统计 10000 帧 |
| 内存占用 | < 50 MB (NIRS 模块) | Task Manager |
| CPU 占用 | < 1% (平均) | Performance Monitor |

---

## 6. 性能调优

### 6.1 缓冲区优化

**默认配置**:
```json
"ReceiveBufferSize": 4096
```

**调整建议**:
- 高延迟环境: 8192 bytes
- 低延迟环境: 2048 bytes
- 最小值: 512 bytes (>= 最大帧长 350 bytes)

### 6.2 线程优先级

**可选优化** (谨慎使用):
```csharp
// 在 Rs232NirsSource.Start() 中
Thread.CurrentThread.Priority = ThreadPriority.AboveNormal;
```

**注意**: 仅在多流同步要求严格时使用。

### 6.3 日志级别

**生产环境**:
```json
"Logging": {
  "LogLevel": {
    "Neo.NIRS": "Warning",
    "Neo.DataSources.Rs232": "Error"
  }
}
```

**调试环境**:
```json
"Logging": {
  "LogLevel": {
    "Neo.NIRS": "Debug",
    "Neo.DataSources.Rs232": "Trace"
  }
}
```

---

## 附录

### A. 参考文档

| 文档 | 路径 |
|------|------|
| NIRS ICD | `evidence/sources/icd/ICD_NIRS_RS232_Protocol_Fields.md` |
| S3-00 实现报告 | `handoff/s3-00-nirs-parser-implementation.md` |
| 项目状态 | `PROJECT_STATE.md` |
| API 文档 | `handoff/nirs-integration-shell-api.md` |

### B. 技术支持

**NEO 系统问题**:
- GitHub Issues: https://github.com/anthropics/claude-code/issues

**Nonin 设备问题**:
- Nonin Medical 官方支持
- 设备序列号位置: 设备背面标签

---

**文档结束**

> 维护者: Claude Code
> 审查周期: 每季度
> 最后审查: 2026-02-06
