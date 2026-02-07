// Rs232ProtocolParserTests.cs
// EEG RS232 协议解析器单元测试
// 测试场景: CRC校验、半包处理、粘包处理

namespace Neo.Tests.DataSources.Rs232;

using Neo.Core.Enums;
using Neo.Core.Models;
using Neo.DataSources.Rs232;
using Xunit;

/// <summary>
/// EegProtocolParser 单元测试。
/// </summary>
/// <remarks>
/// 测试依据: clogik_50_ser.cpp 协议格式
/// - Header: 0xAA 0x55
/// - Data: 36 bytes (18 x int16, 大端序)
/// - CRC: 累加和 (大端序)
/// - 总长: 40 bytes
/// </remarks>
public class Rs232ProtocolParserTests
{
    /// <summary>
    /// 生成有效的测试数据包。
    /// </summary>
    /// <param name="ch1Value">CH1 原始值 (int16)</param>
    /// <returns>40字节完整数据包</returns>
    private static byte[] CreateValidPacket(short ch1Value = 100)
    {
        byte[] packet = new byte[40];

        // Header: 0xAA 0x55
        packet[0] = 0xAA;
        packet[1] = 0x55;

        // Data[0] = CH1 (大端序)
        packet[2] = (byte)((ch1Value >> 8) & 0xFF);
        packet[3] = (byte)(ch1Value & 0xFF);

        // Data[1..17] = 0 (其余通道)
        // 已经是 0，无需设置

        // 计算 CRC (累加和)
        ushort crc = 0;
        for (int i = 0; i < 38; i++)
        {
            crc += packet[i];
        }

        // CRC (大端序)
        packet[38] = (byte)((crc >> 8) & 0xFF);
        packet[39] = (byte)(crc & 0xFF);

        return packet;
    }

    #region CRC 校验测试

    /// <summary>
    /// 测试: CRC 校验通过时应触发 PacketParsed 事件。
    /// </summary>
    [Fact]
    public void CrcValid_ShouldTriggerPacketParsed()
    {
        // Arrange
        var parser = new EegProtocolParser();
        EegSample? receivedSample = null;
        parser.PacketParsed += (sample, raw) => receivedSample = sample;

        byte[] packet = CreateValidPacket(ch1Value: 100);

        // Act
        parser.ProcessBytes(packet, packet.Length, timestampUs: 1000);

        // Assert
        Assert.NotNull(receivedSample);
        Assert.Equal(1000, receivedSample!.Value.TimestampUs);
        Assert.Equal(100 * 0.076, receivedSample!.Value.Ch1Uv, precision: 5);
        Assert.Equal(1, parser.PacketsReceived);
        Assert.Equal(0, parser.CrcErrors);
    }

    /// <summary>
    /// 测试: CRC 校验失败时应触发 CrcErrorOccurred 事件。
    /// </summary>
    [Fact]
    public void CrcInvalid_ShouldTriggerCrcError()
    {
        // Arrange
        var parser = new EegProtocolParser();
        long? errorCount = null;
        parser.CrcErrorOccurred += count => errorCount = count;

        byte[] packet = CreateValidPacket();
        // 破坏 CRC
        packet[38] = 0xFF;
        packet[39] = 0xFF;

        // Act
        parser.ProcessBytes(packet, packet.Length, timestampUs: 1000);

        // Assert
        Assert.NotNull(errorCount);
        Assert.Equal(1, errorCount);
        Assert.Equal(0, parser.PacketsReceived);
        Assert.Equal(1, parser.CrcErrors);
    }

    #endregion

    #region 半包处理测试

    /// <summary>
    /// 测试: 数据包分两次到达（半包）应正确解析。
    /// </summary>
    [Fact]
    public void HalfPacket_ShouldParseCorrectly()
    {
        // Arrange
        var parser = new EegProtocolParser();
        EegSample? receivedSample = null;
        parser.PacketParsed += (sample, raw) => receivedSample = sample;

        byte[] packet = CreateValidPacket(ch1Value: 200);

        // Act - 分两次发送
        // 第一次: 前20字节
        parser.ProcessBytes(packet, 20, timestampUs: 1000);
        Assert.Null(receivedSample); // 不应触发

        // 第二次: 后20字节
        byte[] secondHalf = new byte[20];
        Array.Copy(packet, 20, secondHalf, 0, 20);
        parser.ProcessBytes(secondHalf, 20, timestampUs: 1001);

        // Assert
        Assert.NotNull(receivedSample);
        Assert.Equal(200 * 0.076, receivedSample!.Value.Ch1Uv, precision: 5);
    }

    /// <summary>
    /// 测试: 单字节逐个到达应正确解析。
    /// </summary>
    [Fact]
    public void ByteByByte_ShouldParseCorrectly()
    {
        // Arrange
        var parser = new EegProtocolParser();
        EegSample? receivedSample = null;
        parser.PacketParsed += (sample, raw) => receivedSample = sample;

        byte[] packet = CreateValidPacket(ch1Value: 50);

        // Act - 逐字节发送
        for (int i = 0; i < packet.Length; i++)
        {
            parser.ProcessBytes(new[] { packet[i] }, 1, timestampUs: 1000 + i);
        }

        // Assert
        Assert.NotNull(receivedSample);
        Assert.Equal(1, parser.PacketsReceived);
    }

    #endregion

    #region 粘包处理测试

    /// <summary>
    /// 测试: 两个完整数据包连续到达（粘包）应分别解析。
    /// </summary>
    [Fact]
    public void StickyPackets_ShouldParseBoth()
    {
        // Arrange
        var parser = new EegProtocolParser();
        var samples = new List<EegSample>();
        parser.PacketParsed += (sample, raw) => samples.Add(sample);

        byte[] packet1 = CreateValidPacket(ch1Value: 100);
        byte[] packet2 = CreateValidPacket(ch1Value: 200);

        // 合并两个包
        byte[] combined = new byte[80];
        Array.Copy(packet1, 0, combined, 0, 40);
        Array.Copy(packet2, 0, combined, 40, 40);

        // Act
        parser.ProcessBytes(combined, combined.Length, timestampUs: 1000);

        // Assert
        Assert.Equal(2, samples.Count);
        Assert.Equal(100 * 0.076, samples[0].Ch1Uv, precision: 5);
        Assert.Equal(200 * 0.076, samples[1].Ch1Uv, precision: 5);
        Assert.Equal(2, parser.PacketsReceived);
    }

