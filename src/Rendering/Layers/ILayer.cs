// ILayer.cs
// 渲染层接口 - 来源: ARCHITECTURE.md §5, ADR-008 (三层渲染架构)

using Neo.Rendering.Core;
using Neo.Rendering.Resources;
using Vortice.Direct2D1;

namespace Neo.Rendering.Layers;

/// <summary>
/// 渲染层接口。
/// 定义单个渲染层的基本契约。
/// </summary>
/// <remarks>
/// 依据: ARCHITECTURE.md §5, ADR-008 (三层渲染架构)
///
/// 设计原则:
/// - 每层独立，职责单一
/// - 只做 Draw 调用，不做计算
/// - 支持启用/禁用
///
/// 铁律约束（铁律6）:
/// - Render() 只包含 GPU 绘制调用
/// - 不做 O(N) 计算
/// - 不分配大对象
/// </remarks>
public interface ILayer : IDisposable
{
    /// <summary>
    /// 层名称（调试用）。
    /// </summary>
    string Name { get; }

    /// <summary>
    /// 层是否启用。
    /// </summary>
    bool IsEnabled { get; set; }

    /// <summary>
    /// 层渲染顺序（数值越小越先绘制）。
    /// </summary>
    int Order { get; }

    /// <summary>
    /// 渲染此层。
    /// </summary>
    /// <param name="context">D2D 设备上下文。</param>
    /// <param name="resources">资源缓存。</param>
    /// <param name="renderContext">渲染状态上下文。</param>
    /// <remarks>
    /// 铁律6: 此方法只做 Draw 调用。
    /// </remarks>
    void Render(ID2D1DeviceContext context, ResourceCache resources, RenderContext renderContext);

    /// <summary>
    /// 通知层需要重绘（缓存失效）。
    /// </summary>
    void Invalidate();
}
