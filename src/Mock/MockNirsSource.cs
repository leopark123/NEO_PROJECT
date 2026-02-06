// MockNirsSource.cs
// NIRS 模拟数据源 - 用于无硬件测试

using Neo.Core.Interfaces;
using Neo.Core.Models;

namespace Neo.Mock;

/// <summary>
/// NIRS 模拟数据源。
/// </summary>
/// <remarks>
/// 依据: ICD_NIRS_RS232_Protocol_Fields.md
///
/// 用途:
/// - 无 Nonin X-100M 硬件时验证 NIRS 功能
/// - 系统集成测试
/// - UI 界面测试
///
/// 模拟规格:
/// - 采样率: 1 Hz（符合 Nonin X-100M）
/// - 通道数: 6 (4 物理 + 2 虚拟)
/// - rSO2 范围: 60-90%（正常生理范围）
/// - 可模拟探头断开（"---" 标记）
///
/// 线程模型:
/// - 内部定时器线程生成数据
/// - 时间戳使用注入的 Host 时间基准
/// </remarks>
public sealed class MockNirsSource : ITimeSeriesSource<NirsSample>, IDisposable
{
    private readonly System.Threading.Timer _timer;
    private readonly Func<long> _getTimestampUs;
    private readonly Random _random = new();
    private readonly MockNirsConfig _config;
    private bool _isRunning;
    private bool _disposed;

    // 采样参数（符合 Nonin X-100M）
    private const int SampleRateHz = 1;
    private const int ChannelCountValue = 6;
    private const double SampleIntervalMs = 1000.0; // 1 Hz = 1000 ms

    /// <inheritdoc/>
    public int SampleRate => SampleRateHz;

    /// <inheritdoc/>
    public int ChannelCount => ChannelCountValue;

    /// <inheritdoc/>
    public event Action<NirsSample>? SampleReceived;

