// LayeredRenderer.cs
// 分层渲染器 - 来源: ARCHITECTURE.md §5, ADR-008

using Neo.Rendering.Layers;
using Neo.Rendering.Resources;
using Vortice.Direct2D1;

namespace Neo.Rendering.Core;

/// <summary>
/// 分层渲染器。
/// 管理和协调多个渲染层的绘制。
/// </summary>
/// <remarks>
/// 依据: ARCHITECTURE.md §5, ADR-008 (三层渲染架构)
///
/// 渲染顺序:
/// 1. Grid Layer (Order=0) - 背景网格
/// 2. Content Layer (Order=1) - 波形内容
/// 3. Overlay Layer (Order=2) - 叠加元素
///
/// 设计原则:
/// - 按 Order 排序绘制
/// - 支持动态添加/移除层
/// - 支持层启用/禁用
///
/// 铁律约束（铁律6）:
/// - RenderFrame() 只做 Draw 调用
/// - 不做 O(N) 计算（层数为常数）
/// </remarks>
public sealed class LayeredRenderer : IDisposable
{
    private readonly List<ILayer> _layers = new();
    private readonly object _layerLock = new();
    private bool _disposed;
    private bool _needsSort;

    /// <summary>
    /// 获取所有层（只读）。
    /// </summary>
    public IReadOnlyList<ILayer> Layers
    {
        get
        {
            lock (_layerLock)
            {
                return _layers.ToArray();
            }
        }
    }

    /// <summary>
    /// 层数量。
    /// </summary>
    public int LayerCount
    {
        get
        {
            lock (_layerLock)
            {
                return _layers.Count;
            }
        }
    }

    /// <summary>
    /// 添加渲染层。
    /// </summary>
    /// <param name="layer">要添加的层。</param>
    public void AddLayer(ILayer layer)
    {
        ArgumentNullException.ThrowIfNull(layer);
        ObjectDisposedException.ThrowIf(_disposed, this);

        lock (_layerLock)
        {
            _layers.Add(layer);
            _needsSort = true;
        }
    }

    /// <summary>
    /// 移除渲染层。
    /// </summary>
    /// <param name="layer">要移除的层。</param>
    /// <returns>是否移除成功。</returns>
    public bool RemoveLayer(ILayer layer)
    {
        ArgumentNullException.ThrowIfNull(layer);
        ObjectDisposedException.ThrowIf(_disposed, this);

        lock (_layerLock)
        {
            return _layers.Remove(layer);
        }
    }

    /// <summary>
    /// 按名称获取层。
    /// </summary>
    /// <param name="name">层名称。</param>
    /// <returns>层实例，不存在则返回 null。</returns>
    public ILayer? GetLayer(string name)
    {
        lock (_layerLock)
        {
            return _layers.FirstOrDefault(l => l.Name == name);
        }
    }

    /// <summary>
    /// 按类型获取层。
    /// </summary>
    /// <typeparam name="T">层类型。</typeparam>
    /// <returns>层实例，不存在则返回 null。</returns>
    public T? GetLayer<T>() where T : class, ILayer
    {
        lock (_layerLock)
        {
            return _layers.OfType<T>().FirstOrDefault();
        }
    }

    /// <summary>
    /// 渲染一帧。
    /// </summary>
    /// <param name="context">D2D 设备上下文。</param>
    /// <param name="resources">资源缓存。</param>
    /// <param name="renderContext">渲染状态上下文。</param>
    /// <remarks>
    /// 铁律6: 此方法只做 Draw 调用。
    /// 层按 Order 升序绘制（小的先绘制）。
    /// </remarks>
    public void RenderFrame(ID2D1DeviceContext context, ResourceCache resources, RenderContext renderContext)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (context == null || resources == null || renderContext == null)
            return;

        ILayer[] layersToRender;

        lock (_layerLock)
        {
            // 如果需要排序，则排序
            if (_needsSort)
            {
                _layers.Sort((a, b) => a.Order.CompareTo(b.Order));
                _needsSort = false;
            }

            // 复制层列表以避免在渲染期间锁定
            layersToRender = _layers.ToArray();
        }

        // 按顺序渲染每一层
        foreach (var layer in layersToRender)
        {
            if (layer.IsEnabled)
            {
                layer.Render(context, resources, renderContext);
            }
        }
    }

    /// <summary>
    /// 使所有层无效（需要重绘）。
    /// </summary>
    public void InvalidateAll()
    {
        lock (_layerLock)
        {
            foreach (var layer in _layers)
            {
                layer.Invalidate();
            }
        }
    }

    /// <summary>
    /// 创建默认的三层渲染器配置。
    /// </summary>
    /// <returns>配置好的 LayeredRenderer 实例。</returns>
    /// <remarks>
    /// 包含:
    /// - GridLayer (Order=0)
    /// - ContentLayer (Order=1)
    /// - OverlayLayer (Order=2)
    /// </remarks>
    public static LayeredRenderer CreateDefault()
    {
        var renderer = new LayeredRenderer();
        renderer.AddLayer(new GridLayer());
        renderer.AddLayer(new ContentLayer());
        renderer.AddLayer(new OverlayLayer());
        return renderer;
    }

    /// <summary>
    /// 释放所有层和资源。
    /// </summary>
    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;

        lock (_layerLock)
        {
            foreach (var layer in _layers)
            {
                layer.Dispose();
            }
            _layers.Clear();
        }
    }
}
