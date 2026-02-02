# Handoff: S1-03 Renderer Device API (Vortice 渲染底座)

> **Sprint**: S1
> **Task**: S1-03
> **创建日期**: 2026-01-28
> **状态**: ✅ 完成

---

## 1. 概述

本任务实现了基于 Vortice 的 Direct3D11/Direct2D 渲染基础设施，包括：

- D3D11 设备管理
- DXGI 交换链管理
- DPI 处理
- D2D 设备与上下文
- 渲染资源缓存
- 设备丢失恢复

---

## 2. 文件清单

| 文件 | 用途 |
|------|------|
| `src/Rendering/Device/GraphicsDevice.cs` | D3D11 设备管理、Feature Level 协商 |
| `src/Rendering/Device/SwapChainManager.cs` | 交换链创建、窗口绑定、缓冲区调整、Present |
| `src/Rendering/Device/D2DDeviceManager.cs` | D2D Factory/Device/Context 管理 |
| `src/Rendering/Device/DpiHelper.cs` | DPI 查询、DIP ↔ 像素转换 |
| `src/Rendering/Core/RenderContext.cs` | 渲染上下文状态容器 |
| `src/Rendering/Core/D2DRenderTarget.cs` | IRenderTarget 实现 |
| `src/Rendering/Resources/ResourceCache.cs` | Brush/TextFormat 缓存 |

---

## 3. 组件 API

### 3.1 GraphicsDevice

D3D11 设备管理器，负责设备创建和 Feature Level 协商。

| 成员 | 类型 | 说明 |
|------|------|------|
| `Device` | `ID3D11Device { get; }` | D3D11 设备 |
| `Context` | `ID3D11DeviceContext { get; }` | 设备上下文 |
| `DxgiFactory` | `IDXGIFactory2 { get; }` | DXGI 工厂 |
| `FeatureLevel` | `FeatureLevel { get; }` | 当前 Feature Level |
| `IsDeviceValid` | `bool { get; }` | 设备是否有效 |
| `DeviceLost` | `event Action?` | 设备丢失事件 |
| `DeviceRestored` | `event Action?` | 设备恢复事件 |
| `CreateDevice()` | `bool` | 创建设备 |
| `CheckDeviceLost()` | `bool` | 检测设备丢失 |
| `HandleDeviceLost()` | `bool` | 处理设备丢失并恢复 |

**支持的 Feature Level（按优先级降序）**:
- D3D_FEATURE_LEVEL_11_1
- D3D_FEATURE_LEVEL_11_0
- D3D_FEATURE_LEVEL_10_1
- D3D_FEATURE_LEVEL_10_0

---

### 3.2 SwapChainManager

DXGI 交换链管理器。

| 成员 | 类型 | 说明 |
|------|------|------|
| `SwapChain` | `IDXGISwapChain1? { get; }` | 交换链 |
| `BackBuffer` | `ID3D11Texture2D? { get; }` | 后缓冲区 |
| `RenderTargetView` | `ID3D11RenderTargetView? { get; }` | 渲染目标视图 |
| `IsValid` | `bool { get; }` | 交换链是否有效 |
| `Width` | `int { get; }` | 当前宽度（像素） |
| `Height` | `int { get; }` | 当前高度（像素） |
| `CreateSwapChain(hwnd, size)` | `bool` | 创建交换链 |
| `Resize(size)` | `bool` | 调整大小 |
| `Present(syncInterval)` | `bool` | 呈现帧 |

**交换链配置**:
| 参数 | 值 |
|------|-----|
| Format | B8G8R8A8_UNorm |
| BufferCount | 2 |
| SwapEffect | FlipDiscard |
| Scaling | Stretch |

---

### 3.3 DpiHelper

DPI 处理静态工具类。

| 成员 | 说明 |
|------|------|
| `StandardDpi` | 标准 DPI 值（96.0） |
| `GetSystemDpi()` | 获取系统 DPI |
| `GetWindowDpi(hwnd)` | 获取窗口 DPI |
| `GetScaleFactor(dpi)` | 获取缩放因子 |
| `DipToPixel(dip, dpi)` | DIP 转像素（double 精度） |
| `PixelToDip(pixel, dpi)` | 像素转 DIP（double 精度） |
| `DipToPixelCeiling(dip, dpi)` | DIP 转像素（向上取整） |
| `DipToPixelRound(dip, dpi)` | DIP 转像素（四舍五入） |

**精度说明**: 所有转换使用 `double` 精度，来源于 ADR-006。

---

### 3.4 D2DDeviceManager

Direct2D 设备管理器。

