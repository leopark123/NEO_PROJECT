// NirsIntegrationShell.cs
// NIRS 集成壳 - S3-01 + S3-00 完整实现
//
// 依据: PROJECT_STATE.md S3-00 已完成 (2026-02-06)

using Neo.Core.Enums;
using Neo.Core.Interfaces;
using Neo.Core.Models;

namespace Neo.NIRS;

/// <summary>
/// NIRS 集成壳的状态。
/// </summary>
public enum NirsShellStatus
{
    /// <summary>
    /// 模块已就绪。
    /// </summary>
    Ready,

    /// <summary>
    /// 模块运行中。
    /// </summary>
    Running,

    /// <summary>
    /// 模块已停止。
    /// </summary>
    Stopped
}

/// <summary>
/// NIRS 集成壳。
/// 包装 NIRS 数据源并提供质量标志映射。
/// </summary>
/// <remarks>
/// S3-00 实现完成后更新:
/// - 接受 ITimeSeriesSource&lt;NirsSample&gt; (Rs232NirsSource 或 MockNirsSource)
/// - 质量标志映射（ValidMask → QualityFlag）
/// - 事件转发
/// - 完整生命周期管理
///
/// 质量标志映射（ICD §9.2）:
/// - ValidMask bit=1: QualityFlag.Normal
/// - ValidMask bit=0: QualityFlag.DEVICE_NOT_SUPPORTED (探头断开/信号无效)
/// - Ch5-Ch6 虚拟通道: QualityFlag.DEVICE_NOT_SUPPORTED
/// </remarks>
public sealed class NirsIntegrationShell : IDisposable
{
    private readonly ITimeSeriesSource<NirsSample>? _dataSource;
    private bool _disposed;

    /// <summary>
    /// 当前模块状态。
    /// </summary>
    public NirsShellStatus Status { get; private set; } = NirsShellStatus.Ready;

    /// <summary>
    /// 模块是否可用。
    /// </summary>
    public bool IsAvailable => Status == NirsShellStatus.Ready || Status == NirsShellStatus.Running;

    /// <summary>
    /// 阻塞原因描述（兼容性属性，S3-00 已完成）。
    /// </summary>
    public string BlockReason => _dataSource == null
        ? "NIRS data source not configured."
        : "NIRS module is ready.";

    /// <summary>
    /// NIRS 样本到达事件（含质量标志映射）。
    /// </summary>
    public event Action<NirsSample, QualityFlag[]>? SampleReceived;

    /// <summary>
    /// CRC 错误事件。
    /// </summary>
    public event Action<long>? CrcErrorOccurred;

    /// <summary>
    /// 串口错误事件。
    /// </summary>
    public event Action<Exception>? SerialErrorOccurred;

    /// <summary>
    /// 创建 NIRS 集成壳。
    /// </summary>
    /// <param name="dataSource">可选的 NIRS 数据源（Rs232NirsSource 或 MockNirsSource）</param>
    public NirsIntegrationShell(ITimeSeriesSource<NirsSample>? dataSource = null)
    {
        _dataSource = dataSource;

        if (_dataSource != null)
        {
            _dataSource.SampleReceived += OnDataSourceSampleReceived;

            // 如果是 Rs232NirsSource，订阅额外事件
            if (_dataSource is Neo.DataSources.Rs232.Rs232NirsSource rs232Source)
            {
                rs232Source.CrcErrorOccurred += OnCrcError;
                rs232Source.SerialErrorOccurred += OnSerialError;
            }
        }
    }

    /// <summary>
    /// 启动 NIRS 数据采集。
    /// </summary>
    public void Start()
    {
        if (_dataSource == null)
        {
            System.Diagnostics.Trace.TraceWarning(
                "[NIRS] No data source configured, NIRS module will not run.");
            Status = NirsShellStatus.Stopped;
            return;
        }

        try
        {
            _dataSource.Start();
            Status = NirsShellStatus.Running;
            System.Diagnostics.Trace.TraceInformation(
                "[NIRS] NIRS Integration Shell started successfully.");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Trace.TraceError(
                $"[NIRS] Failed to start: {ex.Message}");
            Status = NirsShellStatus.Stopped;
            throw;
        }
    }

    /// <summary>
    /// 停止 NIRS 数据采集。
    /// </summary>
    public void Stop()
    {
        _dataSource?.Stop();
        Status = NirsShellStatus.Stopped;
    }

    /// <summary>
    /// 数据源样本到达处理。
    /// 进行质量标志映射并转发。
    /// </summary>
    private void OnDataSourceSampleReceived(NirsSample sample)
    {
        // 质量标志映射（ICD §9.2）
        QualityFlag[] qualityFlags = new QualityFlag[6];

        for (int i = 0; i < 6; i++)
        {
            int bitMask = 1 << i;
            if ((sample.ValidMask & bitMask) != 0)
            {
                // 通道有效
                qualityFlags[i] = QualityFlag.Normal;
            }
            else
            {
                // 通道无效（探头断开/信号无效/虚拟通道）
                // 来源: ICD §4.2.4 L155-156 + §6.2 L234-235
                qualityFlags[i] = QualityFlag.LeadOff;
            }
        }

        // 转发事件
        SampleReceived?.Invoke(sample, qualityFlags);
    }

    private void OnCrcError(long errorCount)
    {
        CrcErrorOccurred?.Invoke(errorCount);
    }

    private void OnSerialError(Exception ex)
    {
        SerialErrorOccurred?.Invoke(ex);
    }

    /// <summary>
    /// 创建一个表示阻塞状态的 NIRS 样本（兼容旧代码）。
    /// </summary>
    /// <param name="timestampUs">主机时间戳（微秒），仅用于排序。</param>
    [Obsolete("S3-00 已完成，建议使用实际数据源")]
    public static NirsSample CreateBlockedSample(long timestampUs)
    {
        return new NirsSample
        {
            TimestampUs = timestampUs,
            Ch1Percent = double.NaN,
            Ch2Percent = double.NaN,
            Ch3Percent = double.NaN,
            Ch4Percent = double.NaN,
            Ch5Percent = double.NaN,
            Ch6Percent = double.NaN,
            ValidMask = 0
        };
    }

    /// <summary>
    /// 获取阻塞状态下的质量标志（兼容旧代码）。
    /// </summary>
    [Obsolete("S3-00 已完成，建议使用实际质量标志映射")]
    public static QualityFlag BlockedQualityFlags =>
        QualityFlag.Undocumented | QualityFlag.BlockedBySpec;

    public void Dispose()
    {
        if (!_disposed)
        {
            Stop();

            if (_dataSource != null)
            {
                _dataSource.SampleReceived -= OnDataSourceSampleReceived;

                if (_dataSource is Neo.DataSources.Rs232.Rs232NirsSource rs232Source)
                {
                    rs232Source.CrcErrorOccurred -= OnCrcError;
                    rs232Source.SerialErrorOccurred -= OnSerialError;
                }

                if (_dataSource is IDisposable disposable)
                {
                    disposable.Dispose();
                }
            }

            _disposed = true;
        }
    }
}
