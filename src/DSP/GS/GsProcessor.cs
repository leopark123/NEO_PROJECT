// GsProcessor.cs
// GS 直方图处理器 - 来源: DSP_SPEC.md §3.3

using Neo.Core.Enums;
using Neo.DSP.AEEG;

namespace Neo.DSP.GS;

/// <summary>
/// GS 处理器配置。
/// </summary>
public sealed class GsProcessorConfig
{
    /// <summary>
    /// 通道数。
    /// </summary>
    public int ChannelCount { get; init; } = 4;

    /// <summary>
    /// 默认配置。
    /// </summary>
    public static GsProcessorConfig Default => new();
}

/// <summary>
/// GS 处理器输出。
/// </summary>
public readonly struct GsProcessorOutput
{
    /// <summary>
    /// GS 帧。
    /// </summary>
    public GsFrame Frame { get; init; }

    /// <summary>
    /// 通道索引。
    /// </summary>
    public int ChannelIndex { get; init; }
}

/// <summary>
/// GS 直方图处理器。
/// </summary>
/// <remarks>
/// 依据: DSP_SPEC.md §3.3, §1.4
///
/// 职责:
/// - 接收 S2-02 的 aEEG 输出 (1 Hz min/max 对) 和设备 counter
/// - 构建 GS 灰度直方图数据结构
/// - counter=229 时输出 GsFrame
///
/// Counter 语义（来自设备 data[16]）:
/// - 0-228: 累计中
/// - 229: 本帧结束 (flush)
/// - 255: 忽略（不计入）
///
/// 这是 aEEG 的"统计表达"，不是信号处理。
///
/// 冻结规格:
/// - 230 bins (index 0-229)
/// - 15 秒统计周期（由设备 counter 控制）
/// - 分段映射: 0-10 μV 线性 (100 bins), 10-200 μV log10 (130 bins)
///
/// 禁止事项:
/// - ❌ 对 GS 做平滑/插值
/// - ❌ 改变 bin 数量
/// - ❌ 改变 15 秒周期
/// - ❌ 对 log/linear 分界点做"优化"
/// - ❌ 根据 UI 需要调整 GS
/// - ❌ 引入任何"视觉增强"
///
/// GS 是事实统计，不是图像算法。
/// </remarks>
public sealed class GsProcessor : IDisposable
{
    private readonly GsProcessorConfig _config;
    private readonly GsHistogramAccumulator[] _accumulators;
    private readonly long[] _lastTimestampUs;
    private readonly long _gapThresholdUs;
    private bool _disposed;

    /// <summary>
    /// 处理器配置。
    /// </summary>
    public GsProcessorConfig Config => _config;

    /// <summary>
    /// 通道数。
    /// </summary>
    public int ChannelCount => _config.ChannelCount;

    /// <summary>
    /// 创建 GS 处理器。
    /// </summary>
    /// <param name="config">配置（null 使用默认配置）</param>
    public GsProcessor(GsProcessorConfig? config = null)
    {
        _config = config ?? GsProcessorConfig.Default;

        // 初始化每通道累计器
        _accumulators = new GsHistogramAccumulator[_config.ChannelCount];
        _lastTimestampUs = new long[_config.ChannelCount];

        for (int ch = 0; ch < _config.ChannelCount; ch++)
        {
            _accumulators[ch] = new GsHistogramAccumulator(ch);
            _lastTimestampUs[ch] = -1;
        }

        // Gap 阈值: aEEG 输出率 1 Hz，允许最大 2 秒间隔
        // 超过此间隔视为 gap，重置累计器
        _gapThresholdUs = 2_000_000;  // 2 秒
    }

    /// <summary>
    /// 处理 aEEG 输出。
    /// </summary>
    /// <param name="aeegOutput">aEEG 输出（来自 AeegProcessor）</param>
    /// <param name="counter">设备 counter (data[16]): 0-228=累计, 229=帧结束, 255=忽略</param>
    /// <param name="gsOutput">如果帧完成则返回 GS 输出</param>
    /// <returns>是否输出了完成的帧</returns>
    public bool ProcessAeegOutput(AeegProcessorOutput aeegOutput, byte counter, out GsProcessorOutput gsOutput)
    {
        return ProcessAeegOutput(
            aeegOutput.ChannelIndex,
            aeegOutput.AeegOutput.MinUv,
            aeegOutput.AeegOutput.MaxUv,
            aeegOutput.AeegOutput.TimestampUs,
            aeegOutput.Quality,
            counter,
            out gsOutput);
    }