    /// <summary>
    /// 测试: 粘包中间有垃圾数据应跳过并继续解析。
    /// </summary>
    [Fact]
    public void StickyPacketsWithGarbage_ShouldSkipAndContinue()
    {
        // Arrange
        var parser = new EegProtocolParser();
        var samples = new List<EegSample>();
        parser.PacketParsed += (sample, raw) => samples.Add(sample);

        byte[] packet1 = CreateValidPacket(ch1Value: 100);
        byte[] garbage = new byte[] { 0x12, 0x34, 0x56 }; // 3字节垃圾
        byte[] packet2 = CreateValidPacket(ch1Value: 300);

        // 合并: packet1 + garbage + packet2
        byte[] combined = new byte[83];
        Array.Copy(packet1, 0, combined, 0, 40);
        Array.Copy(garbage, 0, combined, 40, 3);
        Array.Copy(packet2, 0, combined, 43, 40);

        // Act
        parser.ProcessBytes(combined, combined.Length, timestampUs: 1000);

        // Assert
        Assert.Equal(2, samples.Count);
        Assert.Equal(2, parser.PacketsReceived);
    }

    #endregion

    #region 帧头搜索测试

    /// <summary>
    /// 测试: 帧头不完整时应等待更多数据。
    /// </summary>
    [Fact]
    public void IncompleteHeader_ShouldWait()
    {
        // Arrange
        var parser = new EegProtocolParser();
        EegSample? receivedSample = null;
        parser.PacketParsed += (sample, raw) => receivedSample = sample;

        // Act - 只发送 0xAA
        parser.ProcessBytes(new byte[] { 0xAA }, 1, timestampUs: 1000);

        // Assert
        Assert.Null(receivedSample);
        Assert.Equal(0, parser.PacketsReceived);
    }

    /// <summary>
    /// 测试: 错误的第二字节应重置状态机。
    /// </summary>
    [Fact]
    public void WrongSecondByte_ShouldReset()
    {
        // Arrange
        var parser = new EegProtocolParser();
        EegSample? receivedSample = null;
        parser.PacketParsed += (sample, raw) => receivedSample = sample;

        // Act - 发送 0xAA 0x00 (错误的第二字节)
        parser.ProcessBytes(new byte[] { 0xAA, 0x00 }, 2, timestampUs: 1000);

        // Assert
        Assert.Null(receivedSample);

        // 然后发送正确的包，应该能解析
        byte[] packet = CreateValidPacket();
        parser.ProcessBytes(packet, packet.Length, timestampUs: 1001);
        Assert.NotNull(receivedSample);
    }

    #endregion

    #region 多通道解析测试

    /// <summary>
    /// 生成带多通道数据的测试数据包。
    /// </summary>
    private static byte[] CreateMultiChannelPacket(short ch1, short ch2, short ch3)
    {
        byte[] packet = new byte[40];

        // Header
        packet[0] = 0xAA;
        packet[1] = 0x55;

        // Data[0] = CH1 (大端序)
        packet[2] = (byte)((ch1 >> 8) & 0xFF);
        packet[3] = (byte)(ch1 & 0xFF);

        // Data[1] = CH2 (大端序)
        packet[4] = (byte)((ch2 >> 8) & 0xFF);
        packet[5] = (byte)(ch2 & 0xFF);

        // Data[2] = CH3 (大端序)
        packet[6] = (byte)((ch3 >> 8) & 0xFF);
        packet[7] = (byte)(ch3 & 0xFF);

        // 计算 CRC
        ushort crc = 0;
        for (int i = 0; i < 38; i++)
        {
            crc += packet[i];
        }
        packet[38] = (byte)((crc >> 8) & 0xFF);
        packet[39] = (byte)(crc & 0xFF);

        return packet;
    }

    /// <summary>
    /// 测试: 4 通道数据应正确解析。
    /// 来源: DSP_SPEC.md L54-56, ACCEPTANCE_TESTS.md L477
    /// </summary>
    [Fact]
    public void FourChannels_ShouldParseCorrectly()
    {
        // Arrange
        var parser = new EegProtocolParser();
        EegSample? receivedSample = null;
        parser.PacketParsed += (sample, raw) => receivedSample = sample;

        // CH1=100, CH2=50, CH3=75
        byte[] packet = CreateMultiChannelPacket(ch1: 100, ch2: 50, ch3: 75);

        // Act
        parser.ProcessBytes(packet, packet.Length, timestampUs: 1000);

        // Assert
        Assert.NotNull(receivedSample);

        // CH1, CH2, CH3 直接从 data 解析
        // 来源: DSP_SPEC.md L54-56
        Assert.Equal(100 * 0.076, receivedSample!.Value.Ch1Uv, precision: 5);
        Assert.Equal(50 * 0.076, receivedSample!.Value.Ch2Uv, precision: 5);
        Assert.Equal(75 * 0.076, receivedSample!.Value.Ch3Uv, precision: 5);

        // CH4 = CH1 - CH2 (计算通道)
        // 来源: ACCEPTANCE_TESTS.md L477
        double expectedCh4 = (100 - 50) * 0.076;
        Assert.Equal(expectedCh4, receivedSample!.Value.Ch4Uv, precision: 5);

        // 质量标志应为 Normal
        Assert.Equal(QualityFlag.Normal, receivedSample!.Value.QualityFlags);
    }

