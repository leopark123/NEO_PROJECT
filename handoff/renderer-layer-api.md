# Handoff: S1-04 三层渲染框架

> **Sprint**: S1
> **Task**: S1-04
> **创建日期**: 2026-01-28
> **状态**: ✅ 完成

---

## 1. 概述

本任务实现了三层渲染框架，在 S1-03 Vortice 渲染底座基础上构建分层渲染架构。

核心特性:
- 三层独立渲染（Grid → Content → Overlay）
- 层级启用/禁用控制
- 按 Order 自动排序绘制
- 占位渲染（Mock Draw）验证管线

---

## 2. 文件清单

| 文件 | 用途 |
|------|------|
| `src/Rendering/Layers/ILayer.cs` | 渲染层接口 |
| `src/Rendering/Layers/LayerBase.cs` | 渲染层基类 |
| `src/Rendering/Layers/GridLayer.cs` | 网格层（背景） |
| `src/Rendering/Layers/ContentLayer.cs` | 内容层（占位波形） |
| `src/Rendering/Layers/OverlayLayer.cs` | 覆盖层（时间轴、光标） |
| `src/Rendering/Core/LayeredRenderer.cs` | 分层渲染器 |
| `tests/Rendering.Tests/Layers/LayerTests.cs` | 层单元测试 |
| `tests/Rendering.Tests/Layers/LayeredRendererTests.cs` | 渲染器测试 |
| `tests/Rendering.Tests/Layers/LayerOrderingTests.cs` | 排序测试 |
| `tests/Rendering.Tests/Layers/MultiDpiTests.cs` | 多DPI测试 |

---

## 3. 三层架构

```
┌─────────────────────────────────────────────────┐
│  Layer 3: Overlay (Order=2)                     │  ← 最后绘制（最上层）
│  - 时间轴刻度                                    │
│  - 光标/标记                                     │
│  - 文本标签                                      │
├─────────────────────────────────────────────────┤
│  Layer 2: Content (Order=1)                     │  ← 中间绘制
│  - 占位波形                                      │
│  - 通道区域                                      │
│  - (未来: EEG/aEEG/NIRS)                        │
├─────────────────────────────────────────────────┤
│  Layer 1: Grid (Order=0)                        │  ← 最先绘制（最底层）
│  - 背景填充                                      │
│  - 主/次网格线                                   │
│  - 坐标参考                                      │
└─────────────────────────────────────────────────┘
```

### 渲染顺序

层按 `Order` 属性升序绘制（数值越小越先绘制）:

1. **GridLayer (Order=0)** - 背景网格
2. **ContentLayer (Order=1)** - 波形内容
3. **OverlayLayer (Order=2)** - 叠加元素

---

## 4. 组件 API

### 4.1 ILayer 接口

```csharp
public interface ILayer : IDisposable
{
    /// <summary>层名称（调试用）</summary>
    string Name { get; }

    /// <summary>层是否启用</summary>
    bool IsEnabled { get; set; }

    /// <summary>层渲染顺序（数值越小越先绘制）</summary>
    int Order { get; }

    /// <summary>渲染此层</summary>
    void Render(ID2D1DeviceContext context, ResourceCache resources, RenderContext renderContext);

    /// <summary>通知层需要重绘</summary>
    void Invalidate();
}
```

### 4.2 LayeredRenderer

```csharp
public sealed class LayeredRenderer : IDisposable
{
    /// <summary>所有层（只读）</summary>
    IReadOnlyList<ILayer> Layers { get; }

    /// <summary>层数量</summary>
    int LayerCount { get; }

    /// <summary>添加渲染层</summary>
    void AddLayer(ILayer layer);

    /// <summary>移除渲染层</summary>
    bool RemoveLayer(ILayer layer);

    /// <summary>按名称获取层</summary>
    ILayer? GetLayer(string name);

    /// <summary>按类型获取层</summary>
    T? GetLayer<T>() where T : class, ILayer;

    /// <summary>渲染一帧</summary>
    void RenderFrame(ID2D1DeviceContext context, ResourceCache resources, RenderContext renderContext);

    /// <summary>使所有层无效</summary>
    void InvalidateAll();

    /// <summary>创建默认三层配置</summary>
    static LayeredRenderer CreateDefault();
}
```

### 4.3 GridLayer

```csharp
public sealed class GridLayer : LayerBase
{
    string Name => "Grid";
    int Order => 0;

    /// <summary>是否绘制次网格线</summary>
    bool ShowMinorGrid { get; set; } = true;

    /// <summary>是否绘制主网格线</summary>
    bool ShowMajorGrid { get; set; } = true;
}
```

### 4.4 ContentLayer

```csharp
public sealed class ContentLayer : LayerBase
{
    string Name => "Content";
    int Order => 1;

    /// <summary>占位通道数量</summary>
    int PlaceholderChannelCount { get; set; } = 4;
}
```

### 4.5 OverlayLayer

