# UI Shell API — S3-UI-01 交付文档

> **任务编号**: S3-UI-01
> **任务名称**: UI 主窗口壳 + 区域占位
> **日期**: 2026-01-30
> **状态**: ✅ 完成

---

## 声明

**本交付物不包含任何业务逻辑。**

- 未接入 EEG / NIRS / Video 数据
- 未引用 DSP / Playback / Storage 项目
- 未实现任何事件处理逻辑
- 未引入第三方 UI 框架
- 所有按钮 `IsEnabled="False"`
- 所有数值显示为占位文本 (`--`)

---

## 1. 项目结构

```
src/UI/
├── Neo.UI.csproj          ← WPF WinExe, net9.0-windows
├── App.xaml               ← Application 入口
├── App.xaml.cs
├── MainWindow.xaml        ← 主窗口布局 (UI_SPEC §4)
├── MainWindow.xaml.cs     ← Code-behind (仅 InitializeComponent)
├── Views/                 ← 预留: 子页面视图
├── Controls/              ← 预留: 自定义控件
└── Resources/             ← 预留: 样式/资源字典
```

---

## 2. 窗口布局结构

基于 `UI_SPEC.md §4.1` 和 `§4.2`，使用 `DockPanel` 实现：

```
┌──────────────────────────────────────────────────────────────────────────┐
│ TopToolbar (60px) — DockPanel.Top                                        │
│ Logo | SeekBar | ▶ | 📷 | 📝 | --:--:-- | 用户:-- | 床位:--            │
├────────┬─────────────────────────────────────┬─────────┬─────────────────┤
│ LeftNav│         CenterWaveformArea          │ Param   │ VideoNirs       │
│ (60px) │                                     │ (150px) │ (300px)         │
│        │  aEEG Ch1 (15%)                     │         │                 │
│ 首页   │  EEG Ch1  (20%)                     │ 导联    │  视频预览       │
│ 历史   │  aEEG Ch2 (15%)                     │ 增益    │  No Camera      │
│ 显示   │  EEG Ch2  (20%)                     │ Y轴     │                 │
│ 滤波   │  NIRS     (20%)                     │ 滤波    │  NIRS 1-6       │
│ 用户   │  SeekBar  (10%)                     │ 速度    │  --% [Blocked]  │
│ 导出   │                                     │         │                 │
│ 关机   │                                     │         │                 │
├────────┴─────────────────────────────────────┴─────────┴─────────────────┤
│ BottomStatusBar (30px) — DockPanel.Bottom                                │
│ FPS:-- | 存储:--/-- | EEG:○ NIRS:○ Video:○ | Time:--:--:--             │
└──────────────────────────────────────────────────────────────────────────┘
```

---

## 3. 占位区域清单

### 3.1 顶部工具栏 (`TopToolbar`)

| 控件名 | 类型 | 占位内容 | UI_SPEC 引用 |
|--------|------|---------|-------------|
| `LogoPlaceholder` | Border+TextBlock | "N" (品红色块) | §4.1 |
| `SeekBarPlaceholder` | Border+TextBlock | "SeekBar Placeholder" | §6.4 |
| `PlayPauseButton` | Button | "▶" (禁用) | §6.4 |
| `ScreenshotButton` | Button | "📷" (禁用) | §6.6 |
| `AnnotationButton` | Button | "📝" (禁用) | §6.5 |
| `ToolbarTimePlaceholder` | TextBlock | "--:--:--" | §4.1 |
| `ToolbarUserPlaceholder` | TextBlock | "用户: --" | §4.1 |
| `ToolbarBedPlaceholder` | TextBlock | "床位: --" | §4.1 |

### 3.2 左侧导航栏 (`LeftNavigationBar`)

| 控件名 | 图标 | 文字 | UI_SPEC 引用 |
|--------|------|------|-------------|
| `NavHomeButton` | 🏠 | 首页 | §4.1 |
| `NavHistoryButton` | 📋 | 历史 | §9.6 |
| `NavDisplayButton` | 🖥 | 显示 | §9.4 |
| `NavFilterButton` | ⚙ | 滤波 | §9.3 |
| `NavUserButton` | 👤 | 用户 | §9.5 |
| `NavExportButton` | 💾 | 导出 | §4.1 |
| `NavShutdownButton` | ⏻ | 关机 | §4.1 |

所有按钮 `IsEnabled="False"`，无点击行为。

### 3.3 中央波形区 (`CenterWaveformArea`)

