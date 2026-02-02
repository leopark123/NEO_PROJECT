// MultiChannelLodPyramid.cs
// 多通道 LOD 金字塔包装器 - AT-12
//
// 依据: ARCHITECTURE.md §4.3 (LOD 金字塔)

namespace Neo.DSP.LOD;

/// <summary>
/// 多通道 LOD 金字塔包装器。
/// </summary>
/// <remarks>
/// 依据: AT-12 LOD 金字塔
/// 为每个 EEG 通道维护独立的 LOD 金字塔。
/// </remarks>
public sealed class MultiChannelLodPyramid
{
    private readonly LodPyramid[] _channels;

    /// <summary>通道数</summary>
    public int ChannelCount => _channels.Length;

    /// <summary>
    /// 创建多通道 LOD 金字塔。
    /// </summary>
    /// <param name="channelCount">通道数（默认 4）</param>
    /// <param name="firstTimestampUs">第一个样本的时间戳</param>
    /// <param name="sampleIntervalUs">采样间隔</param>
    public MultiChannelLodPyramid(int channelCount = 4, long firstTimestampUs = 0, long sampleIntervalUs = 6250)
    {
        _channels = new LodPyramid[channelCount];
        for (int i = 0; i < channelCount; i++)
            _channels[i] = new LodPyramid(firstTimestampUs, sampleIntervalUs);
    }

    /// <summary>
    /// 获取指定通道的金字塔。
    /// </summary>
    public LodPyramid GetChannel(int channelIndex)
    {
        if (channelIndex < 0 || channelIndex >= _channels.Length)
            throw new ArgumentOutOfRangeException(nameof(channelIndex));

        return _channels[channelIndex];
    }

    /// <summary>
    /// 向指定通道添加样本。
    /// </summary>
    public void AddSample(int channelIndex, double value)
    {
        GetChannel(channelIndex).AddSample(value);
    }

    /// <summary>
    /// 向指定通道批量添加样本。
    /// </summary>
    public void AddSamples(int channelIndex, ReadOnlySpan<double> values)
    {
        GetChannel(channelIndex).AddSamples(values);
    }

    /// <summary>
    /// 根据视口参数为指定通道选择最优层级。
    /// </summary>
    public int SelectLevel(int channelIndex, long timeRangeUs, int viewWidthPixels)
    {
        return GetChannel(channelIndex).SelectLevel(timeRangeUs, viewWidthPixels);
    }

    /// <summary>
    /// 获取指定通道和层级的数据。
    /// </summary>
    public int GetLevel(int channelIndex, int level, long startTimeUs, long endTimeUs, Span<MinMaxPair> output)
    {
        return GetChannel(channelIndex).GetLevel(level, startTimeUs, endTimeUs, output);
    }
}
