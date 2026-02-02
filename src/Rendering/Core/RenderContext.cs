// RenderContext.cs
// 渲染上下文 - 来源: ARCHITECTURE.md §5

using Neo.Core.Models;

namespace Neo.Rendering.Core;

/// <summary>
/// 可见时间范围。
/// </summary>
/// <param name="StartUs">起始时间（微秒）。</param>
/// <param name="EndUs">结束时间（微秒）。</param>
public readonly record struct TimeRange(long StartUs, long EndUs)
{
    /// <summary>
    /// 时间范围长度（微秒）。
    /// </summary>
    public long DurationUs => EndUs - StartUs;
}

/// <summary>
/// 缩放级别定义。
/// </summary>
/// <param name="SecondsPerScreen">每屏显示秒数。</param>
/// <param name="LodLevel">LOD 金字塔层级（0=原始数据）。</param>
public readonly record struct ZoomLevel(double SecondsPerScreen, int LodLevel);

/// <summary>
/// 通道渲染数据。
/// </summary>
/// <remarks>
/// 此结构仅用于渲染层读取，不做任何计算。
/// 铁律6: 渲染线程只 Draw。
/// </remarks>
public readonly struct ChannelRenderData
{
    /// <summary>
    /// 通道索引。
    /// </summary>
    public required int ChannelIndex { get; init; }

    /// <summary>
    /// 通道名称。
    /// </summary>
    public required string ChannelName { get; init; }

    /// <summary>
    /// 数据点数组（只读，由 DSP 线程预填充）。
    /// </summary>
    public required ReadOnlyMemory<float> DataPoints { get; init; }

    /// <summary>
    /// 起始时间戳（微秒）。
    /// </summary>
    public required long StartTimestampUs { get; init; }

    /// <summary>
    /// 采样间隔（微秒）。
    /// </summary>
    public required long SampleIntervalUs { get; init; }

    /// <summary>
    /// 质量标志。
    /// </summary>
    public required ReadOnlyMemory<byte> QualityFlags { get; init; }
}

/// <summary>
/// 渲染上下文，传递给渲染器的状态容器。
/// </summary>
/// <remarks>
/// 依据: ARCHITECTURE.md §5
///
/// 设计原则:
/// - 只读状态容器，不包含绘制逻辑
/// - 所有数据由 DSP 线程预计算
/// - 渲染线程只读取，不修改
///
/// 铁律约束（铁律6）:
/// - 渲染线程只做 GPU 绘制调用
/// - 不做 O(N) 计算
/// - 不分配大对象
/// </remarks>
public sealed class RenderContext
{
    /// <summary>
    /// 当前时间戳（微秒）。
    /// </summary>
    public long CurrentTimestampUs { get; init; }

    /// <summary>
    /// 可见时间范围。
    /// </summary>
    public TimeRange VisibleRange { get; init; }

    /// <summary>
    /// 当前缩放级别。
    /// </summary>
    public ZoomLevel Zoom { get; init; }

    /// <summary>
    /// 通道渲染数据列表（只读）。
    /// </summary>
    public IReadOnlyList<ChannelRenderData> Channels { get; init; } = Array.Empty<ChannelRenderData>();

    /// <summary>
    /// 帧序号（用于调试）。
    /// </summary>
    public long FrameNumber { get; init; }

    /// <summary>
    /// 渲染区域宽度（像素）。
    /// </summary>
    public int ViewportWidth { get; init; }

    /// <summary>
    /// 渲染区域高度（像素）。
    /// </summary>
    public int ViewportHeight { get; init; }

    /// <summary>
    /// 当前 DPI 值。
    /// </summary>
    public double Dpi { get; init; } = 96.0;

    /// <summary>
    /// DPI 缩放因子（1.0 = 100%）。
    /// </summary>
    public double DpiScale => Dpi / 96.0;

    /// <summary>
    /// 将时间戳转换为 X 像素坐标。
    /// </summary>
    /// <param name="timestampUs">时间戳（微秒）。</param>
    /// <returns>X 像素坐标。</returns>
    /// <remarks>使用 double 精度。</remarks>
    public double TimestampToX(long timestampUs)
    {
        if (VisibleRange.DurationUs == 0)
            return 0;

        double normalizedPosition = (double)(timestampUs - VisibleRange.StartUs) / VisibleRange.DurationUs;
        return normalizedPosition * ViewportWidth;
    }

    /// <summary>
    /// 将 X 像素坐标转换为时间戳。
    /// </summary>
    /// <param name="x">X 像素坐标。</param>
    /// <returns>时间戳（微秒）。</returns>
    /// <remarks>使用 double 精度。</remarks>
    public long XToTimestamp(double x)
    {
        if (ViewportWidth == 0)
            return VisibleRange.StartUs;

        double normalizedPosition = x / ViewportWidth;
        return VisibleRange.StartUs + (long)(normalizedPosition * VisibleRange.DurationUs);
    }
}
