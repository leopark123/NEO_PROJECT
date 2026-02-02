// LayerBase.cs
// 渲染层基类 - 来源: ARCHITECTURE.md §5, ADR-008

using Neo.Rendering.Core;
using Neo.Rendering.Resources;
using Vortice.Direct2D1;

namespace Neo.Rendering.Layers;

/// <summary>
/// 渲染层基类。
/// 提供通用功能实现。
/// </summary>
/// <remarks>
/// 依据: ARCHITECTURE.md §5, ADR-008
///
/// 设计原则:
/// - 提供通用启用/禁用逻辑
/// - 提供缓存失效标记
/// - 派生类实现具体绘制
/// </remarks>
public abstract class LayerBase : ILayer
{
    private bool _disposed;

    /// <inheritdoc/>
    public abstract string Name { get; }

    /// <inheritdoc/>
    public bool IsEnabled { get; set; } = true;

    /// <inheritdoc/>
    public abstract int Order { get; }

    /// <summary>
    /// 是否需要重绘。
    /// </summary>
    protected bool NeedsRedraw { get; set; } = true;

    /// <inheritdoc/>
    public void Render(ID2D1DeviceContext context, ResourceCache resources, RenderContext renderContext)
    {
        if (_disposed || !IsEnabled)
            return;

        OnRender(context, resources, renderContext);
    }

    /// <summary>
    /// 执行实际渲染。
    /// </summary>
    /// <param name="context">D2D 设备上下文。</param>
    /// <param name="resources">资源缓存。</param>
    /// <param name="renderContext">渲染状态上下文。</param>
    /// <remarks>
    /// 铁律6: 只做 Draw 调用。
    /// </remarks>
    protected abstract void OnRender(ID2D1DeviceContext context, ResourceCache resources, RenderContext renderContext);

    /// <inheritdoc/>
    public void Invalidate()
    {
        NeedsRedraw = true;
    }

    /// <summary>
    /// 释放资源。
    /// </summary>
    /// <param name="disposing">是否释放托管资源。</param>
    protected virtual void Dispose(bool disposing)
    {
        if (_disposed)
            return;

        _disposed = true;
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }
}
