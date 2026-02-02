// PolylineBuilderTests.cs
// 折线构建器测试 - 来源: ADR-005, 00_CONSTITUTION.md 铁律2/5

using Neo.Core.Enums;
using Neo.Rendering.EEG;
using Xunit;

namespace Neo.Rendering.Tests.Waveform;

/// <summary>
/// 折线构建器测试。
/// </summary>
/// <remarks>
/// 验证规格:
/// - ADR-005: 间隙 > 4 样本必须断线
/// - 铁律2: 不伪造波形
/// - 铁律5: 缺失/饱和必须可见
/// </remarks>
public sealed class PolylineBuilderTests
{
    private const long SampleIntervalUs = 6250;  // 160 Hz
    private const long MaxGapUs = 4 * SampleIntervalUs;  // 25000 μs
    private const float Tolerance = 0.001f;

    private readonly PolylineBuilder _builder = new();

    // 辅助方法
    private static float SimpleTimestampToX(long timestampUs) => timestampUs / 1000.0f;
    private static float SimpleUvToY(double uv) => 500.0f - (float)(uv * 2.0);

    // ============================================
    // 基本构建测试
    // ============================================

    [Fact]
    public void Build_EmptyData_ReturnsEmptyResult()
    {
        var result = _builder.Build(
            [],
            [],
            0,
            SampleIntervalUs,
            SimpleTimestampToX,
            SimpleUvToY,
            0,
            1_000_000);

        Assert.Empty(result.Points);
        Assert.Empty(result.Segments);
        Assert.Empty(result.Gaps);
        Assert.Empty(result.SaturationIndices);
    }

    [Fact]
    public void Build_SinglePoint_ReturnsOnePoint()
    {
        float[] data = [50.0f];
        byte[] quality = [(byte)QualityFlag.Normal];

        var result = _builder.Build(
            data,
            quality,
            0,
            SampleIntervalUs,
            SimpleTimestampToX,
            SimpleUvToY,
            0,
            1_000_000);

        Assert.Single(result.Points);
        Assert.Single(result.Segments);
        Assert.Equal(1, result.Segments[0].PointCount);
    }

    [Fact]
    public void Build_ContinuousData_SingleSegment()
    {
        float[] data = [10.0f, 20.0f, 30.0f, 40.0f, 50.0f];
        byte[] quality = new byte[5];

        var result = _builder.Build(
            data,
            quality,
            0,
            SampleIntervalUs,
            SimpleTimestampToX,
            SimpleUvToY,
            0,
            1_000_000);

        Assert.Equal(5, result.Points.Length);
        Assert.Single(result.Segments);
        Assert.Equal(5, result.Segments[0].PointCount);
        Assert.Empty(result.Gaps);
    }

    // ============================================
    // 间隙处理测试 (ADR-005)
    // ============================================

    [Fact]
    public void Build_GapWithin4Samples_NoBreak()
    {
        // 间隙 = 4 样本 = 25000 μs，刚好在阈值内
        float[] data = [10.0f, 20.0f];
        byte[] quality = [(byte)QualityFlag.Normal, (byte)QualityFlag.Normal];

        // 第二个点的时间戳距离第一个 4 个样本
        var result = _builder.Build(
            data,
            quality,
            0,
            4 * SampleIntervalUs,  // 间隔刚好 4 个样本
            SimpleTimestampToX,
            SimpleUvToY,
            0,
            1_000_000);

        // 应该是连续的一段
        Assert.Equal(2, result.Points.Length);
        Assert.Single(result.Segments);
    }

    [Fact]
    public void Build_GapExceeds4Samples_MustBreak()
    {
        // 模拟间隙 > 4 样本的情况
        // 通过使用更大的采样间隔来模拟
        float[] data = [10.0f, 20.0f, 30.0f];
        byte[] quality = new byte[3];

        // 设置采样间隔大于阈值
        long largeInterval = MaxGapUs + 1000;  // > 25ms

        var result = _builder.Build(
            data,
            quality,
            0,
            largeInterval,
            SimpleTimestampToX,
            SimpleUvToY,
            0,
            10_000_000);

        // 每个点应该是单独的段
        Assert.Equal(3, result.Points.Length);
        Assert.Equal(3, result.Segments.Length);
        Assert.Equal(2, result.Gaps.Length);  // 两个间隙
    }

