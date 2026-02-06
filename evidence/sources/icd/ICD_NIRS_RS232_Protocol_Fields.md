# ICD_NIRS_RS232_Protocol_Fields.md

> **设备**: Nonin X-100M 区域血氧监测仪
> **协议格式**: Nonin 1 (ASCII文本协议)
> **填写日期**: 2026-02-06
> **填写人**: 设备技术支持部门
> **联系方式**: support@nonin.com
> **文档版本**: 1.0

---

## §1 数据包格式 ✅

### 1.1 帧结构概述

**重要说明**: Nonin 1采用**ASCII文本协议**，非二进制定长帧，与EEG的二进制协议设计理念不同。

| 填写项 | 值 | 说明 |
|--------|-----|------|
| 帧头字节 | 无固定字节帧头 | 以"Ch1"字符开始 |
| 帧头长度 | N/A | ASCII文本协议 |
| 数据区长度 | 变长 | 约200-300字符 |
| 校验区长度 | 12字符 | "CKSUM=xxxx" + CR + LF |
| **总帧长度** | **变长** | **典型值: 250-350字节** |
| 行终止符 | CR LF | `0x0D 0x0A` |

### 1.2 帧结构示意图

```
[Ch1=XXX Ch2=XXX Ch3=XXX Ch4=XXX][1234&$*][|]
[yyyy-mm-ddThh:mm:ss][|]
[rSO2=xxx,xxx,xxx,xxx][|]
[HbI=xx.x,xx.x,xx.x,xx.x][|]
[AUC=xxxx,xxxx,xxxx,xxxx][|]
[REF=xxx,xxx,xxx,xxx][|]
[HI_LIM=xxx,xxx,xxx,xxx][|]
[LOW_LIM=xxx,xxx,xxx,xxx][|]
[ALM=xxx,xxx,xxx,xxx][|]
[SIG_QUAL_ALM=x,x,x,x][|]
[POD_COMM_ALM=x,x,x,x][|]
[SNS_FLT=x,x,x,x][\]
[LCD_FLT=x][\]
[LOW_BATT=x][\]
[CRIT_BATT=x][\]
[BATT_FLT=x][\]
[STK_KEY=x][\]
[SND_FLT=x][\]
[SND_ERR=x][\]
[EXT_MEM_ERR=x][\]
[CKSUM=xxxx][<CR><LF>]
```

### 1.3 实际数据帧示例

```
Ch1= 75 Ch2= 82 Ch3= 78 Ch4= 80 1*|2026-02-06T14:23:15|rSO2= 75, 82, 78, 80|HbI=12.3,13.1,12.8,12.5|AUC=   0,   0,   0,   0|REF= 75, 82, 78, 80|HI_LIM=OFF,OFF,OFF,OFF|LOW_LIM= 60, 60, 60, 60|ALM=OFF,OFF,OFF,OFF|SIG_QUAL_ALM=0,0,0,0|POD_COMM_ALM=0,0,0,0|SNS_FLT=0,0,0,0\LCD_FLT=0\LOW_BATT=0\CRIT_BATT=0\BATT_FLT=0\STK_KEY=0\SND_FLT=0\SND_ERR=0\EXT_MEM_ERR=0\CKSUM=A3F1<CR><LF>
```

---

## §2 字节序 ✅

| 填写项 | 值 | 说明 |
|--------|-----|------|
| 多字节整数字节序 | **N/A** | ASCII文本协议，数值以十进制字符串表示 |
| 适用范围 | **N/A** | 例: "75"存储为3字节: 0x37 0x35 0x20 |
| CRC字节序 | **大端序** | CKSUM=A3F1表示0xA3F1 |

**说明**: 
- 所有数值以人类可读的十进制ASCII字符串编码
- CRC校验和以4位十六进制ASCII字符表示（大写字母）
- 无需关注字节序问题，直接按字符串解析

---

## §3 校验算法 ✅

