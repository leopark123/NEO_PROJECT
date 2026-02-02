# NEO 新生儿 aEEG + NIRS 监护系统 — UI 功能梳理

> **版本**: 1.0
> **日期**: 2026-01-30
> **用途**: 交付给 Claude / ChatGPT 制定 UI 开发方案的输入文档
> **配合材料**: 原项目 UI 截图

---

## 一、系统定位

嵌入式床旁监护仪软件。目标用户：NICU 护士/医生。核心场景：72 小时连续脑功能监测。

- **平台**: Windows (.NET 9.0-windows)
- **UI 框架**: WinForms + Vortice Direct2D1/Direct3D11
- **刷新率**: 60 FPS (WinForms Timer 驱动)
- **EEG 采样率**: 160 Hz, 4 通道
- **NIRS**: 6 通道 (当前 Blocked, 协议未提供)
- **视频**: UVC 摄像头, H.264/MP4 录制

---

## 二、数据管线（后端已实现，不涉及 UI 开发）

```
硬件 RS232 ───→ 协议解析 ───→ EegRingBuffer ───→ DSP 滤波 ───→ D2D 渲染
  160Hz 4ch         ↓                                  ↓
              时间戳打点(μs)                      aEEG 处理
                                                  GS 直方图
                                                  LOD 金字塔
```

| 模块 | 状态 | 说明 |
|------|------|------|
| RS232 EEG 采集 | ✅ 已实现 | 160Hz, 4通道, CRC 校验 |
| NIRS 采集 | 🔒 Blocked | 协议未提供 (ADR-015) |
| UVC 视频采集 | ✅ 已实现 | MediaFoundation, H.264/MP4 录制 |
| IIR 滤波链 | ✅ 已实现 | Notch(50Hz) + HPF(0.5/1Hz) + LPF(15/35/50/70Hz) |
| aEEG 处理 | ✅ 已实现 | 带通→整流→包络→GS 直方图 |
| LOD 金字塔 | ✅ 已实现 | 10 层 Min/Max 降采样, <10ms 查询 |
| Zero-Phase 回放滤波 | ✅ 已实现 | filtfilt 前后向 IIR, 预滤波架构 |
| SQLite 存储 | ✅ 已实现 | Chunk BLOB, WAL, 300GiB 滚动清理 |
| 审计日志 | ✅ 已实现 | append-only, 11 种事件类型 |
| 同步回放 | ✅ 已实现 | PlaybackClock + 多流协调 (±100ms) |

---

## 三、UI 功能分区

### A. 主监护界面（实时模式）

| 功能区 | 描述 | 后端支撑 | UI 现状 |
|--------|------|---------|---------|
| **EEG 波形区** | 4 通道实时波形滚动显示, 默认 10 秒/屏 | EegRingBuffer → PolylineBuilder → D2D | ✅ D2D 渲染已实现, 无 UI 控件 |
| **aEEG 趋势区** | 半对数坐标 (10μV 线性, >10μV 对数) Min/Max 包络带 | AeegProcessor → AeegTrendRenderer | ✅ 渲染已实现, 无 UI 控件 |
| **GS 直方图区** | 每 15 秒一列的灰阶直方图 | GsProcessor → GsFrame | ✅ 数据已实现, 渲染待确认 |
| **NIRS 趋势区** | 6 通道 StO2 趋势线 (预留) | 🔒 Blocked | ❌ 占位 |
| **视频预览区** | 实时摄像头画面 | UsbCameraSource → VideoFrameDoubleBuffer | ❌ 未实现 |
| **时间轴** | 底部时间刻度, 当前游标 | OverlayLayer (占位) | ⚠️ 占位绘制 |
| **通道标签** | CH1(C3-P3) / CH2(C4-P4) / CH3(P3-P4) / CH4(C3-C4) | 硬编码 | ⚠️ 仅数据层, 无 UI 标签控件 |
| **质量指示** | 导联脱落 / 饱和 / 缺失 标记 | QualityFlag 枚举 | ❌ 未实现 |

### B. 参数控制面板

