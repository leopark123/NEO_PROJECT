// Rs232ProtocolParserTests.cs
// EEG RS232 协议解析器单元测试
// 测试场景: CRC校验、半包处理、粘包处理

namespace Neo.Tests.DataSources.Rs232;

using Neo.Core.Enums;
using Neo.Core.Models;
using Neo.DataSources.Rs232;

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
        Assert.Equal(1000, receivedSample.TimestampUs);
        Assert.Equal(100 * 0.076, receivedSample.Ch1Uv, precision: 5);
        Assert.Equal(1, parser.PacketsReceived);
        Assert.Equal(0, parser.CrcErrors);
    }

    /// <summary>
    /// 测试: CRC 校验失败时应触发 CrcErrorOccurred 事件。
    /// </summary>
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
        Assert.Equal(200 * 0.076, receivedSample.Ch1Uv, precision: 5);
    }

    /// <summary>
    /// 测试: 单字节逐个到达应正确解析。
    /// </summary>
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
        Assert.Equal(100 * 0.076, receivedSample.Ch1Uv, precision: 5);
        Assert.Equal(50 * 0.076, receivedSample.Ch2Uv, precision: 5);
        Assert.Equal(75 * 0.076, receivedSample.Ch3Uv, precision: 5);

        // CH4 = CH1 - CH2 (计算通道)
        // 来源: ACCEPTANCE_TESTS.md L477
        double expectedCh4 = (100 - 50) * 0.076;
        Assert.Equal(expectedCh4, receivedSample.Ch4Uv, precision: 5);

        // 质量标志应为 Normal
        Assert.Equal(QualityFlag.Normal, receivedSample.QualityFlags);
    }

    /// <summary>
    /// 测试: CH4 计算公式 (CH1 - CH2)。
    /// 来源: ACCEPTANCE_TESTS.md L477
    /// </summary>
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
        Assert.Equal(expectedCh4, receivedSample.Ch4Uv, precision: 5);
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