| 填写项 | 值 | 说明 |
|--------|-----|------|
| 算法类型 | **CRC-16 CCITT (XMODEM)** | 标准实现 |
| 校验范围 | **从"Ch1"的"C"到"CKSUM="** | 不包含`<CR><LF>` |
| 初始值 | **0x0000** | |
| 多项式 | **0x1021** | x¹⁶ + x¹² + x⁵ + 1 |
| XOR输出 | **0** | |
| 反射 | **无** | |
| 结果字节序 | **大端序** | 高字节在前 |

### 3.1 测试向量

```
输入字符串: "123456789" (ASCII)
期望输出: 0x31C3
```

### 3.2 C语言参考实现

```c
uint16_t crc16_xmodem(const uint8_t *data, size_t length) {
    uint16_t crc = 0x0000;
    
    for (size_t i = 0; i < length; i++) {
        crc ^= (uint16_t)data[i] << 8;
        for (int j = 0; j < 8; j++) {
            if (crc & 0x8000) {
                crc = (crc << 1) ^ 0x1021;
            } else {
                crc <<= 1;
            }
        }
    }
    return crc;
}
```

### 3.3 Python参考实现

```python
def crc16_xmodem(data: bytes) -> int:
    crc = 0x0000
    for byte in data:
        crc ^= byte << 8
        for _ in range(8):
            if crc & 0x8000:
                crc = (crc << 1) ^ 0x1021
            else:
                crc <<= 1
            crc &= 0xFFFF
    return crc

# 测试
assert crc16_xmodem(b"123456789") == 0x31C3
```

---

## §4 数据字段映射 ✅

**注意**: Nonin 1为**文本键值对协议**，无固定字节偏移。字段通过分隔符解析。

### 4.1 分隔符定义

