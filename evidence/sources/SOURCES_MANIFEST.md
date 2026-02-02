# 原始证据清单（Sources Manifest）

> **创建日期**: 2026-01-28
> **更新日期**: 2026-01-28
> **状态**: ✅ 已补充完成
> **用途**: 追溯 CONSENSUS_BASELINE.md 中引用的原始证据

---

## 文件清单

| 文件 | 引用位置 | 关键内容 | 状态 |
|------|----------|----------|------|
| `ICD_EEG_RS232_Protocol_Fields.md` | RS232 实现 | EEG RS232 协议字段表 | ✅ 已创建 |
| `ICD Cerebraline Main UI RevA.docx` | CONSENSUS_BASELINE §2.2 | 主界面接口契约 | ✅ 已补充 |
| `ICD_Cerebraline_FDS_Manager_RevA.docx` | CONSENSUS_BASELINE §2.2 | FDS 管理接口 | ✅ 已补充 |
| `ICD_CerebraLine_Video_REV_A.docx` | CONSENSUS_BASELINE §2.2 | 视频模块接口 | ✅ 已补充 |
| `ICD_Crerebraline_AEEG_Module_revA.docx` | CONSENSUS_BASELINE §2.2 | aEEG 模块接口 | ✅ 已补充 |
| `SDS_Cerebraline_FDS_Manager_RevA.doc` | CONSENSUS_BASELINE §6.1 | FDS 设计规格 | ✅ 已补充 |
| `SDS_CerebraLine_AEEG_Module_revA.doc` | CONSENSUS_BASELINE §6.4 | aEEG 算法规格 | ✅ 已补充 |
| `SDS_Cerebraline_Main_UI_Rev_A.doc` | CONSENSUS_BASELINE | 主界面设计规格 | ✅ 已补充 |
| `SDS_CerebraLine_Video_REV_A.doc` | CONSENSUS_BASELINE §6.7 | 视频模块规格 | ✅ 已补充 |
| `readme.txt` | CONSENSUS_BASELINE §6.1 | EEG 参数确认 | ✅ 已补充 |
| `clogik_50_ser.cpp` | CONSENSUS_BASELINE §6.1 | 0.076 μV/LSB、协议解析 | ✅ 已补充 |
| `MR_CerebraLine_RevD.docx` | CONSENSUS_BASELINE §6.2 | 4通道配置 | ✅ 已补充 |

---

## 目录结构

```
evidence/sources/
├── SOURCES_MANIFEST.md        # 本文件
├── icd/                       # 接口控制文档
│   ├── ICD Cerebraline Main UI RevA.doc
│   ├── ICD_Cerebraline_FDS_Manager_RevA.doc
│   ├── ICD_CerebraLine_Video_REV_A.doc
│   └── ICD_Crerebraline_AEEG_Module_revA.doc
├── sds/                       # 软件设计规格
│   ├── readme.txt
│   ├── SDS_Cerebraline_FDS_Manager_RevA.doc
│   ├── SDS_CerebraLine_AEEG_Module_revA.doc
│   ├── SDS_Cerebraline_Main_UI_Rev_A.doc
│   └── SDS_CerebraLine_Video_REV_A.doc
├── device-specs/              # 设备规格书
│   └── MR_CerebraLine_RevD.docx
└── reference-code/            # 参考代码
    └── clogik_50_ser.cpp
```

---

## 引用追溯表

| CONSENSUS_BASELINE 章节 | 参数 | 原始来源 | 仓库路径 |
|------------------------|------|----------|----------|
| §6.1 EEG 参数 | 采样率 160 Hz | readme.txt / SDS | `evidence/sources/sds/readme.txt` |
| §6.1 EEG 参数 | 0.076 μV/LSB | clogik_50_ser.cpp | `evidence/sources/reference-code/clogik_50_ser.cpp` |
| §6.1 EEG 参数 | 波特率 115200 | clogik_50_ser.cpp | `evidence/sources/reference-code/clogik_50_ser.cpp` |
| §6.2 通道配置 | 4通道 (3+1) | MR文档 | `evidence/sources/device-specs/MR_CerebraLine_RevD.docx` |
| §6.4 aEEG 参数 | 2-15Hz 带通 | SDS_AEEG | `evidence/sources/sds/SDS_CerebraLine_AEEG_Module_revA.doc` |
| §6.5 NIRS 参数 | 6通道、1-4Hz | 待确认 | TBD |
| §6.5 NIRS 参数 | 阈值 50/65% | TBD (ADR-013) | 待设备规格确认 |
| §6.7 视频参数 | USB 摄像头 | SDS_Video | `evidence/sources/sds/SDS_CerebraLine_Video_REV_A.doc` |

---

## 待补充项

| 文件 | 说明 | 状态 |
|------|------|------|
| NIRS 设备规格 | ADR-013 约束，阈值来源 | ❌ 待补充 |

---

## ⚠️ 证据限制声明

### ICD 文件格式限制

| 文件 | 格式 | 限制 |
|------|------|------|
| `ICD_Crerebraline_AEEG_Module_revA.doc` | 二进制 .doc | 无法机器解析，需人工提取 |
| 其他 ICD 文件 | 二进制 .doc | 同上 |

**影响**：RS232 协议字段表无法自动提取，需人工导出为文本格式。

### EEG 字段映射完整证据

| 字段 | 证据状态 | 来源 |
|------|----------|------|
| data[0] (CH1) | ✅ 有证据 | DSP_SPEC.md L54, clogik_50_ser.cpp L84 |
| data[1] (CH2) | ✅ 有证据 | DSP_SPEC.md L55 |
| data[2] (CH3) | ✅ 有证据 | DSP_SPEC.md L56 |
| data[3] (GS Bin CH1) | ✅ 有证据 | DSP_SPEC.md L57, clogik_50_ser.cpp L76 |
| data[4] (GS Bin CH2) | ✅ 有证据 | DSP_SPEC.md L58 |
| data[9] (Config) | ✅ 有证据 | DSP_SPEC.md L59, clogik_50_ser.cpp L84 |
| data[16] (Counter) | ✅ 有证据 | DSP_SPEC.md L60, clogik_50_ser.cpp L75-76 |
| CH4 计算公式 | ✅ 有证据 | ACCEPTANCE_TESTS.md L477: CH4 = CH1 - CH2 |

**状态**：所有 EEG 字段已有完整证据支持。

---

**文档结束**
