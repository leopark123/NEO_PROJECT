// EegFilterChain.cs
// EEG 滤波链 - 来源: DSP_SPEC.md §2.5

using System.Buffers;
using Neo.Core.Enums;
using Neo.Core.Interfaces;

namespace Neo.DSP.Filters;

/// <summary>
/// EEG 滤波链配置。
/// </summary>
public sealed class EegFilterChainConfig
{
    /// <summary>陷波滤波器频率（null 表示禁用）</summary>
    public NotchFrequency? NotchFrequency { get; init; } = Filters.NotchFrequency.Hz50;

    /// <summary>高通滤波器截止频率</summary>
    public HighPassCutoff HighPassCutoff { get; init; } = HighPassCutoff.Hz0_5;

    /// <summary>低通滤波器截止频率</summary>
    public LowPassCutoff LowPassCutoff { get; init; } = LowPassCutoff.Hz35;

    /// <summary>通道数</summary>
    public int ChannelCount { get; init; } = 4;

    /// <summary>采样率 (Hz)</summary>
    public int SampleRate { get; init; } = 160;

    /// <summary>
    /// 默认配置。
    /// </summary>
    /// <remarks>
    /// 依据: DSP_SPEC.md §2.1 默认值
    /// - Notch: 50 Hz
    /// - HPF: 0.5 Hz
    /// - LPF: 35 Hz
    /// </remarks>
    public static EegFilterChainConfig Default => new();
}

/// <summary>
/// 单通道滤波器状态。
/// </summary>
internal sealed class ChannelFilterState
{
    public NotchFilter? Notch { get; set; }
    public HighPassFilter HighPass { get; set; } = null!;
    public LowPassFilter LowPass { get; set; } = null!;

    /// <summary>已处理样本数（用于预热判断）</summary>
    public long SamplesProcessed { get; set; }

    /// <summary>上一个样本时间戳（用于 gap 检测）</summary>
    public long LastTimestampUs { get; set; } = -1;

    public void Reset()
    {
        Notch?.Reset();
        HighPass.Reset();
        LowPass.Reset();
        SamplesProcessed = 0;
        LastTimestampUs = -1;
    }
}

/// <summary>
/// EEG 滤波链输出。
/// </summary>
public readonly struct FilteredSample
{
    /// <summary>滤波后的值 (μV)</summary>
    public double Value { get; init; }

    /// <summary>时间戳 (μs)，与输入一致</summary>
    public long TimestampUs { get; init; }

    /// <summary>质量标志</summary>
    public QualityFlag Quality { get; init; }
}

/// <summary>
/// EEG 数字滤波链。
/// </summary>
/// <remarks>
/// 依据: DSP_SPEC.md §2.5
///
/// 处理链:
/// Raw EEG (int16) → Notch → High-Pass → Low-Pass → Filtered EEG (double, μV)
///
/// 关键约束:
/// - 每通道独立滤波状态
/// - 输入/输出时间戳完全一致
/// - Gap 期间不更新滤波器状态
/// - 冷启动期间标记 QualityFlag.Transient
///
/// 铁律4: 所有系数与状态使用 double 精度
/// </remarks>
public sealed class EegFilterChain : IFilterChain, IDisposable
{
    private readonly EegFilterChainConfig _config;
    private readonly ChannelFilterState[] _channelStates;
    private readonly int _warmupSamples;
    private readonly long _expectedSampleIntervalUs;
    private readonly long _maxGapUs;
    private bool _disposed;

    /// <summary>
    /// 滤波链配置。
    /// </summary>
    public EegFilterChainConfig Config => _config;

    /// <summary>
    /// 通道数。
    /// </summary>
    public int ChannelCount => _config.ChannelCount;

    /// <summary>
    /// 预热样本数。
    /// </summary>
    public int WarmupSamples => _warmupSamples;

