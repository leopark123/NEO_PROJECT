// IRenderTarget.cs
// 抽象渲染目标接口 - 来源: ARCHITECTURE.md §5, 00_CONSTITUTION.md 铁律6

namespace Neo.Core.Interfaces;

/// <summary>
/// 抽象渲染目标接口。
/// 波形和图表渲染的目标抽象。
/// </summary>
/// <remarks>
/// 依据: ARCHITECTURE.md §5, 00_CONSTITUTION.md 铁律6
///
/// 职责:
/// - 定义渲染目标的基本契约
/// - 提供渲染区域和状态查询
///
/// 本接口不负责:
/// - 具体渲染实现（由 Vortice 实现，S1-03）
/// - 数据采集（由 ITimeSeriesSource 负责）
/// - DSP 处理（由 IFilterChain 负责）
/// - 数据存储（由 IDataSink 负责）
///
/// 铁律约束（铁律6）:
/// - 渲染线程只做 GPU 绘制调用
/// - 不做 O(N) 计算
/// - 不分配大对象
/// - 不访问 SQLite
/// </remarks>
public interface IRenderTarget
{
    /// <summary>
    /// 渲染区域宽度（像素）。
    /// </summary>
    int Width { get; }

    /// <summary>
    /// 渲染区域高度（像素）。
    /// </summary>
    int Height { get; }

    /// <summary>
    /// 当前 DPI 缩放因子。
    /// </summary>
    float DpiScale { get; }

    /// <summary>
    /// 渲染目标是否有效可用。
    /// </summary>
    bool IsValid { get; }

    /// <summary>
    /// 开始一帧渲染。
    /// </summary>
    void BeginDraw();

    /// <summary>
    /// 结束一帧渲染。
    /// </summary>
    void EndDraw();

    /// <summary>
    /// 处理设备丢失后的恢复。
    /// </summary>
    void HandleDeviceLost();
}
