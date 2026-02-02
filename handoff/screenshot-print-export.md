# S4-04 截图、打印预览与 USB 导出 — Handoff

## 概述

为 NEO 新生儿脑功能监护系统新增三项临床交付级功能：截图保存、打印预览（含可编辑结论文本）、USB 导出。

## 实现方案

### 1. 截图 (Screenshot)

**技术路径**: D3D11 Swap Chain Back Buffer → Staging Texture → System.Drawing.Bitmap → PNG

**流程**:
1. `D2DRenderTarget.CaptureScreenshot()` 创建 CPU 可读的 staging 纹理
2. 通过 `ID3D11DeviceContext.CopyResource()` 将 back buffer 复制到 staging 纹理
3. Map staging 纹理，逐行复制像素到 `System.Drawing.Bitmap`
4. 保存为 PNG 格式

**WYSIWYG**: 直接捕获 GPU 渲染后的 back buffer，所见即所得，不做任何重新渲染或数据重算。

**文件命名**: `NEO_截图_yyyyMMdd_HHmmss.png`，同名自动追加序号 (`_2`, `_3`, ...)。

**存储位置**: `<应用目录>/screenshots/`

### 2. 打印预览 (Print Preview)

**技术路径**: WinForms `PrintPreviewControl` + `PrintDocument` + 自定义预览窗口

**打印内容** (上→下):
1. **标题**: "NEO 新生儿脑功能监护报告"
2. **分隔线**
3. **患者信息**: 床位 / 姓名 / ID（当前为占位符）
4. **记录时段**: 格式 `HH:mm:ss - HH:mm:ss`
5. **打印时间**: 灰色小字
6. **截图图像**: 居中显示，自适应缩放（最大占 55% 页面高度）
7. **分隔线**
8. **结论/备注**: 用户在预览窗口中编辑的文本
9. **页脚**: 居中，含系统名称和日期

**可编辑结论**: 预览窗口顶部提供多行文本框，用户可自由编辑结论内容。点击"刷新预览"可更新预览显示。

### 3. USB 导出 (USB Export)

**技术路径**: `DriveInfo.GetDrives()` + `DriveType.Removable` 检测 + `File.Copy`

**安全约束**:
- **不写入系统盘**: `IsSystemDrive()` 检查，比对 `Environment.SpecialFolder.Windows` 所在盘符
- **不覆盖现有文件**: `GetUniqueFilePath()` 自动追加序号
- **不静默失败**: 所有错误通过 `ExportResult` 返回并弹出 MessageBox

**驱动器选择逻辑**:
- 1 个 USB → 直接使用
- 多个 USB → 弹出选择对话框（显示盘符、卷标、可用空间）
- 0 个 USB → 弹出 `FolderBrowserDialog`（但仍禁止系统盘）

## 键盘快捷键

| 快捷键 | 功能 |
|--------|------|
| `Ctrl+P` | 截图并保存 |
| `Ctrl+Shift+P` | 打开打印预览 |
| `Ctrl+E` | 导出到 USB |

## 新增文件

| 文件 | 用途 |
|------|------|
| `src/Rendering/Core/D2DRenderTarget.cs` | 新增 `CaptureScreenshot()` 方法 |
| `src/Host/Services/ScreenshotService.cs` | 截图服务（捕获、文件命名、保存） |
| `src/Host/Services/PrintService.cs` | 打印预览窗口 + PrintDocument 渲染 |
| `src/Host/Services/UsbExportService.cs` | USB 驱动器检测 + 安全文件复制 |

## 修改文件

| 文件 | 变更 |
|------|------|
| `src/Host/MainForm.cs` | 新增 `_screenshotService` / `_printService` / `_usbExportService` 字段；`KeyDown` 事件处理；`DoScreenshot()` / `DoPrintPreview()` / `DoUsbExport()` 方法 |

## 铁律遵从

| 铁律 | 遵从方式 |
|------|----------|
| 铁律1 (原始数据不可修改) | 截图为 WYSIWYG，直接读取 GPU back buffer，不修改任何数据 |
| 铁律6 (渲染线程只 Draw) | `CaptureScreenshot()` 在 EndDraw 之后调用，不在渲染循环内 |
| 铁律12 (append-only) | 截图文件只创建不覆盖，同名自动追加序号 |

## 构建验证

```
dotnet build Neo.sln → 0 errors
```

## 证据

### 截图证据
- 输出路径: `<应用目录>/screenshots/NEO_截图_yyyyMMdd_HHmmss.png`
- 分辨率: 与窗口客户区一致（默认 1280x720）
- 格式: PNG (32bpp ARGB)
- 文件名含日期时间精确到秒

### 打印预览证据
- 预览窗口包含可编辑结论文本框
- 打印内容包含：标题、患者信息、时间范围、截图图像、结论文本、页脚
- 支持标准 Windows 打印对话框选择打印机

### USB 导出行为
- 检测到 1 个 USB → 直接导出
- 检测到多个 USB → 弹出选择对话框
- 检测到 0 个 USB → 弹出文件夹浏览器
- 系统盘 → 拒绝并提示
- 同名文件 → 自动追加序号 `_2`, `_3`, ...
- 空间不足 → 明确错误提示
- 写保护 → 明确错误提示
