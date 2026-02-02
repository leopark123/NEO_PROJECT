// AeegProcessor.cs
// aEEG 处理器 - 来源: DSP_SPEC.md §3

using Neo.Core.Enums;
using Neo.DSP.Filters;

namespace Neo.DSP.AEEG;

/// <summary>
/// aEEG 处理器配置。
/// </summary>
public sealed class AeegProcessorConfig
{
    /// <summary>通道数</summary>
    public int ChannelCount { get; init; } = 4;

    /// <summary>采样率 (Hz)</summary>
    public int SampleRate { get; init; } = 160;

    /// <summary>
    /// 默认配置。
    /// </summary>
    public static AeegProcessorConfig Default => new();
}

/// <summary>
/// 单通道 aEEG 处理状态。
/// </summary>
internal sealed class AeegChannelState
{
    public AeegBandpassFilter Bandpass { get; } = new();
    public AeegEnvelopeCalculator Envelope { get; } = new();

    /// <summary>上一个样本时间戳（用于 gap 检测）</summary>
    public long LastTimestampUs { get; set; } = -1;

    /// <summary>已处理样本数</summary>
    public long SamplesProcessed { get; set; }

    public void Reset()
    {
        Bandpass.Reset();
        Envelope.Reset();
        LastTimestampUs = -1;
        SamplesProcessed = 0;
    }
}

/// <summary>
/// aEEG 处理器输出（含质量标志）。
/// </summary>
public readonly struct AeegProcessorOutput
{
    /// <summary>aEEG 输出（min/max 对）</summary>
    public AeegOutput AeegOutput { get; init; }

    /// <summary>通道索引</summary>
    public int ChannelIndex { get; init; }

    /// <summary>质量标志</summary>
    public QualityFlag Quality { get; init; }
}

/// <summary>
/// aEEG 处理器。
/// </summary>
/// <remarks>
/// 依据: DSP_SPEC.md §3
///
/// 处理链:
/// Filtered EEG (160 Hz)
///     ↓
/// Bandpass Filter (2-15 Hz)
///     ↓
/// Half-Wave Rectification (y = |x|)
///     ↓
/// Peak Detection (0.5秒窗口内最大值)
///     ↓
/// Smoothing (15秒移动平均)
///     ↓
/// Min/Max Extraction (每秒输出上下边界)
///     ↓
/// aEEG Output (1 Hz)
///
/// 关键约束:
/// - 每通道独立处理状态
/// - 时间戳从输入继承
/// - Gap 期间不累积统计
/// - 冷启动期间标记 QualityFlag.Transient
///
/// ⚠️ 医学约束 (DSP_SPEC.md §3.0):
/// - aEEG ≠ RMS（禁止 RMS 替代）
/// - 处理流程严格遵循医学定义
///
/// 铁律4: 所有计算使用 double 精度
/// 铁律5: 质量问题必须标记
/// </remarks>
public sealed class AeegProcessor : IDisposable
{
    private readonly AeegProcessorConfig _config;
    private readonly AeegChannelState[] _channelStates;
    private readonly long _expectedSampleIntervalUs;
    private readonly long _maxGapUs;
    private bool _disposed;

    /// <summary>
    /// 处理器配置。
    /// </summary>
    public AeegProcessorConfig Config => _config;

    /// <summary>
    /// 通道数。
    /// </summary>
    public int ChannelCount => _config.ChannelCount;

    /// <summary>
    /// 预热样本数。
    /// </summary>
    /// <remarks>
    /// 依据: DSP_SPEC.md §7
    /// 取 Bandpass + Envelope 预热最大值
    /// </remarks>
    public int WarmupSamples { get; }

    /// <summary>
    /// 创建 aEEG 处理器。
    /// </summary>
    /// <param name="config">配置（null 使用默认配置）</param>
    public AeegProcessor(AeegProcessorConfig? config = null)
    {
        _config = config ?? AeegProcessorConfig.Default;

        // 预热样本数 = Bandpass 预热 + Envelope 预热
        // Bandpass: 240 samples (1.5s)
        // Envelope: 2400 samples (15s)
        WarmupSamples = AeegBandpassFilter.WarmupSamples + AeegEnvelopeCalculator.WarmupSamples;

        // 计算期望样本间隔
        _expectedSampleIntervalUs = 1_000_000 / _config.SampleRate;  // 160Hz = 6250μs

        // Gap 阈值：超过 4 个样本间隔视为 gap
        // 依据: DSP_SPEC.md §6.1
        _maxGapUs = _expectedSampleIntervalUs * 5;  // >4 样本 = gap

        // 初始化每通道状态
        _channelStates = new AeegChannelState[_config.ChannelCount];
        for (int ch = 0; ch < _config.ChannelCount; ch++)
        {
            _channelStates[ch] = new AeegChannelState();
        }
    }

