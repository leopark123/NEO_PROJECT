// PolylineBuilder.cs
// 折线段构建器 - 来源: ADR-005, 00_CONSTITUTION.md 铁律2/5

using System.Numerics;
using Neo.Core.Enums;

namespace Neo.Rendering.EEG;

/// <summary>
/// 折线段（一段连续的有效数据）。
/// </summary>
/// <remarks>
/// 依据 ADR-005:
/// - 间隙 > 4 样本时必须断开折线
/// - 无效样本 (NaN, Missing, LeadOff) 断开折线
/// </remarks>
public readonly struct PolylineSegment
{
    /// <summary>
    /// 线段起始索引（在点数组中）。
    /// </summary>
    public required int StartIndex { get; init; }

    /// <summary>
    /// 线段点数。
    /// </summary>
    public required int PointCount { get; init; }

    /// <summary>
    /// 是否包含饱和点。
    /// </summary>
    public required bool HasSaturation { get; init; }

    /// <summary>
    /// 是否包含插值点。
    /// </summary>
    public required bool HasInterpolation { get; init; }
}

/// <summary>
/// 间隙信息（连续无效数据区域）。
/// </summary>
public readonly struct GapInfo
{
    /// <summary>
    /// 间隙起始 X 坐标。
    /// </summary>
    public required float StartX { get; init; }

    /// <summary>
    /// 间隙结束 X 坐标。
    /// </summary>
    public required float EndX { get; init; }

    /// <summary>
    /// 间隙宽度（像素）。
    /// </summary>
    public float Width => EndX - StartX;
}

/// <summary>
/// 折线构建结果。
/// </summary>
public sealed class PolylineBuildResult
{
    /// <summary>
    /// 所有点坐标（X, Y）。
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
}

/// <summary>
/// 折线构建器。
/// 将 EEG 数据转换为可渲染的折线段，处理间隙和质量标志。
/// </summary>
/// <remarks>
/// 依据: ADR-005, 00_CONSTITUTION.md 铁律2/5
///
/// 间隙处理规则 (ADR-005):
/// - 间隙 ≤ 4 样本 (25ms @ 160Hz): 可选插值，标记 Interpolated
/// - 间隙 > 4 样本: 强制断线 + 灰色遮罩
///
/// 铁律约束:
/// - 铁律2: 不伪造波形，间隙 > 4 样本必须断线
/// - 铁律5: 缺失/饱和必须可见
/// - 铁律6: 渲染只做 Draw
///
/// 使用场景:
/// - 此类在渲染帧开始前由预处理线程调用
/// - 构建结果传递给渲染器，渲染器只做 Draw 调用
/// </remarks>
public sealed class PolylineBuilder
{
    // 常量
    private const int EegSampleRateHz = 160;
    private const long SampleIntervalUs = 1_000_000 / EegSampleRateHz;  // 6250 μs
    private const int MaxGapSamples = 4;  // ADR-005
    private const long MaxGapUs = MaxGapSamples * SampleIntervalUs;  // 25000 μs

    // 工作缓冲区（复用以减少分配）
    private Vector2[] _pointBuffer = new Vector2[4096];
    private readonly List<PolylineSegment> _segments = new(64);
    private readonly List<GapInfo> _gaps = new(32);
    private readonly List<int> _saturationIndices = new(128);

    /// <summary>
    /// 最大可插值间隙样本数。
    /// </summary>
    public static int MaxInterpolatableGapSamples => MaxGapSamples;

    /// <summary>
    /// 最大可插值间隙时间（微秒）。
    /// </summary>
    public static long MaxInterpolatableGapUs => MaxGapUs;