| 成员 | 类型 | 说明 |
|------|------|------|
| `Factory` | `ID2D1Factory1? { get; }` | D2D 工厂 |
| `Device` | `ID2D1Device? { get; }` | D2D 设备 |
| `Context` | `ID2D1DeviceContext? { get; }` | D2D 设备上下文 |
| `IsValid` | `bool { get; }` | D2D 是否有效 |
| `CreateD2DResources()` | `bool` | 创建 D2D 资源 |
| `SetRenderTarget(swapChainManager, dpi)` | `bool` | 设置渲染目标 |

---

### 3.5 RenderContext

渲染上下文状态容器（只读，由 DSP 线程预填充）。

| 成员 | 类型 | 说明 |
|------|------|------|
| `CurrentTimestampUs` | `long` | 当前时间戳（微秒） |
| `VisibleRange` | `TimeRange` | 可见时间范围 |
| `Zoom` | `ZoomLevel` | 缩放级别 |
| `Channels` | `IReadOnlyList<ChannelRenderData>` | 通道数据 |
| `FrameNumber` | `long` | 帧序号 |
| `ViewportWidth` | `int` | 视口宽度 |
| `ViewportHeight` | `int` | 视口高度 |
| `Dpi` | `double` | DPI 值 |
| `DpiScale` | `double` | DPI 缩放因子 |
| `TimestampToX(timestampUs)` | `double` | 时间戳转 X 坐标 |
| `XToTimestamp(x)` | `long` | X 坐标转时间戳 |

**辅助类型**:

```csharp
public readonly record struct TimeRange(long StartUs, long EndUs);
public readonly record struct ZoomLevel(double SecondsPerScreen, int LodLevel);

public readonly struct ChannelRenderData
{
    public required int ChannelIndex { get; init; }
    public required string ChannelName { get; init; }
    public required ReadOnlyMemory<float> DataPoints { get; init; }
    public required long StartTimestampUs { get; init; }
    public required long SampleIntervalUs { get; init; }
    public required ReadOnlyMemory<byte> QualityFlags { get; init; }
}
```

---

### 3.6 D2DRenderTarget

实现 `IRenderTarget` 接口的完整渲染目标。

| 成员 | 类型 | 说明 |
|------|------|------|
| `Width` | `int { get; }` | 宽度（像素） |
| `Height` | `int { get; }` | 高度（像素） |
| `DpiScale` | `float { get; }` | DPI 缩放因子 |
| `Dpi` | `double { get; }` | DPI 值 |
| `IsValid` | `bool { get; }` | 是否有效 |
| `D2DContext` | `ID2D1DeviceContext? { get; }` | D2D 上下文 |
| `Resources` | `ResourceCache { get; }` | 资源缓存 |
| `DeviceLost` | `event Action?` | 设备丢失事件 |
| `DeviceRestored` | `event Action?` | 设备恢复事件 |
| `DpiChanged` | `event Action<double>?` | DPI 变更事件 |
| `Initialize(hwnd, size)` | `bool` | 初始化 |
| `Resize(size)` | `bool` | 调整大小 |
| `SetDpi(dpi)` | `void` | 设置 DPI |
| `BeginDraw()` | `void` | 开始绘制 |
| `EndDraw()` | `void` | 结束绘制 |
| `HandleDeviceLost()` | `void` | 处理设备丢失 |

---

### 3.7 ResourceCache

渲染资源缓存，避免每帧分配。

| 成员 | 说明 |
|------|------|
| `IsInitialized` | 是否已初始化 |
| `Initialize(context)` | 初始化缓存 |
| `GetSolidBrush(color)` | 获取纯色画刷 |
| `GetSolidBrush(r, g, b, a)` | 获取纯色画刷（RGBA） |
| `BlackBrush` | 黑色画刷 |
| `WhiteBrush` | 白色画刷 |
| `RedBrush` | 红色画刷 |
| `GreenBrush` | 绿色画刷 |
| `BlueBrush` | 蓝色画刷 |
| `GrayBrush` | 灰色画刷 |
| `LightGrayBrush` | 浅灰画刷 |
| `DarkGrayBrush` | 深灰画刷 |
| `GetTextFormat(fontFamily, fontSize, weight, style)` | 获取文本格式 |
| `DefaultTextFormat` | 默认文本格式（Segoe UI, 12pt） |
| `SmallTextFormat` | 小字体（10pt） |
| `LargeTextFormat` | 大字体（16pt） |
| `Clear()` | 清理所有缓存资源 |

---

## 4. 使用示例

