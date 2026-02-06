// NirsWiring.cs
// NIRS 模块装配 - S3-01 + S3-00: 集成壳接线与生命周期
//
// 依据: PROJECT_STATE.md S3-00 已完成 (2026-02-06)

using System.Diagnostics;
using Neo.NIRS;
using Neo.Mock;

namespace Neo.Host;

/// <summary>
/// NIRS 模块装配（接线/DI/生命周期）。
/// </summary>
/// <remarks>
/// S3-00 更新:
/// - 使用 MockNirsSource 提供模拟数据（无硬件时）
/// - 使用 Rs232NirsSource 连接 Nonin X-100M（有硬件时）
/// - 管理完整生命周期
/// </remarks>
public sealed class NirsWiring : IDisposable
{
    private readonly NirsIntegrationShell _nirsShell;
    private readonly MockNirsSource _mockSource;
    private bool _disposed;

    // 高精度时钟 - 与 EEG 保持一致
    private static readonly long TicksPerMicrosecond = Stopwatch.Frequency / 1_000_000;

    public NirsWiring()
    {
        // 使用 MockNirsSource 提供模拟数据（默认配置）
        // 如需使用真实硬件，替换为 Rs232NirsSource
        _mockSource = new MockNirsSource(
            timestampProvider: GetHostTimestampUs,
            config: new MockNirsConfig
            {
                BaseRso2 = 75.0,              // 基准 75%
                OscillationAmplitude = 10.0,  // ±10% 生理波动
                NoiseStdDev = 2.0,            // 2% 噪声
                Ch1Factor = 1.0,
                Ch2Factor = 0.95,
                Ch3Factor = 1.05,
                Ch4Factor = 0.98,
                Ch1FailureProbability = 0.0,
                Ch2FailureProbability = 0.02, // Ch2 2% 概率断开
                Ch3FailureProbability = 0.0,
                Ch4FailureProbability = 0.01  // Ch4 1% 概率断开
            }
        );

        _nirsShell = new NirsIntegrationShell(_mockSource);
    }

    /// <summary>
    /// 获取主机时间戳（微秒）。
    /// </summary>
    private static long GetHostTimestampUs()
    {
        long ticks = Stopwatch.GetTimestamp();
        return ticks / TicksPerMicrosecond;
    }

    /// <summary>
    /// NIRS 集成壳实例。
    /// </summary>
    public NirsIntegrationShell Shell => _nirsShell;

    /// <summary>
    /// NIRS 模块是否可用。
    /// </summary>
    public bool IsNirsAvailable => _nirsShell.IsAvailable;

    /// <summary>
    /// 启动 NIRS 模块。
    /// 当前仅注册阻塞状态。
    /// </summary>
    public void Start()
    {
        _nirsShell.Start();

        System.Diagnostics.Trace.TraceInformation(
            "[NirsWiring] NIRS module registered. Status: {0}",
            _nirsShell.Status);
    }

    /// <summary>
    /// 停止 NIRS 模块。
    /// </summary>
    public void Stop()
    {
        _nirsShell.Stop();
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            _nirsShell.Dispose();
            _disposed = true;
        }
    }
}