    /// <summary>
    /// 处理 aEEG min/max 对。
    /// </summary>
    /// <param name="channelIndex">通道索引</param>
    /// <param name="minUv">下边界 (μV)</param>
    /// <param name="maxUv">上边界 (μV)</param>
    /// <param name="timestampUs">时间戳 (μs)</param>
    /// <param name="quality">质量标志</param>
    /// <param name="counter">设备 counter (data[16]): 0-228=累计, 229=帧结束, 255=忽略</param>
    /// <param name="gsOutput">如果帧完成则返回 GS 输出</param>
    /// <returns>是否输出了完成的帧</returns>
    public bool ProcessAeegOutput(
        int channelIndex,
        double minUv,
        double maxUv,
        long timestampUs,
        QualityFlag quality,
        byte counter,
        out GsProcessorOutput gsOutput)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        gsOutput = default;

        if (channelIndex < 0 || channelIndex >= _config.ChannelCount)
        {
            throw new ArgumentOutOfRangeException(nameof(channelIndex));
        }

        var accumulator = _accumulators[channelIndex];

        // Gap 检测
        if (_lastTimestampUs[channelIndex] >= 0)
        {
            long delta = timestampUs - _lastTimestampUs[channelIndex];
            if (delta > _gapThresholdUs)
            {
                // Gap 后重置累计器
                accumulator.Reset();
                quality |= QualityFlag.Missing;
            }
        }
        _lastTimestampUs[channelIndex] = timestampUs;

        // 累计到直方图（根据 counter 语义）
        if (accumulator.AccumulateSample(minUv, maxUv, timestampUs, quality, counter, out var frame))
        {
            gsOutput = new GsProcessorOutput
            {
                Frame = frame!,
                ChannelIndex = channelIndex
            };
            return true;
        }

        return false;
    }

    /// <summary>
    /// 获取指定通道当前帧中已累计的样本数。
    /// </summary>
    /// <param name="channelIndex">通道索引</param>
    /// <returns>已累计样本数</returns>
    public int GetSamplesInCurrentFrame(int channelIndex)
    {
        if (channelIndex < 0 || channelIndex >= _config.ChannelCount)
        {
            throw new ArgumentOutOfRangeException(nameof(channelIndex));
        }

        return _accumulators[channelIndex].SamplesInCurrentFrame;
    }

    /// <summary>
    /// 检查指定通道是否有未完成的帧数据。
    /// </summary>
    /// <param name="channelIndex">通道索引</param>
    /// <returns>是否有未完成数据</returns>
    public bool HasPendingData(int channelIndex)
    {
        if (channelIndex < 0 || channelIndex >= _config.ChannelCount)
        {
            throw new ArgumentOutOfRangeException(nameof(channelIndex));
        }

        return _accumulators[channelIndex].HasPendingData;
    }

    /// <summary>
    /// 重置指定通道。
    /// </summary>
    /// <param name="channelIndex">通道索引</param>
    public void ResetChannel(int channelIndex)
    {
        if (channelIndex < 0 || channelIndex >= _config.ChannelCount)
        {
            throw new ArgumentOutOfRangeException(nameof(channelIndex));
        }

        _accumulators[channelIndex].Reset();
        _lastTimestampUs[channelIndex] = -1;
    }

    /// <summary>
    /// 重置所有通道。
    /// </summary>
    public void ResetAll()
    {
        for (int ch = 0; ch < _config.ChannelCount; ch++)
        {
            _accumulators[ch].Reset();
            _lastTimestampUs[ch] = -1;
        }
    }

    /// <summary>
    /// 强制输出所有通道的不完整帧。
    /// </summary>
    /// <param name="endTimestampUs">结束时间戳</param>
    /// <returns>不完整帧列表</returns>
    /// <remarks>
    /// 仅用于监护结束时的 flush。
    /// 返回的帧会标记 QualityFlag.Transient。
    /// </remarks>
    public List<GsProcessorOutput> FlushAll(long endTimestampUs)
    {
        var outputs = new List<GsProcessorOutput>();

        for (int ch = 0; ch < _config.ChannelCount; ch++)
        {
            var frame = _accumulators[ch].FlushIncomplete(endTimestampUs);
            if (frame != null)
            {
                outputs.Add(new GsProcessorOutput
                {
                    Frame = frame,
                    ChannelIndex = ch
                });
            }
        }

        return outputs;
    }

    /// <summary>
    /// 释放资源。
    /// </summary>
    public void Dispose()
    {
        _disposed = true;
    }
}