    /// <summary>
    /// 测试: CH4 计算公式 (CH1 - CH2)。
    /// 来源: ACCEPTANCE_TESTS.md L477
    /// </summary>
    [Fact]
    public void Ch4Calculation_ShouldBeCorrect()
    {
        // Arrange
        var parser = new EegProtocolParser();
        EegSample? receivedSample = null;
        parser.PacketParsed += (sample, raw) => receivedSample = sample;

        // CH1=200, CH2=80 => CH4 = 200-80 = 120
        byte[] packet = CreateMultiChannelPacket(ch1: 200, ch2: 80, ch3: 0);

        // Act
        parser.ProcessBytes(packet, packet.Length, timestampUs: 1000);

        // Assert
        Assert.NotNull(receivedSample);
        double expectedCh4 = (200 - 80) * 0.076;
        Assert.Equal(expectedCh4, receivedSample!.Value.Ch4Uv, precision: 5);
    }

    #endregion

    #region GS Counter 测试

    /// <summary>
    /// 生成带 GS Counter 的测试数据包。
    /// </summary>
    /// <param name="gsCounter">GS Counter 值 (data[16])</param>
    private static byte[] CreatePacketWithGsCounter(short gsCounter)
    {
        byte[] packet = new byte[40];

        // Header
        packet[0] = 0xAA;
        packet[1] = 0x55;

        // Data[16] = GS Counter (大端序, 偏移 2 + 16*2 = 34)
        packet[34] = (byte)((gsCounter >> 8) & 0xFF);
        packet[35] = (byte)(gsCounter & 0xFF);

        // 计算 CRC
        ushort crc = 0;
        for (int i = 0; i < 38; i++)
        {
            crc += packet[i];
        }
        packet[38] = (byte)((crc >> 8) & 0xFF);
        packet[39] = (byte)(crc & 0xFF);

        return packet;
    }

    /// <summary>
    /// 测试: GS Counter = 255 表示无效。
    /// 来源: clogik_50_ser.cpp L75: if (data1[16] != 255)
    /// </summary>
    [Fact]
    public void GsCounter255_ShouldIndicateInvalid()
    {
        // Arrange
        var parser = new EegProtocolParser();
        short[]? rawData = null;
        parser.PacketParsed += (sample, raw) => rawData = raw;

        byte[] packet = CreatePacketWithGsCounter(gsCounter: 255);

        // Act
        parser.ProcessBytes(packet, packet.Length, timestampUs: 1000);

        // Assert
        Assert.NotNull(rawData);
        Assert.Equal(255, rawData![16]); // GS Counter = 255 (无效标记)
        // 注意: 255 表示 GS 数据无效，下游应跳过此包的 GS 直方图处理
    }

    /// <summary>
    /// 测试: GS Counter = 229 表示一个 GS 周期结束。
    /// 来源: clogik_50_ser.cpp L80: if (data[16]==229) // time to draw aEEG GS
    /// </summary>
    [Fact]
    public void GsCounter229_ShouldIndicateCycleEnd()
    {
        // Arrange
        var parser = new EegProtocolParser();
        short[]? rawData = null;
        parser.PacketParsed += (sample, raw) => rawData = raw;

        byte[] packet = CreatePacketWithGsCounter(gsCounter: 229);

        // Act
        parser.ProcessBytes(packet, packet.Length, timestampUs: 1000);

        // Assert
        Assert.NotNull(rawData);
        Assert.Equal(229, rawData![16]); // GS Counter = 229 (周期结束)
        // 注意: 229 表示 15 秒 GS 周期结束，下游应触发 aEEG 绘制
    }

    /// <summary>
    /// 测试: GS Counter 0-228 为正常值。
    /// 来源: clogik_50_ser.cpp L76: counter 0-229
    /// </summary>
    [Fact]
    public void GsCounterNormal_ShouldBeInRange()
    {
        // Arrange
        var parser = new EegProtocolParser();
        short[]? rawData = null;
        parser.PacketParsed += (sample, raw) => rawData = raw;

        byte[] packet = CreatePacketWithGsCounter(gsCounter: 100);

        // Act
        parser.ProcessBytes(packet, packet.Length, timestampUs: 1000);

        // Assert
        Assert.NotNull(rawData);
        Assert.Equal(100, rawData![16]);
        // GS Counter 在 0-228 范围内表示正常 GS 数据
    }

    #endregion

    #region Reset 测试

    /// <summary>
    /// 测试: Reset 后应重新开始解析。
    /// </summary>
    [Fact]
    public void Reset_ShouldClearState()
    {
        // Arrange
        var parser = new EegProtocolParser();
        byte[] packet = CreateValidPacket();

        // 先发送半个包
        parser.ProcessBytes(packet, 20, timestampUs: 1000);

        // Act - 重置
        parser.Reset();

        // 再发送完整包，应该能解析
        EegSample? receivedSample = null;
        parser.PacketParsed += (sample, raw) => receivedSample = sample;
        parser.ProcessBytes(packet, packet.Length, timestampUs: 1001);

        // Assert
        Assert.NotNull(receivedSample);
    }

    #endregion

    #region 辅助断言类 (简化版)

    private static class Assert
    {
        public static void NotNull(object? obj)
        {
            if (obj == null) throw new Exception("Assert.NotNull failed");
        }

        public static void Null(object? obj)
        {
            if (obj != null) throw new Exception("Assert.Null failed");
        }

        public static void Equal<T>(T expected, T actual)
        {
            if (!EqualityComparer<T>.Default.Equals(expected, actual))
                throw new Exception($"Assert.Equal failed: expected {expected}, actual {actual}");
        }

        public static void Equal(double expected, double actual, int precision)
        {
            double tolerance = Math.Pow(10, -precision);
            if (Math.Abs(expected - actual) > tolerance)
                throw new Exception($"Assert.Equal failed: expected {expected}, actual {actual}");
        }

        public static void True(bool condition)
        {
            if (!condition) throw new Exception("Assert.True failed");
        }

        public static void False(bool condition)
        {
            if (condition) throw new Exception("Assert.False failed");
        }
    }

    #endregion
}

