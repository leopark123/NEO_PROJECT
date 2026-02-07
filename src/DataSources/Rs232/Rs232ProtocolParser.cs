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
/// NIRS 协议解析器（Nonin X-100M）。
/// </summary>
/// <remarks>
/// 依据: evidence/sources/icd/ICD_NIRS_RS232_Protocol_Fields.md
///
/// 协议类型: ASCII 文本协议（Nonin 1 format）
///
/// 协议格式:
/// | 字段组 | 分隔符 | 说明 |
/// |--------|--------|------|
/// | Ch1 v1 v2 v3 v4 alarms | 空格 | 实时通道值 + 告警指示器 |
/// | TIMESTAMP=... | \| | ISO 8601 时间戳 |
/// | rSO2=v1,v2,v3,v4 | \| | 区域氧饱和度 (0-100%) |
/// | HbI=... | \| | 血红蛋白指数 |
/// | AUC=... | \| | 曲线下面积 |
/// | 系统状态 | \\ | LCD故障、电池等 |
/// | CKSUM=XXXX | \| | CRC-16 CCITT (XMODEM) |
/// | &lt;CR&gt;&lt;LF&gt; | - | 行终止符 |
///
/// 帧长: 250-350 bytes (可变长)
/// 采样率: 1 Hz (固定)
/// 通道数: 4 物理 (Ch1-Ch4) + 2 虚拟 (Ch5-Ch6)
///
/// CRC 算法:
/// - 类型: CRC-16 CCITT (XMODEM)
/// - 多项式: 0x1021
/// - 初始值: 0x0000
/// - 校验范围: 从 'C' (Ch1) 到 '=' (CKSUM=) 之前
/// - 测试向量: "123456789" → 0x31C3
/// </remarks>
public sealed class NirsProtocolParser
{
    // 协议常量 - 来源: ICD_NIRS_RS232_Protocol_Fields.md §3
    private const int MaxFrameLength = 512; // 最大帧长（含冗余）
    private const byte CR = 0x0D;
    private const byte LF = 0x0A;
    private readonly object _sync = new();

    // 解析状态
    private readonly List<byte> _lineBuffer = new();
    private long _packetsReceived;
    private long _crcErrors;
    private long _parseErrors;

    /// <summary>
    /// 已接收的有效包数量。
    /// </summary>
    public long PacketsReceived => _packetsReceived;

    /// <summary>
    /// CRC 错误包数量。
    /// </summary>
    public long CrcErrors => _crcErrors;

    /// <summary>
    /// 解析错误包数量（格式错误、字段缺失等）。
    /// </summary>
    public long ParseErrors => _parseErrors;