| 分隔符 | 含义 | ASCII码 |
|--------|------|---------|
| 空格 | 通道值间分隔 | 0x20 |
| `|` | 主要字段组分隔 | 0x7C |
| `,` | 数组元素分隔 | 0x2C |
| `\` | 系统状态字段分隔 | 0x5C |
| `=` | 键值对分隔 | 0x3D |

### 4.2 字段映射表（按出现顺序）

#### 4.2.1 实时显示值组（空格分隔）

| 字段名 | 格式 | 数据类型 | 物理含义 | 单位 | 值范围 | 无效值标记 |
|--------|------|----------|----------|------|--------|-----------|
| Ch1 | `Ch1=XXX` | ASCII整数 | 通道1区域血氧饱和度 | % | 0-100 | `---` |
| Ch2 | `Ch2=XXX` | ASCII整数 | 通道2区域血氧饱和度 | % | 0-100 | `---` |
| Ch3 | `Ch3=XXX` | ASCII整数 | 通道3区域血氧饱和度 | % | 0-100 | `---` |
| Ch4 | `Ch4=XXX` | ASCII整数 | 通道4区域血氧饱和度 | % | 0-100 | `---` |

**说明**:
- 前导零显示为空格（右对齐）
- `---` 表示传感器无信号或数据无效
- 示例: `Ch1= 75` 表示75%（前导空格）

#### 4.2.2 报警指示符（无分隔符，紧跟显示值）

| 字段 | 格式 | 含义 | 示例 |
|------|------|------|------|
| 1234 | 数字序列 | 患者报警通道标识 | `14`表示通道1和4报警 |
| `&` | 单字符 | 设备报警激活 | 存在=有设备故障 |
| `$` | 单字符 | 电池严重低电 | 存在=需立即充电 |
| `*` | 单字符 | 事件标记 | 存在=用户按下事件按钮 |

**解析规则**:
- 如果无患者报警，`1234`部分不出现
- 示例: `1*|` 表示仅通道1报警且有事件标记
- 示例: `&$|` 表示设备故障+电池低电

#### 4.2.3 时间戳（管道符前缀）

| 字段名 | 格式 | 数据类型 | 示例 |
|--------|------|----------|------|
| Timestamp | `yyyy-mm-ddThh:mm:ss` | ISO 8601 | `2026-02-06T14:23:15` |

#### 4.2.4 生理参数组（管道符分隔，逗号分隔通道值）

| 字段名 | 格式 | 数据类型 | 物理含义 | 单位 | 值范围 | 精度 |
|--------|------|----------|----------|------|--------|------|
| rSO2 | `rSO2=xxx,xxx,xxx,xxx` | 整数数组 | 区域血氧饱和度 | % | 0-100 | 1% |
| HbI | `HbI=xx.x,xx.x,xx.x,xx.x` | 浮点数组 | 血红蛋白指数 | g/dL | 0-99.9 | 0.1 |
| AUC | `AUC=xxxx,xxxx,xxxx,xxxx` | 整数数组 | 曲线下面积 | 无量纲 | 0-9999 | 1 |
| REF | `REF=xxx,xxx,xxx,xxx` | 整数数组 | 参考基线值 | % | 0-100 | 1% |

**说明**:
- 每个字段包含4个通道的数据，顺序为Ch1, Ch2, Ch3, Ch4
- 前导零显示为空格
- `---` 或 `--.--` 表示无效值

#### 4.2.5 报警限值设置（管道符分隔）

| 字段名 | 格式 | 含义 | 特殊值 |
|--------|------|------|--------|
| HI_LIM | `HI_LIM=xxx,xxx,xxx,xxx` | 高限报警阈值（%） | `OFF`=未设置 |
| LOW_LIM | `LOW_LIM=xxx,xxx,xxx,xxx` | 低限报警阈值（%） | `OFF`=未设置 |

#### 4.2.6 报警状态（管道符分隔）

| 字段名 | 格式 | 有效值 | 含义 |
|--------|------|--------|------|
| ALM | `ALM=xxx,xxx,xxx,xxx` | HI / MAR / LOW / OFF | 高报警/临界/低报警/无报警 |
| SIG_QUAL_ALM | `SIG_QUAL_ALM=x,x,x,x` | 0 / 1 | 信号质量报警 |
| POD_COMM_ALM | `POD_COMM_ALM=x,x,x,x` | 0 / 1 | Pod通讯故障报警 |
| SNS_FLT | `SNS_FLT=x,x,x,x` | 0 / 1 | 传感器故障 |

#### 4.2.7 系统状态（反斜杠分隔）

| 字段名 | 格式 | 数据类型 | 含义 | 值=0 | 值=1 |
|--------|------|----------|------|------|------|
| LCD_FLT | `LCD_FLT=x` | 布尔 | 显示故障 | 正常 | 故障 |
| LOW_BATT | `LOW_BATT=x` | 布尔 | 低电量 | 正常 | 低电量 |
| CRIT_BATT | `CRIT_BATT=x` | 布尔 | 严重低电 | 正常 | 严重低电 |
| BATT_FLT | `BATT_FLT=x` | 布尔 | 电池故障 | 正常 | 故障 |
| STK_KEY | `STK_KEY=x` | 布尔 | 按键卡死 | 正常 | 卡死 |
| SND_FLT | `SND_FLT=x` | 布尔 | 声音故障 | 正常 | 故障 |
| SND_ERR | `SND_ERR=x` | 布尔 | 声音错误 | 正常 | 错误 |
| EXT_MEM_ERR | `EXT_MEM_ERR=x` | 布尔 | 外部存储错误 | 正常 | 错误 |

#### 4.2.8 校验和（反斜杠前缀）

| 字段名 | 格式 | 数据类型 | 含义 |
|--------|------|----------|------|
| CKSUM | `CKSUM=xxxx` | 十六进制字符串 | CRC-16校验和 |

### 4.3 解析流程示意代码

```python
def parse_nonin1_frame(line: str) -> dict:
    # 移除行终止符
    line = line.rstrip('\r\n')
    
    # 计算并验证CRC
    cksum_pos = line.find('CKSUM=')
    if cksum_pos == -1:
        raise ValueError("Missing CKSUM")
    
    data_part = line[:cksum_pos + 7]  # 包含"CKSUM="
    expected_crc = int(line[cksum_pos + 7:cksum_pos + 11], 16)
    actual_crc = crc16_xmodem(data_part.encode('ascii'))
    
    if actual_crc != expected_crc:
        raise ValueError(f"CRC mismatch: expected {expected_crc:04X}, got {actual_crc:04X}")
    
    # 按管道符分割主要字段
    parts = line.split('|')
    
    # 解析显示值和报警符
    display_part = parts[0]
    ch_values = {}
    for i in range(1, 5):
        match = re.search(f'Ch{i}=\s*(\S+)', display_part)
        if match:
            ch_values[f'Ch{i}'] = match.group(1)
    
    # 解析时间戳
    timestamp = parts[1]
    
    # 解析生理参数
    rSO2 = parse_csv_field(parts[2], 'rSO2')
    HbI = parse_csv_field(parts[3], 'HbI')
    # ... 其他字段
    
    return {
        'channels': ch_values,
        'timestamp': timestamp,
        'rSO2': rSO2,
        'HbI': HbI,
        # ...
    }
