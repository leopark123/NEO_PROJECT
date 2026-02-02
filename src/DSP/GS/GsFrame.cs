// GsFrame.cs
// GS 直方图帧结构 - 来源: DSP_SPEC.md §3.3

using Neo.Core.Enums;

namespace Neo.DSP.GS;

/// <summary>
/// GS 直方图帧（15 秒统计周期）。
/// </summary>
/// <remarks>
/// 依据: DSP_SPEC.md §3.3
///
/// 冻结规格:
/// - 统计周期: 15 秒
/// - bin 数: 230 (index 0-229)
/// - 每 bin 为计数值，最大饱和 249
/// - 时间戳: 继承上游 aEEG 时间戳 (μs)
///
/// Counter 语义:
/// - 0-228: 累计中
/// - 229: 本帧结束 (flush)
/// - 255: 忽略 (不计入)
/// </remarks>
public sealed class GsFrame
{
    /// <summary>
    /// 统计周期（秒）。
    /// </summary>
    public const double PeriodSeconds = 15.0;

    /// <summary>
    /// 统计周期（毫秒）。
    /// </summary>
    public const long PeriodMs = 15_000;

    /// <summary>
    /// 统计周期（微秒）。
    /// </summary>
    public const long PeriodUs = 15_000_000;

    /// <summary>
    /// bin 数量。
    /// </summary>
    public const int BinCount = GsBinMapper.TotalBins;

    /// <summary>
    /// bin 值最大饱和值。
    /// </summary>
    public const byte MaxBinValue = 249;

    /// <summary>
    /// Counter 特殊值: 周期结束。
    /// </summary>
    public const byte CounterEndOfCycle = 229;

    /// <summary>
    /// Counter 特殊值: 忽略。
    /// </summary>
    public const byte CounterIgnore = 255;

    /// <summary>
    /// 直方图 bin 数组 (230 bins)。
    /// </summary>
    /// <remarks>
    /// 每个 bin 存储该电压区间的样本计数。
    /// 最大值 249（达到即饱和）。
    /// </remarks>
    public byte[] Bins { get; }

    /// <summary>
    /// 帧起始时间戳 (μs)。
    /// </summary>
    public long StartTimestampUs { get; private set; }

    /// <summary>
    /// 帧结束时间戳 (μs)。
    /// </summary>
    public long EndTimestampUs { get; private set; }

    /// <summary>
    /// 通道索引。
    /// </summary>
    public int ChannelIndex { get; private set; }

    /// <summary>
    /// 质量标志。
    /// </summary>
    public QualityFlag Quality { get; private set; }

    /// <summary>
    /// 该帧累计的样本数。
    /// </summary>
    public int SampleCount { get; private set; }

    /// <summary>
    /// 帧是否已完成（flush）。
    /// </summary>
    public bool IsComplete { get; private set; }

    /// <summary>
    /// 创建新的 GS 帧。
    /// </summary>
    public GsFrame()
    {
        Bins = new byte[BinCount];
        Reset();
    }

    /// <summary>
    /// 重置帧状态。
    /// </summary>
    public void Reset()
    {
        Array.Clear(Bins, 0, BinCount);
        StartTimestampUs = 0;
        EndTimestampUs = 0;
        ChannelIndex = -1;
        Quality = QualityFlag.Normal;
        SampleCount = 0;
        IsComplete = false;
    }

    /// <summary>
    /// 初始化新帧。
    /// </summary>
    /// <param name="channelIndex">通道索引</param>
    /// <param name="startTimestampUs">起始时间戳 (μs)</param>
    public void Initialize(int channelIndex, long startTimestampUs)
    {
        Reset();
        ChannelIndex = channelIndex;
        StartTimestampUs = startTimestampUs;
    }

    /// <summary>
    /// 增加 bin 计数。
    /// </summary>
    /// <param name="binIndex">bin 索引 (0-229)</param>
    /// <returns>是否成功增加（未达到饱和）</returns>
    /// <remarks>
    /// 达到最大值 249 后饱和，不再增加。
    /// </remarks>
    public bool IncrementBin(int binIndex)
    {
        if (binIndex < 0 || binIndex >= BinCount)
        {
            return false;
        }

        if (Bins[binIndex] < MaxBinValue)
        {
            Bins[binIndex]++;
            SampleCount++;
            return true;
        }

        // 饱和，计入样本数但不增加 bin 值
        SampleCount++;
        return false;
    }

    /// <summary>
    /// 完成帧（flush）。
    /// </summary>
    /// <param name="endTimestampUs">结束时间戳 (μs)</param>
    /// <param name="quality">质量标志</param>
    public void Complete(long endTimestampUs, QualityFlag quality)
    {
        EndTimestampUs = endTimestampUs;
        Quality = quality;
        IsComplete = true;
    }

    /// <summary>
    /// 创建帧的深拷贝。
    /// </summary>
    /// <returns>新的帧副本</returns>
    public GsFrame Clone()
    {
        var clone = new GsFrame
        {
            StartTimestampUs = this.StartTimestampUs,
            EndTimestampUs = this.EndTimestampUs,
            ChannelIndex = this.ChannelIndex,
            Quality = this.Quality,
            SampleCount = this.SampleCount,
            IsComplete = this.IsComplete
        };
        Array.Copy(this.Bins, clone.Bins, BinCount);
        return clone;
    }

    /// <summary>
    /// 获取帧持续时间 (μs)。
    /// </summary>
    public long DurationUs => EndTimestampUs - StartTimestampUs;

    /// <summary>
    /// 获取帧中心时间戳 (μs)。
    /// </summary>
    /// <remarks>
    /// 依据: CONSENSUS_BASELINE.md §5.3
    /// 时间戳表示窗口中心时间。
    /// </remarks>
    public long CenterTimestampUs => StartTimestampUs + DurationUs / 2;
}