| 功能 | 当前交互方式 | 需要的 UI |
|------|-------------|----------|
| **增益调节** | F5/F6 键盘 (7 级: 10/20/50/70/100/200/1000 μV/cm) | 按钮/下拉/旋钮 + 当前值显示 |
| **LPF 截止频率** | F7/F8 键盘 (15/35/50/70 Hz) | 按钮/下拉 + 当前值显示 |
| **HPF 截止频率** | 无 UI (代码中固定 0.5Hz) | 按钮/下拉 |
| **Notch 滤波** | 无 UI (代码中固定 50Hz) | 开/关切换 |
| **走纸速度** | 无 UI (固定 10 秒/屏) | 选择器 (5/10/15/30 秒/屏) |
| **通道开关** | 无 UI | 每通道独立开关 |

### C. 工具栏操作

| 功能 | 当前交互方式 | 后端状态 |
|------|-------------|---------|
| **截图** | Ctrl+P → 保存 PNG + 审计 | ✅ ScreenshotService |
| **打印预览** | Ctrl+Shift+P → PrintPreviewForm | ✅ PrintService |
| **USB 导出** | Ctrl+E → 文件选择 | ✅ UsbExportService |
| **标注/事件标记** | 无 | ❌ 未实现 |
| **监控启动/停止** | 自动启动 | ⚠️ 无手动控制 |

### D. 回放界面

| 功能 | 后端状态 | UI 现状 |
|------|---------|---------|
| **播放/暂停** | PlaybackClock ✅ | ❌ 无按钮 |
| **进度条 (SeekBar)** | PlaybackClock.SeekTo() ✅ | ❌ 无控件 |
| **时间跳转** | NotifySeek() ✅ | ❌ 无输入框 |
| **变速播放** | 未实现 | ❌ |
| **EEG 波形回放** | EegPlaybackSource + ZeroPhase ✅ | ❌ 无 UI 切换 |
| **视频同步回放** | VideoPlaybackSource ✅ | ❌ 无 UI |
| **多流同步指示** | MultiStreamCoordinator ✅ | ❌ 无 UI |

### E. 状态栏 / 系统信息

| 信息 | 来源 | UI 现状 |
|------|------|---------|
| **当前时间** | Stopwatch | ❌ |
| **运行时长** | _sessionStartUs | ❌ |
| **帧率 (FPS)** | _frameNumber / elapsed | ❌ |
| **存储使用量** | StorageReaper | ❌ |
| **设备连接状态** | Rs232 / UVC / NIRS | ❌ |
| **数据源类型** | Mock / RS232 | ❌ |
| **滤波参数** | _currentLpfCutoff, _currentGain | ⚠️ 仅标题栏文字 |

### F. 患者信息

| 功能 | 现状 |
|------|------|
| **床位号** | 硬编码 "---" |
| **患者姓名/ID** | 硬编码 "---" |
| **输入/编辑界面** | ❌ 未实现 |

---

## 四、技术约束（UI 开发必须遵守）

| 编号 | 铁律 | 对 UI 的影响 |
|------|------|-------------|
| 铁律 2 | 不得伪造波形 | 缺失数据必须留白或标记, 不能插值填充 |
| 铁律 3 | 缩放用 Min/Max | LOD 金字塔已实现, UI 缩放必须调用 SelectLevel |
| 铁律 5 | 缺失/饱和必须可见 | 质量异常区间必须有视觉标记 (颜色/图标) |
| 铁律 6 | 渲染线程只做 Draw | UI 不能在渲染回调中做 O(N) 计算 |
| 铁律 7 | 全链路可审计 | 所有参数变更必须经过 AuditLog |
| 铁律 11 | 统一 int64 μs 时间轴 | 所有时间显示/跳转基于微秒时间戳 |

---

## 五、现有渲染架构

```
WinForms MainForm (整个窗口)
  └─ D2DRenderTarget (Vortice.Direct2D1 + Direct3D11)
       └─ LayeredRenderer
            ├─ Layer 0: GridLayer      — 背景网格 (缓存, 仅尺寸变化时重绘)
            ├─ Layer 1: ContentLayer   — 波形绘制 (每帧更新)
            └─ Layer 2: OverlayLayer   — 时间轴/光标/标记 (每帧更新)
```

