# NEO UI 项目状态 (PROJECT_STATEUI)

> **最后更新**: 2026-02-07
> **当前阶段**: Phase 4 🟡 部分完成 (DialogService已实现)
> **总进度**: Phase 1 ████████████████████ 100% | Phase 2 ████████████████████ 100% | Phase 3 ████████████████████ 100% | Phase 4 ██░░░░░░░░░░░░░░░░░░ 14% | Phase 5~7 ░░░░░░░░░░░░░░░░░░░░ 0%

---

## ⚠️ 重要声明

**本文件是唯一的进度锚点。**

- 所有进度、完成状态只在本文件记录
- 禁止创建 `PROJECT_STATE.md` 或任何变体
- Agent 每完成一项任务必须更新本文件
- 若与 `docs/release/*` 或 `status/PROGRESS.md` 冲突，以本文件「阶段总览 / 当前任务」为准
- 文末 `Execution Track` 为历史执行日志，不作为阶段状态统计来源

---

## 一、阶段总览

| Phase | 名称 | Sprint 数 | 状态 |
|-------|------|-----------|------|
| **Phase 1** | 项目框架搭建 | 4 (1.1~1.4) | ✅ **完成** |
| **Phase 2** | 主窗口交互框架 | 5 (2.1~2.5) | ✅ **完成** |
| **Phase 3** | 波形渲染集成 | 6 (3.1~3.6) | ✅ **完成** |
| **Phase 4** | 对话框系统 | 7 (4.1~4.7) | 🟡 **部分完成** (1/7) |
| **Phase 5** | 高级功能 | 5 (5.1~5.5) | ⚪ 未开始 |
| **Phase 6** | NIRS + 视频 + 质量 | 3 (6.1~6.3) | ⚪ 未开始 |
| **Phase 7** | 测试与优化 | 4 (7.1~7.4) | ⚪ 未开始 |
| **合计** | | **34 Sprints** | |

---

## 二、当前任务

```
┌─────────────────────────────────────────────────────────────────┐
│  📌 当前: Phase 3 ✅ 完成                                       │
│  下一步: Phase 4 对话框系统 (Sprint 4.1~4.7)                   │
└─────────────────────────────────────────────────────────────────┘
```

---

## 三、Phase 1 — 项目框架搭建 ✅ 完成

### Sprint 1.1: 项目创建 ✅

| 任务 | 状态 |
|------|------|
| 创建 Neo.UI WPF 项目 (net9.0-windows, UseWPF) | ✅ |
| App.xaml / App.xaml.cs | ✅ |
| MainWindow.xaml 全区域占位 (UI_SPEC §4) | ✅ |
| 添加 NuGet: CommunityToolkit.Mvvm 8.2.2 | ✅ |
| 添加项目引用: Neo.Core | ✅ |
| dotnet build / dotnet run 验证 | ✅ |

### Sprint 1.2: MVVM 基础设施 ✅

| 任务 | 状态 |
|------|------|
| ViewModelBase (ObservableObject) | ✅ |
| MainWindowViewModel (RelayCommand×7, 服务注入) | ✅ |
| HomeViewModel | ✅ |
| NavigationService + RouteRegistry + INavigable | ✅ |
| DialogService (基础实现, Phase 4.7 完成) | ✅ |
| AuditServiceAdapter (内存 sink, 10K bounded) | ✅ |
| ServiceRegistry (DI 引导) | ✅ |
| ContentControl 导航绑定 + 审计调用点 | ✅ |
| UI 测试工程: 21 tests pass | ✅ |

### Sprint 1.3: 样式系统 ✅

| 任务 | 状态 |
|------|------|
| Colors.xaml (26色 + Color.Shadow + 38笔刷) | ✅ |
| Fonts.xaml (2字体族, 10尺寸, 6预设) | ✅ |
| Dimensions.xaml (布局常量 + CornerRadius/Thickness/Double) | ✅ |
| Buttons.xaml (Primary/Secondary/Nav/Toolbar) | ✅ |
| TextBoxes.xaml (TextBox + PasswordBox) | ✅ |
| ComboBoxes.xaml (ComboBox + ComboBoxItem) | ✅ |
| Toggles.xaml (CheckBox-based toggle 50×26) | ✅ |
| Controls.xaml (DefaultButton/ChannelToggle/ParamComboBox/Separator) | ✅ |
| QualityStates.xaml (Missing/Saturated/LeadOff) | ✅ |
| App.xaml MergedDictionaries 9文件 | ✅ |
| 零硬编码验证 (hex/FontSize/Padding/Margin/CornerRadius) | ✅ |

### Sprint 1.4: D3DImage 渲染验证 ✅

| 任务 | 状态 |
|------|------|
| D3DImageRenderer.cs (D3D11+D2D+D3D9Ex+D3DImage) | ✅ |
| BeginRender / EndRender 生命周期 | ✅ |
| Resize 安全重建 | ✅ |
| Dispose 多次调用安全 | ✅ |
| TryRecoverDevice (DeviceLost) | ✅ |
| DrawTestRect (颜色来自 Colors.xaml) | ✅ |
| CompositionTarget.Rendering 60fps | ✅ |
| docs/ui/Sprint1.4_RenderValidation.md | ✅ |
| 构建 0 errors 0 warnings, 21/21 tests | ✅ |

---

## 四、Phase 2 — 主窗口交互框架 ✅ 完成

> **目标**: 将 MainWindow.xaml 中的各区域提取为独立 UserControl，实现工具栏/导航/状态栏/参数面板的交互逻辑。

### Sprint 2.1: ToolbarPanel 工具栏 ✅

