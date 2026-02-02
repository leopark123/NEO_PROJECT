// Rs232ProtocolParser.cs
// EEG RS232 协议解析器 - 来源: clogik_50_ser.cpp, CONSENSUS_BASELINE.md §12.2

namespace Neo.DataSources.Rs232;

using System.Diagnostics;
using Neo.Core.Enums;
using Neo.Core.Models;

/// <summary>
/// EEG RS232 协议解析器。
/// 解析 Cerebralogik 5.0 串口数据包。
/// </summary>
/// <remarks>
/// 依据: clogik_50_ser.cpp (evidence/sources/reference-code/)
///
/// 协议格式:
/// | 字段 | 长度 | 说明 |
/// |------|------|------|
/// | Header | 2 bytes | 0xAA 0x55 |
/// | Data | 36 bytes | 18 个 int16 (大端序) |
/// | CRC | 2 bytes | 累加和 (大端序) |
/// | 总长 | 40 bytes | |
///
/// 数据字段含义 (来自 clogik_50_ser.cpp):
/// - data[0]: EEG CH1 原始值
/// - data[1]: EEG CH2 原始值
/// - data[2]: EEG CH3 原始值
/// - data[3]: aEEG GS Histogram bin (CH1)
/// - data[9]: 配置信息
/// - data[16]: GS 计数器 (0-229, 255=无效)
///
/// 转换公式: μV = raw * 0.076
/// </remarks>
public sealed class EegProtocolParser
{
    // 协议常量 - 来源: clogik_50_ser.cpp
    private const byte Header1 = 0xAA;
    private const byte Header2 = 0x55;
    private const int DataLength = 36;  // 18 个 int16
    private const int PacketLength = 40; // Header(2) + Data(36) + CRC(2)

    /// <summary>
    /// 原始值到 μV 的转换系数。
    /// 来源: clogik_50_ser.cpp L84: ((float)data1[0])*0.076
    /// </summary>
    public const double UvPerLsb = 0.076;

    // 解析状态机
    private ParserPhase _phase = ParserPhase.WaitHeader1;
    private ushort _crc;
    private int _dataIndex;
    private readonly short[] _dataBuffer = new short[18];
    private readonly byte[] _rawBytes = new byte[DataLength];

    // 统计
    private long _packetsReceived;
    private long _crcErrors;

    /// <summary>
    /// 已接收的有效包数量。
    /// </summary>
    public long PacketsReceived => _packetsReceived;

    /// <summary>
    /// CRC 错误包数量。
    /// </summary>
    public long CrcErrors => _crcErrors;

    /// <summary>
    /// 解析完成事件。
    /// 参数: (EegSample sample, short[] rawData)
    /// </summary>
    public event Action<EegSample, short[]>? PacketParsed;

    /// <summary>
    /// CRC 错误事件。
    /// </summary>
    public event Action<long>? CrcErrorOccurred;

    /// <summary>
    /// 处理接收到的字节流。
    /// </summary>
    /// <param name="buffer">接收缓冲区</param>
    /// <param name="length">有效字节数</param>
    /// <param name="timestampUs">主机接收时间戳（微秒）</param>
    public void ProcessBytes(byte[] buffer, int length, long timestampUs)
    {
        for (int i = 0; i < length; i++)
        {
            ProcessByte(buffer[i], timestampUs);
        }
    }

    /// <summary>
    /// 处理单个字节。
    /// 状态机实现 - 来源: clogik_50_ser.cpp L21-94
    /// </summary>
    private void ProcessByte(byte b, long timestampUs)
    {
        switch (_phase)
        {
            case ParserPhase.WaitHeader1:
                // 等待第一个帧头字节 0xAA
                // 来源: clogik_50_ser.cpp L27-33
                if (b == Header1)
                {
                    _phase = ParserPhase.WaitHeader2;
                    _crc = Header1;
                }
                break;

            case ParserPhase.WaitHeader2:
                // 等待第二个帧头字节 0x55
                // 来源: clogik_50_ser.cpp L36-45
                if (b == Header2)
                {
                    _phase = ParserPhase.CollectData;
                    _crc += Header2;
                    _dataIndex = 0;
                }
                else
                {
                    // 不是有效帧头，重置
                    _phase = ParserPhase.WaitHeader1;
                }
                break;

            case ParserPhase.CollectData:
                // 收集 36 字节数据
                // 来源: clogik_50_ser.cpp L47-60
                _rawBytes[_dataIndex] = b;
                _crc += b;

                // 大端序解析: 高字节在前
                // 来源: clogik_50_ser.cpp L49-52
                int wordIndex = _dataIndex / 2;
                if ((_dataIndex % 2) == 0)
                {
                    // 高字节
                    _dataBuffer[wordIndex] = (short)((b & 0xFF) << 8);
                }
                else
                {
                    // 低字节
                    _dataBuffer[wordIndex] |= (short)(b & 0xFF);
                }

                _dataIndex++;
                if (_dataIndex == DataLength)
                {
                    _phase = ParserPhase.WaitCrcHigh;
                }
                break;

            case ParserPhase.WaitCrcHigh:
                // CRC 高字节
                // 来源: clogik_50_ser.cpp L62-66
                _receivedCrcHigh = b;
                _phase = ParserPhase.WaitCrcLow;
                break;

            case ParserPhase.WaitCrcLow:
                // CRC 低字节并验证
                // 来源: clogik_50_ser.cpp L68-91
                ushort receivedCrc = (ushort)((_receivedCrcHigh << 8) | b);

                if (receivedCrc == _crc)
                {
                    // CRC 校验通过
                    _packetsReceived++;
                    OnPacketComplete(timestampUs);
                }
                else
                {
                    // CRC 校验失败
                    _crcErrors++;
                    CrcErrorOccurred?.Invoke(_crcErrors);
                }

                _phase = ParserPhase.WaitHeader1;
                break;
        }
    }