/// <summary>
/// NirsProtocolParser 单元测试。
/// </summary>
/// <remarks>
/// 测试依据: ICD_NIRS_RS232_Protocol_Fields.md
/// - 协议: ASCII 文本（Nonin 1 format）
/// - 分隔符: 空格 | , \
/// - CRC: CRC-16 CCITT (XMODEM)
/// - 帧长: 250-350 bytes (可变)
/// - 采样率: 1 Hz
/// </remarks>
public class NirsProtocolParserTests
{
    /// <summary>
    /// 生成有效的 NIRS 测试帧（ASCII 文本）。
    /// </summary>
    /// <remarks>
    /// 格式参考: ICD §4 示例帧（line 56）
    /// </remarks>
    private static byte[] CreateValidFrame(
        string ch1 = "72",
        string ch2 = "68",
        string ch3 = "70",
        string ch4 = "65")
    {
        // 构造 ASCII 帧
        // 注意：CRC 计算范围从 'C' (Ch1) 到 '=' (CKSUM=)
        string dataWithoutCrc =
            $"Ch1 {ch1} {ch2} {ch3} {ch4} 1 2 3 4 | " +
            "TIMESTAMP=2024-12-17T09:15:42.000+08:00 | " +
            $"rSO2={ch1},{ch2},{ch3},{ch4} | " +
            "HbI=11.5,10.8,11.2,10.5 | " +
            "AUC=0.00,0.00,0.00,0.00 | " +
            "REF=70,70,70,70 | " +
            "HI_LIM=OFF,OFF,OFF,OFF | " +
            "LOW_LIM=50,50,50,50 | " +
            "ALM=0,0,0,0 | " +
            "SIG_QUAL_ALM=0,0,0,0 | " +
            "POD_COMM_ALM=0,0,0,0 | " +
            "SNS_FLT=0,0,0,0 | " +
            "LCD_FLT\\0\\LOW_BATT\\0\\CRIT_BATT\\0\\BATT_FLT\\0\\STK_KEY\\0\\SND_FLT\\0\\SND_ERR\\0\\EXT_MEM_ERR\\0 | " +
            "CKSUM=";

        byte[] dataBytes = System.Text.Encoding.ASCII.GetBytes(dataWithoutCrc);
        ushort crc = CalculateCrc16Ccitt(dataBytes);

        string fullFrame = dataWithoutCrc + crc.ToString("X4") + "\r\n";
        return System.Text.Encoding.ASCII.GetBytes(fullFrame);
    }

