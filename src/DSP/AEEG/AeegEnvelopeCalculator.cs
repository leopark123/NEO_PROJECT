// AeegEnvelopeCalculator.cs
// aEEG 包络计算器 - 来源: DSP_SPEC.md §3.1

namespace Neo.DSP.AEEG;

/// <summary>
/// aEEG 输出数据（每秒一对 min/max）。
/// </summary>
/// <remarks>
/// 依据: DSP_SPEC.md §3.3
/// 输出率: 1 Hz
/// </remarks>
public readonly struct AeegOutput
{
    /// <summary>下边界 (μV)</summary>
    public double MinUv { get; init; }

    /// <summary>上边界 (μV)</summary>
    public double MaxUv { get; init; }

    /// <summary>时间戳 (μs)，对应该秒起始时间</summary>
    public long TimestampUs { get; init; }

    /// <summary>是否有效（已完成预热）</summary>
    public bool IsValid { get; init; }
}

/// <summary>
/// aEEG 包络计算器。
/// </summary>
/// <remarks>
/// 依据: DSP_SPEC.md §3.1
///
/// 处理流程:
/// 1. Peak Detection: 0.5秒窗口内最大值
/// 2. Smoothing: 15秒移动平均
/// 3. Min/Max Extraction: 每秒输出上下边界
///
/// 输出率: 1 Hz (每秒输出一对 min/max 值)
///
/// 铁律4: 所有计算使用 double 精度
/// </remarks>
public sealed class AeegEnvelopeCalculator
{
    /// <summary>采样率 (Hz)</summary>
    private const int SampleRate = 160;

    /// <summary>峰值检测窗口 (秒)</summary>
    private const double PeakWindowSeconds = 0.5;

    /// <summary>平滑窗口 (秒)</summary>
    private const double SmoothingWindowSeconds = 15.0;

    /// <summary>输出周期 (秒)</summary>
    private const double OutputPeriodSeconds = 1.0;

    /// <summary>峰值检测窗口样本数</summary>
    private const int PeakWindowSamples = (int)(SampleRate * PeakWindowSeconds);  // 80

    /// <summary>平滑窗口样本数（基于峰值输出率）</summary>
    private const int SmoothingWindowPeaks = (int)(SmoothingWindowSeconds / PeakWindowSeconds);  // 30

    /// <summary>每秒峰值数</summary>
    private const int PeaksPerSecond = (int)(1.0 / PeakWindowSeconds);  // 2

    // 峰值检测状态
    private readonly double[] _peakBuffer;
    private int _peakBufferIndex;
    private int _peakSamplesInWindow;

    // 平滑状态（存储最近 15 秒的峰值）
    private readonly double[] _smoothingBuffer;
    private int _smoothingBufferIndex;
    private int _smoothingBufferCount;
    private double _smoothingSum;

    // Min/Max 提取状态（每秒输出）
    private double _currentSecondMin;
    private double _currentSecondMax;
    private int _peaksInCurrentSecond;
    private long _currentSecondStartUs;

    // 预热状态
    private long _totalSamplesProcessed;

    /// <summary>
    /// 预热样本数。
    /// </summary>
    /// <remarks>
    /// 依据: DSP_SPEC.md §7
    /// 需要填满 15 秒平滑窗口 = 15 × 160 = 2400 样本
    /// </remarks>
    public static int WarmupSamples => (int)(SmoothingWindowSeconds * SampleRate);  // 2400

    /// <summary>
    /// 预热时间（秒）。
    /// </summary>
    public static double WarmupSeconds => SmoothingWindowSeconds;  // 15

    /// <summary>
    /// 创建 aEEG 包络计算器。
    /// </summary>
    public AeegEnvelopeCalculator()
    {
        _peakBuffer = new double[PeakWindowSamples];
        _smoothingBuffer = new double[SmoothingWindowPeaks];
        Reset();
    }