```csharp
// 创建渲染目标
using var renderTarget = new D2DRenderTarget();

// 初始化（在窗口创建后）
if (!renderTarget.Initialize(hwnd, new Size(1920, 1080)))
{
    throw new Exception("Failed to initialize render target");
}

// 订阅事件
renderTarget.DeviceLost += () => Console.WriteLine("Device lost!");
renderTarget.DeviceRestored += () => Console.WriteLine("Device restored!");
renderTarget.DpiChanged += dpi => Console.WriteLine($"DPI changed to {dpi}");

// 渲染循环
while (running)
{
    if (!renderTarget.IsValid)
        continue;

    renderTarget.BeginDraw();

    // 使用 D2D 上下文绘制
    var ctx = renderTarget.D2DContext!;
    ctx.Clear(new Color4(0.1f, 0.1f, 0.1f, 1.0f));

    // 使用缓存的资源
    ctx.DrawLine(
        new Vector2(0, 0),
        new Vector2(100, 100),
        renderTarget.Resources.WhiteBrush,
        2.0f);

    ctx.DrawText(
        "Hello",
        renderTarget.Resources.DefaultTextFormat,
        new RectangleF(10, 10, 200, 50),
        renderTarget.Resources.WhiteBrush);

    renderTarget.EndDraw();
}

// 窗口大小变化时
renderTarget.Resize(new Size(newWidth, newHeight));

// DPI 变化时
renderTarget.SetDpi(newDpi);
```

---

## 5. 设备丢失恢复流程

```
┌─────────────────────────────────────────────────────────┐
│                    设备丢失检测                           │
│  Present() 返回 DXGI_ERROR_DEVICE_REMOVED/RESET         │
│  EndDraw() 返回失败                                      │
└──────────────────────┬──────────────────────────────────┘
                       │
                       ▼
┌─────────────────────────────────────────────────────────┐
│                 HandleDeviceLost()                       │
│  1. 清理资源缓存 (ResourceCache.Clear())                 │
│  2. 触发 DeviceLost 事件                                 │
│  3. 释放 D2D/SwapChain/D3D 资源                         │
└──────────────────────┬──────────────────────────────────┘
                       │
                       ▼
┌─────────────────────────────────────────────────────────┐
│                   重建设备                               │
│  1. GraphicsDevice.CreateDevice()                       │
│  2. SwapChainManager.CreateSwapChain()                  │
│  3. D2DDeviceManager.CreateD2DResources()               │
│  4. D2DDeviceManager.SetRenderTarget()                  │
│  5. ResourceCache.Initialize()                          │
└──────────────────────┬──────────────────────────────────┘
                       │
                       ▼
┌─────────────────────────────────────────────────────────┐
│                 触发 DeviceRestored 事件                 │
│                 继续正常渲染                              │
└─────────────────────────────────────────────────────────┘
```

---

## 6. 铁律约束（铁律6）

| 约束 | 实现方式 |
|------|----------|
| 渲染线程只 Draw | RenderContext 只读，所有数据由 DSP 预计算 |
| 不做 O(N) 计算 | TimestampToX/XToTimestamp 为 O(1) |
| 不分配大对象 | ResourceCache 预创建，复用画刷和文本格式 |
| 不访问 SQLite | 渲染层无任何存储依赖 |

---

## 7. 依赖

| 包 | 版本 | 用途 |
|-----|------|------|
| Vortice.Direct3D11 | 3.7.0 | D3D11 API |
| Vortice.Direct2D1 | 3.7.0 | D2D API |
| Vortice.DXGI | 3.7.0 | DXGI API |
| Vortice.DirectWrite | 3.7.0 | 文本渲染 |

---

## 8. 测试覆盖

| 测试文件 | 覆盖内容 |
|----------|----------|
| `GraphicsDeviceTests.cs` | 设备创建、销毁、丢失检测、恢复事件 |
| `DpiHelperTests.cs` | DPI 转换、精度验证、往返测试 |
| `RenderContextTests.cs` | 时间戳转换、坐标计算、边界条件 |

---

## 9. 证据引用

| 实现 | 依据文档 | 章节 |
|------|----------|------|
| Vortice 选型 | DECISIONS.md | ADR-002 |
| 三层架构 | DECISIONS.md | ADR-008 |
| double 精度 | DECISIONS.md | ADR-006 |
| 铁律6 约束 | 00_CONSTITUTION.md | 铁律6 |
| IRenderTarget | ARCHITECTURE.md | §5 |
| RenderContext | ARCHITECTURE.md | §5 |

---

## 10. 未实现/延后项

| 项目 | 原因 | 计划 |
|------|------|------|
| 波形绘制 | 超出本任务范围 | S1-04 后续任务 |
| 网格层缓存 | 依赖波形实现 | S1-04 后续任务 |
| 覆盖层 | 依赖 UI 交互 | S2+ |

---

**文档结束**