```csharp
public sealed class OverlayLayer : LayerBase
{
    string Name => "Overlay";
    int Order => 2;

    /// <summary>是否显示时间轴</summary>
    bool ShowTimeAxis { get; set; } = true;

    /// <summary>是否显示光标</summary>
    bool ShowCursor { get; set; } = true;

    /// <summary>光标位置（归一化 0-1）</summary>
    float CursorPosition { get; set; } = 0.5f;
}
```

---

## 5. 使用示例

### 5.1 创建默认渲染器

```csharp
// 创建包含三层的默认渲染器
using var renderer = LayeredRenderer.CreateDefault();

// 获取特定层进行配置
var gridLayer = renderer.GetLayer<GridLayer>();
gridLayer.ShowMinorGrid = false;  // 关闭次网格

var overlayLayer = renderer.GetLayer<OverlayLayer>();
overlayLayer.CursorPosition = 0.75f;  // 设置光标位置
```

### 5.2 渲染循环

```csharp
// 在渲染循环中
void OnRender()
{
    renderTarget.BeginDraw();

    // 渲染所有层（按 Order 顺序）
    renderer.RenderFrame(
        renderTarget.D2DContext,
        renderTarget.Resources,
        currentRenderContext);

    renderTarget.EndDraw();
}
```

### 5.3 启用/禁用层

```csharp
// 禁用网格层
renderer.GetLayer("Grid")!.IsEnabled = false;

// 或按类型
renderer.GetLayer<GridLayer>()!.IsEnabled = false;
```

### 5.4 自定义层顺序

```csharp
// 层按 Order 自动排序
// 添加顺序不影响绘制顺序
using var renderer = new LayeredRenderer();
renderer.AddLayer(new OverlayLayer());   // Order=2
renderer.AddLayer(new GridLayer());      // Order=0
renderer.AddLayer(new ContentLayer());   // Order=1

// RenderFrame 时按 Order 排序：Grid → Content → Overlay
```

---

## 6. 扩展规则

### 6.1 添加新层

1. 继承 `LayerBase` 或实现 `ILayer`
2. 设置唯一的 `Order` 值
3. 实现 `OnRender()` 方法（仅 Draw 调用）

```csharp
public sealed class CustomLayer : LayerBase
{
    public override string Name => "Custom";
    public override int Order => 10;  // 在 Overlay 之后

    protected override void OnRender(
        ID2D1DeviceContext context,
        ResourceCache resources,
        RenderContext renderContext)
    {
        // 仅 Draw 调用，铁律6
        var brush = resources.GetSolidBrush(1, 0, 0, 1);
        context.DrawLine(...);
    }
}
```

### 6.2 未来扩展（S2/S3）

| 层 | 未来功能 |
|----|----------|
| ContentLayer | EEG 波形、aEEG 灰度图、NIRS 趋势图 |
| OverlayLayer | 测量标注、事件标记、伪迹遮罩 |
| GridLayer | 时间刻度标签、可缓存到纹理 |

---

## 7. 约束

| 约束 | 实现 |
|------|------|
| 每层独立类 | ✅ GridLayer/ContentLayer/OverlayLayer |
| 渲染顺序固定 | ✅ Grid(0) → Content(1) → Overlay(2) |
| 渲染线程只 Draw（铁律6） | ✅ Render() 仅含 Draw 调用 |
| 支持启用/禁用 | ✅ IsEnabled 属性 |
| 无真实波形实现 | ✅ 占位渲染 |

---

## 8. 测试覆盖

| 测试文件 | 覆盖内容 |
|----------|----------|
| `LayerTests.cs` | 层属性、启用/禁用、默认值 |
| `LayeredRendererTests.cs` | 创建、添加/移除、按名称/类型获取 |
| `LayerOrderingTests.cs` | Order 值、排序正确性 |
| `MultiDpiTests.cs` | 不同 DPI 和分辨率下无异常 |

---

## 9. 证据引用

| 实现 | 依据文档 | 章节 |
|------|----------|------|
| 三层架构 | ARCHITECTURE.md | §5 |
| 三层设计 | DECISIONS.md | ADR-008 |
| 渲染线程约束 | 00_CONSTITUTION.md | 铁律6 |
| Vortice 渲染 | DECISIONS.md | ADR-002 |

---

## 10. 依赖关系

### 前置依赖
- S1-03: Vortice 渲染底座（D2DRenderTarget, ResourceCache）

### 被依赖
- S1-05: 模拟数据源（将数据传递给 ContentLayer）
- S1-06: 系统集成（完整渲染管线）
- S2-xx: DSP 链路（aEEG 渲染扩展）

---

## 11. 版本兼容性说明

本任务期间进行了以下版本升级：

| 组件 | 原版本 | 新版本 |
|------|--------|--------|
| .NET | 8.0 | 9.0 |
| Vortice.* | 3.7.0 | 3.8.1 |

Vortice 3.8.1 API 变更适配：
- `SwapChainDescription1.Width/Height` 改为 `uint`
- `ResizeBuffers` 参数改为 `uint`
- `Present` 参数改为 `uint`
- `DotsPerInch` 属性改为 `SetDpi()` 方法
- `FontStyle` 需完全限定避免歧义

---

**文档结束**