    /// <summary>
    /// 处理单个滤波后的样本。
    /// </summary>
    /// <param name="channelIndex">通道索引 (0-3)</param>
    /// <param name="filteredValue">滤波后的 EEG 值 (μV)</param>
    /// <param name="timestampUs">时间戳 (μs)</param>
    /// <param name="inputQuality">输入质量标志（从 EegFilterChain 继承）</param>
    /// <param name="output">如果有输出则返回 aEEG 输出</param>
    /// <returns>是否产生了新的输出</returns>
    /// <remarks>
    /// 处理链: Bandpass → Rectify → Envelope → Output
    ///
    /// Gap 处理: Gap 期间重置状态，不累积统计
    /// 质量标志: 继承输入质量，冷启动期间添加 Transient
    /// </remarks>
    public bool ProcessSample(
        int channelIndex,
        double filteredValue,
        long timestampUs,
        QualityFlag inputQuality,
        out AeegProcessorOutput output)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (channelIndex < 0 || channelIndex >= _config.ChannelCount)
            throw new ArgumentOutOfRangeException(nameof(channelIndex));

        output = default;

        var state = _channelStates[channelIndex];
        var quality = inputQuality;

        // Gap 检测
        if (state.LastTimestampUs >= 0)
        {
            long delta = timestampUs - state.LastTimestampUs;
            if (delta > _maxGapUs)
            {
                // Gap 后重置状态，不累积统计
                state.Reset();
                quality |= QualityFlag.Missing;
            }
        }
        state.LastTimestampUs = timestampUs;
        state.SamplesProcessed++;

        // 1. 带通滤波 (2-15 Hz)
        double bandpassOutput = state.Bandpass.Process(filteredValue);

        // 2. 半波整流
        double rectifiedValue = AeegRectifier.Rectify(bandpassOutput);

        // 3. 包络计算（峰值检测 + 平滑 + Min/Max）
        if (state.Envelope.ProcessSample(rectifiedValue, timestampUs, out var aeegOutput))
        {
            // 检查预热状态
            if (!state.Envelope.IsWarmedUp)
            {
                quality |= QualityFlag.Transient;
            }

            output = new AeegProcessorOutput
            {
                AeegOutput = aeegOutput,
                ChannelIndex = channelIndex,
                Quality = quality
            };

            return true;
        }

        return false;
    }

    /// <summary>
    /// 处理来自 EegFilterChain 的 FilteredSample。
    /// </summary>
    /// <param name="channelIndex">通道索引</param>
    /// <param name="sample">FilteredSample from EegFilterChain</param>
    /// <param name="output">如果有输出则返回 aEEG 输出</param>
    /// <returns>是否产生了新的输出</returns>
    public bool ProcessFilteredSample(
        int channelIndex,
        FilteredSample sample,
        out AeegProcessorOutput output)
    {
        return ProcessSample(
            channelIndex,
            sample.Value,
            sample.TimestampUs,
            sample.Quality,
            out output);
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

        return _channelStates[channelIndex].Envelope.IsWarmedUp;
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
    /// 重置指定通道的处理状态。
    /// </summary>
    /// <param name="channelIndex">通道索引</param>
    public void ResetChannel(int channelIndex)
    {
        if (channelIndex < 0 || channelIndex >= _config.ChannelCount)
            throw new ArgumentOutOfRangeException(nameof(channelIndex));

        _channelStates[channelIndex].Reset();
    }

    /// <summary>
    /// 重置所有通道的处理状态。
    /// </summary>
    public void ResetAll()
    {
        for (int ch = 0; ch < _config.ChannelCount; ch++)
        {
            _channelStates[ch].Reset();
        }
    }

    /// <summary>
    /// 释放资源。
    /// </summary>
    public void Dispose()
    {
        _disposed = true;
    }
}