    /// <summary>
    /// 计算 CRC-16 CCITT (XMODEM)。
    /// </summary>
    /// <remarks>
    /// 来源: ICD §3.3 L98-112 (C 参考实现)
    /// 测试向量: "123456789" → 0x31C3
    /// </remarks>
    private static ushort CalculateCrc16Ccitt(byte[] data)
    {
        ushort crc = 0x0000;
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

    #region CRC-16 CCITT 测试

    /// <summary>
    /// 测试: CRC-16 CCITT (XMODEM) 测试向量。
    /// </summary>
    /// <remarks>
    /// 来源: ICD §3.3 L93-94
    /// 输入: "123456789" (ASCII)
    /// 期望输出: 0x31C3
    /// </remarks>
    [Fact]
    public void Crc16Ccitt_TestVector_ShouldMatch()
    {
        // Arrange
        byte[] testInput = System.Text.Encoding.ASCII.GetBytes("123456789");

        // Act
        ushort crc = CalculateCrc16Ccitt(testInput);

        // Assert
        Assert.Equal((ushort)0x31C3, crc);
    }

    /// <summary>
    /// 测试: CRC 校验通过时应触发 PacketParsed 事件。
    /// </summary>
    [Fact]
    public void NirsCrcValid_ShouldTriggerPacketParsed()
    {
        // Arrange
        var parser = new NirsProtocolParser();
        NirsSample? receivedSample = null;
        parser.PacketParsed += sample => receivedSample = sample;

        byte[] frame = CreateValidFrame(ch1: "72", ch2: "68", ch3: "70", ch4: "65");

        // Act
        parser.ProcessBytes(frame, frame.Length, timestampUs: 1000);

        // Assert
        Assert.NotNull(receivedSample);
        Assert.Equal(1000, receivedSample!.Value.TimestampUs);
        Assert.Equal(72.0, receivedSample!.Value.Ch1Percent, precision: 1);
        Assert.Equal(68.0, receivedSample!.Value.Ch2Percent, precision: 1);
        Assert.Equal(70.0, receivedSample!.Value.Ch3Percent, precision: 1);
        Assert.Equal(65.0, receivedSample!.Value.Ch4Percent, precision: 1);
        Assert.Equal(1, parser.PacketsReceived);
        Assert.Equal(0, parser.CrcErrors);
    }

    /// <summary>
    /// 测试: CRC 校验失败时应触发 CrcErrorOccurred 事件。
    /// </summary>
    [Fact]
    public void NirsCrcInvalid_ShouldTriggerCrcError()
    {
        // Arrange
        var parser = new NirsProtocolParser();
        long? errorCount = null;
        parser.CrcErrorOccurred += count => errorCount = count;

        // 创建帧并破坏 CRC
        string invalidFrame = "Ch1 72 68 70 65 1 2 3 4 | TIMESTAMP=2024-12-17T09:15:42.000+08:00 | " +
            "rSO2=72,68,70,65 | HbI=11.5,10.8,11.2,10.5 | CKSUM=0000\r\n"; // 错误的 CRC
        byte[] frameBytes = System.Text.Encoding.ASCII.GetBytes(invalidFrame);

        // Act
        parser.ProcessBytes(frameBytes, frameBytes.Length, timestampUs: 1000);

        // Assert
        Assert.NotNull(errorCount);
        Assert.Equal(1, errorCount);
        Assert.Equal(0, parser.PacketsReceived);
        Assert.Equal(1, parser.CrcErrors);
    }

    #endregion

    #region 字段解析测试

    /// <summary>
    /// 测试: rSO2 字段正常值解析。
    /// </summary>
    /// <remarks>
    /// 来源: ICD §4.2.4 L153-157
    /// 值域: 0-100%
    /// </remarks>
    [Fact]
    public void NirsRso2_ValidValues_ShouldParse()
    {
        // Arrange
        var parser = new NirsProtocolParser();
        NirsSample? receivedSample = null;
        parser.PacketParsed += sample => receivedSample = sample;

        byte[] frame = CreateValidFrame(ch1: "85", ch2: "90", ch3: "78", ch4: "82");

        // Act
        parser.ProcessBytes(frame, frame.Length, timestampUs: 2000);

        // Assert
        Assert.NotNull(receivedSample);
        Assert.Equal(85.0, receivedSample!.Value.Ch1Percent, precision: 1);
        Assert.Equal(90.0, receivedSample!.Value.Ch2Percent, precision: 1);
        Assert.Equal(78.0, receivedSample!.Value.Ch3Percent, precision: 1);
        Assert.Equal(82.0, receivedSample!.Value.Ch4Percent, precision: 1);

        // 所有 4 个物理通道有效
        Assert.Equal(0x0F, receivedSample!.Value.ValidMask); // bit0-3 = 1
    }

    /// <summary>
    /// 测试: "---" 无效值标记处理。
    /// </summary>
    /// <remarks>
    /// 来源: ICD §4.2.4 L155-156
    /// "---" 表示探头未连接或信号无效
    /// </remarks>
    [Fact]
    public void NirsRso2_InvalidMarker_ShouldBeInvalid()
    {
        // Arrange
        var parser = new NirsProtocolParser();
        NirsSample? receivedSample = null;
        parser.PacketParsed += sample => receivedSample = sample;

        // Ch2 和 Ch4 无效
        byte[] frame = CreateValidFrame(ch1: "72", ch2: "---", ch3: "68", ch4: "---");

        // Act
        parser.ProcessBytes(frame, frame.Length, timestampUs: 3000);

        // Assert
        Assert.NotNull(receivedSample);
        Assert.Equal(72.0, receivedSample!.Value.Ch1Percent, precision: 1);
        Assert.Equal(0.0, receivedSample!.Value.Ch2Percent, precision: 1); // 无效值为 0
        Assert.Equal(68.0, receivedSample!.Value.Ch3Percent, precision: 1);
        Assert.Equal(0.0, receivedSample!.Value.Ch4Percent, precision: 1); // 无效值为 0

        // ValidMask: bit0=1 (Ch1), bit1=0 (Ch2), bit2=1 (Ch3), bit3=0 (Ch4)
        Assert.Equal(0x05, receivedSample!.Value.ValidMask); // 0b00000101
    }

    /// <summary>
    /// 测试: 虚拟通道 Ch5-Ch6 应始终无效。
    /// </summary>
    /// <remarks>
    /// 来源: ICD §6.2 L234-235
    /// NEO 需要 6 通道，但 Nonin X-100M 只有 4 物理通道
    /// Ch5-Ch6 为虚拟通道，状态为 DEVICE_NOT_SUPPORTED
    /// </remarks>
    [Fact]
    public void NirsVirtualChannels_ShouldBeInvalid()
    {
        // Arrange
        var parser = new NirsProtocolParser();
        NirsSample? receivedSample = null;
        parser.PacketParsed += sample => receivedSample = sample;

        byte[] frame = CreateValidFrame();

        // Act
        parser.ProcessBytes(frame, frame.Length, timestampUs: 4000);

        // Assert
        Assert.NotNull(receivedSample);

        // Ch5-Ch6 值为 0
        Assert.Equal(0.0, receivedSample!.Value.Ch5Percent, precision: 1);
        Assert.Equal(0.0, receivedSample!.Value.Ch6Percent, precision: 1);

        // ValidMask: bit4 和 bit5 应为 0 (无效)
        byte validMask = receivedSample!.Value.ValidMask;
        Assert.Equal(0, validMask & 0x10); // bit4 = 0
        Assert.Equal(0, validMask & 0x20); // bit5 = 0
    }

    #endregion

    #region 帧边界测试

    /// <summary>
    /// 测试: 帧分多次到达应正确解析。
    /// </summary>
    [Fact]
    public void NirsHalfFrame_ShouldParseCorrectly()
    {
        // Arrange
        var parser = new NirsProtocolParser();
        NirsSample? receivedSample = null;
        parser.PacketParsed += sample => receivedSample = sample;

        byte[] frame = CreateValidFrame(ch1: "75", ch2: "80", ch3: "72", ch4: "78");

        // Act - 分两次发送
        int halfLen = frame.Length / 2;
        parser.ProcessBytes(frame, halfLen, timestampUs: 5000);
        Assert.Null(receivedSample); // 第一次不应触发

        byte[] secondHalf = new byte[frame.Length - halfLen];
        Array.Copy(frame, halfLen, secondHalf, 0, secondHalf.Length);
        parser.ProcessBytes(secondHalf, secondHalf.Length, timestampUs: 5001);

        // Assert
        Assert.NotNull(receivedSample);
        Assert.Equal(75.0, receivedSample!.Value.Ch1Percent, precision: 1);
    }

    /// <summary>
    /// 测试: 两帧连续到达（粘包）应分别解析。
    /// </summary>
    [Fact]
    public void NirsStickyFrames_ShouldParseBoth()
    {
        // Arrange
        var parser = new NirsProtocolParser();
        var samples = new List<NirsSample>();
        parser.PacketParsed += sample => samples.Add(sample);

        byte[] frame1 = CreateValidFrame(ch1: "70", ch2: "72", ch3: "68", ch4: "74");
        byte[] frame2 = CreateValidFrame(ch1: "85", ch2: "88", ch3: "82", ch4: "86");

        byte[] combined = new byte[frame1.Length + frame2.Length];
        Array.Copy(frame1, 0, combined, 0, frame1.Length);
        Array.Copy(frame2, 0, combined, frame1.Length, frame2.Length);

        // Act
        parser.ProcessBytes(combined, combined.Length, timestampUs: 6000);

        // Assert
        Assert.Equal(2, samples.Count);
        Assert.Equal(70.0, samples[0].Ch1Percent, precision: 1);
        Assert.Equal(85.0, samples[1].Ch1Percent, precision: 1);
        Assert.Equal(2, parser.PacketsReceived);
    }

    /// <summary>
    /// 测试: 缺少 CKSUM 字段应解析失败。
    /// </summary>
    [Fact]
    public void NirsMissingCksum_ShouldFail()
    {
        // Arrange
        var parser = new NirsProtocolParser();
        NirsSample? receivedSample = null;
        parser.PacketParsed += sample => receivedSample = sample;

        // 缺少 CKSUM 字段
        string invalidFrame = "Ch1 72 68 70 65 1 2 3 4 | rSO2=72,68,70,65\r\n";
        byte[] frameBytes = System.Text.Encoding.ASCII.GetBytes(invalidFrame);

        // Act
        parser.ProcessBytes(frameBytes, frameBytes.Length, timestampUs: 7000);

        // Assert
        Assert.Null(receivedSample);
        Assert.Equal(0, parser.PacketsReceived);
        Assert.True(parser.ParseErrors > 0);
    }

    /// <summary>
    /// 测试: 缺少 rSO2 字段应解析失败。
    /// </summary>
    [Fact]
    public void NirsMissingRso2_ShouldFail()
    {
        // Arrange
        var parser = new NirsProtocolParser();
        NirsSample? receivedSample = null;
        parser.PacketParsed += sample => receivedSample = sample;

        // 计算正确的 CRC 但缺少 rSO2 字段
        string dataWithoutCrc = "Ch1 72 68 70 65 1 2 3 4 | TIMESTAMP=2024-12-17T09:15:42.000+08:00 | CKSUM=";
        byte[] dataBytes = System.Text.Encoding.ASCII.GetBytes(dataWithoutCrc);
        ushort crc = CalculateCrc16Ccitt(dataBytes);
        string fullFrame = dataWithoutCrc + crc.ToString("X4") + "\r\n";
        byte[] frameBytes = System.Text.Encoding.ASCII.GetBytes(fullFrame);

        // Act
        parser.ProcessBytes(frameBytes, frameBytes.Length, timestampUs: 8000);

        // Assert
        Assert.Null(receivedSample);
        Assert.Equal(0, parser.PacketsReceived);
    }

    #endregion

    #region Reset 测试

    /// <summary>
    /// 测试: Reset 后应清除缓冲区状态。
    /// </summary>
    [Fact]
    public void NirsReset_ShouldClearBuffer()
    {
        // Arrange
        var parser = new NirsProtocolParser();
        byte[] frame = CreateValidFrame();

        // 发送半帧
        parser.ProcessBytes(frame, frame.Length / 2, timestampUs: 9000);

        // Act - 重置
        parser.Reset();

        // 重新发送完整帧，应该能解析
        NirsSample? receivedSample = null;
        parser.PacketParsed += sample => receivedSample = sample;
        parser.ProcessBytes(frame, frame.Length, timestampUs: 9001);

        // Assert
        Assert.NotNull(receivedSample);
    }

    #endregion

    #region 边界值测试

    /// <summary>
    /// 测试: rSO2 = 0% 应有效。
    /// </summary>
    [Fact]
    public void NirsRso2_ZeroPercent_ShouldBeValid()
    {
        // Arrange
        var parser = new NirsProtocolParser();
        NirsSample? receivedSample = null;
        parser.PacketParsed += sample => receivedSample = sample;

        byte[] frame = CreateValidFrame(ch1: "0", ch2: "0", ch3: "0", ch4: "0");

        // Act
        parser.ProcessBytes(frame, frame.Length, timestampUs: 10000);

        // Assert
        Assert.NotNull(receivedSample);
        Assert.Equal(0.0, receivedSample!.Value.Ch1Percent, precision: 1);
        Assert.Equal(0x0F, receivedSample!.Value.ValidMask); // 全部有效
    }

    /// <summary>
    /// 测试: rSO2 = 100% 应有效。
    /// </summary>
    [Fact]
    public void NirsRso2_HundredPercent_ShouldBeValid()
    {
        // Arrange
        var parser = new NirsProtocolParser();
        NirsSample? receivedSample = null;
        parser.PacketParsed += sample => receivedSample = sample;

        byte[] frame = CreateValidFrame(ch1: "100", ch2: "100", ch3: "100", ch4: "100");

        // Act
        parser.ProcessBytes(frame, frame.Length, timestampUs: 11000);

        // Assert
        Assert.NotNull(receivedSample);
        Assert.Equal(100.0, receivedSample!.Value.Ch1Percent, precision: 1);
        Assert.Equal(0x0F, receivedSample!.Value.ValidMask);
    }

    /// <summary>
    /// 测试: rSO2 > 100% 应无效。
    /// </summary>
    [Fact]
    public void NirsRso2_OutOfRange_ShouldBeInvalid()
    {
        // Arrange
        var parser = new NirsProtocolParser();
        NirsSample? receivedSample = null;
        parser.PacketParsed += sample => receivedSample = sample;

        byte[] frame = CreateValidFrame(ch1: "150", ch2: "72", ch3: "68", ch4: "70");

        // Act
        parser.ProcessBytes(frame, frame.Length, timestampUs: 12000);

        // Assert
        Assert.NotNull(receivedSample);
        // Ch1 超出范围应标记为无效
        Assert.Equal(0, receivedSample!.Value.ValidMask & 0x01); // bit0 = 0 (Ch1 无效)
    }

    #endregion
}

/// <summary>
/// NIRS 解析器健壮性测试。
/// </summary>
/// <remarks>
/// 测试异常场景和边界条件：
/// - 帧截断和不完整数据
/// - 字段格式错误
/// - 极端值处理
/// - 并发访问安全性
/// </remarks>
public class NirsProtocolParserRobustnessTests
{
    #region 帧截断测试

