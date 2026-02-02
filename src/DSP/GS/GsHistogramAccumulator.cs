// GsHistogramAccumulator.cs
// GS 直方图累计器 - 来源: DSP_SPEC.md §3.3

using Neo.Core.Enums;

namespace Neo.DSP.GS;

/// <summary>
/// GS 直方图累计器（单通道）。
/// </summary>
/// <remarks>
/// 依据: DSP_SPEC.md §3.3, §1.4
///
/// 职责:
/// - 接收 aEEG 输出 (1 Hz min/max 对) 和设备 counter
/// - 根据 counter 语义累计到直方图 bin
/// - counter=229 时输出 GsFrame
///
/// Counter 语义（来自设备 data[16]）:
/// - 0-228: 累计中
/// - 229: 本帧结束 (flush)
/// - 255: 忽略（不计入）
///
/// 冻结规格:
/// - 统计周期: 15 秒（由设备 counter 控制）
/// - bin 数: 230
/// - 最大饱和值: 249
///
/// Gap 处理:
/// - Gap 期间不累计
/// - 不生成伪 bin
/// - Gap 标记传递到输出 Quality
///
/// 禁止事项:
/// - ❌ 禁止对 GS 做平滑/插值
/// - ❌ 禁止改变 bin 数量
/// - ❌ 禁止改变 15 秒周期
/// </remarks>
public sealed class GsHistogramAccumulator
{
    /// <summary>
    /// Counter 值: 周期结束。
    /// </summary>
    public const byte CounterEndOfCycle = GsFrame.CounterEndOfCycle;  // 229

    /// <summary>
    /// Counter 值: 忽略。
    /// </summary>
    public const byte CounterIgnore = GsFrame.CounterIgnore;  // 255

    /// <summary>
    /// 通道索引。
    /// </summary>
    public int ChannelIndex { get; }

    // 当前累计帧
    private readonly GsFrame _currentFrame;

    // 累计状态
    private int _samplesInCurrentFrame;
    private long _frameStartTimestampUs;
    private QualityFlag _accumulatedQuality;
    private bool _hasFirstSample;

    /// <summary>
    /// 创建 GS 直方图累计器。
    /// </summary>
    /// <param name="channelIndex">通道索引</param>
    public GsHistogramAccumulator(int channelIndex)
    {
        ChannelIndex = channelIndex;
        _currentFrame = new GsFrame();
        Reset();
    }

    /// <summary>
    /// 重置累计器状态。
    /// </summary>
    public void Reset()
    {
        _currentFrame.Reset();
        _samplesInCurrentFrame = 0;
        _frameStartTimestampUs = 0;
        _accumulatedQuality = QualityFlag.Normal;
        _hasFirstSample = false;
    }

    /// <summary>
    /// 累计 aEEG 样本。
    /// </summary>
    /// <param name="minUv">下边界 (μV)</param>
    /// <param name="maxUv">上边界 (μV)</param>
    /// <param name="timestampUs">时间戳 (μs)</param>
    /// <param name="quality">质量标志（从上游继承）</param>
    /// <param name="counter">设备 counter (data[16]): 0-228=累计, 229=帧结束, 255=忽略</param>
    /// <param name="outputFrame">如果周期结束则返回完成的帧</param>
    /// <returns>是否输出了完成的帧</returns>
    /// <remarks>
    /// Counter 语义（来自设备 data[16]）:
    /// - 0-228: 累计样本到当前帧
    /// - 229: 完成当前帧并输出
    /// - 255: 忽略该样本（不计入任何统计）
    ///
    /// 处理流程:
    /// 1. 检查 counter=255 → 忽略
    /// 2. 将 minUv 和 maxUv 分别映射到 bin
    /// 3. 增加对应 bin 的计数
    /// 4. 检查 counter=229 → 完成帧并输出
    ///
    /// 边界处理:
    /// - 负值忽略（不计入任何 bin）
    /// - &gt;= 200 μV 计入 bin 229
    /// </remarks>
    public bool AccumulateSample(
        double minUv,
        double maxUv,
        long timestampUs,
        QualityFlag quality,
        byte counter,
        out GsFrame? outputFrame)
    {
        outputFrame = null;

        // Counter=255: 忽略该样本
        if (counter == CounterIgnore)
        {
            return false;
        }

        // 初始化第一个样本
        if (!_hasFirstSample)
        {
            _frameStartTimestampUs = timestampUs;
            _currentFrame.Initialize(ChannelIndex, timestampUs);
            _hasFirstSample = true;
        }

        // 累计质量标志
        _accumulatedQuality |= quality;

        // 映射 min 和 max 到 bin 并累计
        int minBin = GsBinMapper.MapToBin(minUv);
        int maxBin = GsBinMapper.MapToBin(maxUv);

        // 只累计有效 bin（-1 表示忽略）
        if (minBin >= 0)
        {
            _currentFrame.IncrementBin(minBin);
        }

        if (maxBin >= 0)
        {
            _currentFrame.IncrementBin(maxBin);
        }

        _samplesInCurrentFrame++;

        // Counter=229: 周期结束，完成帧
        if (counter == CounterEndOfCycle)
        {
            // 完成当前帧
            _currentFrame.Complete(timestampUs, _accumulatedQuality);

            // 输出帧副本
            outputFrame = _currentFrame.Clone();

            // 准备下一帧
            _samplesInCurrentFrame = 0;
            _frameStartTimestampUs = timestampUs;
            _accumulatedQuality = QualityFlag.Normal;
            _currentFrame.Initialize(ChannelIndex, timestampUs);

            return true;
        }

        // Counter=0-228: 继续累计
        return false;
    }

    /// <summary>
    /// 获取当前帧中已累计的样本数。
    /// </summary>
    public int SamplesInCurrentFrame => _samplesInCurrentFrame;

    /// <summary>
    /// 获取当前帧的起始时间戳。
    /// </summary>
    public long CurrentFrameStartUs => _frameStartTimestampUs;

    /// <summary>
    /// 获取当前帧的累计质量标志。
    /// </summary>
    public QualityFlag CurrentQuality => _accumulatedQuality;

    /// <summary>
    /// 是否有未完成的帧数据。
    /// </summary>
    public bool HasPendingData => _samplesInCurrentFrame > 0;

    /// <summary>
    /// 强制输出当前不完整的帧（用于 flush）。
    /// </summary>
    /// <param name="endTimestampUs">结束时间戳</param>
    /// <returns>不完整的帧（如果有数据），否则 null</returns>
    /// <remarks>
    /// 仅用于特殊情况（如监护结束、gap 后重置）。
    /// 正常情况下应等待 15 秒周期自然结束。
    /// </remarks>
    public GsFrame? FlushIncomplete(long endTimestampUs)
    {
        if (_samplesInCurrentFrame == 0)
        {
            return null;
        }

        _currentFrame.Complete(endTimestampUs, _accumulatedQuality | QualityFlag.Transient);
        var frame = _currentFrame.Clone();

        // 重置状态
        Reset();

        return frame;
    }
}
