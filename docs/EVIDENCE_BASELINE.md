# EVIDENCE Baseline Matrix

Last updated: 2026-02-09

## Purpose
This file defines which evidence document version is authoritative when multiple revisions conflict (for example RevA vs RevD). The goal is to keep code, tests, and reports aligned to one baseline.

## Priority Rule
1. Medical requirements baseline: use latest MR as primary.
2. Protocol baseline: use latest ICD for wire format details.
3. Legacy SRS/SDS/ICD RevA content is secondary reference unless explicitly adopted.
4. If two sources conflict, prefer the higher-priority source and record the decision in docs.

## Module Matrix
| Module | Primary Evidence | Secondary Evidence | Conflict Resolution |
|---|---|---|---|
| EEG channels/derivations | `MR_CerebraLine_RevD.docx` | `SRS Cerebraline Main UI Rev_A.doc` | Use RevD channel definitions (CH1..CH4 with CH4 computed). |
| EEG serial protocol | `ICD_*AEEG*_revA.doc` + parser test vectors | `clogik_50_ser.cpp` reference | Preserve byte-level compatibility with parser tests. |
| NIRS serial protocol | `Nonin脑氧连接协议.docx` | `页面提取自－Nonin脑氧连接协议.pdf` | Use Nonin 1 text frame as baseline (`CRLF`, `CRC-16 CCITT/XMODEM`, `rSO2`). |
| UI feature scope | `spec/UI_SPEC.md` | legacy SRS UI docs | Current product spec wins; legacy docs treated as historical reference. |

## Current Decisions
1. EEG display source options must include `CH1/CH2/CH3/CH4`.
2. NIRS transport enforces Nonin baseline serial config (`57600`, `8N1`).
3. NIRS parser currently treats `rSO2` as required field and parses it as the minimum clinical path; advanced fields are optional extensions.

## Verification Checklist
1. `WaveformViewModel.SourceOptions` contains CH1..CH4.
2. `Rs232NirsSource` rejects non-Nonin serial config.
3. Parser tests cover CRC-16 CCITT (XMODEM) vector `123456789 -> 0x31C3`.
4. Reports must not claim "no P0 issues" if checklist fails.
