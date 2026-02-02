// AeegSeriesBuilderTests.cs
// aEEG 序列构建器测试 - 来源: DSP_SPEC.md §3

using Neo.Core.Enums;
using Neo.Rendering.AEEG;
using Neo.Rendering.Mapping;
using Xunit;

namespace Neo.Rendering.Tests.Waveform;

/// <summary>
/// aEEG 序列构建器测试。
/// </summary>
/// <remarks>
/// 验证规格:
/// - 间隙 > 2 秒必须断线
/// - 铁律2: 不伪造波形
/// - 铁律5: 缺失必须可见
/// - 使用 AeegSemiLogMapper 进行 Y 轴映射
/// </remarks>
public sealed class AeegSeriesBuilderTests
{
    private const long AeegSampleIntervalUs = 1_000_000;  // 1 Hz
    private const long MaxGapUs = 2 * AeegSampleIntervalUs;  // 2 秒
    private const float Tolerance = 0.001f;
    private const double TotalHeight = 1000.0;

    private readonly AeegSeriesBuilder _builder = new();
    private readonly AeegSemiLogMapper _mapper;

    public AeegSeriesBuilderTests()
    {
        _mapper = new AeegSemiLogMapper(TotalHeight);
    }

    // 辅助方法
    private static float SimpleTimestampToX(long timestampUs) => timestampUs / 1_000_000.0f;

    // ============================================
    // 基本构建测试
    // ============================================

    [Fact]
    public void Build_EmptyData_ReturnsEmptyResult()
    {
        var result = _builder.Build(
            [],
            [],
            [],
            [],
            _mapper,
            0,
            SimpleTimestampToX,
            0,
            10_000_000);

        Assert.Empty(result.Points);
        Assert.Empty(result.Segments);
        Assert.Empty(result.Gaps);
    }

    [Fact]
    public void Build_SinglePoint_ReturnsOnePoint()
    {
        float[] minValues = [5.0f];
        float[] maxValues = [50.0f];
        long[] timestamps = [0];
        byte[] quality = [(byte)QualityFlag.Normal];

        var result = _builder.Build(
            minValues,
            maxValues,
            timestamps,
            quality,
            _mapper,
            0,
            SimpleTimestampToX,
            0,
            10_000_000);

        Assert.Single(result.Points);
        Assert.Single(result.Segments);
    }

    [Fact]
    public void Build_ContinuousData_SingleSegment()
    {
        float[] minValues = [5.0f, 8.0f, 10.0f, 12.0f, 15.0f];
        float[] maxValues = [20.0f, 25.0f, 30.0f, 35.0f, 40.0f];
        long[] timestamps = [0, 1_000_000, 2_000_000, 3_000_000, 4_000_000];
        byte[] quality = new byte[5];

        var result = _builder.Build(
            minValues,
            maxValues,
            timestamps,
            quality,
            _mapper,
            0,
            SimpleTimestampToX,
            0,
            10_000_000);

        Assert.Equal(5, result.Points.Length);
        Assert.Single(result.Segments);
        Assert.Equal(5, result.Segments[0].PointCount);
        Assert.Empty(result.Gaps);
    }

    // ============================================
    // 间隙处理测试
    // ============================================

    [Fact]
    public void MaxTolerableGapUs_Is2Seconds()
    {
        Assert.Equal(2_000_000, AeegSeriesBuilder.MaxTolerableGapUs);
    }

    [Fact]
    public void Build_GapWithin2Seconds_NoBreak()
    {
        float[] minValues = [5.0f, 10.0f];
        float[] maxValues = [20.0f, 30.0f];
        long[] timestamps = [0, 2_000_000];  // 刚好 2 秒
        byte[] quality = new byte[2];

        var result = _builder.Build(
            minValues,
            maxValues,
            timestamps,
            quality,
            _mapper,
            0,
            SimpleTimestampToX,
            0,
            10_000_000);

        Assert.Equal(2, result.Points.Length);
        Assert.Single(result.Segments);
        Assert.Empty(result.Gaps);
    }

