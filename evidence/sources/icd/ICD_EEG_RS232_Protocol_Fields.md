# ICD: EEG RS232 协议字段表

> **版本**: 1.0
> **创建日期**: 2026-01-28
> **来源**: 从 spec/DSP_SPEC.md, clogik_50_ser.cpp 提取
> **状态**: 已验证

---

## 1. 数据包格式

| 字段 | 偏移 | 长度 | 说明 | 来源 |
|------|------|------|------|------|
| Header | 0 | 2 | 0xAA 0x55 | clogik_50_ser.cpp L29, L38 |
| Data | 2 | 36 | 18 个 int16 (大端序) | clogik_50_ser.cpp L48-55 |
| CRC | 38 | 2 | 累加和 (大端序) | clogik_50_ser.cpp L62-70 |
| **总长** | - | **40** | bytes | - |

---

## 2. Data 字段映射

| 索引 | 字段名 | 数据类型 | 说明 | 来源 |
|------|--------|----------|------|------|
| data[0] | EEG CH1 | int16 | 通道1原始值 | DSP_SPEC.md L54 |
| data[1] | EEG CH2 | int16 | 通道2原始值 | DSP_SPEC.md L55 |
| data[2] | EEG CH3 | int16 | 通道3原始值 | DSP_SPEC.md L56 |
| data[3] | GS Bin (CH1) | int16 | aEEG 直方图 bin (通道1) | DSP_SPEC.md L57 |
| data[4] | GS Bin (CH2) | int16 | aEEG 直方图 bin (通道2) | DSP_SPEC.md L58 |
| data[5-8] | (保留) | int16 | 未使用 | - |
| data[9] | Config | int16 | 配置字 | DSP_SPEC.md L59 |
| data[10-15] | (保留) | int16 | 未使用 | - |
| data[16] | GS Counter | int16 | 直方图计数器 | DSP_SPEC.md L60 |
| data[17] | (保留) | int16 | 未使用 | - |

---

## 3. GS Counter 语义

| 值 | 含义 | 来源 |
|----|------|------|
| 0-228 | 正常 GS 数据 | sds/readme.txt L11-12 |
| 229 | 15秒周期结束 | sds/readme.txt L12 |
| 255 | 数据无效，应忽略 | sds/readme.txt L11 |

---

## 4. 通道配置

| 通道 | 名称 | 导联 | 类型 | 来源 |
|------|------|------|------|------|
| CH1 | 通道1 | C3-P3 (A-B) | 物理 | CONSENSUS_BASELINE.md §6.2 |
| CH2 | 通道2 | C4-P4 (C-D) | 物理 | CONSENSUS_BASELINE.md §6.2 |
| CH3 | 通道3 | P3-P4 (B-C) | 物理 | CONSENSUS_BASELINE.md §6.2 |
| CH4 | 通道4 | C3-C4 (A-D) | **计算** | CONSENSUS_BASELINE.md §6.2 |

### 4.1 CH4 计算公式

```
CH4 = CH1 - CH2
```

来源: ACCEPTANCE_TESTS.md L477

---

## 5. 数值转换

| 参数 | 值 | 来源 |
|------|-----|------|
| 转换系数 | 0.076 μV/LSB | clogik_50_ser.cpp L84 |
| 公式 | μV = raw_value × 0.076 | clogik_50_ser.cpp L84 |

---

## 6. 串口参数

| 参数 | 值 | 来源 |
|------|-----|------|
| 波特率 | 115200 bps | CONSENSUS_BASELINE.md §12.2 |
| 数据位 | 8 | CONSENSUS_BASELINE.md §12.2 |
| 停止位 | 1 | CONSENSUS_BASELINE.md §12.2 |
| 校验位 | None | CONSENSUS_BASELINE.md §12.2 |

---

## 7. CRC 校验

| 项目 | 规格 | 来源 |
|------|------|------|
| 算法 | 累加和 | clogik_50_ser.cpp L31, L41, L54 |
| 范围 | Header + Data (字节 0-37) | clogik_50_ser.cpp L31-54 |
| 字节序 | 大端序 | clogik_50_ser.cpp L63, L69 |

---

## 8. NIRS RS232 协议

**状态**: ⛔ 未定义

CONSENSUS_BASELINE.md §12.3 仅定义接口参数，协议格式 TBD：

| 已定义 | 未定义 |
|--------|--------|
| 接口类型: RS232 | 帧头 |
| 通道数: 6 | 数据长度 |
| 采样率: 1-4 Hz | 字节序 |
| - | CRC 算法 |
| - | 字段映射 |

---

## 9. 证据溯源

| 文档 | 路径 | 用途 |
|------|------|------|
| DSP_SPEC.md | spec/DSP_SPEC.md | 字段映射定义 |
| CONSENSUS_BASELINE.md | spec/CONSENSUS_BASELINE.md | 串口参数、通道配置 |
| ACCEPTANCE_TESTS.md | spec/ACCEPTANCE_TESTS.md | CH4 计算公式 |
| clogik_50_ser.cpp | evidence/sources/reference-code/ | 协议解析参考实现 |
| readme.txt | evidence/sources/sds/ | GS Counter 语义 |

---

**文档结束**
