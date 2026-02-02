// AeegSeriesBuilder.cs
// aEEG 序列构建器 - 来源: DSP_SPEC.md §3

using System.Numerics;
using Neo.Core.Enums;
using Neo.Rendering.Mapping;

namespace Neo.Rendering.AEEG;

/// <summary>
/// aEEG 趋势线段（一段连续的有效数据）。
/// </summary>
public readonly struct AeegTrendSegment
{
    /// <summary>
    /// 线段起始索引（在点数组中）。
    /// </summary>
    public required int StartIndex { get; init; }

    /// <summary>
    /// 线段点数。
    /// </summary>
    public required int PointCount { get; init; }
}

/// <summary>
/// aEEG 趋势点。
/// </summary>
public readonly struct AeegTrendPoint
{
    /// <summary>
    /// X 坐标（像素）。
    /// </summary>
    public required float X { get; init; }

    /// <summary>
    /// 上边界 Y 坐标（像素）。
    /// </summary>
    public required float MaxY { get; init; }

    /// <summary>
    /// 下边界 Y 坐标（像素）。
    /// </summary>
    public required float MinY { get; init; }
}

/// <summary>
/// aEEG 间隙信息。
/// </summary>
public readonly struct AeegGapInfo
{
    /// <summary>
    /// 间隙起始 X 坐标。
    /// </summary>
    public required float StartX { get; init; }

    /// <summary>
    /// 间隙结束 X 坐标。
    /// </summary>
    public required float EndX { get; init; }
}

/// <summary>
/// aEEG 序列构建结果。
/// </summary>
public sealed class AeegSeriesBuildResult
{
    /// <summary>
    /// 趋势点数组。
    /// </summary>
    public required AeegTrendPoint[] Points { get; init; }

    /// <summary>
    /// 连续线段列表。
    /// </summary>
    public required AeegTrendSegment[] Segments { get; init; }

    /// <summary>
    /// 间隙区域列表。
    /// </summary>
    public required AeegGapInfo[] Gaps { get; init; }
}

/// <summary>
/// aEEG 序列构建器。
/// 将 aEEG min/max 数据转换为可渲染的趋势线段。
/// </summary>
/// <remarks>
/// 依据: DSP_SPEC.md §3, 00_CONSTITUTION.md 铁律2/5
///
/// 间隙处理规则:
/// - 间隙 > 2 秒: 断开趋势线 + 灰色遮罩
///
/// 铁律约束:
/// - 铁律2: 不伪造波形，间隙必须断线
/// - 铁律5: 缺失必须可见
/// - 铁律6: 渲染只做 Draw
///
/// 使用场景:
/// - 此类在渲染帧开始前由预处理线程调用
/// - 构建结果传递给渲染器，渲染器只做 Draw 调用
/// </remarks>
public sealed class AeegSeriesBuilder
{
    // 常量
    private const int AeegOutputRateHz = 1;  // aEEG 输出 1 Hz
    private const long AeegSampleIntervalUs = 1_000_000 / AeegOutputRateHz;  // 1 秒
    private const long MaxGapUs = 2 * AeegSampleIntervalUs;  // 2 秒间隙阈值

    // 工作缓冲区
    private AeegTrendPoint[] _pointBuffer = new AeegTrendPoint[1024];
    private readonly List<AeegTrendSegment> _segments = new(64);
    private readonly List<AeegGapInfo> _gaps = new(32);

    /// <summary>
    /// 最大可容忍间隙时间（微秒）。
    /// </summary>
    public static long MaxTolerableGapUs => MaxGapUs;