    /// <summary>
    /// 处理单个整流后的样本。
    /// </summary>
    /// <param name="rectifiedValue">整流后的值 (μV, 非负)</param>
    /// <param name="timestampUs">时间戳 (μs)</param>
    /// <param name="output">如果有输出则返回 aEEG 输出</param>
    /// <returns>是否产生了新的输出</returns>
    /// <remarks>
    /// 处理流程:
    /// 1. 累积样本到峰值检测窗口 (0.5秒)
    /// 2. 提取窗口最大值作为峰值
    /// 3. 对峰值进行 15 秒移动平均
    /// 4. 每秒提取 min/max 作为输出
    /// </remarks>
    public bool ProcessSample(double rectifiedValue, long timestampUs, out AeegOutput output)
    {
        output = default;
        _totalSamplesProcessed++;

        // 初始化当前秒起始时间
        if (_currentSecondStartUs == 0)
        {
            _currentSecondStartUs = timestampUs;
        }

        // 添加到峰值检测窗口
        _peakBuffer[_peakBufferIndex] = rectifiedValue;
        _peakBufferIndex = (_peakBufferIndex + 1) % PeakWindowSamples;
        _peakSamplesInWindow++;

        // 峰值检测窗口满（0.5 秒）
        if (_peakSamplesInWindow >= PeakWindowSamples)
        {
            // 提取窗口最大值
            double peak = ExtractPeak();
            _peakSamplesInWindow = 0;

            // 添加到平滑缓冲区
            double smoothedPeak = AddToSmoothingBuffer(peak);

            // 累积到当前秒的 min/max
            if (_peaksInCurrentSecond == 0)
            {
                _currentSecondMin = smoothedPeak;
                _currentSecondMax = smoothedPeak;
            }
            else
            {
                _currentSecondMin = Math.Min(_currentSecondMin, smoothedPeak);
                _currentSecondMax = Math.Max(_currentSecondMax, smoothedPeak);
            }
            _peaksInCurrentSecond++;

            // 每秒输出（2 个峰值 = 1 秒）
            if (_peaksInCurrentSecond >= PeaksPerSecond)
            {
                bool isValid = _totalSamplesProcessed >= WarmupSamples;

                // 时间戳使用窗口中心（CONSENSUS_BASELINE.md §5.3）
                // 窗口 [T, T+1s] 的时间戳 = T + 0.5s = T + 500,000 μs
                const long HalfSecondUs = 500_000;

                output = new AeegOutput
                {
                    MinUv = _currentSecondMin,
                    MaxUv = _currentSecondMax,
                    TimestampUs = _currentSecondStartUs + HalfSecondUs,
                    IsValid = isValid
                };

                // 重置秒统计
                _peaksInCurrentSecond = 0;
                _currentSecondMin = double.MaxValue;
                _currentSecondMax = double.MinValue;
                _currentSecondStartUs = timestampUs;

                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// 提取峰值检测窗口的最大值。
    /// </summary>
    private double ExtractPeak()
    {
        double max = double.MinValue;
        for (int i = 0; i < PeakWindowSamples; i++)
        {
            if (_peakBuffer[i] > max)
            {
                max = _peakBuffer[i];
            }
        }
        return max;
    }

    /// <summary>
    /// 添加峰值到平滑缓冲区并返回平滑后的值。
    /// </summary>
    private double AddToSmoothingBuffer(double peak)
    {
        // 移动平均：移除旧值，添加新值
        if (_smoothingBufferCount >= SmoothingWindowPeaks)
        {
            _smoothingSum -= _smoothingBuffer[_smoothingBufferIndex];
        }
        else
        {
            _smoothingBufferCount++;
        }

        _smoothingBuffer[_smoothingBufferIndex] = peak;
        _smoothingSum += peak;
        _smoothingBufferIndex = (_smoothingBufferIndex + 1) % SmoothingWindowPeaks;

        // 返回移动平均值
        return _smoothingSum / _smoothingBufferCount;
    }

    /// <summary>
    /// 重置计算器状态。
    /// </summary>
    public void Reset()
    {
        Array.Clear(_peakBuffer, 0, _peakBuffer.Length);
        _peakBufferIndex = 0;
        _peakSamplesInWindow = 0;

        Array.Clear(_smoothingBuffer, 0, _smoothingBuffer.Length);
        _smoothingBufferIndex = 0;
        _smoothingBufferCount = 0;
        _smoothingSum = 0;

        _currentSecondMin = double.MaxValue;
        _currentSecondMax = double.MinValue;
        _peaksInCurrentSecond = 0;
        _currentSecondStartUs = 0;

        _totalSamplesProcessed = 0;
    }

    /// <summary>
    /// 是否已完成预热。
    /// </summary>
    public bool IsWarmedUp => _totalSamplesProcessed >= WarmupSamples;

    /// <summary>
    /// 已处理样本数。
    /// </summary>
    public long SamplesProcessed => _totalSamplesProcessed;
}