    [Fact]
    public void MaxInterpolatableGapSamples_Is4()
    {
        Assert.Equal(4, PolylineBuilder.MaxInterpolatableGapSamples);
    }

    [Fact]
    public void MaxInterpolatableGapUs_Is25000()
    {
        Assert.Equal(25000, PolylineBuilder.MaxInterpolatableGapUs);
    }

    // ============================================
    // NaN 处理测试
    // ============================================

    [Fact]
    public void Build_NaNValue_BreaksSegment()
    {
        float[] data = [10.0f, float.NaN, 30.0f];
        byte[] quality = new byte[3];

        var result = _builder.Build(
            data,
            quality,
            0,
            SampleIntervalUs,
            SimpleTimestampToX,
            SimpleUvToY,
            0,
            1_000_000);

        // 应该有两段（NaN 点之前和之后）
        Assert.Equal(2, result.Points.Length);
        Assert.Equal(2, result.Segments.Length);
    }

    [Fact]
    public void Build_AllNaN_ReturnsEmpty()
    {
        float[] data = [float.NaN, float.NaN, float.NaN];
        byte[] quality = new byte[3];

        var result = _builder.Build(
            data,
            quality,
            0,
            SampleIntervalUs,
            SimpleTimestampToX,
            SimpleUvToY,
            0,
            1_000_000);

        Assert.Empty(result.Points);
        Assert.Empty(result.Segments);
    }

    // ============================================
    // 质量标志测试 (铁律5)
    // ============================================

    [Theory]
    [InlineData(QualityFlag.Missing)]
    [InlineData(QualityFlag.LeadOff)]
    [InlineData(QualityFlag.Undocumented)]
    public void Build_InvalidQualityFlag_BreaksSegment(QualityFlag flag)
    {
        float[] data = [10.0f, 20.0f, 30.0f];
        byte[] quality = [(byte)QualityFlag.Normal, (byte)flag, (byte)QualityFlag.Normal];

        var result = _builder.Build(
            data,
            quality,
            0,
            SampleIntervalUs,
            SimpleTimestampToX,
            SimpleUvToY,
            0,
            1_000_000);

        // 应该有两段
        Assert.Equal(2, result.Points.Length);
        Assert.Equal(2, result.Segments.Length);
    }

    [Fact]
    public void Build_SaturatedFlag_RecordedInIndices()
    {
        float[] data = [10.0f, 20.0f, 30.0f];
        byte[] quality = [(byte)QualityFlag.Normal, (byte)QualityFlag.Saturated, (byte)QualityFlag.Normal];

        var result = _builder.Build(
            data,
            quality,
            0,
            SampleIntervalUs,
            SimpleTimestampToX,
            SimpleUvToY,
            0,
            1_000_000);

        // 饱和点应该被记录
        Assert.Single(result.SaturationIndices);
        Assert.Equal(1, result.SaturationIndices[0]);
    }

    [Fact]
    public void Build_SaturatedFlag_SegmentMarkedHasSaturation()
    {
        float[] data = [10.0f, 20.0f, 30.0f];
        byte[] quality = [(byte)QualityFlag.Normal, (byte)QualityFlag.Saturated, (byte)QualityFlag.Normal];

        var result = _builder.Build(
            data,
            quality,
            0,
            SampleIntervalUs,
            SimpleTimestampToX,
            SimpleUvToY,
            0,
            1_000_000);

        Assert.True(result.Segments[0].HasSaturation);
    }