    /// <summary>
    /// 测试: 只有 CR 没有 LF 应不触发解析。
    /// </summary>
    [Fact]
    public void NirsOnlyCR_ShouldNotParse()
    {
        // Arrange
        var parser = new NirsProtocolParser();
        NirsSample? receivedSample = null;
        parser.PacketParsed += sample => receivedSample = sample;

        // 帧以 CR 结束但没有 LF
        string frame = "Ch1 72 68 70 65 1 2 3 4 | rSO2=72,68,70,65 | CKSUM=0000\r";
        byte[] frameBytes = System.Text.Encoding.ASCII.GetBytes(frame);

        // Act
        parser.ProcessBytes(frameBytes, frameBytes.Length, timestampUs: 1000);

        // Assert
        Assert.Null(receivedSample);
        Assert.Equal(0, parser.PacketsReceived);
    }

    /// <summary>
    /// 测试: 多个连续 CR 应不影响解析。
    /// </summary>
    [Fact]
    public void NirsMultipleCR_ShouldHandleCorrectly()
    {
        // Arrange
        var parser = new NirsProtocolParser();
        var samples = new List<NirsSample>();
        parser.PacketParsed += sample => samples.Add(sample);

        // 创建有效帧
        byte[] validFrame = CreateValidNirsFrame("75", "80", "72", "78");

        // 在帧前插入多个 CR
        byte[] testData = new byte[validFrame.Length + 3];
        testData[0] = 0x0D; // CR
        testData[1] = 0x0D; // CR
        testData[2] = 0x0D; // CR
        Array.Copy(validFrame, 0, testData, 3, validFrame.Length);

        // Act
        parser.ProcessBytes(testData, testData.Length, timestampUs: 2000);

        // Assert
        Assert.Equal(1, samples.Count); // 应只解析出一个有效帧
    }