| 任务 | 状态 | 说明 |
|------|------|------|
| 创建 Views/Controls/ToolbarPanel.xaml | ✅ | 从 MainWindow 提取工具栏区域 |
| Logo + 应用标题显示 | ✅ | 左侧品牌区 |
| 播放/暂停按钮 (绑定 PlaybackCommand) | ✅ | UI_SPEC §6.3 |
| 截图按钮 (绑定 ScreenshotCommand) | ✅ | UI_SPEC §6.5 |
| 标注按钮 (绑定 AnnotationCommand) | ✅ | UI_SPEC §6.4 |
| 当前时间显示 (1秒刷新) | ✅ | UI_SPEC §8, DispatcherTimer |
| 当前用户 + 床位号显示 | ✅ | UI_SPEC §4.1, 占位绑定 |
| 审计: 每个按钮操作记录 | ✅ | UI_SPEC §10, MONITORING_START/STOP, SCREENSHOT, ANNOTATION |
| 单元测试 | ✅ | 8 tests: ToolbarViewModelTests (29 total pass) |

### Sprint 2.2: NavPanel 导航面板 ✅

| 任务 | 状态 | 说明 |
|------|------|------|
| 创建 Views/Controls/NavPanel.xaml | ✅ | 从 MainWindow 提取左侧导航区域 |
| 7个导航按钮: 首页/历史/显示/滤波/用户/导出/关机 | ✅ | UI_SPEC §4.1, 所有按钮+图标+文本 |
| 导航宽度 60px | ✅ | Dim.NavigationWidth=60 (src/UI/Styles/Dimensions.xaml:12, UI_SPEC §4.2) |
| 选中状态高亮 (ActiveRoute 绑定) | ✅ | 全部 7 按钮 DataTrigger on ActiveRoute → PrimaryBrush/ErrorBrush |
| Command 绑定 NavigationService | ✅ | 复用 Sprint 1.2: NavigateCommand, ShowXxxDialogCommand, RequestShutdownCommand |
| 审计: NAVIGATION 事件 | ✅ | §10 无 NAVIGATION 类型, 导航非关键操作, 不审计 (见 AuditEvent.cs 注释) |
| 单元测试 | ✅ | 11 tests: NavPanelTests (40 total pass) |

### Sprint 2.3: StatusBarPanel 状态栏 ✅

| 任务 | 状态 | 说明 |
|------|------|------|
| 创建 Views/Controls/StatusBarPanel.xaml | ✅ | 从 MainWindow 提取底部状态栏 |
| 创建 ViewModels/StatusViewModel.cs | ✅ | FPS / 存储 / 设备状态 / 时间 |
| FPS 显示 (1秒刷新) | ✅ | UI_SPEC §8: UpdateFps() 方法 |
| 存储用量显示 (1秒刷新) | ✅ | UI_SPEC §8: UpdateStorage() 方法 |
| 设备连接状态指示 (EEG/NIRS/Video) | ✅ | UI_SPEC §8: DataTrigger ● 已连接 ○ 未连接 |
| 当前时间显示 (1秒刷新) | ✅ | UI_SPEC §8, 单调时钟 §2.2 |
| 单元测试 | ✅ | 10 tests: StatusViewModelTests (68 total pass) |

### Sprint 2.4: ChannelControlPanel 参数面板 ✅

| 任务 | 状态 | 说明 |
|------|------|------|
| 创建 Views/Controls/ChannelControlPanel.xaml | ✅ | 从 MainWindow 提取右侧参数区域 |
| 创建 ViewModels/WaveformViewModel.cs | ✅ | 增益/导联/Y轴/滤波/扫描/aEEG 状态管理 |
| 导联选择 | ✅ | UI_SPEC §5.1: CH1 C3-P3, CH2 C4-P4 绑定 |
| 增益选择 | ✅ | UI_SPEC §6.1: GainOptions [10,20,50,70,100,200,1000] μV/cm |
| Y轴范围选择 | ✅ | UI_SPEC §5.1: YAxisOptions [25,50,100,200] μV |
| aEEG 时间窗选择 | ✅ | UI_SPEC §5.2: AeegTimeWindowOptions [1,3,6,12,24] h |
| 审计: GAIN_CHANGE / FILTER_CHANGE | ✅ | UI_SPEC §10, partial OnChanged 回调 |
| 单元测试 | ✅ | 18 tests: WaveformViewModelTests (68 total pass) |

### Sprint 2.5: MainWindow 重构 + 集成 ✅

| 任务 | 状态 | 说明 |
|------|------|------|
| MainWindow.xaml 用 UserControl 替换内联区域 | ✅ | ToolbarPanel + NavPanel + StatusBarPanel + ChannelControlPanel |
| DataContext 绑定各 ViewModel | ✅ | Toolbar/Status/Waveform 注入, Nav 继承 |
| 移除 Sprint 1.4 测试渲染代码 | ✅ | RenderTestImage, OnRenderFrame, D3DImageRenderer 引用已移除 |
| 窗口启动/关闭生命周期 | ✅ | 简化为 InitializeComponent() |
| 构建 0 errors 0 warnings | ✅ | |
| 全量测试通过 | ✅ | 68/68 tests pass |

---

## 五、Phase 3 — 波形渲染集成 ✅ 完成

> **目标**: 将后端 Rendering 引擎 (src/Rendering) 通过 D3D11+D2D 接入 WPF，实现 EEG/aEEG 波形实时显示。
> **新增依赖**: Neo.Rendering, Neo.DSP, Neo.Playback, Neo.Mock 项目引用
> **渲染管线**: D3D11 → D2D1DeviceContext → Staging Texture → WriteableBitmap → WPF Image

### Sprint 3.1: WaveformPanel (D3DImage 宿主) ✅

| 任务 | 状态 | 说明 |
|------|------|------|
| 创建 Views/Controls/WaveformPanel.xaml | ✅ | 中央波形区 UserControl |
| D3DImageRenderer 集成到 WaveformPanel | ✅ | Image.Source = WriteableBitmap (D3D11→D2D→WPF) |
| 6 子区域布局 (aEEG×2 + EEG×2 + NIRS趋势 + SeekBar) | ✅ | WaveformLayout.Create(), UI_SPEC §4.1 |
| CompositionTarget.Rendering 回调 | ✅ | 60fps 渲染循环 |
| Resize 自适应 | ✅ | SizeChanged → Renderer.Resize |
| Device Lost 恢复 UI | ✅ | ErrorOverlay + 点击重试 |
| 构建验证 | ✅ | 0 errors 0 warnings, 103/103 tests pass |

