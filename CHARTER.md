# NEO UI 开发宪章 (CHARTER)

> **版本**: 1.0  
> **状态**: 🔒 FROZEN — 不可违反  
> **生效日期**: 2026-01-30  

---

## 一、统一文件清单（唯一认可）

| 序号 | 文件名 | 用途 | 位置 |
|------|--------|------|------|
| 1 | `CHARTER.md` | 项目宪法（本文件） | spec/ |
| 2 | `UI_SPEC.md` | UI 功能冻结规格 | spec/ |
| 3 | `PROJECT_STATEUI.md` | **唯一进度锚点** | 根目录 |
| 4 | `NEO_UI_Development_Plan_WPF.md` | 开发技术方案 | 根目录 |
| 5 | `ACCEPTANCE_TESTS.md` | 验收测试清单 | spec/ |
| 6 | `PROMPT_UI_EXEC_CLAUDE.md` | Claude 执行 Prompt | 根目录 |
| 7 | `PROMPT_UI_AUDIT_CODEX.md` | Codex 审计 Prompt | 根目录 |

**🚫 禁止创建**: `PROJECT_STATE.md`, `PROJECT_STATE_UI.md`, 或任何其他变体。

---

## 二、权力等级

```
Level 1 (最高): CHARTER.md
Level 2: UI_SPEC.md
Level 3: ACCEPTANCE_TESTS.md
Level 4: NEO_UI_Development_Plan_WPF.md
Level 5: PROJECT_STATEUI.md
```

**冲突解决**: 高 Level 优先。

---

## 三、铁律（IRON RULES）

### 🔴 医学数据铁律

| 编号 | 禁止 | 必须 |
|------|------|------|
| M-01 | UI 计算 rSO2 值 | rSO2 只能来自设备 |
| M-02 | UI 推断 NIRS 阈值/报警 | 阈值只能来自设备 |
| M-03 | UI 计算 AUC/Baseline | 医学指标只能来自设备 |
| M-04 | UI 插值/填充缺失数据 | 缺失必须显示断裂+灰色 |
| M-05 | UI 平滑/美化波形 | 波形必须真实呈现 |
| M-06 | UI 隐藏异常状态 | 异常必须明确显示 |

### 🔴 渲染铁律

| 编号 | 禁止 | 必须 |
|------|------|------|
| R-01 | 渲染回调中做 O(N) 计算 | 渲染只做 Draw |
| R-02 | 缩放用均值/抽点 | 必须用 LOD Min/Max |
| R-03 | 每帧创建 D2D 资源 | 资源预创建+复用 |

### 🔴 流程铁律

| 编号 | 禁止 | 必须 |
|------|------|------|
| P-01 | 实现 UI_SPEC.md 未定义功能 | 只做规格内的功能 |
| P-02 | 跳过当前任务做后续 | 严格按顺序执行 |
| P-03 | 不更新 PROJECT_STATEUI.md | 每完成一项必须更新 |
| P-04 | 依赖聊天历史 | 只依赖指定文件 |
| P-05 | 参数变更不写审计 | 所有操作可审计 |
| P-06 | 创建文件名变体 | 只用统一文件清单 |

---

## 四、技术栈（冻结）

| 层级 | 技术 | 状态 |
|------|------|------|
| 平台 | Windows .NET 9.0 | 🔒 |
| UI 框架 | WPF | 🔒 |
| 架构 | MVVM | 🔒 |
| MVVM 框架 | CommunityToolkit.Mvvm 8.2.2 | 🔒 |
| 渲染 | Vortice.Direct2D1 3.8.1 | 🔒 |
| WPF 集成 | D3DImage | 🔒 |

---

## 五、Agent 规则

### 5.1 文件访问

```
只能读取:
1. spec/CHARTER.md
2. spec/UI_SPEC.md
3. PROJECT_STATEUI.md
4. NEO_UI_Development_Plan_WPF.md
5. spec/ACCEPTANCE_TESTS.md

只能写入:
• PROJECT_STATEUI.md（进度）
• 代码文件（实现）
```

### 5.2 进度规则

```
所有进度只写入 PROJECT_STATEUI.md
禁止在其他文件记录进度
```

### 5.3 UI 实现规则

```
UI 必须完全对齐 UI_SPEC.md:
• 定义了 → 必须实现
• 没定义 → 禁止实现
• 有歧义 → 标记 Blocked
```

---

## 六、审计要求

| 操作 | 事件类型 |
|------|----------|
| 监测开始 | MONITORING_START |
| 监测结束 | MONITORING_STOP |
| 增益变更 | GAIN_CHANGE |
| 滤波变更 | FILTER_CHANGE |
| 时间轴 Seek | SEEK |
| 截图 | SCREENSHOT |
| 标注 | ANNOTATION |
| 用户登录/登出 | USER_LOGIN/LOGOUT |

---

## 七、质量指示（必须显示）

| 状态 | 视觉 | 禁止 |
|------|------|------|
| 缺失数据 | 断裂 + 灰色 | 插值 |
| 信号饱和 | 红色高亮 | 隐藏 |
| 导联脱落 | 橙色 + 图标 | 省略 |
| NIRS 无数据 | "--%" | 估算值 |

---

## 八、NIRS 规则

```
数据来源: 只能来自设备
协议未定义: 显示 "--%" 或 "Blocked"
禁止: 模拟数据、估算值、历史填充
```

---

## 九、违反后果

| 违反类型 | 后果 |
|----------|------|
| 医学铁律 | 代码回滚 |
| 渲染铁律 | 代码修正 |
| 流程铁律 | 重新执行 |
| 创建文件变体 | 删除重做 |
| 与 UI_SPEC 不对齐 | 代码回滚 |

---

**本宪章自 2026-01-30 起生效，不可违反。**

*CHARTER v1.0 - FROZEN*