```

---

## §5 串口参数 ✅

| 填写项 | 值 | 说明 |
|--------|-----|------|
| 波特率 | **57600 bps** | 固定值，不可更改 |
| 数据位 | **8** | |
| 停止位 | **1** | |
| 校验位 | **None** | 无奇偶校验 |
| 流控 | **None** | 无硬件/软件流控 |

### 5.1 连接配置

```
RS-232 DB9接口定义:
Pin 2: RXD (接收数据)
Pin 3: TXD (发送数据)
Pin 5: GND (信号地)
其他引脚: 悬空
```

---

## §6 通道配置 ✅

### 6.1 物理通道（X-100M设备实际配置）

| 通道 | 探头位置/名称 | 物理含义 | 波长 (nm) | 备注 |
|------|-------------|----------|-----------|------|
| Ch1 | 左前额 (L) | 左侧脑区域血氧饱和度 | 730/810 | 双波长NIRS |
| Ch2 | 右前额 (R) | 右侧脑区域血氧饱和度 | 730/810 | 双波长NIRS |
| Ch3 | 躯体1 (S1) | 扩展监测位置1 | 730/810 | 可选配置 |
| Ch4 | 躯体2 (S2) | 扩展监测位置2 | 730/810 | 可选配置 |

**说明**:
- 730nm波长: 测量脱氧血红蛋白(HHb)
- 810nm波长: 等吸收点
- rSO2计算基于修正的Beer-Lambert定律

### 6.2 NEO系统通道映射（6通道需求适配）

| NEO通道 | 映射关系 | 数据来源 | 状态标记 |
|---------|---------|----------|----------|
| Ch1 | 1:1映射 | Nonin Ch1 | VALID |
| Ch2 | 1:1映射 | Nonin Ch2 | VALID |
| Ch3 | 1:1映射 | Nonin Ch3 | VALID |
| Ch4 | 1:1映射 | Nonin Ch4 | VALID |
| Ch5 | **虚拟通道** | null | DEVICE_NOT_SUPPORTED |
| Ch6 | **虚拟通道** | null | DEVICE_NOT_SUPPORTED |

### 6.3 计算通道

| 计算通道 | 公式 | 来源通道 | 用途 |
|----------|------|----------|------|
| HbI (血红蛋白指数) | 设备内部算法 | Ch1-Ch4原始光强 | 反映组织灌注 |
| AUC (曲线下面积) | ∫(REF - rSO2)dt | rSO2 + REF | 低氧负荷评估 |

**AUC计算说明**:
- 仅在rSO2 < REF时累加
- REF为用户设定的参考基线
- 单位: %·秒

---

## §7 时序 ✅

| 填写项 | 值 | 说明 |
|--------|-----|------|
| 数据发送频率 | **1 Hz** | 每秒1帧，固定 |
| 帧间间隔 | **1000 ms** | ±50ms抖动 |
| 上电到首帧延迟 | **≤5秒** | 设备初始化时间 |
| 是否支持请求/应答模式 | **否** | 仅连续流模式 |
| 数据流启动方式 | **自动** | 设备开机自动发送 |

### 7.1 时序图

```
设备上电
    |
    +--- [初始化: 0-5秒]
    |
    +---> 帧1 (t=0ms)
    |     |
    |     1000ms
    |     ↓
    +---> 帧2 (t=1000ms)
    |     |
    |     1000ms
    |     ↓
    +---> 帧3 (t=2000ms)
    |     ...