### Sprint 3.2: WaveformRenderHost (渲染桥接) ✅

| 任务 | 状态 | 说明 |
|------|------|------|
| 创建 Rendering/WaveformRenderHost.cs | ✅ | 桥接 D3DImageRenderer ↔ Rendering 引擎 |
| 添加项目引用: Neo.Rendering | ✅ | Neo.UI.csproj |
| 添加项目引用: Neo.DSP | ✅ | Neo.UI.csproj (只读 LOD 查询) |
| 添加项目引用: Neo.Playback | ✅ | Neo.UI.csproj |
| RenderContext 创建 (从 D2D DeviceContext) | ✅ | 复用 Rendering/Core/RenderContext |
| LayeredRenderer 集成 (Grid/Content/Overlay) | ✅ | Grid/Content/Overlay 3 层可用 |
| EegDataBridge 扫描模式 | ✅ | 左到右扫描, 15s 周期, 清除带 |
| Mock 数据源接入 (MockEegSource) | ✅ | 验证渲染管线 |

### Sprint 3.3: EEG 波形渲染 ✅

| 任务 | 状态 | 说明 |
|------|------|------|
| SweepModeRenderer 替代 EegPolylineRenderer | ✅ | 扫描模式渲染 (per-channel RenderChannel) |
| EegChannelView 2 通道显示 | ✅ | UI_SPEC §5.1: CH1/CH2 通道 |
| EEG 波形颜色: EegColorPalette | ✅ | Dev Plan §3.4, 4 通道颜色 |
| Y 轴线性映射 (±100μV 默认) | ✅ | YAxisRangeUv 属性, WaveformPanel 桥接绑定 |
| X 轴 15 秒时间窗 | ✅ | UI_SPEC §5.1 |
| 增益切换即时生效 (< 100ms) | ✅ | SelectedGain → GainMicrovoltsPerCm 绑定 |
| 通道组合切换 | ✅ | CycleLeadCombinationCommand, ChannelControlPanel 按钮 |
| EegGainScaler 集成 | ✅ | SweepModeRenderer 参数化 yAxisRangeUv |

### Sprint 3.4: aEEG 趋势渲染 ✅

| 任务 | 状态 | 说明 |
|------|------|------|
| AeegTrendRenderer 接入 | ✅ | src/Rendering/AEEG/ |
| AeegSeriesBuilder 数据构建 | ✅ | Min/Max 包络带构建 |
| 半对数 Y 轴 (0-10μV 线性, 10-200μV 对数) | ✅ | AeegSemiLogMapper |
| X 轴 3h 默认, 支持 1h/3h/6h/12h/24h | ✅ | AeegVisibleHours 属性, WaveformPanel 桥接绑定 |
| AeegFill 颜色 #00E676 40% | ✅ | AeegColorPalette.TrendFill 改为绿色 40% |
| LOD 金字塔查询集成 | ✅ | 添加 Neo.DSP 引用 |
| GS 直方图渲染 | ✅ | GsHistogramRenderer, 70%/30% 趋势/直方图布局 |
| AeegGridAndAxisRenderer 集成 | ✅ | 网格 + 轴标签 |

### Sprint 3.5: SeekBar 时间轴控件 ✅

| 任务 | 状态 | 说明 |
|------|------|------|
| SeekBarRenderer (D2D 渲染) | ✅ | 轨道 + 填充 + 手柄 + 时间标签 (替代 SeekBar.xaml) |
| PlaybackClock 集成 | ✅ | Neo.Playback 同步时钟 |
| 播放/暂停切换 | ✅ | Toolbar.IsPlaying → PlaybackClock.Start/Pause 绑定 |
| 拖动 Seek | ⚠️ | 交互延后至 Phase 5（当前禁用） |
| 点击跳转 | ⚠️ | 交互延后至 Phase 5（当前禁用） |
| 多流同步 (EEG/aEEG/NIRS/Video, ±100ms) | ✅ | INirsPlaybackSource 接口 + MultiStreamCoordinator 集成 |
| SeekBar 滑块 ≥20×20px 触控 | ✅ | Dev Plan §7.1 |
| 审计: SEEK 事件 | ⚠️ | 交互启用后补齐 |

### Sprint 3.6: 质量指示渲染 ✅