    /// <summary>
    /// 测试: 帧被截断到一半应等待更多数据。
    /// </summary>
    [Fact]
    public void NirsTruncatedFrame_ShouldWaitForMore()
    {
        // Arrange
        var parser = new NirsProtocolParser();
        NirsSample? receivedSample = null;
        parser.PacketParsed += sample => receivedSample = sample;

        byte[] fullFrame = CreateValidNirsFrame("70", "72", "68", "74");
        byte[] truncated = new byte[fullFrame.Length / 2];
        Array.Copy(fullFrame, truncated, truncated.Length);

        // Act - 发送截断帧
        parser.ProcessBytes(truncated, truncated.Length, timestampUs: 3000);
        Assert.Null(receivedSample);

        // 发送剩余部分
        byte[] remaining = new byte[fullFrame.Length - truncated.Length];
        Array.Copy(fullFrame, truncated.Length, remaining, 0, remaining.Length);
        parser.ProcessBytes(remaining, remaining.Length, timestampUs: 3001);

        // Assert
        Assert.NotNull(receivedSample);
    }

    #endregion

    #region 字段格式错误测试

    /// <summary>
    /// 测试: rSO2 字段包含非法字符应解析失败。
    /// </summary>
    [Fact]
    public void NirsRso2_IllegalCharacters_ShouldFail()
    {
        // Arrange
        var parser = new NirsProtocolParser();
        NirsSample? receivedSample = null;
        parser.PacketParsed += sample => receivedSample = sample;

        // rSO2 字段包含字母
        byte[] invalidFrame = CreateInvalidNirsFrame("ABC", "68", "70", "65");

        // Act
        parser.ProcessBytes(invalidFrame, invalidFrame.Length, timestampUs: 4000);

        // Assert
        // 由于格式错误，可能解析失败或值无效
        // 检查是否有 ParseErrors 或 Ch1 标记为无效
        if (receivedSample.HasValue)
        {
            Assert.Equal(0, receivedSample.Value.ValidMask & 0x01); // Ch1 应无效
        }
    }

    /// <summary>
    /// 测试: 超长字段应被截断或拒绝。
    /// </summary>
    [Fact]
    public void NirsRso2_VeryLongField_ShouldHandle()
    {
        // Arrange
        var parser = new NirsProtocolParser();
        NirsSample? receivedSample = null;
        parser.PacketParsed += sample => receivedSample = sample;

        // 超长值（1000 位数字）
        string longValue = new string('9', 1000);
        byte[] invalidFrame = CreateInvalidNirsFrame(longValue, "68", "70", "65");

        // Act
        parser.ProcessBytes(invalidFrame, invalidFrame.Length, timestampUs: 5000);

        // Assert
        // 应该被解析器拒绝或将 Ch1 标记为无效
        if (receivedSample.HasValue)
        {
            Assert.Equal(0, receivedSample.Value.ValidMask & 0x01);
        }
    }

    /// <summary>
    /// 测试: 缺少逗号分隔符应解析失败。
    /// </summary>
    [Fact]
    public void NirsRso2_MissingComma_ShouldFail()
    {
        // Arrange
        var parser = new NirsProtocolParser();
        NirsSample? receivedSample = null;
        parser.PacketParsed += sample => receivedSample = sample;

        string dataWithoutCrc = "Ch1 72 68 70 65 1 2 3 4 | rSO2=72 68 70 65 | CKSUM="; // 缺少逗号
        byte[] dataBytes = System.Text.Encoding.ASCII.GetBytes(dataWithoutCrc);
        ushort crc = CalculateCrc16Ccitt(dataBytes);
        string fullFrame = dataWithoutCrc + crc.ToString("X4") + "\r\n";
        byte[] frameBytes = System.Text.Encoding.ASCII.GetBytes(fullFrame);

        // Act
        parser.ProcessBytes(frameBytes, frameBytes.Length, timestampUs: 6000);

        // Assert
        Assert.Null(receivedSample); // 格式错误应解析失败
    }

    #endregion

    #region 极端值测试

    /// <summary>
    /// 测试: 负数 rSO2 值应无效。
    /// </summary>
    [Fact]
    public void NirsRso2_NegativeValue_ShouldBeInvalid()
    {
        // Arrange
        var parser = new NirsProtocolParser();
        NirsSample? receivedSample = null;
        parser.PacketParsed += sample => receivedSample = sample;

        byte[] frame = CreateValidNirsFrame("-10", "68", "70", "65");

        // Act
        parser.ProcessBytes(frame, frame.Length, timestampUs: 7000);

        // Assert
        if (receivedSample.HasValue)
        {
            // 负数应标记为无效
            Assert.Equal(0, receivedSample.Value.ValidMask & 0x01);
        }
    }