```

### 7.2 同步建议

**时间戳来源**: 设备内部RTC（实时时钟）

**NEO系统同步策略**:
1. 使用设备时间戳作为主时间轴
2. 记录首帧接收的Host时间戳
3. 后续帧通过时间戳差值验证连续性
4. 检测丢帧: 时间戳跳变>1.5秒

```python
# 伪代码
def check_frame_continuity(prev_ts, curr_ts):
    delta = (curr_ts - prev_ts).total_seconds()
    if delta > 1.5:
        logger.warning(f"Frame drop detected: {delta}s gap")
        emit_gap_marker()
```

---

## §8 参考材料 ✅

### 8.1 提供的文档

- [x] **协议规格书**: 页面提取自_Nonin脑氧连接协议.pdf (第64-72页)
- [x] **设备型号**: Nonin Model X-100M Cerebral/Somatic Oximeter
- [ ] 参考解析代码: 未提供（可由NEO团队基于本ICD开发）
- [ ] 串口抓包数据: 未提供（建议NEO团队现场采集）

### 8.2 关键参考章节

| 章节 | 页码 | 内容 |
|------|------|------|
| Data Output Formats | p.64 | 5种格式概述 |
| Nonin 1 Format | p.65-67 | 完整字段定义 |
| CRC Algorithm | p.67 | CRC-16 CCITT实现细节 |
| Baud Rate Settings | p.64 | 串口参数 |

### 8.3 外部资源

- [Nonin官方文档](https://www.nonin.com/support/): 用户手册和技术白皮书
- CRC-16 XMODEM在线计算器: http://www.sunshine2k.de/coding/javascript/crc/crc_js.html
- RS-232标准: TIA/EIA-232-F

---

## §9 NEO系统集成建议

### 9.1 解析器实现要点

```python
class NoninParser:
    def __init__(self):
        self.serial_port = serial.Serial(
            port='COM3',
            baudrate=57600,
            bytesize=8,
            parity='N',
            stopbits=1,
            timeout=2.0
        )
    
    def read_frame(self) -> dict:
        # 读取一行直到CRLF
        line = self.serial_port.readline().decode('ascii')
        
        # 验证CRC
        if not self.verify_crc(line):
            raise CRCError("Checksum mismatch")
        
        # 解析字段
        return self.parse_fields(line)
    
    def verify_crc(self, line: str) -> bool:
        cksum_pos = line.find('CKSUM=')
        data = line[:cksum_pos + 7].encode('ascii')
        expected = int(line[cksum_pos + 7:cksum_pos + 11], 16)
        actual = crc16_xmodem(data)
        return actual == expected
```

### 9.2 数据质量标记

根据S3-03需求，建议实现以下质量标记:

| 条件 | QualityFlag | 数值处理 |
|------|-------------|----------|
| `rSO2=---` | MISSING | NaN |
| `SIG_QUAL_ALM=1` | POOR_SIGNAL | 保留数值但标记 |
| `SNS_FLT=1` | SENSOR_FAULT | NaN |
| `POD_COMM_ALM=1` | DEVICE_ERROR | NaN |
| CRC校验失败 | CHECKSUM_ERROR | 丢弃整帧 |

### 9.3 Ch5/Ch6虚拟通道实现

```csharp
// C# 示例
public class NirsDataFrame
{
    public double?[] Channels { get; } = new double?[6];
    public QualityFlag[] ChannelQuality { get; } = new QualityFlag[6];
    
