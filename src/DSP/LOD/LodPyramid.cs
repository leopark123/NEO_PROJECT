// LodPyramid.cs
// 单通道 LOD 金字塔实现 - AT-12
//
// 依据: ARCHITECTURE.md §4.3 (LOD 金字塔)
//       AT-12 审计缺口: 多层 Min/Max 降采样

namespace Neo.DSP.LOD;

/// <summary>
/// 单通道 LOD 金字塔（增量构建，线程安全）。
/// </summary>
/// <remarks>
/// 依据: AT-12 LOD 金字塔
///
/// L0: List&lt;double&gt; — 原始值 (8 bytes/sample)
/// L1+: List&lt;MinMaxPair&gt; — 2x 降采样 (16 bytes/entry)
///
/// Spike 保护: MinMaxPair.Merge 保留全局极值，
/// 单样本尖峰在所有层级的 Min 或 Max 中保留。
///
/// 线程安全: lock(_syncRoot) 保护 AddSamples 和 GetLevel。
/// </remarks>
public sealed class LodPyramid : ILodPyramid
{
    private const int MaxLevels = 10; // 2^10 = 1024x max downsampling

    private readonly object _syncRoot = new();
    private readonly long _firstTimestampUs;
    private readonly long _sampleIntervalUs;

    // L0: 原始数据
    private readonly List<double> _level0 = new();

    // L1+: MinMaxPair 降采样层
    private readonly List<MinMaxPair>[] _levels;

    // 每层待合并的暂存条目
    private readonly MinMaxPair?[] _pending;

    public int TotalSamples
    {
        get { lock (_syncRoot) return _level0.Count; }
    }

    public int LevelCount => MaxLevels + 1;

    public long FirstTimestampUs => _firstTimestampUs;

    public long SampleIntervalUs => _sampleIntervalUs;

    /// <summary>
    /// 创建 LOD 金字塔。
    /// </summary>
    /// <param name="firstTimestampUs">第一个样本的时间戳（微秒）</param>
    /// <param name="sampleIntervalUs">采样间隔（微秒），160Hz = 6250</param>
    public LodPyramid(long firstTimestampUs, long sampleIntervalUs = 6250)
    {
        _firstTimestampUs = firstTimestampUs;
        _sampleIntervalUs = sampleIntervalUs;

        _levels = new List<MinMaxPair>[MaxLevels];
        _pending = new MinMaxPair?[MaxLevels];

        for (int i = 0; i < MaxLevels; i++)
            _levels[i] = new List<MinMaxPair>();
    }

    public void AddSample(double value)
    {
        lock (_syncRoot)
        {
            _level0.Add(value);
            PropagateUp(MinMaxPair.FromSingle(value), 0);
        }
    }

    public void AddSamples(ReadOnlySpan<double> values)
    {
        lock (_syncRoot)
        {
            for (int i = 0; i < values.Length; i++)
            {
                _level0.Add(values[i]);
                PropagateUp(MinMaxPair.FromSingle(values[i]), 0);
            }
        }
    }

    /// <summary>
    /// 从 L0 向上逐级传播新条目。
    /// </summary>
    private void PropagateUp(MinMaxPair entry, int levelIndex)
    {
        if (levelIndex >= MaxLevels) return;

        if (_pending[levelIndex].HasValue)
        {
            // 已有暂存条目，合并成新条目并存入当前层
            var merged = MinMaxPair.Merge(_pending[levelIndex]!.Value, entry);
            _levels[levelIndex].Add(merged);
            _pending[levelIndex] = null;

            // 向上传播
            PropagateUp(merged, levelIndex + 1);
        }
        else
        {
            // 暂存当前条目，等待配对
            _pending[levelIndex] = entry;
        }
    }

    public int SelectLevel(long timeRangeUs, int viewWidthPixels)
    {
        if (viewWidthPixels <= 0 || timeRangeUs <= 0)
            return 0;

        long samplesInRange = timeRangeUs / _sampleIntervalUs;

        for (int level = 0; level <= MaxLevels; level++)
        {
            long pointsAtLevel = samplesInRange >> level; // / 2^level
            if (pointsAtLevel <= 0) return level;
            if (pointsAtLevel / viewWidthPixels <= 4)
                return level;
        }

        return MaxLevels;
    }

    public int GetLevel(int level, long startTimeUs, long endTimeUs, Span<MinMaxPair> output)
    {
        if (level < 0 || level > MaxLevels)
            throw new ArgumentOutOfRangeException(nameof(level));

        lock (_syncRoot)
        {
            if (_level0.Count == 0 || output.Length == 0)
                return 0;

            if (level == 0)
                return GetLevel0(startTimeUs, endTimeUs, output);

            return GetLevelN(level, startTimeUs, endTimeUs, output);
        }
    }

    public int GetLevelCount(int level)
    {
        if (level < 0 || level > MaxLevels)
            throw new ArgumentOutOfRangeException(nameof(level));

        lock (_syncRoot)
        {
            if (level == 0) return _level0.Count;
            return _levels[level - 1].Count;
        }
    }

    /// <summary>
    /// 获取 L0 数据，动态转换为 MinMaxPair。
    /// </summary>
    private int GetLevel0(long startTimeUs, long endTimeUs, Span<MinMaxPair> output)
    {
        int startIndex = TimeToIndex(startTimeUs, 0);
        int endIndex = TimeToIndex(endTimeUs, 0);

        startIndex = Math.Max(0, startIndex);
        endIndex = Math.Min(_level0.Count, endIndex);

        int count = Math.Min(endIndex - startIndex, output.Length);
        for (int i = 0; i < count; i++)
            output[i] = MinMaxPair.FromSingle(_level0[startIndex + i]);

        return count;
    }

    /// <summary>
    /// 获取 LN（N>=1）数据。
    /// </summary>
    private int GetLevelN(int level, long startTimeUs, long endTimeUs, Span<MinMaxPair> output)
    {
        var levelData = _levels[level - 1];
        if (levelData.Count == 0) return 0;

        int startIndex = TimeToIndex(startTimeUs, level);
        int endIndex = TimeToIndex(endTimeUs, level);

        startIndex = Math.Max(0, startIndex);
        endIndex = Math.Min(levelData.Count, endIndex);

        int count = Math.Min(endIndex - startIndex, output.Length);
        for (int i = 0; i < count; i++)
            output[i] = levelData[startIndex + i];

        return count;
    }

    /// <summary>
    /// 时间→索引转换。
    /// </summary>
    private int TimeToIndex(long timeUs, int level)
    {
        long elapsed = timeUs - _firstTimestampUs;
        if (elapsed < 0) return 0;

        long intervalAtLevel = _sampleIntervalUs << level; // * 2^level
        return (int)(elapsed / intervalAtLevel);
    }
}