### 关键渲染类

| 类 | 位置 | 职责 |
|----|------|------|
| D2DRenderTarget | src/Rendering/Core/ | D3D11 设备管理, 设备丢失恢复 |
| LayeredRenderer | src/Rendering/Core/ | 编排三层渲染顺序 |
| RenderContext | src/Rendering/Core/ | 帧级上下文 (时间范围/缩放/通道数据) |
| EegPolylineRenderer | src/Rendering/EEG/ | 波形折线绘制 |
| PolylineBuilder | src/Rendering/EEG/ | 样本→折线预处理, Gap 检测, Min/Max 提取 |
| EegGainScaler | src/Rendering/EEG/ | 增益缩放 (7 档) |
| AeegTrendRenderer | src/Rendering/AEEG/ | aEEG 趋势曲线绘制 |
| AeegSemiLogMapper | src/Rendering/Mapping/ | 半对数坐标映射 |
| ResourceCache | src/Rendering/Resources/ | 画刷/字体缓存 (避免每帧创建) |

### 当前 D2D 渲染区域

整个 MainForm ClientArea 全部由 D2D 绘制。没有 WinForms 控件叠加。

**UI 开发需要决策**: WinForms 控件 (按钮/面板) 与 D2D 渲染区如何共存？
- 方案 A: WinForms 控件在 D2D 区域外 (面板分割)
- 方案 B: 所有 UI 用 D2D 自绘 (一致性好, 开发量大)
- 方案 C: 混合 — 工具栏/状态栏用 WinForms, 波形区用 D2D

---

## 六、关键参数规格

### 增益档位 (EegGainScaler)

| 枚举值 | 显示文本 | 含义 |
|--------|---------|------|
| UvPerCm10 | 10 μV/cm | 最高灵敏度 |
| UvPerCm20 | 20 μV/cm | |
| UvPerCm50 | 50 μV/cm | |
| UvPerCm70 | 70 μV/cm | |
| UvPerCm100 | 100 μV/cm | 默认 |
| UvPerCm200 | 200 μV/cm | |
| UvPerCm1000 | 1000 μV/cm | 最低灵敏度 |

### 滤波器档位

| 滤波器 | 可选值 | 默认 |
|--------|--------|------|
| LPF (低通) | 15 / 35 / 50 / 70 Hz | 35 Hz |
| HPF (高通) | 0.5 / 1.0 Hz | 0.5 Hz |
| Notch (陷波) | 50 Hz / Off | 50 Hz |

### 走纸速度

当前固定 10 秒/屏。医疗设备常用档位: 5 / 10 / 15 / 30 秒/屏。

### 通道定义

| 通道 | 导联 | 类型 |
|------|------|------|
| CH1 | C3-P3 | 物理 |
| CH2 | C4-P4 | 物理 |
| CH3 | P3-P4 | 物理 |
| CH4 | C3-C4 | 计算 (CH1 - CH2) |

### aEEG 纵轴

- 0 ~ 10 μV: 线性刻度
- 10 ~ 100 μV: 对数刻度 (log10)
- 医学合规要求 (AT-25)

### 质量标记 (QualityFlag)

| 标记 | 含义 | 视觉表现建议 |
|------|------|-------------|
| Normal | 正常 | 默认颜色 |
| Missing | 数据缺失 | 留白 + 灰色虚线 |
| Saturated | 信号饱和 | 红色高亮 |
| LeadOff | 导联脱落 | 橙色 + 图标 |
| Interpolated | 插值 (禁止使用) | — |
| BlockedBySpec | 规格阻塞 (NIRS) | 灰色 + "Blocked" 文字 |

---

## 七、项目文件结构（与 UI 相关的）

