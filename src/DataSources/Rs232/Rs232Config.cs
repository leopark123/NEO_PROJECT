// Rs232Config.cs
// RS232 串口配置 - 来源: CONSENSUS_BASELINE.md §12.2, clogik_50_ser.cpp

namespace Neo.DataSources.Rs232;

/// <summary>
/// RS232 串口配置。
/// </summary>
/// <remarks>
/// 依据: CONSENSUS_BASELINE.md §12.2
/// | 参数 | 值 |
/// |------|-----|
/// | 波特率 | 115200 bps |
/// | 数据格式 | 8N1 |
/// | 帧头 | 0xAA 0x55 |
/// | 数据长度 | 36字节 (18个int16) |
/// | 校验方式 | 累加和CRC |
/// </remarks>
public sealed class Rs232Config
{
    /// <summary>
    /// 串口名称（如 "COM1"）。
    /// </summary>
    public required string PortName { get; init; }

    /// <summary>
    /// 波特率。
    /// 依据: CONSENSUS_BASELINE §12.2, clogik_50_ser.cpp L120
    /// </summary>
    public int BaudRate { get; init; } = 115200;

    /// <summary>
    /// 数据位。
    /// </summary>
    public int DataBits { get; init; } = 8;

    /// <summary>
    /// 停止位。
    /// </summary>
    public StopBitsOption StopBits { get; init; } = StopBitsOption.One;

    /// <summary>
    /// 校验位。
    /// </summary>
    public ParityOption Parity { get; init; } = ParityOption.None;

    /// <summary>
    /// 读取超时（毫秒）。
    /// </summary>
    public int ReadTimeoutMs { get; init; } = 1000;

    /// <summary>
    /// 接收缓冲区大小。
    /// </summary>
    public int ReceiveBufferSize { get; init; } = 4096;
}

/// <summary>
/// 停止位选项。
/// </summary>
public enum StopBitsOption
{
    One = 1,
    OnePointFive = 2,
    Two = 3
}

/// <summary>
/// 校验位选项。
/// </summary>
public enum ParityOption
{
    None = 0,
    Odd = 1,
    Even = 2
}
