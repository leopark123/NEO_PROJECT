// EegWaveformRenderData.cs
// EEG 波形预构建渲染数据 - 来源: ARCHITECTURE.md §5, 00_CONSTITUTION.md 铁律6

using System.Numerics;
using Vortice.Mathematics;

namespace Neo.Rendering.EEG;

/// <summary>
/// EEG 波形预构建渲染数据（单通道）。
/// </summary>
/// <remarks>
/// 此结构由 PolylineBuilder 在预处理阶段构建，
/// 渲染器只读取并执行 Draw 调用。
///
/// 铁律6: 渲染线程只做 Draw，无 O(N) 计算。
/// </remarks>
public readonly struct EegChannelRenderData
{
    /// <summary>
    /// 通道索引。
    /// </summary>
    public required int ChannelIndex { get; init; }

    /// <summary>
    /// 预构建的点坐标数组。
    /// </summary>
    public required Vector2[] Points { get; init; }

    /// <summary>
    /// 连续线段列表。
    /// </summary>
    public required PolylineSegment[] Segments { get; init; }

    /// <summary>
    /// 间隙区域列表。
    /// </summary>
    public required GapInfo[] Gaps { get; init; }

    /// <summary>
    /// 饱和点索引列表。
    /// </summary>
    public required int[] SaturationIndices { get; init; }

    /// <summary>
    /// 通道显示区域。
    /// </summary>
    public required Rect ChannelArea { get; init; }

    /// <summary>
    /// 通道颜色。
    /// </summary>
    public required Color4 Color { get; init; }

    /// <summary>
    /// 线宽。
    /// </summary>
    public required float LineWidth { get; init; }

    /// <summary>
    /// 基线 Y 坐标。
    /// </summary>
    public required float BaselineY { get; init; }

    /// <summary>
    /// 是否有有效数据。
    /// </summary>
    public bool HasData => Points.Length > 0;
}

/// <summary>
/// EEG 波形预构建渲染数据（所有通道）。
/// </summary>
/// <remarks>
/// 此结构包含所有通道的预构建渲染数据，
/// 由预处理线程构建，渲染器只做 Draw 调用。
///
/// 铁律6: 渲染线程只做 Draw，无 O(N) 计算。
/// </remarks>
public readonly struct EegWaveformRenderData
{
    /// <summary>
    /// 各通道的渲染数据。
    /// </summary>
    public required EegChannelRenderData[] Channels { get; init; }

    /// <summary>
    /// 是否有有效数据。
    /// </summary>
    public bool HasData => Channels.Length > 0;
}