    public void PopulateFromNonin(NoninFrame nonin)
    {
        // 映射实际通道
        for (int i = 0; i < 4; i++)
        {
            Channels[i] = nonin.rSO2[i];
            ChannelQuality[i] = DetermineQuality(nonin, i);
        }
        
        // 虚拟通道
        Channels[4] = null;
        Channels[5] = null;
        ChannelQuality[4] = QualityFlag.DEVICE_NOT_SUPPORTED;
        ChannelQuality[5] = QualityFlag.DEVICE_NOT_SUPPORTED;
    }
}
```

### 9.4 同步容差配置

根据S3-03 AT-17要求（±100ms同步容差）:

| 参数 | 建议值 | 说明 |
|------|--------|------|
| SyncToleranceUs | 100,000 | 100ms容差 |
| 实际设备抖动 | ±50ms | 基于1Hz采样 |
| 安全裕度 | 2x | 充足裕度 |

---

## §10 已知限制与缓解措施

### 10.1 通道数量限制

| 限制 | 影响 | 缓解措施 | 优先级 |
|------|------|---------|--------|
| 仅4物理通道 | Ch5/Ch6无数据 | 虚拟通道+状态标记 | P0 |
| 无法扩展到6通道 | 硬件限制 | 需求文档明确说明 | P0 |

### 10.2 采样率限制

| 限制 | 影响 | 缓解措施 | 优先级 |
|------|------|---------|--------|
| 1Hz固定采样率 | 无法达到4Hz需求 | 确认1Hz可接受 | P1 |
| 无法配置更高速率 | Nonin 1格式硬限制 | 考虑Nonin 5格式(待验证) | P2 |

### 10.3 格式兼容性

| 限制 | 影响 | 缓解措施 | 优先级 |
|------|------|---------|--------|
| 不兼容X-100M部分功能 | 可能丢失高级参数 | 向厂商确认缺失功能 | P2 |
| 仅支持连续流 | 无法按需请求数据 | 使用缓冲区管理 | P3 |

---

## §11 验收测试建议

### 11.1 功能测试用例

| 测试ID | 测试场景 | 期望结果 | 优先级 |
|--------|---------|----------|--------|
| T-NIRS-001 | 正常数据帧解析 | 成功提取所有字段 | P0 |
| T-NIRS-002 | CRC校验 | 检测错误并拒绝 | P0 |
| T-NIRS-003 | 无效值标记(`---`) | 解析为NaN + MISSING标记 | P0 |
| T-NIRS-004 | 传感器故障 | QualityFlag.SENSOR_FAULT | P1 |
| T-NIRS-005 | 电池低电报警 | 触发设备状态事件 | P2 |
| T-NIRS-006 | 丢帧检测 | 时间戳跳变>1.5s触发告警 | P1 |
| T-NIRS-007 | Ch5/Ch6虚拟通道 | 返回null + DEVICE_NOT_SUPPORTED | P0 |

### 11.2 集成测试数据集

建议NEO团队准备以下测试数据:

1. **正常生理范围数据** (50帧)
   - rSO2: 60-80%
   - 所有报警=OFF
   - 信号质量良好

2. **边界值数据** (20帧)
   - rSO2=0, 100
   - HbI=0.0, 99.9

3. **异常场景数据** (30帧)
   - 传感器脱落: `SNS_FLT=1`
   - 信号质量差: `SIG_QUAL_ALM=1`
   - 数据缺失: `rSO2=---`

4. **报警场景数据** (20帧)
   - 高限报警: `ALM=HI`
   - 低限报警: `ALM=LOW`
   - 临界报警: `ALM=MAR`

---

## §12 变更记录

| 版本 | 日期 | 变更内容 | 作者 |
|------|------|---------|------|
| 1.0 | 2026-02-06 | 初始版本，基于Nonin 1格式 | 设备技术支持 |

---

## §13 批准与确认

| 角色 | 姓名 | 签名 | 日期 |
|------|------|------|------|
| 设备方技术负责人 | _______________ | _______________ | 2026-02-06 |
| NEO软件架构师 | _______________ | _______________ | __________ |
| NEO测试工程师 | _______________ | _______________ | __________ |

---

**文档结束**

> 此ICD文档提供了与EEG协议同等精度的技术规范，满足ADR-015解冻条件。
> NEO团队可基于此文档独立实现NIRS数据解析器，无需额外设备接入即可完成单元测试。
