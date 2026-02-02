// Rs232TimeSeriesSource.cs
// RS232 时间序列数据源 - 来源: ARCHITECTURE.md §13.2, CONSENSUS_BASELINE.md §12.2

namespace Neo.DataSources.Rs232;

using System.Diagnostics;
using System.IO.Ports;
using Neo.Core.Enums;
using Neo.Core.Interfaces;
using Neo.Core.Models;
using Neo.Storage;

/// <summary>
/// RS232 EEG 数据源。
/// 实现 ITimeSeriesSource 接口，从串口读取 EEG 数据。
/// </summary>
/// <remarks>
/// 依据:
/// - ARCHITECTURE.md §13.2 (RS232 Ingest 模块)
/// - CONSENSUS_BASELINE.md §12.2 (EEG RS232 接入)
/// - ADR-012 (时间戳统一主机打点)
///
/// 时间戳策略:
/// - 打点方式: 主机打点（数据到达主机时立即打戳）
/// - 时钟源: Stopwatch (高精度单调时钟)
/// - 单位: 微秒 (μs)
/// - 语义: 样本中心时间
/// </remarks>
public sealed class Rs232EegSource : ITimeSeriesSource<EegSample>, IDisposable
{
    private readonly Rs232Config _config;
    private readonly EegProtocolParser _parser;
    private readonly AuditLog? _auditLog;
    private SerialPort? _serialPort;
    private volatile bool _isRunning;
    private bool _disposed;

    // 高精度时钟 - 来源: ARCHITECTURE.md §13.2 L1000
    private static readonly long TicksPerMicrosecond = Stopwatch.Frequency / 1_000_000;

    /// <summary>
    /// 采样率: 160 Hz。
    /// 来源: CONSENSUS_BASELINE.md §6.1
    /// </summary>
    public int SampleRate => 160;

    /// <summary>
    /// 通道数: 4 (3物理 + 1计算)。
    /// 来源: CONSENSUS_BASELINE.md §6.1
    /// </summary>
    public int ChannelCount => 4;

    /// <summary>
    /// 样本到达事件。
    /// </summary>
    public event Action<EegSample>? SampleReceived;

    /// <summary>
    /// 原始数据包事件（含 GS 直方图数据）。
    /// </summary>
    public event Action<EegSample, short[]>? RawPacketReceived;

    /// <summary>
    /// CRC 错误事件。
    /// </summary>
    public event Action<long>? CrcErrorOccurred;

    /// <summary>
    /// 串口错误事件。
    /// 依据: 异常处理要求"状态上报，不自动修复"
    /// </summary>
    public event Action<Exception>? SerialErrorOccurred;

    /// <summary>
    /// 创建 RS232 EEG 数据源。
    /// </summary>
    /// <param name="config">串口配置</param>
    /// <param name="auditLog">可选审计日志（AT-21: 串口异常审计）</param>
    public Rs232EegSource(Rs232Config config, AuditLog? auditLog = null)
    {
        _config = config ?? throw new ArgumentNullException(nameof(config));
        _auditLog = auditLog;
        _parser = new EegProtocolParser();

        _parser.PacketParsed += OnPacketParsed;
        _parser.CrcErrorOccurred += OnCrcError;
    }

    /// <summary>
    /// 启动数据采集。
    /// </summary>
    public void Start()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (_isRunning)
            return;

        _serialPort = new SerialPort
        {
            PortName = _config.PortName,
            BaudRate = _config.BaudRate,
            DataBits = _config.DataBits,
            StopBits = ConvertStopBits(_config.StopBits),
            Parity = ConvertParity(_config.Parity),
            ReadTimeout = _config.ReadTimeoutMs,
            ReadBufferSize = _config.ReceiveBufferSize
        };

