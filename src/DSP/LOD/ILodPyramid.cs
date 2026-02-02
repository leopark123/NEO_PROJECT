// ILodPyramid.cs
// LOD 金字塔接口 - AT-12
//
// 依据: ARCHITECTURE.md §4.3 (LOD 金字塔)

namespace Neo.DSP.LOD;

/// <summary>
/// LOD 金字塔接口：多层 Min/Max 降采样。
/// </summary>
/// <remarks>
/// 依据: ARCHITECTURE.md §4.3
///
/// 层级定义:
/// - L0: 原始值 (1:1)
/// - L1: 2x 降采样 (Min/Max)
/// - L2: 4x 降采样
/// - ...
/// - LN: 2^N 降采样
/// </remarks>
public interface ILodPyramid
{
    /// <summary>总样本数（L0 原始数据）</summary>
    int TotalSamples { get; }

    /// <summary>层级数（包含 L0）</summary>
    int LevelCount { get; }

    /// <summary>第一个样本的时间戳（微秒）</summary>
    long FirstTimestampUs { get; }

    /// <summary>采样间隔（微秒）</summary>
    long SampleIntervalUs { get; }

    /// <summary>
    /// 添加新样本（增量构建）。
    /// </summary>
    void AddSample(double value);

    /// <summary>
    /// 批量添加样本。
    /// </summary>
    void AddSamples(ReadOnlySpan<double> values);

    /// <summary>
    /// 根据视口宽度和时间范围自动选择最优层级。
    /// </summary>
    /// <param name="timeRangeUs">可见时间范围（微秒）</param>
    /// <param name="viewWidthPixels">视口宽度（像素）</param>
    /// <returns>最优层级编号</returns>
    int SelectLevel(long timeRangeUs, int viewWidthPixels);

    /// <summary>
    /// 获取指定层级在时间范围内的数据。
    /// </summary>
    /// <param name="level">层级编号</param>
    /// <param name="startTimeUs">起始时间（微秒）</param>
    /// <param name="endTimeUs">结束时间（微秒）</param>
    /// <param name="output">输出缓冲区</param>
    /// <returns>实际填充的条目数</returns>
    int GetLevel(int level, long startTimeUs, long endTimeUs, Span<MinMaxPair> output);

    /// <summary>
    /// 获取指定层级的条目数。
    /// </summary>
    int GetLevelCount(int level);
}