    [Fact]
    public void Build_GapExceeds2Seconds_MustBreak()
    {
        float[] minValues = [5.0f, 10.0f, 15.0f];
        float[] maxValues = [20.0f, 30.0f, 40.0f];
        long[] timestamps = [0, 2_100_000, 4_200_000];  // 每个间隔 > 2 秒
        byte[] quality = new byte[3];

        var result = _builder.Build(
            minValues,
            maxValues,
            timestamps,
            quality,
            _mapper,
            0,
            SimpleTimestampToX,
            0,
            10_000_000);

        Assert.Equal(3, result.Points.Length);
        Assert.Equal(3, result.Segments.Length);  // 三个独立段
        Assert.Equal(2, result.Gaps.Length);  // 两个间隙
    }

    // ============================================
    // NaN 处理测试
    // ============================================

    [Fact]
    public void Build_NaNMinValue_BreaksSegment()
    {
        float[] minValues = [5.0f, float.NaN, 15.0f];
        float[] maxValues = [20.0f, 30.0f, 40.0f];
        long[] timestamps = [0, 1_000_000, 2_000_000];
        byte[] quality = new byte[3];

        var result = _builder.Build(
            minValues,
            maxValues,
            timestamps,
            quality,
            _mapper,
            0,
            SimpleTimestampToX,
            0,
            10_000_000);

        Assert.Equal(2, result.Points.Length);
        Assert.Equal(2, result.Segments.Length);
    }

    [Fact]
    public void Build_NaNMaxValue_BreaksSegment()
    {
        float[] minValues = [5.0f, 10.0f, 15.0f];
        float[] maxValues = [20.0f, float.NaN, 40.0f];
        long[] timestamps = [0, 1_000_000, 2_000_000];
        byte[] quality = new byte[3];

        var result = _builder.Build(
            minValues,
            maxValues,
            timestamps,
            quality,
            _mapper,
            0,
            SimpleTimestampToX,
            0,
            10_000_000);

        Assert.Equal(2, result.Points.Length);
        Assert.Equal(2, result.Segments.Length);
    }

    // ============================================
    // 质量标志测试
    // ============================================

    [Theory]
    [InlineData(QualityFlag.Missing)]
    [InlineData(QualityFlag.LeadOff)]
    public void Build_InvalidQualityFlag_BreaksSegment(QualityFlag flag)
    {
        float[] minValues = [5.0f, 10.0f, 15.0f];
        float[] maxValues = [20.0f, 30.0f, 40.0f];
        long[] timestamps = [0, 1_000_000, 2_000_000];
        byte[] quality = [(byte)QualityFlag.Normal, (byte)flag, (byte)QualityFlag.Normal];

        var result = _builder.Build(
            minValues,
            maxValues,
            timestamps,
            quality,
            _mapper,
            0,
            SimpleTimestampToX,
            0,
            10_000_000);

        Assert.Equal(2, result.Points.Length);
        Assert.Equal(2, result.Segments.Length);
    }

    // ============================================
    // Y 坐标映射测试（使用 AeegSemiLogMapper）
    // ============================================

    [Fact]
    public void Build_UsesAeegSemiLogMapper()
    {
        // 10 μV 应该在 Y = 500 (中间)
        float[] minValues = [10.0f];
        float[] maxValues = [10.0f];
        long[] timestamps = [0];
        byte[] quality = [(byte)QualityFlag.Normal];

        var result = _builder.Build(
            minValues,
            maxValues,
            timestamps,
            quality,
            _mapper,
            0,  // renderAreaTop = 0
            SimpleTimestampToX,
            0,
            10_000_000);

        // 10 μV 映射到 Y = totalHeight / 2 = 500
        Assert.Equal(500.0f, result.Points[0].MinY, 1.0f);
        Assert.Equal(500.0f, result.Points[0].MaxY, 1.0f);
    }

    [Fact]
    public void Build_0uV_AtBottom()
    {
        float[] minValues = [0.0f];
        float[] maxValues = [0.0f];
        long[] timestamps = [0];
        byte[] quality = [(byte)QualityFlag.Normal];

        var result = _builder.Build(
            minValues,
            maxValues,
            timestamps,
            quality,
            _mapper,
            0,
            SimpleTimestampToX,
            0,
            10_000_000);

        // 0 μV 映射到 Y = totalHeight = 1000 (底部)
        Assert.Equal(1000.0f, result.Points[0].MinY, 1.0f);
    }