    /// <summary>
    /// 构建折线段。
    /// </summary>
    /// <param name="dataPoints">数据点数组（μV 值）。</param>
    /// <param name="qualityFlags">质量标志数组。</param>
    /// <param name="startTimestampUs">起始时间戳（微秒）。</param>
    /// <param name="sampleIntervalUs">采样间隔（微秒）。</param>
    /// <param name="timestampToX">时间戳到 X 坐标的转换函数。</param>
    /// <param name="uvToY">μV 值到 Y 坐标的转换函数。</param>
    /// <param name="visibleStartUs">可见范围起始时间（微秒）。</param>
    /// <param name="visibleEndUs">可见范围结束时间（微秒）。</param>
    /// <returns>折线构建结果。</returns>
    /// <remarks>
    /// 注意: 此方法应在渲染帧开始前调用，不在渲染线程中。
    /// </remarks>
    public PolylineBuildResult Build(
        ReadOnlySpan<float> dataPoints,
        ReadOnlySpan<byte> qualityFlags,
        long startTimestampUs,
        long sampleIntervalUs,
        Func<long, float> timestampToX,
        Func<double, float> uvToY,
        long visibleStartUs,
        long visibleEndUs)
    {
        // 清空缓冲区
        _segments.Clear();
        _gaps.Clear();
        _saturationIndices.Clear();

        if (dataPoints.Length == 0)
        {
            return new PolylineBuildResult
            {
                Points = [],
                Segments = [],
                Gaps = [],
                SaturationIndices = []
            };
        }

        // 确保缓冲区足够大
        EnsureCapacity(dataPoints.Length);

        int pointCount = 0;
        int segmentStart = 0;
        bool inSegment = false;
        bool segmentHasSaturation = false;
        bool segmentHasInterpolation = false;

        float? lastValidX = null;
        long lastValidTimestampUs = 0;

        for (int i = 0; i < dataPoints.Length; i++)
        {
            float value = dataPoints[i];
            long timestampUs = startTimestampUs + i * sampleIntervalUs;

            // 跳过不在可见范围内的点
            if (timestampUs < visibleStartUs || timestampUs > visibleEndUs)
            {
                // 如果正在段中，结束当前段
                if (inSegment && pointCount > segmentStart)
                {
                    _segments.Add(new PolylineSegment
                    {
                        StartIndex = segmentStart,
                        PointCount = pointCount - segmentStart,
                        HasSaturation = segmentHasSaturation,
                        HasInterpolation = segmentHasInterpolation
                    });
                    inSegment = false;
                }
                continue;
            }

            // 获取质量标志
            QualityFlag quality = i < qualityFlags.Length
                ? (QualityFlag)qualityFlags[i]
                : QualityFlag.Normal;

            // 检查样本有效性
            bool isValid = !float.IsNaN(value) &&
                           (quality & (QualityFlag.Missing | QualityFlag.LeadOff | QualityFlag.Undocumented)) == 0;

            bool isSaturated = (quality & QualityFlag.Saturated) != 0;
            bool isInterpolated = (quality & QualityFlag.Interpolated) != 0;

            if (!isValid)
            {
                // 无效样本，结束当前段
                if (inSegment && pointCount > segmentStart)
                {
                    _segments.Add(new PolylineSegment
                    {
                        StartIndex = segmentStart,
                        PointCount = pointCount - segmentStart,
                        HasSaturation = segmentHasSaturation,
                        HasInterpolation = segmentHasInterpolation
                    });

                    // 记录间隙起始
                    if (lastValidX.HasValue)
                    {
                        float gapStartX = lastValidX.Value;
                        float gapEndX = timestampToX(timestampUs);
                        _gaps.Add(new GapInfo
                        {
                            StartX = gapStartX,
                            EndX = gapEndX
                        });
                    }
                }
                inSegment = false;
                continue;
            }

            // 计算坐标
            float x = timestampToX(timestampUs);
            float y = uvToY(value);

            // 检查时间间隙 (ADR-005)
            bool hasGap = inSegment && (timestampUs - lastValidTimestampUs) > MaxGapUs;

            if (hasGap)
            {
                // 间隙 > 4 样本，断开线段 (铁律2)
                if (pointCount > segmentStart)
                {
                    _segments.Add(new PolylineSegment
                    {
                        StartIndex = segmentStart,
                        PointCount = pointCount - segmentStart,
                        HasSaturation = segmentHasSaturation,
                        HasInterpolation = segmentHasInterpolation
                    });
                }

                // 记录间隙
                if (lastValidX.HasValue)
                {
                    _gaps.Add(new GapInfo
                    {
                        StartX = lastValidX.Value,
                        EndX = x
                    });
                }

                // 开始新段
                segmentStart = pointCount;
                segmentHasSaturation = false;
                segmentHasInterpolation = false;
            }

            // 开始新段（如果需要）
            if (!inSegment)
            {
                segmentStart = pointCount;
                segmentHasSaturation = false;
                segmentHasInterpolation = false;
                inSegment = true;
            }

            // 添加点
            _pointBuffer[pointCount] = new Vector2(x, y);

            // 记录饱和点
            if (isSaturated)
            {
                _saturationIndices.Add(pointCount);
                segmentHasSaturation = true;
            }

            if (isInterpolated)
            {
                segmentHasInterpolation = true;
            }

            pointCount++;
            lastValidX = x;
            lastValidTimestampUs = timestampUs;
        }

        // 结束最后一段
        if (inSegment && pointCount > segmentStart)
        {
            _segments.Add(new PolylineSegment
            {
                StartIndex = segmentStart,
                PointCount = pointCount - segmentStart,
                HasSaturation = segmentHasSaturation,
                HasInterpolation = segmentHasInterpolation
            });
        }

        // 复制结果
        var points = new Vector2[pointCount];
        Array.Copy(_pointBuffer, points, pointCount);

        return new PolylineBuildResult
        {
            Points = points,
            Segments = [.. _segments],
            Gaps = [.. _gaps],
            SaturationIndices = [.. _saturationIndices]
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
            _pointBuffer = new Vector2[newSize];
        }
    }
}
