// MockEegSource.cs
// EEG 模拟数据源 - 来源: TASK-S1-05

using Neo.Core.Enums;
using Neo.Core.Interfaces;
using Neo.Core.Models;

namespace Neo.Mock;

/// <summary>
/// EEG 模拟数据源。
/// </summary>
/// <remarks>
/// 依据: TASK-S1-05 (spec/tasks/TASK-S1-05.md)
///
/// 用途:
/// - 无 RS232 硬件时验证渲染管线
/// - 系统集成测试
///
/// 波形规格 (TASK-S1-05 §4.1):
/// - AlphaFrequency: 10 Hz
/// - AlphaAmplitude: 30 μV
/// - BaseAmplitude: 50 μV
/// - NoiseStdDev: 5 μV
/// - ChannelFactors: [1.0, 0.9, 1.1, 0.95]
///
/// 线程模型:
/// - 内部定时器线程生成数据
/// - 时间戳使用注入的 Host 时间基准
/// </remarks>
public sealed class MockEegSource : ITimeSeriesSource<EegSample>, IDisposable
{
    private readonly System.Threading.Timer _timer;
    private readonly Func<long> _getTimestampUs;
    private readonly Random _random = new();
    private bool _isRunning;
    private bool _disposed;

    // 采样参数
    private const int SampleRateHz = 160;
    private const int ChannelCountValue = 4;
    private const double SampleIntervalMs = 1000.0 / SampleRateHz;

    // 波形参数 (TASK-S1-05 §4.1 MockEegConfig 默认值)
    private const double AlphaFrequency = 10.0;   // Hz (Alpha 波: 8-12Hz)
    private const double AlphaAmplitude = 30.0;   // μV
    private const double BaseAmplitude = 50.0;    // μV (未使用，保留兼容)
    private const double NoiseStdDev = 5.0;       // μV

    // 各通道差异因子 (TASK-S1-05 §4.1)
    private static readonly double[] ChannelFactors = [1.0, 0.9, 1.1, 0.95];

    /// <inheritdoc/>
    public int SampleRate => SampleRateHz;

    /// <inheritdoc/>
    public int ChannelCount => ChannelCountValue;

    /// <inheritdoc/>
    public event Action<EegSample>? SampleReceived;

    /// <summary>
    /// 创建模拟数据源。
    /// </summary>
    /// <param name="timestampProvider">
    /// 时间戳提供者（微秒），确保与 Host 时间基准统一。
    /// 依据: TASK-S1-05 §4.1 "时间戳使用 HostClock"
    /// </param>
    public MockEegSource(Func<long> timestampProvider)
    {
        _getTimestampUs = timestampProvider ?? throw new ArgumentNullException(nameof(timestampProvider));
        _timer = new System.Threading.Timer(OnTimerCallback, null, Timeout.Infinite, Timeout.Infinite);
    }

    /// <inheritdoc/>
    public void Start()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (_isRunning)
            return;

        _isRunning = true;
        _timer.Change(0, (int)SampleIntervalMs);
    }

    /// <inheritdoc/>
    public void Stop()
    {
        _isRunning = false;
        _timer.Change(Timeout.Infinite, Timeout.Infinite);
    }

    /// <summary>
    /// 定时器回调，生成模拟样本。
    /// </summary>
    /// <remarks>
    /// 波形生成算法 (TASK-S1-05 §4.2):
    /// - 基础波形：Alpha 波 (10Hz)
    /// - 添加高斯噪声
    /// - 各通道有差异因子
    /// </remarks>
    private void OnTimerCallback(object? state)
    {
        if (!_isRunning)
            return;

        // 使用注入的时间戳提供者（与 Host 时间基准统一）
        long timestampUs = _getTimestampUs();
        double timeSeconds = timestampUs / 1_000_000.0;

        // Alpha 波 (TASK-S1-05 §4.2)
        double alpha = AlphaAmplitude * Math.Sin(2 * Math.PI * AlphaFrequency * timeSeconds);

        // 高斯噪声
        double noise = NextGaussian() * NoiseStdDev;

        // 生成样本 (TASK-S1-05 §4.2)
        var sample = new EegSample
        {
            TimestampUs = timestampUs,
            Ch1Uv = (alpha + noise) * ChannelFactors[0],
            Ch2Uv = (alpha + noise) * ChannelFactors[1],
            Ch3Uv = (alpha + noise) * ChannelFactors[2],
            Ch4Uv = (alpha + noise) * ChannelFactors[3],
            QualityFlags = QualityFlag.Normal
        };

        SampleReceived?.Invoke(sample);
    }

    /// <summary>
    /// 生成高斯分布随机数（Box-Muller 变换）。
    /// </summary>
    private double NextGaussian()
    {
        double u1 = 1.0 - _random.NextDouble();
        double u2 = 1.0 - _random.NextDouble();
        return Math.Sqrt(-2.0 * Math.Log(u1)) * Math.Sin(2.0 * Math.PI * u2);
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        if (_disposed)
            return;

        Stop();
        _timer.Dispose();
        _disposed = true;
    }
}