    /// <summary>
    /// 创建 NIRS 模拟数据源。
    /// </summary>
    /// <param name="timestampProvider">
    /// 时间戳提供者（微秒），确保与 Host 时间基准统一。
    /// </param>
    /// <param name="config">可选配置，不提供则使用默认值</param>
    public MockNirsSource(Func<long> timestampProvider, MockNirsConfig? config = null)
    {
        _getTimestampUs = timestampProvider ?? throw new ArgumentNullException(nameof(timestampProvider));
        _config = config ?? new MockNirsConfig();
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
    /// 定时器回调，生成模拟 NIRS 样本。
    /// </summary>
    /// <remarks>
    /// 数据生成算法:
    /// - 基准值：BaseRso2 (默认 75%)
    /// - 变化范围：±10%（正弦波模拟生理波动）
    /// - 添加噪声：±2%
    /// - 探头断开模拟：随机失效概率
    /// </remarks>
    private void OnTimerCallback(object? state)
    {
        if (!_isRunning)
            return;

        // 使用注入的时间戳提供者（与 Host 时间基准统一）
        long timestampUs = _getTimestampUs();
        double timeSeconds = timestampUs / 1_000_000.0;

        // 生成 4 个物理通道的 rSO2 值
        double ch1 = GenerateRso2Value(timeSeconds, _config.Ch1Factor, _config.Ch1FailureProbability);
        double ch2 = GenerateRso2Value(timeSeconds, _config.Ch2Factor, _config.Ch2FailureProbability);
        double ch3 = GenerateRso2Value(timeSeconds, _config.Ch3Factor, _config.Ch3FailureProbability);
        double ch4 = GenerateRso2Value(timeSeconds, _config.Ch4Factor, _config.Ch4FailureProbability);

        // ValidMask: bit0-bit3 对应 Ch1-Ch4
        byte validMask = 0;
        if (ch1 >= 0) validMask |= 0x01; // Ch1 有效
        if (ch2 >= 0) validMask |= 0x02; // Ch2 有效
        if (ch3 >= 0) validMask |= 0x04; // Ch3 有效
        if (ch4 >= 0) validMask |= 0x08; // Ch4 有效
        // bit4 和 bit5 保持为 0 (Ch5-Ch6 虚拟通道始终无效)

        // 无效通道值设为 0
        ch1 = ch1 < 0 ? 0 : ch1;
        ch2 = ch2 < 0 ? 0 : ch2;
        ch3 = ch3 < 0 ? 0 : ch3;
        ch4 = ch4 < 0 ? 0 : ch4;

        var sample = new NirsSample
        {
            TimestampUs = timestampUs,
            Ch1Percent = ch1,
            Ch2Percent = ch2,
            Ch3Percent = ch3,
            Ch4Percent = ch4,
            Ch5Percent = 0.0, // 虚拟通道
            Ch6Percent = 0.0, // 虚拟通道
            ValidMask = validMask
        };

        SampleReceived?.Invoke(sample);
    }

    /// <summary>
    /// 生成单个通道的 rSO2 值。
    /// </summary>
    /// <param name="timeSeconds">当前时间（秒）</param>
    /// <param name="channelFactor">通道因子（用于差异化各通道）</param>
    /// <param name="failureProbability">探头失效概率（0-1）</param>
    /// <returns>rSO2 值（0-100%），-1 表示无效</returns>
    private double GenerateRso2Value(double timeSeconds, double channelFactor, double failureProbability)
    {
        // 模拟探头断开
        if (_random.NextDouble() < failureProbability)
        {
            return -1; // 无效值标记
        }

        // 正弦波模拟生理波动（周期 60 秒）
        double oscillation = _config.OscillationAmplitude * Math.Sin(2 * Math.PI * timeSeconds / 60.0);

        // 高斯噪声
        double noise = NextGaussian() * _config.NoiseStdDev;

        // 计算 rSO2
        double rso2 = _config.BaseRso2 * channelFactor + oscillation + noise;

        // 限制在 0-100% 范围内
        return Math.Clamp(rso2, 0.0, 100.0);
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

/// <summary>
/// NIRS 模拟数据源配置。
/// </summary>
public class MockNirsConfig
{
    /// <summary>
    /// 基准 rSO2 值（%）。
    /// 默认: 75%（正常生理范围 60-90%）
    /// </summary>
    public double BaseRso2 { get; init; } = 75.0;

    /// <summary>
    /// 生理波动幅度（%）。
    /// 默认: ±10%
    /// </summary>
    public double OscillationAmplitude { get; init; } = 10.0;

    /// <summary>
    /// 噪声标准差（%）。
    /// 默认: 2%
    /// </summary>
    public double NoiseStdDev { get; init; } = 2.0;

    /// <summary>
    /// Ch1 通道因子（相对于基准值）。
    /// 默认: 1.0
    /// </summary>
    public double Ch1Factor { get; init; } = 1.0;

    /// <summary>
    /// Ch2 通道因子。
    /// 默认: 0.95（略低于 Ch1）
    /// </summary>
    public double Ch2Factor { get; init; } = 0.95;

    /// <summary>
    /// Ch3 通道因子。
    /// 默认: 1.05（略高于 Ch1）
    /// </summary>
    public double Ch3Factor { get; init; } = 1.05;

    /// <summary>
    /// Ch4 通道因子。
    /// 默认: 0.98
    /// </summary>
    public double Ch4Factor { get; init; } = 0.98;

    /// <summary>
    /// Ch1 探头失效概率（0-1）。
    /// 默认: 0（始终有效）
    /// </summary>
    public double Ch1FailureProbability { get; init; } = 0.0;

    /// <summary>
    /// Ch2 探头失效概率。
    /// 默认: 0.02（2% 概率断开，模拟不稳定探头）
    /// </summary>
    public double Ch2FailureProbability { get; init; } = 0.02;

    /// <summary>
    /// Ch3 探头失效概率。
    /// 默认: 0
    /// </summary>
    public double Ch3FailureProbability { get; init; } = 0.0;

    /// <summary>
    /// Ch4 探头失效概率。
    /// 默认: 0.01（1% 概率）
    /// </summary>
    public double Ch4FailureProbability { get; init; } = 0.01;
}
