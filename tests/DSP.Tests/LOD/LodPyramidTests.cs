// LodPyramidTests.cs
// AT-12 验收测试: LOD 金字塔

using Xunit;
using Neo.DSP.LOD;

namespace Neo.DSP.Tests.LOD;

/// <summary>
/// AT-12 LOD 金字塔验收测试。
/// </summary>
public class LodPyramidTests
{
    private const long SampleIntervalUs = 6250; // 160 Hz

    /// <summary>
    /// L0 应返回原始数据（转为 MinMaxPair，Min=Max=原始值）。
    /// </summary>
    [Fact]
    public void L0_ReturnsOriginalData()
    {
        var pyramid = new LodPyramid(0, SampleIntervalUs);
        double[] data = [1.0, 2.0, 3.0, 4.0, 5.0];
        pyramid.AddSamples(data);

        var output = new MinMaxPair[5];
        int count = pyramid.GetLevel(0, 0, SampleIntervalUs * 5, output);

        Assert.Equal(5, count);
        for (int i = 0; i < 5; i++)
        {
            Assert.Equal(data[i], output[i].Min);
            Assert.Equal(data[i], output[i].Max);
        }
    }

    /// <summary>
    /// L1 应 2x 降采样并保留正确的 Min/Max。
    /// </summary>
    [Fact]
    public void L1_DownsamplesBy2_CorrectMinMax()
    {
        var pyramid = new LodPyramid(0, SampleIntervalUs);
        double[] data = [1.0, 5.0, 2.0, 8.0, 3.0, 7.0, 0.0, 9.0];
        pyramid.AddSamples(data);

        var output = new MinMaxPair[4];
        int count = pyramid.GetLevel(1, 0, SampleIntervalUs * 8, output);

        Assert.Equal(4, count);

        // Pair 0: [1.0, 5.0] → min=1.0, max=5.0
        Assert.Equal(1.0, output[0].Min);
        Assert.Equal(5.0, output[0].Max);

        // Pair 1: [2.0, 8.0] → min=2.0, max=8.0
        Assert.Equal(2.0, output[1].Min);
        Assert.Equal(8.0, output[1].Max);

        // Pair 2: [3.0, 7.0] → min=3.0, max=7.0
        Assert.Equal(3.0, output[2].Min);
        Assert.Equal(7.0, output[2].Max);

        // Pair 3: [0.0, 9.0] → min=0.0, max=9.0
        Assert.Equal(0.0, output[3].Min);
        Assert.Equal(9.0, output[3].Max);
    }

    /// <summary>
    /// 高层级应正确降采样（合并 MinMaxPair）。
    /// </summary>
    [Theory]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    public void HigherLevels_DownsampleCorrectly(int level)
    {
        var pyramid = new LodPyramid(0, SampleIntervalUs);

        // Generate enough data: 2^level samples per entry
        int samplesPerEntry = 1 << level;
        int totalSamples = samplesPerEntry * 4; // 4 entries at target level

        var data = new double[totalSamples];
        for (int i = 0; i < totalSamples; i++)
            data[i] = Math.Sin(2.0 * Math.PI * i / totalSamples) * 100;

        pyramid.AddSamples(data);

        int expectedCount = totalSamples / samplesPerEntry;
        Assert.Equal(expectedCount, pyramid.GetLevelCount(level));

        var output = new MinMaxPair[expectedCount];
        int count = pyramid.GetLevel(level, 0, SampleIntervalUs * totalSamples, output);

        Assert.Equal(expectedCount, count);

        // Each entry should contain min/max of its 2^level source samples
        for (int e = 0; e < count; e++)
        {
            double expectedMin = double.MaxValue;
            double expectedMax = double.MinValue;
            for (int s = e * samplesPerEntry; s < (e + 1) * samplesPerEntry; s++)
            {
                if (data[s] < expectedMin) expectedMin = data[s];
                if (data[s] > expectedMax) expectedMax = data[s];
            }

            Assert.Equal(expectedMin, output[e].Min, 10);
            Assert.Equal(expectedMax, output[e].Max, 10);
        }
    }