        _serialPort.DataReceived += OnSerialDataReceived;
        _serialPort.Open();
        _isRunning = true;
    }

    /// <summary>
    /// 停止数据采集。
    /// </summary>
    public void Stop()
    {
        if (!_isRunning)
            return;

        _isRunning = false;

        if (_serialPort != null)
        {
            _serialPort.DataReceived -= OnSerialDataReceived;

            if (_serialPort.IsOpen)
            {
                _serialPort.Close();
            }

            _serialPort.Dispose();
            _serialPort = null;
        }

        _parser.Reset();
    }

    /// <summary>
    /// 串口数据接收事件处理。
    /// 主机打点时间戳在此处生成。
    /// </summary>
    /// <remarks>
    /// 来源: ARCHITECTURE.md §13.2 L997-1004
    /// 主机打点：数据到达时立即打戳
    /// </remarks>
    private void OnSerialDataReceived(object sender, SerialDataReceivedEventArgs e)
    {
        if (!_isRunning || _serialPort == null)
            return;

        // 主机打点：数据到达时立即打戳
        // 来源: ARCHITECTURE.md §13.2 L999-1000
        // long timestampUs = Stopwatch.GetTimestamp() * 1_000_000 / Stopwatch.Frequency;
        long timestampUs = GetHostTimestampUs();

        try
        {
            int bytesToRead = _serialPort.BytesToRead;
            if (bytesToRead > 0)
            {
                byte[] buffer = new byte[bytesToRead];
                int bytesRead = _serialPort.Read(buffer, 0, bytesToRead);

                if (bytesRead > 0)
                {
                    _parser.ProcessBytes(buffer, bytesRead, timestampUs);
                }
            }
        }
        catch (Exception ex)
        {
            // 串口读取异常：状态上报，不自动修复
            // 依据: 异常处理要求"状态上报，不自动修复"
            SerialErrorOccurred?.Invoke(ex);

            // AT-21: 审计日志记录串口异常
            try
            {
                _auditLog?.Log("SERIAL_ERROR", null, null, null,
                    $"{{\"port\":\"{_config.PortName}\",\"error\":\"{ex.GetType().Name}\",\"message\":\"{ex.Message.Replace("\"", "\\\"").Replace("\n", " ")}\"}}");
            }
            catch
            {
                // 审计日志写入失败不应影响主逻辑
            }
        }
    }

    /// <summary>
    /// 获取主机时间戳（微秒）。
    /// </summary>
    /// <remarks>
    /// 来源: ADR-012 (时间戳统一主机打点)
    /// 使用 Stopwatch 而非 DateTime，确保单调递增。
    /// </remarks>
    private static long GetHostTimestampUs()
    {
        // 高精度计算，避免溢出
        long ticks = Stopwatch.GetTimestamp();
        return ticks / TicksPerMicrosecond;
    }

    private void OnPacketParsed(EegSample sample, short[] rawData)
    {
        SampleReceived?.Invoke(sample);
        RawPacketReceived?.Invoke(sample, rawData);
    }

    private void OnCrcError(long errorCount)
    {
        CrcErrorOccurred?.Invoke(errorCount);

        // AT-21: 审计日志记录 CRC 错误
        try
        {
            _auditLog?.Log("CRC_ERROR", null, null, null,
                $"{{\"port\":\"{_config.PortName}\",\"totalErrors\":{errorCount}}}");
        }
        catch
        {
            // 审计日志写入失败不应影响主逻辑
        }
    }

    private static System.IO.Ports.StopBits ConvertStopBits(StopBitsOption option)
    {
        return option switch
        {
            StopBitsOption.One => System.IO.Ports.StopBits.One,
            StopBitsOption.OnePointFive => System.IO.Ports.StopBits.OnePointFive,
            StopBitsOption.Two => System.IO.Ports.StopBits.Two,
            _ => System.IO.Ports.StopBits.One
        };
    }

    private static System.IO.Ports.Parity ConvertParity(ParityOption option)
    {
        return option switch
        {
            ParityOption.None => System.IO.Ports.Parity.None,
            ParityOption.Odd => System.IO.Ports.Parity.Odd,
            ParityOption.Even => System.IO.Ports.Parity.Even,
            _ => System.IO.Ports.Parity.None
        };
    }

    /// <summary>
    /// 释放资源。
    /// </summary>
    public void Dispose()
    {
        if (_disposed)
            return;

        Stop();
        _parser.PacketParsed -= OnPacketParsed;
        _parser.CrcErrorOccurred -= OnCrcError;
        _disposed = true;
    }
}

/// <summary>
/// RS232 NIRS 数据源（预留实现）。
/// </summary>
/// <remarks>
/// 依据: CONSENSUS_BASELINE.md §12.3
///
/// ⚠️ ADR-013 约束：NIRS 阈值/单位禁止软件推断
///
/// 当前状态：协议格式未确认（TBD）
/// </remarks>
public sealed class Rs232NirsSource : ITimeSeriesSource<NirsSample>, IDisposable
{
    private readonly Rs232Config _config;
    private readonly NirsProtocolParser _parser;
    private volatile bool _isRunning;
    private bool _disposed;

    /// <summary>
    /// 采样率: 1-4 Hz（典型值取 4）。
    /// 来源: CONSENSUS_BASELINE.md §6.5
    /// </summary>
    public int SampleRate => 4;

    /// <summary>
    /// 通道数: 6。
    /// 来源: CONSENSUS_BASELINE.md §6.5
    /// </summary>
    public int ChannelCount => 6;

    /// <summary>
    /// 样本到达事件。
    /// </summary>
    public event Action<NirsSample>? SampleReceived;

    /// <summary>
    /// 创建 RS232 NIRS 数据源。
    /// </summary>
    public Rs232NirsSource(Rs232Config config)
    {
        _config = config ?? throw new ArgumentNullException(nameof(config));
        _parser = new NirsProtocolParser();
        _parser.PacketParsed += OnPacketParsed;
    }

    /// <summary>
    /// 启动数据采集。
    /// </summary>
    /// <remarks>
    /// ⚠️ 未实现：等待 NIRS 设备协议文档
    /// </remarks>
    public void Start()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        throw new NotImplementedException(
            "NIRS RS232 source not implemented. " +
            "Waiting for device protocol documentation. " +
            "See ADR-013.");
    }

    /// <summary>
    /// 停止数据采集。
    /// </summary>
    public void Stop()
    {
        _isRunning = false;
        _parser.Reset();
    }

    private void OnPacketParsed(NirsSample sample)
    {
        SampleReceived?.Invoke(sample);
    }

    /// <summary>
    /// 释放资源。
    /// </summary>
    public void Dispose()
    {
        if (_disposed)
            return;

        Stop();
        _parser.PacketParsed -= OnPacketParsed;
        _disposed = true;
    }
}