    /// <summary>
    /// 创建 EEG 滤波链。
    /// </summary>
    /// <param name="config">配置（null 使用默认配置）</param>
    public EegFilterChain(EegFilterChainConfig? config = null)
    {
        _config = config ?? EegFilterChainConfig.Default;

        // 计算预热样本数（取最大值，由 HPF 决定）
        // 依据: DSP_SPEC.md §7
        _warmupSamples = HighPassFilter.GetWarmupSamples(_config.HighPassCutoff);

        // 计算期望样本间隔
        _expectedSampleIntervalUs = 1_000_000 / _config.SampleRate;  // 160Hz = 6250μs

        // Gap 阈值：超过 4 个样本间隔视为 gap
        // 依据: DSP_SPEC.md §6.1 (gap_detection.interpolate_max_samples: 4)
        _maxGapUs = _expectedSampleIntervalUs * 5;  // >4 样本 = gap

        // 初始化每通道滤波器
        _channelStates = new ChannelFilterState[_config.ChannelCount];
        for (int ch = 0; ch < _config.ChannelCount; ch++)
        {
            _channelStates[ch] = CreateChannelState();
        }
    }

    /// <summary>
    /// 创建单通道滤波器状态。
    /// </summary>
    private ChannelFilterState CreateChannelState()
    {
        var state = new ChannelFilterState
        {
            HighPass = HighPassFilter.Create(_config.HighPassCutoff),
            LowPass = LowPassFilter.Create(_config.LowPassCutoff)
        };

        if (_config.NotchFrequency.HasValue)
        {
            state.Notch = NotchFilter.Create(_config.NotchFrequency.Value);
        }

        return state;
    }

    /// <summary>
    /// 处理单个样本（实时模式）。
    /// </summary>
    /// <param name="channelIndex">通道索引 (0-3)</param>
    /// <param name="rawValue">原始值 (int16 raw)</param>
    /// <param name="timestampUs">时间戳 (μs)</param>
    /// <param name="scaleFactor">比例因子 (μV/LSB)，默认 0.076</param>
    /// <returns>滤波后的样本</returns>
    /// <remarks>
    /// 处理链: Notch → HPF → LPF
    /// 时间戳保持不变（样本中心时间）
    /// </remarks>
    public FilteredSample ProcessSample(
        int channelIndex,
        short rawValue,
        long timestampUs,
        double scaleFactor = 0.076)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (channelIndex < 0 || channelIndex >= _config.ChannelCount)
            throw new ArgumentOutOfRangeException(nameof(channelIndex));

        var state = _channelStates[channelIndex];
        var quality = QualityFlag.Normal;

        // 检测 gap
        // 依据: DSP_SPEC.md §6.1 - gap 内不更新滤波器状态
        if (state.LastTimestampUs >= 0)
        {
            long delta = timestampUs - state.LastTimestampUs;
            if (delta > _maxGapUs)
            {
                // Gap 后重置滤波器状态
                state.Reset();
                quality |= QualityFlag.Missing;
            }
        }
        state.LastTimestampUs = timestampUs;

        // 转换为 μV
        double valueUv = rawValue * scaleFactor;

        // 滤波处理链: Notch → HPF → LPF
        double filtered = valueUv;

        if (state.Notch != null)
        {
            filtered = state.Notch.Process(filtered);
        }

        filtered = state.HighPass.Process(filtered);
        filtered = state.LowPass.Process(filtered);

        // 更新样本计数
        state.SamplesProcessed++;

        // 检查预热状态
        // 依据: DSP_SPEC.md §7 - 冷启动期间标记 Transient
        if (state.SamplesProcessed < _warmupSamples)
        {
            quality |= QualityFlag.Transient;
        }