```
src/
├── Host/                           ← UI 入口
│   ├── Program.cs                  ← Main() 入口
│   ├── MainForm.cs                 ← 主窗口 (当前所有 UI 逻辑, 634 行)
│   ├── Neo.Host.csproj             ← WinExe, net9.0-windows
│   ├── Services/
│   │   ├── ScreenshotService.cs    ← 截图 (D3D11 后缓冲 → PNG)
│   │   ├── PrintService.cs         ← 打印预览 (PrintPreviewForm)
│   │   └── UsbExportService.cs     ← USB 导出
│   ├── NirsWiring.cs               ← NIRS 集成壳
│   └── VideoWiring.cs              ← 视频采集接线
│
├── Rendering/                      ← D2D 渲染层
│   ├── Core/
│   │   ├── D2DRenderTarget.cs      ← D3D11 设备, 设备丢失恢复
│   │   ├── LayeredRenderer.cs      ← 三层渲染编排
│   │   └── RenderContext.cs        ← 帧级渲染上下文
│   ├── Device/
│   │   ├── GraphicsDevice.cs       ← D3D11 设备生命周期
│   │   ├── SwapChainManager.cs     ← DXGI 交换链
│   │   ├── D2DDeviceManager.cs     ← D2D 设备创建
│   │   └── DpiHelper.cs            ← DPI 感知
│   ├── EEG/
│   │   ├── EegPolylineRenderer.cs  ← 波形折线渲染
│   │   ├── PolylineBuilder.cs      ← 样本→折线预处理
│   │   ├── EegGainScaler.cs        ← 增益缩放 (7 档)
│   │   ├── EegChannelView.cs       ← 通道逻辑视图
│   │   └── EegColorPalette.cs      ← 通道颜色
│   ├── AEEG/
│   │   ├── AeegTrendRenderer.cs    ← aEEG 趋势曲线
│   │   ├── AeegSeriesBuilder.cs    ← aEEG Min/Max 累积
│   │   ├── AeegGridAndAxisRenderer.cs ← aEEG 网格和坐标轴
│   │   └── AeegColorPalette.cs     ← aEEG 颜色映射
│   ├── Mapping/
│   │   ├── AeegSemiLogMapper.cs    ← 半对数坐标映射
│   │   └── AeegAxisTicks.cs        ← 坐标轴刻度生成
│   ├── Layers/
│   │   ├── ILayer.cs               ← 层接口
│   │   ├── LayerBase.cs            ← 层基类
│   │   ├── GridLayer.cs            ← Layer 0: 背景网格
│   │   ├── ContentLayer.cs         ← Layer 1: 波形内容
│   │   └── OverlayLayer.cs         ← Layer 2: 叠加层 (占位)
│   └── Resources/
│       └── ResourceCache.cs        ← 画刷/字体缓存
│
├── DSP/                            ← 信号处理 (纯计算, 无 UI)
├── Storage/                        ← 数据存储 (SQLite, 无 UI)
├── Playback/                       ← 回放控制 (无 UI)
├── Video/                          ← 视频采集/回放 (无 UI)
├── Infrastructure/                 ← 缓冲区/并发 (无 UI)
├── DataSources/                    ← RS232 硬件接入 (无 UI)
├── NIRS/                           ← NIRS 集成壳 (Blocked)
└── Mock/                           ← Mock 数据源 (开发用)
```

---

## 八、现有键盘快捷键

| 快捷键 | 功能 | 审计事件 |
|--------|------|---------|
| F5 | 增益降低 (灵敏度提高) | GAIN_CHANGE |
| F6 | 增益增加 (灵敏度降低) | GAIN_CHANGE |
| F7 | LPF 截止频率降低 | FILTER_CHANGE |
| F8 | LPF 截止频率增加 | FILTER_CHANGE |
| Ctrl+P | 截图 (保存 PNG) | SCREENSHOT |
| Ctrl+Shift+P | 打印预览 | PRINT |
| Ctrl+E | USB 导出 | USB_EXPORT |

---

## 九、审计日志事件类型 (AT-21)