    /// <summary>
    /// 构建 aEEG 趋势序列。
    /// </summary>
    /// <param name="minValues">下边界值数组 (μV)。</param>
    /// <param name="maxValues">上边界值数组 (μV)。</param>
    /// <param name="timestamps">时间戳数组 (μs)。</param>
    /// <param name="qualityFlags">质量标志数组。</param>
    /// <param name="mapper">半对数映射器。</param>
    /// <param name="renderAreaTop">渲染区域顶部 Y 坐标。</param>
    /// <param name="timestampToX">时间戳到 X 坐标的转换函数。</param>
    /// <param name="visibleStartUs">可见范围起始时间（微秒）。</param>
    /// <param name="visibleEndUs">可见范围结束时间（微秒）。</param>
    /// <returns>序列构建结果。</returns>
    public AeegSeriesBuildResult Build(
        ReadOnlySpan<float> minValues,
        ReadOnlySpan<float> maxValues,
        ReadOnlySpan<long> timestamps,
        ReadOnlySpan<byte> qualityFlags,
        AeegSemiLogMapper mapper,
        float renderAreaTop,
        Func<long, float> timestampToX,
        long visibleStartUs,
        long visibleEndUs)
    {
        // 清空缓冲区
        _segments.Clear();
        _gaps.Clear();

        int count = Math.Min(minValues.Length, Math.Min(maxValues.Length, timestamps.Length));

        if (count == 0)
        {
            return new AeegSeriesBuildResult
            {
                Points = [],
                Segments = [],
                Gaps = []
            };
        }

        // 确保缓冲区足够大
        EnsureCapacity(count);

        int pointCount = 0;
        int segmentStart = 0;
        bool inSegment = false;

        float? lastX = null;
        long lastTimestampUs = 0;
        bool lastWasValid = false;

        for (int i = 0; i < count; i++)
        {
            float minUv = minValues[i];
            float maxUv = maxValues[i];
            long timestampUs = timestamps[i];

            // 检查是否在可见范围内
            if (timestampUs < visibleStartUs || timestampUs > visibleEndUs)
            {
                // 如果正在段中，结束当前段
                if (inSegment && pointCount > segmentStart)
                {
                    _segments.Add(new AeegTrendSegment
                    {
                        StartIndex = segmentStart,
                        PointCount = pointCount - segmentStart
                    });
                    inSegment = false;
                }
                continue;
            }

            // 获取质量标志
            QualityFlag quality = i < qualityFlags.Length
                ? (QualityFlag)qualityFlags[i]
                : QualityFlag.Normal;

            // 检查有效性
            bool isValid = !float.IsNaN(minUv) && !float.IsNaN(maxUv) &&
                           (quality & (QualityFlag.Missing | QualityFlag.LeadOff)) == 0;

            // 计算坐标
            float x = timestampToX(timestampUs);
            double mappedMinY = mapper.MapVoltageToY(minUv);
            double mappedMaxY = mapper.MapVoltageToY(maxUv);

            if (!isValid || double.IsNaN(mappedMinY) || double.IsNaN(mappedMaxY))
            {
                // 无效点，结束当前段
                if (inSegment && pointCount > segmentStart)
                {
                    _segments.Add(new AeegTrendSegment
                    {
                        StartIndex = segmentStart,
                        PointCount = pointCount - segmentStart
                    });

                    // 记录间隙
                    if (lastX.HasValue)
                    {
                        _gaps.Add(new AeegGapInfo
                        {
                            StartX = lastX.Value,
                            EndX = x
                        });
                    }
                }
                inSegment = false;
                lastWasValid = false;
                continue;
            }

            float minY = renderAreaTop + (float)mappedMinY;
            float maxY = renderAreaTop + (float)mappedMaxY;

            // 检查时间间隙
            bool hasGap = lastWasValid && (timestampUs - lastTimestampUs) > MaxGapUs;

            if (hasGap)
            {
                // 间隙 > 2 秒，断开线段 (铁律2)
                if (pointCount > segmentStart)
                {
                    _segments.Add(new AeegTrendSegment
                    {
                        StartIndex = segmentStart,
                        PointCount = pointCount - segmentStart
                    });
                }

                // 记录间隙
                if (lastX.HasValue)
                {
                    _gaps.Add(new AeegGapInfo
                    {
                        StartX = lastX.Value,
                        EndX = x
                    });
                }

                // 开始新段
                segmentStart = pointCount;
                inSegment = false;
            }

            // 开始新段（如果需要）
            if (!inSegment)
            {
                segmentStart = pointCount;
                inSegment = true;
            }

            // 添加点
            _pointBuffer[pointCount] = new AeegTrendPoint
            {
                X = x,
                MinY = minY,
                MaxY = maxY
            };

            pointCount++;
            lastX = x;
            lastTimestampUs = timestampUs;
            lastWasValid = true;
        }

        // 结束最后一段
        if (inSegment && pointCount > segmentStart)
        {
            _segments.Add(new AeegTrendSegment
            {
                StartIndex = segmentStart,
                PointCount = pointCount - segmentStart
            });
        }

        // 复制结果
        var points = new AeegTrendPoint[pointCount];
        Array.Copy(_pointBuffer, points, pointCount);

        return new AeegSeriesBuildResult
        {
            Points = points,
            Segments = [.. _segments],
            Gaps = [.. _gaps]
        };
    }

    /// <summary>
    /// 确保缓冲区容量。
    /// </summary>
    private void EnsureCapacity(int requiredSize)
    {
        if (_pointBuffer.Length < requiredSize)
        {
            int newSize = Math.Max(requiredSize, _pointBuffer.Length * 2);
            _pointBuffer = new AeegTrendPoint[newSize];
        }
    }
}