        return new FilteredSample
        {
            Value = filtered,
            TimestampUs = timestampUs,  // 时间戳保持不变
            Quality = quality
        };
    }

    /// <summary>
    /// 处理单个样本（已转换为 μV）。
    /// </summary>
    /// <param name="channelIndex">通道索引 (0-3)</param>
    /// <param name="valueUv">输入值 (μV)</param>
    /// <param name="timestampUs">时间戳 (μs)</param>
    /// <returns>滤波后的样本</returns>
    public FilteredSample ProcessSampleUv(
        int channelIndex,
        double valueUv,
        long timestampUs)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (channelIndex < 0 || channelIndex >= _config.ChannelCount)
            throw new ArgumentOutOfRangeException(nameof(channelIndex));

        var state = _channelStates[channelIndex];
        var quality = QualityFlag.Normal;

        // 检测 gap
        if (state.LastTimestampUs >= 0)
        {
            long delta = timestampUs - state.LastTimestampUs;
            if (delta > _maxGapUs)
            {
                state.Reset();
                quality |= QualityFlag.Missing;
            }
        }
        state.LastTimestampUs = timestampUs;

        // 滤波处理链: Notch → HPF → LPF
        double filtered = valueUv;

        if (state.Notch != null)
        {
            filtered = state.Notch.Process(filtered);
        }

        filtered = state.HighPass.Process(filtered);
        filtered = state.LowPass.Process(filtered);

        // 更新样本计数
        state.SamplesProcessed++;

        // 检查预热状态
        if (state.SamplesProcessed < _warmupSamples)
        {
            quality |= QualityFlag.Transient;
        }

        return new FilteredSample
        {
            Value = filtered,
            TimestampUs = timestampUs,
            Quality = quality
        };
    }

    /// <summary>
    /// 检查指定通道是否已完成预热。
    /// </summary>
    /// <param name="channelIndex">通道索引</param>
    /// <returns>是否已预热</returns>
    public bool IsWarmedUp(int channelIndex)
    {
        if (channelIndex < 0 || channelIndex >= _config.ChannelCount)
            throw new ArgumentOutOfRangeException(nameof(channelIndex));

        return _channelStates[channelIndex].SamplesProcessed >= _warmupSamples;
    }

    /// <summary>
    /// 获取指定通道已处理的样本数。
    /// </summary>
    /// <param name="channelIndex">通道索引</param>
    /// <returns>已处理样本数</returns>
    public long GetSamplesProcessed(int channelIndex)
    {
        if (channelIndex < 0 || channelIndex >= _config.ChannelCount)
            throw new ArgumentOutOfRangeException(nameof(channelIndex));

        return _channelStates[channelIndex].SamplesProcessed;
    }

    /// <summary>
    /// 重置指定通道的滤波器状态。
    /// </summary>
    /// <param name="channelIndex">通道索引</param>
    public void ResetChannel(int channelIndex)
    {
        if (channelIndex < 0 || channelIndex >= _config.ChannelCount)
            throw new ArgumentOutOfRangeException(nameof(channelIndex));

        _channelStates[channelIndex].Reset();
    }

    /// <summary>
    /// 重置所有通道的滤波器状态。
    /// </summary>
    public void ResetAll()
    {
        for (int ch = 0; ch < _config.ChannelCount; ch++)
        {
            _channelStates[ch].Reset();
        }
    }

    /// <summary>
    /// Zero-phase 批量滤波（回放模式用）。
    /// 使用临时滤波器实例，不影响实时通道状态。
    /// </summary>
    /// <remarks>
    /// 依据: AT-19 Zero-Phase 滤波
    /// 每个滤波器阶段独立做 filtfilt（前后向 IIR）。
    /// </remarks>
    public void ProcessBlockZeroPhase(int channelIndex, ReadOnlySpan<double> inputUv, Span<double> outputUv)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (channelIndex < 0 || channelIndex >= _config.ChannelCount)
            throw new ArgumentOutOfRangeException(nameof(channelIndex));

        if (inputUv.Length == 0) return;

        var temp = ArrayPool<double>.Shared.Rent(inputUv.Length);
        var buf = temp.AsSpan(0, inputUv.Length);
        inputUv.CopyTo(buf);

        // 依次对每个滤波器做 zero-phase
        if (_config.NotchFrequency.HasValue)
        {
            var notch = NotchFilter.Create(_config.NotchFrequency.Value);
            notch.ProcessZeroPhase(buf, outputUv);
            outputUv.CopyTo(buf);
        }

        var hpf = HighPassFilter.Create(_config.HighPassCutoff);
        hpf.ProcessZeroPhase(buf, outputUv);
        outputUv.CopyTo(buf);

        var lpf = LowPassFilter.Create(_config.LowPassCutoff);
        lpf.ProcessZeroPhase(buf, outputUv);

        ArrayPool<double>.Shared.Return(temp);
    }

    /// <summary>
    /// 释放资源。
    /// </summary>
    public void Dispose()
    {
        _disposed = true;
    }
}