| 事件类型 | 触发点 | 状态 |
|---------|--------|------|
| MONITORING_START | 监控启动 | ✅ MainForm.OnFormLoad |
| MONITORING_STOP | 监控停止 | ✅ MainForm.OnFormClosing |
| FILTER_CHANGE | LPF 截止频率变更 | ✅ MainForm.DoCycleLpfCutoff |
| GAIN_CHANGE | 增益变更 | ✅ MainForm.DoCycleGain |
| SCREENSHOT | 截图保存 | ✅ ScreenshotService |
| PRINT | 打印完成 | ✅ PrintService |
| USB_EXPORT | USB 导出 | ✅ UsbExportService |
| DEVICE_LOST | D3D11 设备丢失 | ✅ MainForm.OnDeviceLost |
| DEVICE_RESTORED | D3D11 设备恢复 | ✅ MainForm.OnDeviceRestored |
| CRC_ERROR | RS232 CRC 校验失败 | ✅ Rs232EegSource (构造注入) |
| SERIAL_ERROR | RS232 串口异常 | ✅ Rs232EegSource (构造注入) |
| STORAGE_CLEANUP | 存储滚动清理 | ✅ StorageReaper |

---

## 十、UI 开发方案需要决策的问题

> 以下问题需要结合原项目 UI 截图, 由 Claude / ChatGPT 在制定方案时回答。

1. **布局方案**: 主界面如何划分区域？(EEG 波形区 / aEEG 趋势区 / GS 直方图区 / 视频预览区 / 参数面板 / 状态栏)
2. **控件与 D2D 共存策略**: WinForms 控件如何与 D2D 渲染区共存？(分割面板 / 全 D2D 自绘 / 混合)
3. **参数面板位置**: 侧边栏 vs 顶部工具栏 vs 底部面板？
4. **回放控制 UI**: 独立面板 vs 底部条 vs 模式切换？
5. **患者信息入口**: 对话框 vs 内嵌面板？
6. **响应式布局**: 是否需要支持多种分辨率？(嵌入式设备通常固定分辨率)
7. **触摸支持**: 是否需要触摸操作？(床旁设备可能有触摸屏)
8. **深色/浅色主题**: 当前背景 RGB(0.1, 0.1, 0.1) 深色。是否保持？
9. **标注功能范围**: 事件标记 (时间点标注) vs 区间标注 vs 文本注释？
10. **国际化**: 中文 / 英文 / 双语？

---

## 附录 A: 依赖图

```
Neo.Host (WinExe)
  ├── Neo.Core              (模型/接口/枚举)
  ├── Neo.Infrastructure    (缓冲区)
  ├── Neo.Rendering         (D2D 渲染)
  │    ├── Neo.Core
  │    ├── Vortice.Direct2D1 3.8.1
  │    ├── Vortice.Direct3D11 3.8.1
  │    └── Vortice.DXGI 3.8.1
  ├── Neo.DSP               (滤波/aEEG/LOD)
  │    └── Neo.Core
  ├── Neo.Storage            (SQLite)
  │    ├── Neo.Core
  │    └── Microsoft.Data.Sqlite
  ├── Neo.Playback           (回放时钟)
  │    ├── Neo.Core
  │    ├── Neo.Infrastructure
  │    └── Neo.DSP
  ├── Neo.Video              (UVC 视频)
  ├── Neo.DataSources        (RS232)
  │    ├── Neo.Core
  │    ├── Neo.Storage
  │    └── System.IO.Ports
  ├── Neo.NIRS               (NIRS 壳)
  └── Neo.Mock               (Mock 数据源)
       └── Neo.Core
```

## 附录 B: 测试覆盖

| 测试套件 | 测试数 | 通过 | 失败 | 说明 |
|----------|--------|------|------|------|
| DSP.Tests | 199 | 199 | 0 | 滤波/aEEG/GS/LOD/ZeroPhase |
| Rendering.Tests | 321 | 320 | 1 | DPI 浮点舍入 (预存) |
| Storage.Tests | 23 | 23 | 0 | Chunk/Reaper/AuditLog |
| Playback.Tests | 44 | 44 | 0 | PlaybackClock/ZeroPhase 集成 |
| Infrastructure.Tests | 20 | 18 | 2 | 多消费者压力测试 (预存) |
| StressTests | 1 | 0 | 1 | SQLite 事务竞态 (测试代码) |
| **合计** | **608** | **604** | **4** | **99.3% 通过率** |
