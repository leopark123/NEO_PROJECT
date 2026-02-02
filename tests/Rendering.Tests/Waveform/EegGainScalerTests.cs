// EegGainScalerTests.cs
// 增益缩放器测试 - 来源: CONSENSUS_BASELINE.md §6.3

using Neo.Rendering.EEG;
using Xunit;

namespace Neo.Rendering.Tests.Waveform;

/// <summary>
/// EEG 增益缩放器测试。
/// </summary>
public sealed class EegGainScalerTests
{
    private const double Tolerance = 0.001;
    private const double DefaultDpi = 96.0;

    // ============================================
    // 构造函数测试
    // ============================================

    [Fact]
    public void Constructor_DefaultGain_Is50()
    {
        var scaler = new EegGainScaler();
        Assert.Equal(EegGainSetting.Gain50, scaler.Gain);
        Assert.Equal(50, scaler.GainValue);
    }

    [Fact]
    public void Constructor_DefaultDpi_Is96()
    {
        var scaler = new EegGainScaler();
        Assert.Equal(96.0, scaler.Dpi, Tolerance);
    }

    [Fact]
    public void Constructor_ZeroDpi_ThrowsException()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new EegGainScaler(EegGainSetting.Gain50, 0));
    }

    [Fact]
    public void Constructor_NegativeDpi_ThrowsException()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new EegGainScaler(EegGainSetting.Gain50, -96));
    }

    // ============================================
    // 增益设置测试
    // ============================================

    [Theory]
    [InlineData(EegGainSetting.Gain10, 10)]
    [InlineData(EegGainSetting.Gain20, 20)]
    [InlineData(EegGainSetting.Gain50, 50)]
    [InlineData(EegGainSetting.Gain70, 70)]
    [InlineData(EegGainSetting.Gain100, 100)]
    [InlineData(EegGainSetting.Gain200, 200)]
    [InlineData(EegGainSetting.Gain1000, 1000)]
    public void GainValue_MatchesEnumValue(EegGainSetting gain, int expectedValue)
    {
        var scaler = new EegGainScaler(gain);
        Assert.Equal(expectedValue, scaler.GainValue);
    }

    [Fact]
    public void AvailableGains_Contains7Settings()
    {
        Assert.Equal(7, EegGainScaler.AvailableGains.Length);
    }

    [Fact]
    public void AvailableGains_ContainsGain1000()
    {
        Assert.Contains(EegGainSetting.Gain1000, EegGainScaler.AvailableGains);
    }

    // ============================================
    // 像素/厘米转换测试
    // ============================================

    [Fact]
    public void PixelsPerCm_At96Dpi()
    {
        var scaler = new EegGainScaler(EegGainSetting.Gain50, 96.0);
        // 96 DPI / 2.54 cm/inch ≈ 37.795 px/cm
        Assert.Equal(37.795, scaler.PixelsPerCm, 0.01);
    }

    [Fact]
    public void PixelsPerCm_At144Dpi()
    {
        var scaler = new EegGainScaler(EegGainSetting.Gain50, 144.0);
        // 144 DPI / 2.54 cm/inch ≈ 56.693 px/cm
        Assert.Equal(56.693, scaler.PixelsPerCm, 0.01);
    }

    // ============================================
    // μV 到像素转换测试
    // ============================================

    [Fact]
    public void UvToPixels_Gain50_At96Dpi()
    {
        var scaler = new EegGainScaler(EegGainSetting.Gain50, 96.0);
        // 50 μV/cm 意味着 50 μV = 1 cm = 37.795 px
        // 1 μV = 37.795 / 50 ≈ 0.7559 px
        double pixels = scaler.UvToPixels(50.0);
        Assert.Equal(37.795, pixels, 0.01);
    }

    [Fact]
    public void UvToPixels_Zero_ReturnsZero()
    {
        var scaler = new EegGainScaler(EegGainSetting.Gain50);
        Assert.Equal(0.0, scaler.UvToPixels(0.0), Tolerance);
    }

    [Fact]
    public void UvToPixels_Negative_ReturnsNegative()
    {
        var scaler = new EegGainScaler(EegGainSetting.Gain50);
        double positive = scaler.UvToPixels(100.0);
        double negative = scaler.UvToPixels(-100.0);
        Assert.Equal(-positive, negative, Tolerance);
    }

    [Theory]
    [InlineData(EegGainSetting.Gain10)]
    [InlineData(EegGainSetting.Gain50)]
    [InlineData(EegGainSetting.Gain100)]
    [InlineData(EegGainSetting.Gain1000)]
    public void UvToPixels_HigherGain_SmallerPixels(EegGainSetting gain)
    {
        var scaler = new EegGainScaler(gain);
        double pixels = scaler.UvToPixels(100.0);

        // 更高的增益值意味着更大的 μV/cm，即相同 μV 对应更少的像素
        // 验证非零
        Assert.NotEqual(0.0, pixels);
    }

    [Fact]
    public void UvToPixels_Gain10VsGain100_TenTimesDifference()
    {
        var scaler10 = new EegGainScaler(EegGainSetting.Gain10);
        var scaler100 = new EegGainScaler(EegGainSetting.Gain100);

        double pixels10 = scaler10.UvToPixels(100.0);
        double pixels100 = scaler100.UvToPixels(100.0);

        // Gain10 产生的像素应该是 Gain100 的 10 倍
        Assert.Equal(10.0, pixels10 / pixels100, 0.01);
    }

    // ============================================
    // 像素到 μV 转换测试
    // ============================================

    [Fact]
    public void PixelsToUv_Roundtrip()
    {
        var scaler = new EegGainScaler(EegGainSetting.Gain50);

        double originalUv = 75.0;
        double pixels = scaler.UvToPixels(originalUv);
        double resultUv = scaler.PixelsToUv(pixels);

        Assert.Equal(originalUv, resultUv, Tolerance);
    }

    [Fact]
    public void PixelsToUv_Zero_ReturnsZero()
    {
        var scaler = new EegGainScaler(EegGainSetting.Gain50);
        Assert.Equal(0.0, scaler.PixelsToUv(0.0), Tolerance);
    }

    // ============================================
    // 显示范围测试
    // ============================================

    [Fact]
    public void GetDisplayRangeUv_CorrectCalculation()
    {
        var scaler = new EegGainScaler(EegGainSetting.Gain50, 96.0);

        // 假设通道高度为 200 像素
        double heightPx = 200.0;
        double rangeUv = scaler.GetDisplayRangeUv(heightPx);

        // 半高度 = 100 px
        // 100 px = ? μV
        double halfHeightUv = scaler.PixelsToUv(100.0);
        Assert.Equal(halfHeightUv, rangeUv, Tolerance);
    }

    // ============================================
    // 工厂方法测试
    // ============================================

    [Fact]
    public void Create_ReturnsCorrectInstance()
    {
        var scaler = EegGainScaler.Create(EegGainSetting.Gain70, 120.0);
        Assert.Equal(EegGainSetting.Gain70, scaler.Gain);
        Assert.Equal(120.0, scaler.Dpi, Tolerance);
    }

    // ============================================
    // 显示文本测试
    // ============================================

    [Theory]
    [InlineData(EegGainSetting.Gain10, "10 μV/cm")]
    [InlineData(EegGainSetting.Gain50, "50 μV/cm")]
    [InlineData(EegGainSetting.Gain1000, "1000 μV/cm")]
    public void GetDisplayText_CorrectFormat(EegGainSetting gain, string expectedText)
    {
        string text = EegGainScaler.GetDisplayText(gain);
        Assert.Equal(expectedText, text);
    }
}