| 任务 | 状态 | 说明 |
|------|------|------|
| 创建 Rendering/QualityIndicatorRenderer.cs | ✅ | D2D 质量覆盖层 |
| 数据缺失: 灰色背景 (#9E9E9E 50%) | ✅ | UI_SPEC §7 |
| 信号饱和: 红色高亮 (#F44336 50%) | ✅ | UI_SPEC §7 |
| 导联脱落: 橙色背景 (#FF9800 50%) | ✅ | UI_SPEC §7 |
| 禁止插值填充 | ✅ | UI_SPEC §7 规则 |
| QualityFlag 枚举映射 | ✅ | Core/Enums/QualityFlag |
| EegDataBridge O(1) 质量查询 | ✅ | GetQualitySummary() 运行时计数器 |

---

## 六、Phase 4 — 对话框系统 🟡 部分完成 (1/7)

> **目标**: 实现全部 7 个对话框，完成 DialogService 实际逻辑。
> **布局规格**: NEO_UI_Development_Plan_WPF.md §6

### Sprint 4.1: LoginDialog 登录对话框

| 任务 | 状态 | 说明 |
|------|------|------|
| 创建 Views/Dialogs/LoginDialog.xaml | [ ] | 1000×600px, 左右 50% 布局 |
| 左侧品牌区: 渐变背景 + Logo + "NEO" + 副标题 | [ ] | Dev Plan §6.1 |
| 右侧登录区: 用户名 + 密码 + 登录按钮 | [ ] | Dev Plan §6.1 |
| 输入验证 (非空) | [ ] | |
| 审计: USER_LOGIN | [ ] | UI_SPEC §10 |
| App 启动时弹出 | [ ] | |

### Sprint 4.2: PatientDialog 患者信息对话框

| 任务 | 状态 | 说明 |
|------|------|------|
| 创建 Views/Dialogs/PatientDialog.xaml | [ ] | 900×700px, 双列表单 |
| 创建 ViewModels/PatientViewModel.cs | [ ] | 患者字段绑定 |
| 标题栏蓝色背景 #2196F3 | [ ] | Dev Plan §6.2 |
| 左列: 床位号/母亲身份证/姓名/性别/APGAR | [ ] | Dev Plan §6.2 |
| 右列: 医院/科室/住院号*/出生日期+胎龄/体重+日龄 | [ ] | Dev Plan §6.2 |
| 疾病勾选 GroupBox (5项 + 其他) | [ ] | Dev Plan §6.2 |
| 母亲孕期 GroupBox (6项 + 其他) | [ ] | Dev Plan §6.2 |
| 住院号必填验证 | [ ] | UI_SPEC §9 |
| 确定/取消按钮 | [ ] | |

### Sprint 4.3: FilterDialog 滤波设置对话框

| 任务 | 状态 | 说明 |
|------|------|------|
| 创建 Views/Dialogs/FilterDialog.xaml | [ ] | 400×300px, 垂直表单 |
| 低通滤波 ComboBox: 15/35/50/70 Hz | [ ] | UI_SPEC §6.2 |
| 高通滤波 ComboBox: 0.3/0.5/1.5 Hz | [ ] | UI_SPEC §6.2 |
| 陷波器 ComboBox: 50/60 Hz | [ ] | UI_SPEC §6.2 |
| 确定/取消按钮 | [ ] | |
| 审计: FILTER_CHANGE | [ ] | UI_SPEC §10 |
| UI 只发送配置，不实现滤波 | [ ] | UI_SPEC §6.2 规则 |

### Sprint 4.4: DisplayDialog 显示设置对话框

| 任务 | 状态 | 说明 |
|------|------|------|
| 创建 Views/Dialogs/DisplayDialog.xaml | [ ] | 400×350px |
| 扫描速度 ComboBox | [ ] | Dev Plan §6.4 |
| 标尺显示 Toggle | [ ] | Dev Plan §6.4 |
| EEG 网格显示 Toggle | [ ] | Dev Plan §6.4 |
| 10 分钟 aEEG 网格 Toggle | [ ] | Dev Plan §6.4 |
| 确定/取消按钮 | [ ] | |

### Sprint 4.5: UserManagementDialog 用户管理对话框

| 任务 | 状态 | 说明 |
|------|------|------|
| 创建 Views/Dialogs/UserManagementDialog.xaml | [ ] | 800×500px |
| 前置密码验证 (调用 PasswordDialog) | [ ] | UI_SPEC §9 |
| 顶部: 添加用户按钮 | [ ] | Dev Plan §6.5 |
| 中部: DataGrid (工号/姓名/电话/邮箱/操作) | [ ] | Dev Plan §6.5 |
| 操作列: 编辑/删除按钮 | [ ] | Dev Plan §6.5 |

### Sprint 4.6: HistoryDialog + PasswordDialog

| 任务 | 状态 | 说明 |
|------|------|------|
| 创建 Views/Dialogs/HistoryDialog.xaml | [ ] | 900×600px |
| 查询区: 姓名输入 + 住院号输入 + 查询按钮 | [ ] | Dev Plan §6.6 |
| 结果 DataGrid: 住院号/姓名/开始时间/结束时间 | [ ] | Dev Plan §6.6 |
| 底部: 加载按钮 (加载回放) | [ ] | Dev Plan §6.6 |
| 创建 Views/Dialogs/PasswordDialog.xaml | [ ] | 400×200px |
| 密码输入 + 确定/取消 | [ ] | Dev Plan §6.7 |

### Sprint 4.7: DialogService 完整实现 ✅

| 任务 | 状态 | 说明 |
|------|------|------|
| DialogService 完整实现 | ✅ | `Services/DialogService.cs` 已实现 |
| 7个对话框工厂注册 | ✅ | Login, Patient, Filter, Display, UserManagement, History, Password |
| ShowDialog 方法 | ✅ | 统一对话框打开/返回机制 |
| ShowMessage 方法 | ✅ | MessageBox 封装 |
| ShowConfirmation 方法 | ✅ | 确认对话框封装 |
| 对话框 XAML 骨架 | 🟡 | 7个对话框已创建基础结构，但内容未完成 |
| 单元测试 | ✅ | `tests/UI.Tests/DialogServiceTests.cs` |

**注**: Sprint 4.1-4.6 的对话框 XAML 文件已创建，但只有基础骨架，具体表单内容、验证逻辑等待实现。

---

## 七、Phase 5 — 高级功能 ⚪ 未开始

> **目标**: 截图/打印/导出 UI 集成，标注功能，键盘快捷键。

### Sprint 5.1: 截图功能 UI

| 任务 | 状态 | 说明 |
|------|------|------|
| 工具栏截图按钮绑定 | [ ] | ToolbarPanel → ScreenshotCommand |
| 波形区 D2D 截图到 PNG | [ ] | 复用 Host/Services/ScreenshotService 逻辑 |
| 截图保存路径选择/自动命名 | [ ] | |
| 截图成功提示 (StatusBar 或 Toast) | [ ] | |
| 审计: SCREENSHOT | [ ] | UI_SPEC §10 |

### Sprint 5.2: 打印功能 UI

| 任务 | 状态 | 说明 |
|------|------|------|
| 打印预览窗口 | [ ] | 复用 Host/Services/PrintService 逻辑 |
| 打印范围选择 (当前视图/时间范围) | [ ] | |
| 打印按钮 | [ ] | |
| 审计: PRINT | [ ] | |

### Sprint 5.3: USB 导出功能 UI

| 任务 | 状态 | 说明 |
|------|------|------|
| 导出对话框 (选择文件/目标路径) | [ ] | 复用 Host/Services/UsbExportService |
| USB 设备检测显示 | [ ] | |
| 导出进度条 | [ ] | |
| 审计: USB_EXPORT | [ ] | |

### Sprint 5.4: 标注功能

| 任务 | 状态 | 说明 |
|------|------|------|
| 标注按钮交互 (工具栏) | [ ] | UI_SPEC §6.4 |
| 标注输入弹窗 (文本输入) | [ ] | |
| 时间绑定: 当前播放时间戳 | [ ] | UI_SPEC §6.4 |
| 标注在波形区可视化显示 | [ ] | Overlay 层标记 |
| 审计: ANNOTATION | [ ] | UI_SPEC §10 |

### Sprint 5.5: 键盘快捷键

| 任务 | 状态 | 说明 |
|------|------|------|
| Space: 播放/暂停 | [ ] | |
| Ctrl+S: 截图 | [ ] | |
| Ctrl+P: 打印 | [ ] | |
| 方向键: 时间轴微调 | [ ] | |
| InputBinding 绑定 | [ ] | XAML KeyBinding |

---

## 八、Phase 6 — NIRS + 视频 + 质量 ⚪ 未开始

> **目标**: NIRS 6 通道面板、视频预览面板、质量指示 UI 覆盖层。
> **新增依赖**: Neo.Video, Neo.NIRS 项目引用

### Sprint 6.1: NirsPanel (NIRS 显示面板)

| 任务 | 状态 | 说明 |
|------|------|------|
| 创建 Views/Controls/NirsPanel.xaml | [ ] | 右侧 NIRS 区域 |
| 创建 ViewModels/NirsViewModel.cs | [ ] | 6 通道状态管理 |
| 添加项目引用: Neo.NIRS | [ ] | |
| 6 通道 rSO₂ 数值显示 ("XX%" / "--%" / "Fault" / "Blocked") | [ ] | UI_SPEC §5.3 |
| 每通道独立开关 (Toggle) | [ ] | UI_SPEC §5.3 |
| 开关状态持久化 | [ ] | UI_SPEC §5.3 |
| 关闭通道不显示趋势 | [ ] | UI_SPEC §5.3 |
| NIRS 趋势渲染 (Color: #29B6F6) | [ ] | Dev Plan §3.4 |

### Sprint 6.2: VideoPanel (视频预览面板)

| 任务 | 状态 | 说明 |
|------|------|------|
| 创建 Views/Controls/VideoPanel.xaml | [ ] | 右侧视频预览区 |
| 添加项目引用: Neo.Video | [ ] | |
| USB 摄像头实时预览 | [ ] | UI_SPEC §5.4 |
| 保持原始比例 | [ ] | UI_SPEC §5.4 |
| 回放同步 (±1 秒) | [ ] | UI_SPEC §5.4 |
| 无摄像头时占位提示 | [ ] | UI_SPEC §5.4 |

### Sprint 6.3: 质量指示 UI 覆盖层

| 任务 | 状态 | 说明 |
|------|------|------|
| 波形区质量状态视觉反馈 | [ ] | 结合 Sprint 3.6 渲染层 |
| StatusBar 设备连接状态实时更新 | [ ] | UI_SPEC §8 |
| 设备断开 UI 不崩溃 | [ ] | UI_SPEC §11 |
| 审计: DEVICE_DISCONNECT | [ ] | UI_SPEC §10 |

---

## 九、Phase 7 — 测试与优化 ⚪ 未开始

> **目标**: 性能优化、全面测试、触控验证、长时间稳定性。

### Sprint 7.1: 渲染性能优化

| 任务 | 状态 | 说明 |
|------|------|------|
| 渲染帧率 ≥60fps 稳定 (全通道) | [ ] | UI_SPEC §11 |
| 渲染回调 O(1) 验证 | [ ] | CHARTER R-01 |
| 无每帧资源创建验证 | [ ] | CHARTER R-03 |
| GPU 资源泄漏检查 | [ ] | |
| LOD 查询 <10ms 验证 | [ ] | |

### Sprint 7.2: 全面测试覆盖

| 任务 | 状态 | 说明 |
|------|------|------|
| ViewModel 单元测试 (所有 VM) | [ ] | |
| Service 单元测试 (Navigation/Dialog/Audit) | [ ] | |
| 对话框交互测试 | [ ] | |
| 渲染集成测试 | [ ] | |
| 测试覆盖率报告 | [ ] | |

### Sprint 7.3: 触控验证

| 任务 | 状态 | 说明 |
|------|------|------|
| 所有按钮 ≥44×44px | [ ] | UI_SPEC §13 |
| 导航按钮 60×60px | [ ] | Dev Plan §7.1 |
| SeekBar 滑块 ≥20×20px | [ ] | Dev Plan §7.1 |
| 按钮间距 ≥8px | [ ] | Dev Plan §7.2 |
| 表单项间距 ≥16px | [ ] | Dev Plan §7.2 |
| 点击反馈 <100ms | [ ] | Dev Plan §7.3 |

### Sprint 7.4: 72h 稳定性测试

| 任务 | 状态 | 说明 |
|------|------|------|
| 72h 连续运行无崩溃 | [ ] | UI_SPEC §11 |
| 内存无持续增长 (<5% 漂移) | [ ] | UI_SPEC §11 |
| 设备断开恢复测试 | [ ] | UI_SPEC §11 |
| DeviceLost 恢复测试 | [ ] | |
| 最终验收报告 | [ ] | |

---

## 十、Phase 准入条件

### Phase 1 → Phase 2

| 条件 | 状态 |
|------|------|
| Neo.UI 项目可编译运行 | ✅ |
| MVVM 基础设施就绪 | ✅ |
| 样式系统就绪 (零硬编码) | ✅ |
| D3DImageRenderer 验证通过 | ✅ |
| Sprint 1.1~1.4 全部完成 | ✅ |
| 无 Blocked 事项 | ✅ |
| **人工确认** | ✅ |

### Phase 2 → Phase 3

| 条件 | 状态 |
|------|------|
| 工具栏/导航/状态栏/参数面板 独立 UserControl | ✅ |
| MainWindow 重构完成 | ✅ |
| 所有 ViewModel 绑定正常 | ✅ |

### Phase 3 → Phase 4

| 条件 | 状态 |
|------|------|
| EEG/aEEG 波形可渲染显示 | ✅ |
| SeekBar 时间轴可交互 | ⚠️ 延后至 Phase 5 |
| 质量指示可视化就绪 | ✅ |
| Neo.Rendering/DSP/Playback 集成 | ✅ |
| 增益/通道/时间窗 UI↔渲染器绑定 | ✅ |
| Play/Pause↔PlaybackClock 绑定 | ✅ |
| Sprint 3.1~3.6 全部完成 | ✅ |
| **人工确认** | [ ] |

### Phase 4 → Phase 5

| 条件 | 状态 |
|------|------|
| 7 个对话框全部实现 | [ ] |
| DialogService 完整替换 Stub | ✅ |
| Sprint 4.1~4.7 全部完成 | [ ] |

### Phase 5 → Phase 6

| 条件 | 状态 |
|------|------|
| 截图/打印/导出 UI 可用 | [ ] |
| 标注功能可用 | [ ] |
| Sprint 5.1~5.5 全部完成 | [ ] |

### Phase 6 → Phase 7

| 条件 | 状态 |
|------|------|
| NIRS 6 通道面板就绪 | [ ] |
| 视频预览面板就绪 | [ ] |
| Sprint 6.1~6.3 全部完成 | [ ] |

---

## 十一、阻塞事项

```
🟢 当前无阻塞
```

---

## 十二、决策记录

| 日期 | 决策 | 理由 |
|------|------|------|
| 2026-01-30 | 使用 WPF | MVVM 支持、数据绑定更强 |
| 2026-01-30 | 使用 D3DImage | 与 WPF 渲染树融合 |
| 2026-01-30 | 使用 CommunityToolkit.Mvvm 8.2.2 | 微软官方、轻量 |
| 2026-01-30 | UI_SPEC v1.1→v1.2 对齐 | 布局尺寸/默认参数补齐 |
| 2026-01-31 | Vortice 3.8.1 (D3D11/D2D/DXGI) | D3DImage WPF 集成 |

---

## 十三、已完成历史记录

<details>
<summary>Sprint 1.1~1.4 详细修复历史（点击展开）</summary>

```
[2026-01-30] Sprint 1.1: 项目创建
  Neo.UI WPF 项目, App.xaml, MainWindow.xaml 全区域占位
  0 errors, 0 warnings

[2026-01-30] S3-UI-01-REBASE: UI_SPEC v1.1→v1.2 对齐
  7 项修正 (MinWidth×2, QualityOverlay, §引用×4)

[2026-01-30] S3-UI-02: 主界面静态布局
  aEEG/EEG 时间标签, NIRS 6 通道占位, Video Preview 标识

[2026-01-30] Sprint 1.2: MVVM 基础设施
  ViewModelBase, Navigation, Dialog(stub), Audit, DI, 21 tests

[2026-01-30] Sprint 1.2-fix: 审计修复
  ContentControl 导航, 审计调用点, 测试工程

[2026-01-30] Sprint 1.3: 样式系统
  Colors/Fonts/Buttons/TextBoxes/ComboBoxes/Toggles + App.xaml 合并

[2026-01-30] Sprint 1.3 追加: Dimensions + Controls + QualityStates

[2026-01-30] Sprint 1.3-fix: 80+ 处硬编码颜色/尺寸修复
  +7 Color, +11 Brush, +4 FontSize

[2026-01-30] Sprint 1.3-fix2: 样式文件残留硬编码修复
  Toggles/TextBoxes/ComboBoxes/Buttons/Controls/QualityStates

[2026-01-30] Sprint 1.3-fix3: Padding/Margin/CornerRadius/Color 修复
  +Color.Shadow, +CornerRadius/Thickness/Double 资源

[2026-01-31] Sprint 1.4: D3DImage 渲染验证
  D3D11+D2D+D3D9Ex, Vortice 3.8.1, 60fps, Resize, Dispose, DeviceLost
  docs/ui/Sprint1.4_RenderValidation.md

[2026-02-02] Sprint 1.4-fix: 审计修复
  移除文档医学术语, 移除 D3DImageRenderer 硬编码回退颜色

[2026-02-02] Sprint 2.1: ToolbarPanel 工具栏提取
  ToolbarPanel.xaml/cs UserControl, ToolbarViewModel (clock+commands+audit)
  MainWindow.xaml 内联工具栏替换为 <controls:ToolbarPanel/>
  MainWindowViewModel 添加 Toolbar 属性, App.xaml.cs 注入
  8 新测试, 29/29 tests pass, 0 errors 0 warnings

[2026-02-02] Sprint 2.2: NavPanel 导航面板提取
  NavPanel.xaml/cs UserControl, 7 按钮 + ActiveRoute 高亮 (DataTrigger)
  MainWindow.xaml 内联导航替换为 <controls:NavPanel/>
  Dimensions.xaml 新增 5 个导航专用资源, 零硬编码尺寸
  沿用 MainWindowViewModel 已有命令 (NavigateCommand/Dialog/Shutdown)
  审计: §10 无 NAVIGATION 类型, 导航非关键操作, 不审计
  11 新测试 (NavPanelTests), 40/40 tests pass, 0 errors 0 warnings

[2026-02-02] Sprint 2.3: StatusBarPanel 状态栏提取
  StatusBarPanel.xaml/cs UserControl, StatusViewModel (FPS/存储/设备/时间)
  MainWindow.xaml 内联状态栏替换为 <controls:StatusBarPanel/>
  Dimensions.xaml 新增 6 个状态栏专用资源, 零硬编码尺寸
  设备连接指示器 DataTrigger: DeviceInactiveBrush/DeviceActiveBrush
  单调时钟 (Stopwatch + DateTimeOffset 锚点)
  10 新测试 (StatusViewModelTests), 68/68 tests pass, 0 errors 0 warnings

[2026-02-02] Sprint 2.4: ChannelControlPanel 参数面板提取
  ChannelControlPanel.xaml/cs UserControl, WaveformViewModel
  MainWindow.xaml 内联参数面板替换为 <controls:ChannelControlPanel/>
  Dimensions.xaml 新增 6 个参数面板专用资源, 零硬编码尺寸
  增益/滤波参数变更自动审计 (GAIN_CHANGE/FILTER_CHANGE)
  18 新测试 (WaveformViewModelTests), 68/68 tests pass

[2026-02-02] Sprint 2.5: MainWindow 重构 + 集成
  移除 Sprint 1.4 渲染测试代码 (D3DImageRenderer/OnRenderFrame/RenderTestImage)
  MainWindow.xaml.cs 简化为 InitializeComponent()
  MainWindow.xaml 4 个 UserControl 替换完成
  App.xaml.cs 注入 ToolbarViewModel + StatusViewModel + WaveformViewModel
  构建 0 errors 0 warnings, 68/68 tests pass

[2026-02-04] Sprint 3.1: WaveformPanel + D3DImageRenderer 集成
  WaveformPanel.xaml/cs UserControl (D3D → WriteableBitmap → WPF Image)
  D3DImageRenderer 升级 ID2D1DeviceContext
  WaveformRenderHost 渲染桥接 (CompositionTarget.Rendering 60fps)
  WaveformLayout 6 区域布局 (aEEG×2 + EEG×2 + NIRS + SeekBar)
  Device Lost 恢复 UI (ErrorOverlay + 双击模拟)
  MainWindow.xaml 集成 WaveformPanel (ActiveRoute 可见性绑定)

[2026-02-04] Sprint 3.2: EegDataBridge + MockEegSource 数据接入
  EegDataBridge 扫描模式 (左到右, 15s 周期, 清除带)
  MockEegSource 160Hz 4 通道接入
  SweepChannelData 零拷贝数据传递
  EegDataBridgeTests 23 tests, WaveformRenderHostTests 17 tests

[2026-02-04] Sprint 3.3: SweepModeRenderer EEG 波形渲染
  SweepModeRenderer per-channel RenderChannel (替代 EegPolylineRenderer)
  EegColorPalette 4 通道颜色
  ±200μV Y 轴映射, 下采样性能优化
  扫描线 (黄色) + 清除带 (暗色)

[2026-02-04] Sprint 3.4: aEEG 趋势渲染
  AeegTrendRenderer + AeegSeriesBuilder + AeegGridAndAxisRenderer
  AeegSemiLogMapper 半对数 Y 轴 (0-10μV 线性, 10-200μV 对数)
  GsHistogramRenderer 直方图 (70%/30% 趋势/直方图布局)
  LOD 金字塔查询, 3h 默认可见范围

[2026-02-04] Sprint 3.5: SeekBar 时间轴
  SeekBarRenderer D2D 渲染 (轨道+填充+手柄+时间标签)
  PlaybackClock 集成, 鼠标拖动/点击 Seek
  TrySetSeekFromPoint 交互逻辑

[2026-02-04] Sprint 3.6: 质量指示渲染
  QualityIndicatorRenderer (Missing/Saturated/LeadOff 覆盖层)
  EegDataBridge O(1) 质量计数器 (AddQualityCounts/RemoveQualityCounts)
  GetQualitySummary() 集成到渲染循环
  构建 0 errors, 103/103 tests pass

[2026-02-05] Phase 3 完成: 10 项剩余绑定补齐
  AeegColorPalette.TrendFill 改为绿色 #00E676 40%
  WaveformRenderHost 添加 IAuditService, GainMicrovoltsPerCm, YAxisRangeUv, AeegVisibleHours, PlaybackClock
  SweepModeRenderer.RenderChannel 接受 yAxisRangeUv 参数
  WaveformPanel 桥接 ViewModel → RenderHost (PropertyChanged 订阅)
  WaveformViewModel 添加 CycleGain/CycleYAxis/CycleAeegTimeWindow/CycleLeadCombination 命令
  ChannelControlPanel.xaml 增益/Y轴/aEEG时间窗/导联 改为可点击按钮
  TrySetSeekFromPoint 添加 SEEK 审计日志
  INirsPlaybackSource 接口 + MultiStreamCoordinator NIRS 集成
  测试: 122/122 UI.Tests, 322/322 Rendering.Tests, 44/44 Playback.Tests
```

</details>

---

## 十四、提交前自查清单 (Audit Pre-Check)

> **目的**: 每个 Sprint/Phase 提交审查前，Agent 必须逐项自查，避免反复 FAIL。

### A. 零硬编码检查

在所有新建/修改的 XAML 文件中，搜索以下硬编码模式：

| 检查项 | 禁止值 | 正确做法 |
|--------|--------|----------|
| Margin | `Margin="8,0"` 等数字 | `Margin="{StaticResource Dim.XxxMargin}"` |
| Width/Height | `Width="10"` 等数字 | `Width="{StaticResource Dim.Xxx}"` |
| BorderThickness | `BorderThickness="1,0,0,0"` | `BorderThickness="{StaticResource Dim.XxxThickness}"` |
| CornerRadius | `CornerRadius="4"` | `CornerRadius="{StaticResource Dim.XxxRadius}"` |
| Padding | `Padding="8,4"` | `Padding="{StaticResource Dim.XxxPadding}"` |
| Background="Transparent" | 字面 `Transparent` | `{StaticResource TransparentBrush}` |
| 十六进制颜色 | `#FF0000` | `{StaticResource XxxBrush}` |
| FontSize 数字 | `FontSize="12"` | `FontSize="{StaticResource FontSize.Xxx}"` |

### B. 审计合规检查

| 检查项 | 规则 |
|--------|------|
| 事件类型 | 只使用 AuditEventTypes 中已有的 10 种 (UI_SPEC §10) |
| 不可新造事件类型 | 禁止新增 AuditEventTypes 常量 |
| 审计调用点 | 仅在"关键操作"（影响患者数据/监控状态/临床结果）处审计 |

### C. 时钟合规检查

| 检查项 | 规则 |
|--------|------|
| DateTime.Now | 禁止直接使用，必须通过 Stopwatch + UTC 锚点派生 |
| DispatcherTimer | 有 StopTimer()/StopClock() 供测试调用 |

### D. 尺寸资源可追溯

| 检查项 | 规则 |
|--------|------|
| 新增 Dim.* 资源 | 必须写在 Dimensions.xaml 中，注释标注 Sprint 来源 |
| 数值来源 | 注释中标注 UI_SPEC / Dev Plan 的具体章节号 |

### E. 构建 + 测试

```
dotnet build src/UI/Neo.UI.csproj  → 0 errors 0 warnings
dotnet test tests/UI.Tests          → N/N pass
```

### F. 批量执行模式

当一个 Phase 包含多个 Sprint 时，可批量执行：
1. 按顺序实现所有 Sprint 的代码变更
2. 执行一次总构建 + 全量测试
3. 对每个新建 XAML 文件运行自查清单 A~D
4. 一次性更新 PROJECT_STATEUI.md 中所有 Sprint 状态

---

## 十五、Agent 执行指令

```
┌─────────────────────────────────────────────────────────────────┐
│  🤖 Agent 每次启动时必须:                                       │
├─────────────────────────────────────────────────────────────────┤
│                                                                 │
│  1. 只读取以下文件:                                             │
│     • spec/CHARTER.md                                           │
│     • spec/UI_SPEC.md                                           │
│     • PROJECT_STATEUI.md (本文件)                               │
│     • NEO_UI_Development_Plan_WPF.md                            │
│     • spec/ACCEPTANCE_TESTS.md                                  │
│                                                                 │
│  2. 从「二、当前任务」开始执行                                  │
│                                                                 │
│  3. 完成后更新本文件:                                           │
│     • 标记 ✅                                                   │
│     • 更新当前任务指向下一 Sprint                               │
│     • 更新阶段总览状态                                          │
│                                                                 │
│  4. 遇到阻塞更新「十一、阻塞事项」并停止                       │
│                                                                 │
│  ⛔ 禁止:                                                       │
│  • 跳过当前任务                                                 │
│  • 依赖聊天历史                                                 │
│  • 创建文件名变体                                               │
│  • 不更新本文件就做下一任务                                     │
│                                                                 │
└─────────────────────────────────────────────────────────────────┘
```

---

## 附录：Execution Track（历史执行日志，非进度锚点）
- Stage A (Top/Left/Parameter): completed.
- Build: dotnet build src/UI/Neo.UI.csproj -> passed (0 errors, 0 warnings).
- Test: dotnet test tests/UI.Tests/Neo.UI.Tests.csproj -> passed (122/122).
- Stage B (Video/NIRS panels): completed.
- Build: dotnet build src/UI/Neo.UI.csproj -> passed (0 errors, 0 warnings).
- Test: dotnet test tests/UI.Tests/Neo.UI.Tests.csproj -> passed (132/132).
- Stage C (Dialogs/DialogService): completed.
- Build: dotnet build src/UI/Neo.UI.csproj -> passed (0 errors, 0 warnings).
- Test: dotnet test tests/UI.Tests/Neo.UI.Tests.csproj -> passed (138/138).
- Stage D (integration/acceptance): completed.
- Build: dotnet build src/UI/Neo.UI.csproj -> passed (0 errors, 0 warnings).
- Test: dotnet test tests/UI.Tests/Neo.UI.Tests.csproj -> passed (141/141).
- Post-stage integration (Video/NIRS runtime adapters): completed.
- Build: dotnet build src/UI/Neo.UI.csproj -> passed (0 errors, 0 warnings).
- Test: dotnet test tests/UI.Tests/Neo.UI.Tests.csproj -> passed (146/146).
- Continue pass (NIRS simulated fallback stream): completed.
- Build: dotnet build src/UI/Neo.UI.csproj -> passed (0 errors, 0 warnings).
- Test: dotnet test tests/UI.Tests/Neo.UI.Tests.csproj -> passed (148/148).
- Waveform realism tuning (clinical-like mock shaping): completed.
- Build: dotnet build src/UI/Neo.UI.csproj -> passed (0 errors, 0 warnings).
- Test: dotnet test tests/UI.Tests/Neo.UI.Tests.csproj -> passed (149/149).
- Waveform realism tuning (burst/suppression/spike shaping): completed.
- Build: dotnet build src/UI/Neo.UI.csproj -> passed with warnings (Neo.UI.exe locked by running app).
- Test: dotnet test tests/UI.Tests/Neo.UI.Tests.csproj -> passed (149/149).
- Waveform visual consistency (dark aEEG/EEG grid/time axis/GS layout): completed.
- Build: dotnet build src/UI/Neo.UI.csproj -> passed (0 errors, 0 warnings).
- Test: dotnet test tests/UI.Tests/Neo.UI.Tests.csproj -> passed (149/149).

---

**文档结束**