| 行 | 控件名 | 高度比例 | 占位文字 | UI_SPEC 引用 |
|----|--------|---------|---------|-------------|
| 0 | `AeegCh1Placeholder` | 15% | "aEEG 趋势区 — Ch1 占位" | §5.2 |
| 1 | `EegCh1WaveformPlaceholder` | 20% | "EEG 波形区 — Ch1 占位" | §5.1 |
| 2 | `AeegCh2Placeholder` | 15% | "aEEG 趋势区 — Ch2 占位" | §5.2 |
| 3 | `EegCh2WaveformPlaceholder` | 20% | "EEG 波形区 — Ch2 占位" | §5.1 |
| 4 | `NirsTrendPlaceholder` | 20% | "NIRS 趋势区 — 6通道占位 [Blocked]" | §5.3 |
| 5 | `TimelineSeekBarPlaceholder` | 10% | "SeekBar 时间轴占位" | §6.4 |

每行附带通道标签和 Y 轴说明文字。

### 3.4 右侧参数面板 (`RightParamPanel`)

| 控件名 | 占位内容 | UI_SPEC 引用 |
|--------|---------|-------------|
| `ParamLeadCh1` | "CH1: C3-P3" | §6.3 |
| `ParamLeadCh2` | "CH2: C4-P4" | §6.3 |
| `ParamGainValue` | "100 μV/cm" | §6.1 |
| `ParamYAxisRange` | "±100 μV" | §5.1 |
| `ParamHpf` | "HPF: 0.5 Hz" | §6.2 |
| `ParamLpf` | "LPF: 35 Hz" | §6.2 |
| `ParamNotch` | "Notch: 50 Hz" | §6.2 |
| `ParamSweepSpeed` | "15 秒/屏" | §5.1 |

### 3.5 右侧视频+NIRS面板 (`RightVideoNirsPanel`)

| 控件名 | 占位内容 | UI_SPEC 引用 |
|--------|---------|-------------|
| `VideoPreviewPlaceholder` | "视频预览 / No Camera" | §5.4 |
| `NirsCh1Placeholder` ~ `NirsCh6Placeholder` | "CHx rSO₂: --%" | §5.3 |

### 3.6 底部状态栏 (`BottomStatusBar`)

| 控件名 | 占位内容 | UI_SPEC 引用 |
|--------|---------|-------------|
| `StatusFps` | "FPS: --" | §8.1 |
| `StatusStorage` | "存储: -- / --" | §8.1 |
| `StatusEegIndicator` | 灰色圆点 ○ | §8.2 |
| `StatusNirsIndicator` | 灰色圆点 ○ | §8.2 |
| `StatusVideoIndicator` | 灰色圆点 ○ | §8.2 |
| `StatusTime` | "Time: --:--:--" | §8.1 |

---

## 4. 颜色使用

| 用途 | 色值 | UI_SPEC 引用 |
|------|------|-------------|
| 波形区背景 | #1A1A1A | §11.2 BackgroundDark |
| 面板/工具栏背景 | #2D2D2D | §11.2 Surface |
| Logo / 床位号 | #D81B60 | §11.1 Primary |
| EEG Ch1 标签 | #00E676 | §11.4 EegChannel1 |
| EEG Ch2 标签 | #FFD54F | §11.4 EegChannel2 |
| NIRS 标签 | #29B6F6 | §11.4 NirsTrend |
| 关机按钮 | #F44336 | §11.3 Error |
| 连接指示灯 (未连接) | #9E9E9E | §8.2 |

---

## 5. 构建验证

```
项目: src/UI/Neo.UI.csproj
框架: net9.0-windows (WPF)
构建: dotnet build -c Release → 0 错误, 0 警告
运行: dotnet run → 窗口正常显示, 无异常
```

---

## 6. 后续集成点

本壳为后续 Sprint 提供以下集成点：

| 占位控件 | 后续任务 | Phase |
|---------|---------|-------|
| `AeegCh1Placeholder` / `AeegCh2Placeholder` | D3DImage + aEEG 渲染 | Phase 3 |
| `EegCh1WaveformPlaceholder` / `EegCh2WaveformPlaceholder` | D3DImage + EEG 波形渲染 | Phase 3 |
| `NirsTrendPlaceholder` | NIRS 趋势渲染 | Phase 6 |
| `VideoPreviewPlaceholder` | UVC 摄像头预览 | Phase 5 |
| `TimelineSeekBarPlaceholder` | PlaybackClock SeekBar | Phase 5 |
| 所有导航按钮 | NavigationService 路由 | Phase 2 |
| 参数面板 | ViewModel 数据绑定 | Phase 2 |
| 状态栏 | 后端服务状态绑定 | Phase 2 |