    [Fact]
    public void Build_InterpolatedFlag_SegmentMarkedHasInterpolation()
    {
        float[] data = [10.0f, 20.0f, 30.0f];
        byte[] quality = [(byte)QualityFlag.Normal, (byte)QualityFlag.Interpolated, (byte)QualityFlag.Normal];

        var result = _builder.Build(
            data,
            quality,
            0,
            SampleIntervalUs,
            SimpleTimestampToX,
            SimpleUvToY,
            0,
            1_000_000);

        Assert.True(result.Segments[0].HasInterpolation);
    }

    // ============================================
    // 可见范围测试
    // ============================================

    [Fact]
    public void Build_OutOfVisibleRange_Excluded()
    {
        float[] data = [10.0f, 20.0f, 30.0f, 40.0f, 50.0f];
        byte[] quality = new byte[5];

        // 只显示中间 3 个点的时间范围
        long visibleStart = 1 * SampleIntervalUs;
        long visibleEnd = 3 * SampleIntervalUs;

        var result = _builder.Build(
            data,
            quality,
            0,
            SampleIntervalUs,
            SimpleTimestampToX,
            SimpleUvToY,
            visibleStart,
            visibleEnd);

        // 应该只有 3 个点（索引 1, 2, 3）
        Assert.Equal(3, result.Points.Length);
    }

    // ============================================
    // 坐标转换测试
    // ============================================

    [Fact]
    public void Build_CorrectXCoordinates()
    {
        float[] data = [10.0f, 20.0f, 30.0f];
        byte[] quality = new byte[3];

        var result = _builder.Build(
            data,
            quality,
            0,
            SampleIntervalUs,
            SimpleTimestampToX,
            SimpleUvToY,
            0,
            1_000_000);

        // 验证 X 坐标
        Assert.Equal(0.0f, result.Points[0].X, Tolerance);
        Assert.Equal(SampleIntervalUs / 1000.0f, result.Points[1].X, Tolerance);
        Assert.Equal(2 * SampleIntervalUs / 1000.0f, result.Points[2].X, Tolerance);
    }

    [Fact]
    public void Build_CorrectYCoordinates()
    {
        float[] data = [0.0f, 50.0f, 100.0f];
        byte[] quality = new byte[3];

        var result = _builder.Build(
            data,
            quality,
            0,
            SampleIntervalUs,
            SimpleTimestampToX,
            SimpleUvToY,
            0,
            1_000_000);

        // 验证 Y 坐标 (500 - uv * 2)
        Assert.Equal(500.0f, result.Points[0].Y, Tolerance);
        Assert.Equal(400.0f, result.Points[1].Y, Tolerance);
        Assert.Equal(300.0f, result.Points[2].Y, Tolerance);
    }

    // ============================================
    // 间隙信息测试
    // ============================================

    [Fact]
    public void Build_GapInfo_CorrectCoordinates()
    {
        float[] data = [10.0f, 20.0f];
        byte[] quality = new byte[2];

        // 使用大间隔触发间隙
        long largeInterval = MaxGapUs + 1000;

        var result = _builder.Build(
            data,
            quality,
            0,
            largeInterval,
            SimpleTimestampToX,
            SimpleUvToY,
            0,
            10_000_000);

        Assert.Single(result.Gaps);
        Assert.Equal(0.0f, result.Gaps[0].StartX, Tolerance);
        Assert.True(result.Gaps[0].EndX > result.Gaps[0].StartX);
    }

    // ============================================
    // 大数据测试
    // ============================================

    [Fact]
    public void Build_LargeDataset_HandledCorrectly()
    {
        int count = 10000;
        float[] data = new float[count];
        byte[] quality = new byte[count];

        for (int i = 0; i < count; i++)
        {
            data[i] = (float)Math.Sin(i * 0.1) * 100.0f;
        }

        var result = _builder.Build(
            data,
            quality,
            0,
            SampleIntervalUs,
            SimpleTimestampToX,
            SimpleUvToY,
            0,
            100_000_000);

        Assert.Equal(count, result.Points.Length);
        Assert.Single(result.Segments);
    }
}