    /// <summary>
    /// 解析完成事件。
    /// 参数: NirsSample
    /// </summary>
    public event Action<NirsSample>? PacketParsed;

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
        lock (_sync)
        {
            for (int i = 0; i < length; i++)
            {
                byte b = buffer[i];

                // 检测行终止符 CR LF
                if (b == LF && _lineBuffer.Count > 0 && _lineBuffer[^1] == CR)
                {
                    // 移除 CR
                    _lineBuffer.RemoveAt(_lineBuffer.Count - 1);

                    // 处理完整行
                    if (_lineBuffer.Count > 0)
                    {
                        ProcessLine(_lineBuffer.ToArray(), timestampUs);
                    }

                    _lineBuffer.Clear();
                }
                else
                {
                    // Ignore leading CR/LF noise between frames.
                    if (_lineBuffer.Count == 0 && (b == CR || b == LF))
                    {
                        continue;
                    }

                    _lineBuffer.Add(b);

                    // 防止缓冲区溢出
                    if (_lineBuffer.Count > MaxFrameLength)
                    {
                        _lineBuffer.Clear();
                    }
                }
            }
        }
    }

    /// <summary>
    /// 处理完整的 ASCII 行。
    /// </summary>
    /// <remarks>
    /// 来源: ICD_NIRS_RS232_Protocol_Fields.md §4
    /// </remarks>
    private void ProcessLine(byte[] lineBytes, long timestampUs)
    {
        try
        {
            string line = System.Text.Encoding.ASCII.GetString(lineBytes);

            // 提取并验证 CRC
            // 格式: "... | CKSUM=XXXX"
            int cksumIndex = line.IndexOf("CKSUM=", StringComparison.Ordinal);
            if (cksumIndex == -1)
            {
                _parseErrors++;
                return; // 无效帧：缺少 CKSUM
            }

            // CRC 计算范围: 从开头到 "CKSUM=" 的 '='
            // 来源: ICD §3.3 L91-93
            string dataForCrc = line.Substring(0, cksumIndex + 6);
            ushort calculatedCrc = CalculateCrc16Ccitt(System.Text.Encoding.ASCII.GetBytes(dataForCrc));

            // 提取接收到的 CRC（4位十六进制）
            if (cksumIndex + 10 > line.Length) // "CKSUM=" + 4 hex digits
            {
                _parseErrors++;
                return;
            }

            string receivedCrcStr = line.Substring(cksumIndex + 6, 4);
            if (!ushort.TryParse(receivedCrcStr, System.Globalization.NumberStyles.HexNumber, null, out ushort receivedCrc))
            {
                _parseErrors++;
                return;
            }

            // 验证 CRC
            if (calculatedCrc != receivedCrc)
            {
                _crcErrors++;
                CrcErrorOccurred?.Invoke(_crcErrors);
                return;
            }

            // CRC 通过，解析字段
            var sample = ParseFields(line, timestampUs);
            if (sample.HasValue)
            {
                _packetsReceived++;
                PacketParsed?.Invoke(sample.Value);
            }
            else
            {
                _parseErrors++;
            }
        }
        catch
        {
            _parseErrors++;
        }
    }

    /// <summary>
    /// 解析 NIRS 数据字段。
    /// </summary>
    /// <remarks>
    /// 来源: ICD_NIRS_RS232_Protocol_Fields.md §4.2
    /// 主要提取 rSO2 字段（格式: "rSO2=v1,v2,v3,v4"）
    ///
    /// 字段映射:
    /// - Ch1-Ch4: 物理通道（从 rSO2 字段提取）
    /// - Ch5-Ch6: 虚拟通道（固定为 0，状态为 DEVICE_NOT_SUPPORTED）
    ///
    /// 无效值处理:
    /// - "---" 表示探头未连接或信号无效
    /// - 映射到 ValidMask 的对应位清零
    /// </remarks>
    private NirsSample? ParseFields(string line, long timestampUs)
    {
        try
        {
            // 提取 rSO2 字段
            // 格式: "rSO2=v1,v2,v3,v4" 或 "rSO2=72,---,68,---"
            int rso2Index = line.IndexOf("rSO2=", StringComparison.Ordinal);
            if (rso2Index == -1)
            {
                return null; // 缺少 rSO2 字段
            }

            // 找到 rSO2 值的结束位置（下一个 | 或行尾）
            int rso2Start = rso2Index + 5; // "rSO2=".Length
            int rso2End = line.IndexOf('|', rso2Start);
            if (rso2End == -1)
            {
                rso2End = line.Length;
            }

            string rso2Values = line.Substring(rso2Start, rso2End - rso2Start).Trim();
            string[] values = rso2Values.Split(',');

            if (values.Length < 4)
            {
                return null; // 格式错误：通道数不足
            }

            // 解析 Ch1-Ch4
            double ch1 = 0, ch2 = 0, ch3 = 0, ch4 = 0;
            byte validMask = 0;

            if (TryParseRso2Value(values[0], out ch1)) validMask |= 0x01; // Ch1
            if (TryParseRso2Value(values[1], out ch2)) validMask |= 0x02; // Ch2
            if (TryParseRso2Value(values[2], out ch3)) validMask |= 0x04; // Ch3
            if (TryParseRso2Value(values[3], out ch4)) validMask |= 0x08; // Ch4

            // Ch5-Ch6 为虚拟通道，固定无效
            // 来源: ICD §6.2 L234-235
            double ch5 = 0;
            double ch6 = 0;
            // validMask bit4 和 bit5 保持为 0 (无效)

            return new NirsSample
            {
                TimestampUs = timestampUs,
                Ch1Percent = ch1,
                Ch2Percent = ch2,
                Ch3Percent = ch3,
                Ch4Percent = ch4,
                Ch5Percent = ch5,
                Ch6Percent = ch6,
                ValidMask = validMask
            };
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// 解析单个 rSO2 值。
    /// </summary>
    /// <param name="valueStr">值字符串（如 "72" 或 "---"）</param>
    /// <param name="value">解析后的值</param>
    /// <returns>true 表示有效值，false 表示无效（"---"）</returns>
    private static bool TryParseRso2Value(string valueStr, out double value)
    {
        valueStr = valueStr.Trim();

        if (valueStr == "---")
        {
            // 无效值标记
            // 来源: ICD §4.2.4 L155-156
            value = 0;
            return false;
        }

        if (double.TryParse(valueStr, out value))
        {
            // 值域检查: 0-100%
            // 来源: ICD §4.2.4 L154
            if (value >= 0 && value <= 100)
            {
                return true;
            }
        }

        value = 0;
        return false;
    }

    /// <summary>
    /// 计算 CRC-16 CCITT (XMODEM)。
    /// </summary>
    /// <remarks>
    /// 来源: ICD_NIRS_RS232_Protocol_Fields.md §3.3
    ///
    /// 算法参数:
    /// - 多项式: 0x1021 (x^16 + x^12 + x^5 + 1)
    /// - 初始值: 0x0000
    /// - XOR输出: 0x0000
    /// - 字节顺序: MSB first (大端序)
    ///
    /// 测试向量:
    /// - 输入: "123456789" (ASCII)
    /// - 输出: 0x31C3
    /// </remarks>
    private static ushort CalculateCrc16Ccitt(byte[] data)
    {
        ushort crc = 0x0000; // 初始值
        const ushort polynomial = 0x1021;

        foreach (byte b in data)
        {
            crc ^= (ushort)(b << 8);

            for (int i = 0; i < 8; i++)
            {
                if ((crc & 0x8000) != 0)
                {
                    crc = (ushort)((crc << 1) ^ polynomial);
                }
                else
                {
                    crc <<= 1;
                }
            }
        }

        return crc;
    }

    /// <summary>
    /// 重置解析器状态。
    /// </summary>
    public void Reset()
    {
        _lineBuffer.Clear();
    }
}