    /// <summary>
    /// 正尖峰和负尖峰应在所有层级中保留。
    /// </summary>
    [Fact]
    public void SpikePreservation_PositiveAndNegative()
    {
        var pyramid = new LodPyramid(0, SampleIntervalUs);

        // 100 zero samples with spikes at positions 25 and 75
        var data = new double[128]; // 2^7 for clean pyramid levels
        data[25] = 500.0;   // positive spike
        data[75] = -300.0;  // negative spike

        pyramid.AddSamples(data);

        // Check each level preserves the spike
        for (int level = 0; level <= 5; level++)
        {
            int entryCount = pyramid.GetLevelCount(level);
            var output = new MinMaxPair[entryCount];
            pyramid.GetLevel(level, 0, SampleIntervalUs * 128, output);

            double globalMin = double.MaxValue;
            double globalMax = double.MinValue;
            for (int i = 0; i < entryCount; i++)
            {
                if (output[i].Min < globalMin) globalMin = output[i].Min;
                if (output[i].Max > globalMax) globalMax = output[i].Max;
            }

            Assert.True(globalMax == 500.0, $"Level {level} should preserve positive spike, got {globalMax}");
            Assert.True(globalMin == -300.0, $"Level {level} should preserve negative spike, got {globalMin}");
        }
    }

    /// <summary>
    /// 按时间范围查询应返回正确子集。
    /// </summary>
    [Fact]
    public void QueryByTimeRange_CorrectSubset()
    {
        var pyramid = new LodPyramid(0, SampleIntervalUs);

        var data = new double[100];
        for (int i = 0; i < 100; i++)
            data[i] = i;

        pyramid.AddSamples(data);

        // Query samples 20-40 at L0
        long startUs = 20 * SampleIntervalUs;
        long endUs = 40 * SampleIntervalUs;

        var output = new MinMaxPair[20];
        int count = pyramid.GetLevel(0, startUs, endUs, output);

        Assert.Equal(20, count);
        Assert.Equal(20.0, output[0].Min);
        Assert.Equal(39.0, output[count - 1].Min);
    }

    /// <summary>
    /// SelectLevel 应根据视口宽度选择最优密度。
    /// </summary>
    [Fact]
    public void SelectLevel_PicksOptimalDensity()
    {
        var pyramid = new LodPyramid(0, SampleIntervalUs);

        // Add enough data for meaningful selection
        var data = new double[16000]; // 100 seconds
        pyramid.AddSamples(data);

        // Short time range, wide viewport → L0
        int level0 = pyramid.SelectLevel(SampleIntervalUs * 100, 1000); // ~100 samples in 1000px
        Assert.Equal(0, level0);

        // Long time range, narrow viewport → higher level
        int levelHigh = pyramid.SelectLevel(SampleIntervalUs * 16000, 200); // 16000 samples in 200px
        Assert.True(levelHigh > 0, $"Expected level > 0, got {levelHigh}");
    }

    /// <summary>
    /// 增量构建结果应与一次性全量构建一致。
    /// </summary>
    [Fact]
    public void IncrementalBuild_MatchesFullBuild()
    {
        var data = new double[256];
        var rng = new Random(42);
        for (int i = 0; i < 256; i++)
            data[i] = rng.NextDouble() * 200 - 100;

        // Full build
        var fullPyramid = new LodPyramid(0, SampleIntervalUs);
        fullPyramid.AddSamples(data);

        // Incremental build (various chunk sizes)
        var incPyramid = new LodPyramid(0, SampleIntervalUs);
        int offset = 0;
        int[] chunks = [17, 33, 7, 64, 1, 128, 6]; // intentionally uneven
        foreach (int chunk in chunks)
        {
            int take = Math.Min(chunk, 256 - offset);
            if (take <= 0) break;
            incPyramid.AddSamples(data.AsSpan(offset, take));
            offset += take;
        }

        // Verify all levels match
        for (int level = 0; level <= 5; level++)
        {
            int fullCount = fullPyramid.GetLevelCount(level);
            int incCount = incPyramid.GetLevelCount(level);
            Assert.True(fullCount == incCount, $"Level {level} count mismatch: full={fullCount}, inc={incCount}");

            if (fullCount == 0) continue;

            var fullOut = new MinMaxPair[fullCount];
            var incOut = new MinMaxPair[incCount];
            fullPyramid.GetLevel(level, 0, SampleIntervalUs * 256, fullOut);
            incPyramid.GetLevel(level, 0, SampleIntervalUs * 256, incOut);

            for (int i = 0; i < fullCount; i++)
            {
                Assert.Equal(fullOut[i].Min, incOut[i].Min, 10);
                Assert.Equal(fullOut[i].Max, incOut[i].Max, 10);
            }
        }
    }

    /// <summary>
    /// 空金字塔查询应返回空结果。
    /// </summary>
    [Fact]
    public void EmptyPyramid_ReturnsEmpty()
    {
        var pyramid = new LodPyramid(0, SampleIntervalUs);

        Assert.Equal(0, pyramid.TotalSamples);

        var output = new MinMaxPair[10];
        int count = pyramid.GetLevel(0, 0, 1_000_000, output);
        Assert.Equal(0, count);
    }