    [Fact]
    public void Build_200uV_AtTop()
    {
        float[] minValues = [200.0f];
        float[] maxValues = [200.0f];
        long[] timestamps = [0];
        byte[] quality = [(byte)QualityFlag.Normal];

        var result = _builder.Build(
            minValues,
            maxValues,
            timestamps,
            quality,
            _mapper,
            0,
            SimpleTimestampToX,
            0,
            10_000_000);

        // 200 μV 映射到 Y = 0 (顶部)
        Assert.Equal(0.0f, result.Points[0].MinY, 1.0f);
    }

    [Fact]
    public void Build_MinMaxDifferent_CorrectYValues()
    {
        float[] minValues = [5.0f];   // 线性段，下边界
        float[] maxValues = [100.0f]; // 对数段，上边界
        long[] timestamps = [0];
        byte[] quality = [(byte)QualityFlag.Normal];

        var result = _builder.Build(
            minValues,
            maxValues,
            timestamps,
            quality,
            _mapper,
            0,
            SimpleTimestampToX,
            0,
            10_000_000);

        // MinY 对应下边界 (更大的 Y 值)
        // MaxY 对应上边界 (更小的 Y 值)
        Assert.True(result.Points[0].MinY > result.Points[0].MaxY);
    }

    // ============================================
    // 可见范围测试
    // ============================================

    [Fact]
    public void Build_OutOfVisibleRange_Excluded()
    {
        float[] minValues = [5.0f, 10.0f, 15.0f, 20.0f, 25.0f];
        float[] maxValues = [20.0f, 30.0f, 40.0f, 50.0f, 60.0f];
        long[] timestamps = [0, 1_000_000, 2_000_000, 3_000_000, 4_000_000];
        byte[] quality = new byte[5];

        // 只显示中间 3 个点
        long visibleStart = 1_000_000;
        long visibleEnd = 3_000_000;

        var result = _builder.Build(
            minValues,
            maxValues,
            timestamps,
            quality,
            _mapper,
            0,
            SimpleTimestampToX,
            visibleStart,
            visibleEnd);

        Assert.Equal(3, result.Points.Length);
    }

    // ============================================
    // 渲染区域偏移测试
    // ============================================

    [Fact]
    public void Build_WithRenderAreaTop_CorrectOffset()
    {
        float renderAreaTop = 100.0f;
        float[] minValues = [10.0f];  // 10 μV 映射到 Y = 500
        float[] maxValues = [10.0f];
        long[] timestamps = [0];
        byte[] quality = [(byte)QualityFlag.Normal];

        var result = _builder.Build(
            minValues,
            maxValues,
            timestamps,
            quality,
            _mapper,
            renderAreaTop,
            SimpleTimestampToX,
            0,
            10_000_000);

        // 应该加上 renderAreaTop 偏移
        Assert.Equal(600.0f, result.Points[0].MinY, 1.0f);  // 500 + 100
    }

    // ============================================
    // 间隙信息测试
    // ============================================

    [Fact]
    public void Build_GapInfo_CorrectCoordinates()
    {
        float[] minValues = [5.0f, 10.0f];
        float[] maxValues = [20.0f, 30.0f];
        long[] timestamps = [0, 3_000_000];  // > 2 秒间隔
        byte[] quality = new byte[2];

        var result = _builder.Build(
            minValues,
            maxValues,
            timestamps,
            quality,
            _mapper,
            0,
            SimpleTimestampToX,
            0,
            10_000_000);

        Assert.Single(result.Gaps);
        Assert.Equal(0.0f, result.Gaps[0].StartX, Tolerance);
        Assert.Equal(3.0f, result.Gaps[0].EndX, Tolerance);
    }

    // ============================================
    // 大数据测试
    // ============================================

    [Fact]
    public void Build_LargeDataset_HandledCorrectly()
    {
        int count = 3600;  // 1 小时的数据
        float[] minValues = new float[count];
        float[] maxValues = new float[count];
        long[] timestamps = new long[count];
        byte[] quality = new byte[count];

        for (int i = 0; i < count; i++)
        {
            minValues[i] = 5.0f + (float)Math.Sin(i * 0.01) * 3.0f;
            maxValues[i] = 30.0f + (float)Math.Sin(i * 0.01) * 10.0f;
            timestamps[i] = i * AeegSampleIntervalUs;
        }

        var result = _builder.Build(
            minValues,
            maxValues,
            timestamps,
            quality,
            _mapper,
            0,
            SimpleTimestampToX,
            0,
            count * AeegSampleIntervalUs);

        Assert.Equal(count, result.Points.Length);
        Assert.Single(result.Segments);
    }
}