    private byte _receivedCrcHigh;

    /// <summary>
    /// 完整数据包处理。
    /// </summary>
    /// <remarks>
    /// 字段映射证据（来自 spec/DSP_SPEC.md L53-60）:
    /// - data[0] = EEG CH1 raw
    /// - data[1] = EEG CH2 raw
    /// - data[2] = EEG CH3 raw
    /// - data[3] = aEEG GS histogram bin (CH1)
    /// - data[16] = GS counter (0-229有效, 255=忽略)
    ///
    /// CH4 计算公式（来自 spec/ACCEPTANCE_TESTS.md L477）:
    /// - CH4 = CH1 - CH2 (计算通道)
    ///
    /// 转换系数（来自 clogik_50_ser.cpp L84）:
    /// - μV = raw * 0.076
    /// </remarks>
    private void OnPacketComplete(long timestampUs)
    {
        // 解析 EEG 数据
        // 来源: DSP_SPEC.md L54-56
        double ch1Uv = _dataBuffer[0] * UvPerLsb;
        double ch2Uv = _dataBuffer[1] * UvPerLsb;
        double ch3Uv = _dataBuffer[2] * UvPerLsb;

        // CH4 = CH1 - CH2 (计算通道)
        // 来源: ACCEPTANCE_TESTS.md L477: ch4 = ch1 - ch2
        double ch4Uv = ch1Uv - ch2Uv;

        var sample = new EegSample
        {
            TimestampUs = timestampUs,
            Ch1Uv = ch1Uv,
            Ch2Uv = ch2Uv,
            Ch3Uv = ch3Uv,
            Ch4Uv = ch4Uv,
            QualityFlags = QualityFlag.Normal
        };

        // 复制原始数据供外部使用（如 GS 直方图）
        short[] rawDataCopy = new short[18];
        Array.Copy(_dataBuffer, rawDataCopy, 18);

        PacketParsed?.Invoke(sample, rawDataCopy);
    }

    /// <summary>
    /// 重置解析器状态。
    /// </summary>
    public void Reset()
    {
        _phase = ParserPhase.WaitHeader1;
        _crc = 0;
        _dataIndex = 0;
    }

    /// <summary>
    /// 解析器状态机阶段。
    /// </summary>
    private enum ParserPhase
    {
        WaitHeader1,
        WaitHeader2,
        CollectData,
        WaitCrcHigh,
        WaitCrcLow
    }
}

/// <summary>
/// NIRS 协议解析器（预留接口）。
/// </summary>
/// <remarks>
/// 依据: CONSENSUS_BASELINE.md §12.3
///
/// ⚠️ ADR-013 约束：NIRS 阈值/单位禁止软件推断
///
/// 当前状态：协议格式未确认（TBD）
/// 需要设备文档提供：
/// - 帧格式
/// - 数据字段定义
/// - CRC 算法
/// </remarks>
public sealed class NirsProtocolParser
{
    /// <summary>
    /// 解析完成事件。
    /// </summary>
    public event Action<NirsSample>? PacketParsed;

    /// <summary>
    /// 处理接收到的字节流。
    /// </summary>
    /// <remarks>
    /// ⚠️ 未实现：等待 NIRS 设备协议文档
    /// </remarks>
    public void ProcessBytes(byte[] buffer, int length, long timestampUs)
    {
        // NIRS 协议格式 TBD - 等待设备文档
        // 依据 ADR-013: 禁止软件推断
        throw new NotImplementedException(
            "NIRS protocol parser not implemented. " +
            "Waiting for device protocol documentation. " +
            "See ADR-013: NIRS thresholds/units must not be inferred.");
    }

    /// <summary>
    /// 重置解析器状态。
    /// </summary>
    public void Reset()
    {
        // 预留
    }
}