    /// <summary>
    /// AT-12 验收: 写入 1 小时 EEG 数据 (160 Hz = 576,000 样本)，
    /// 任意时间范围查询 &lt; 10 ms。
    /// </summary>
    /// <remarks>
    /// 依据: ACCEPTANCE_TESTS.md AT-12
    /// - 测试步骤 1: 写入 1 小时测试数据 (160 Hz)
    /// - 验收标准: 查询时间 &lt; 10 ms
    ///
    /// 实际查询场景: SelectLevel 根据视口选择合适层级，
    /// 返回数据量远小于 L0 原始样本数。
    /// </remarks>
    [Fact]
    public void AT12_OneHourData_QueryUnder10ms()
    {
        const int OneHourSamples = 160 * 3600; // 576,000
        var pyramid = new LodPyramid(0, SampleIntervalUs);

        // 写入 1 小时数据（分批添加，模拟增量构建）
        var rng = new Random(42);
        const int BatchSize = 1600; // 10 seconds per batch
        var batch = new double[BatchSize];
        for (int offset = 0; offset < OneHourSamples; offset += BatchSize)
        {
            int count = Math.Min(BatchSize, OneHourSamples - offset);
            for (int i = 0; i < count; i++)
                batch[i] = rng.NextDouble() * 200 - 100;
            pyramid.AddSamples(batch.AsSpan(0, count));
        }

        Assert.Equal(OneHourSamples, pyramid.TotalSamples);

        long totalDurationUs = (long)OneHourSamples * SampleIntervalUs;
        var sw = new System.Diagnostics.Stopwatch();

        // --- 场景 1: SelectLevel + 自动选级查询（1 小时全程，1920px 视口）---
        int autoLevel = pyramid.SelectLevel(totalDurationUs, 1920);
        Assert.True(autoLevel > 0, $"1-hour range should select LOD > 0, got {autoLevel}");

        int autoLevelCount = pyramid.GetLevelCount(autoLevel);
        var autoOutput = new MinMaxPair[autoLevelCount];

        sw.Restart();
        for (int iter = 0; iter < 100; iter++)
            pyramid.GetLevel(autoLevel, 0, totalDurationUs, autoOutput);
        sw.Stop();
        double autoAvgMs = sw.Elapsed.TotalMilliseconds / 100.0;
        Assert.True(autoAvgMs < 10.0,
            $"AT-12: Auto-level({autoLevel}) full-hour query {autoAvgMs:F3}ms exceeds 10ms");

        // --- 场景 2: 各 LOD 层级子范围查询（中间 10 分钟）---
        long subStart = totalDurationUs / 3;
        long subEnd = subStart + 10 * 60 * 1_000_000L; // 10 minutes
        for (int level = 1; level <= 5; level++)
        {
            int levelCount = pyramid.GetLevelCount(level);
            if (levelCount == 0) continue;

            var output = new MinMaxPair[levelCount];
            sw.Restart();
            for (int iter = 0; iter < 100; iter++)
                pyramid.GetLevel(level, subStart, subEnd, output);
            sw.Stop();
            double avgMs = sw.Elapsed.TotalMilliseconds / 100.0;
            Assert.True(avgMs < 10.0,
                $"AT-12: Level-{level} 10min subrange query {avgMs:F3}ms exceeds 10ms");
        }

        // --- 场景 3: L0 子范围查询（10 秒窗口 = 1600 样本）---
        long l0Start = totalDurationUs / 2;
        long l0End = l0Start + 10 * 1_000_000L; // 10 seconds
        var l0Output = new MinMaxPair[1600];

        sw.Restart();
        for (int iter = 0; iter < 100; iter++)
            pyramid.GetLevel(0, l0Start, l0End, l0Output);
        sw.Stop();
        double l0AvgMs = sw.Elapsed.TotalMilliseconds / 100.0;
        Assert.True(l0AvgMs < 10.0,
            $"AT-12: L0 10sec subrange query {l0AvgMs:F3}ms exceeds 10ms");
    }

    /// <summary>
    /// MultiChannelLodPyramid 基本功能验证。
    /// </summary>
    [Fact]
    public void MultiChannel_BasicFunctionality()
    {
        var multi = new MultiChannelLodPyramid(4, 0, SampleIntervalUs);

        for (int ch = 0; ch < 4; ch++)
        {
            var data = new double[16];
            for (int i = 0; i < 16; i++)
                data[i] = (ch + 1) * 10.0 + i;
            multi.AddSamples(ch, data);
        }

        // Verify each channel has independent data
        for (int ch = 0; ch < 4; ch++)
        {
            var output = new MinMaxPair[16];
            int count = multi.GetLevel(ch, 0, 0, SampleIntervalUs * 16, output);
            Assert.Equal(16, count);
            Assert.Equal((ch + 1) * 10.0, output[0].Min);
        }
    }
}