    /// <summary>
    /// 测试: 科学计数法表示的值（如 7.2E+01）应正确解析或标记无效。
    /// </summary>
    [Fact]
    public void NirsRso2_ScientificNotation_ShouldHandle()
    {
        // Arrange
        var parser = new NirsProtocolParser();
        NirsSample? receivedSample = null;
        parser.PacketParsed += sample => receivedSample = sample;

        byte[] frame = CreateValidNirsFrame("7.2E+01", "6.8E+01", "70", "65");

        // Act
        parser.ProcessBytes(frame, frame.Length, timestampUs: 8000);

        // Assert
        if (receivedSample.HasValue)
        {
            // .NET double.TryParse 支持科学计数法
            // 7.2E+01 = 72.0
            if ((receivedSample.Value.ValidMask & 0x01) != 0)
            {
                Assert.Equal(72.0, receivedSample.Value.Ch1Percent, precision: 1);
            }
        }
    }

    /// <summary>
    /// 测试: 浮点数溢出（超大值）应处理。
    /// </summary>
    [Fact]
    public void NirsRso2_FloatOverflow_ShouldHandle()
    {
        // Arrange
        var parser = new NirsProtocolParser();
        NirsSample? receivedSample = null;
        parser.PacketParsed += sample => receivedSample = sample;

        byte[] frame = CreateValidNirsFrame("1E+308", "68", "70", "65"); // 接近 double 上限

        // Act
        parser.ProcessBytes(frame, frame.Length, timestampUs: 9000);

        // Assert
        if (receivedSample.HasValue)
        {
            // 超出范围应标记为无效
            Assert.Equal(0, receivedSample.Value.ValidMask & 0x01);
        }
    }

    /// <summary>
    /// 测试: 所有通道均为 "---" 应全部无效。
    /// </summary>
    [Fact]
    public void NirsRso2_AllInvalid_ShouldBeAllZero()
    {
        // Arrange
        var parser = new NirsProtocolParser();
        NirsSample? receivedSample = null;
        parser.PacketParsed += sample => receivedSample = sample;

        byte[] frame = CreateValidNirsFrame("---", "---", "---", "---");

        // Act
        parser.ProcessBytes(frame, frame.Length, timestampUs: 10000);

        // Assert
        Assert.NotNull(receivedSample);
        Assert.Equal(0x00, receivedSample.Value.ValidMask); // 所有通道无效
        Assert.Equal(0.0, receivedSample.Value.Ch1Percent);
        Assert.Equal(0.0, receivedSample.Value.Ch2Percent);
        Assert.Equal(0.0, receivedSample.Value.Ch3Percent);
        Assert.Equal(0.0, receivedSample.Value.Ch4Percent);
    }

    #endregion

    #region 并发访问测试

    /// <summary>
    /// 测试: 多线程并发调用 ProcessBytes 应不崩溃。
    /// </summary>
    /// <remarks>
    /// 注意: 这只是基础并发测试，实际使用中应确保单线程调用
    /// </remarks>
    [Fact]
    public void NirsConcurrent_MultipleThreads_ShouldNotCrash()
    {
        // Arrange
        var parser = new NirsProtocolParser();
        int samplesReceived = 0;
        parser.PacketParsed += _ => System.Threading.Interlocked.Increment(ref samplesReceived);

        byte[] frame = CreateValidNirsFrame("75", "80", "72", "78");

        // Act - 多线程并发发送数据
        var tasks = new System.Threading.Tasks.Task[10];
        for (int i = 0; i < tasks.Length; i++)
        {
            int threadId = i;
            tasks[i] = System.Threading.Tasks.Task.Run(() =>
            {
                for (int j = 0; j < 10; j++)
                {
                    parser.ProcessBytes(frame, frame.Length, timestampUs: threadId * 1000 + j);
                    System.Threading.Thread.Sleep(1); // 模拟间隔
                }
            });
        }

        System.Threading.Tasks.Task.WaitAll(tasks);

        // Assert
        // 至少应解析一些帧（具体数量取决于竞争条件）
        Assert.True(samplesReceived > 0);
    }

    #endregion

    #region 辅助方法

    private static byte[] CreateValidNirsFrame(string ch1, string ch2, string ch3, string ch4)
    {
        string dataWithoutCrc =
            $"Ch1 {ch1} {ch2} {ch3} {ch4} 1 2 3 4 | " +
            "TIMESTAMP=2024-12-17T09:15:42.000+08:00 | " +
            $"rSO2={ch1},{ch2},{ch3},{ch4} | " +
            "HbI=11.5,10.8,11.2,10.5 | " +
            "CKSUM=";

        byte[] dataBytes = System.Text.Encoding.ASCII.GetBytes(dataWithoutCrc);
        ushort crc = CalculateCrc16Ccitt(dataBytes);
        string fullFrame = dataWithoutCrc + crc.ToString("X4") + "\r\n";
        return System.Text.Encoding.ASCII.GetBytes(fullFrame);
    }

    private static byte[] CreateInvalidNirsFrame(string ch1, string ch2, string ch3, string ch4)
    {
        // 故意创建带有错误 CRC 的帧，用于测试格式错误
        string frame = $"Ch1 {ch1} {ch2} {ch3} {ch4} 1 2 3 4 | rSO2={ch1},{ch2},{ch3},{ch4} | CKSUM=FFFF\r\n";
        return System.Text.Encoding.ASCII.GetBytes(frame);
    }

    private static ushort CalculateCrc16Ccitt(byte[] data)
    {
        ushort crc = 0x0000;
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

    private static class Assert
    {
        public static void NotNull(object? obj)
        {
            if (obj == null) throw new Exception("Assert.NotNull failed");
        }

        public static void Null(object? obj)
        {
            if (obj != null) throw new Exception("Assert.Null failed");
        }

        public static void Equal<T>(T expected, T actual)
        {
            if (!EqualityComparer<T>.Default.Equals(expected, actual))
                throw new Exception($"Assert.Equal failed: expected {expected}, actual {actual}");
        }

        public static void Equal(double expected, double actual, int precision)
        {
            double tolerance = Math.Pow(10, -precision);
            if (Math.Abs(expected - actual) > tolerance)
                throw new Exception($"Assert.Equal failed: expected {expected}, actual {actual}");
        }

        public static void True(bool condition)
        {
            if (!condition) throw new Exception("Assert.True failed");
        }
    }

    #endregion
}



