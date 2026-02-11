using Neo.Rendering.Layers;
using Xunit;

namespace Neo.Rendering.Tests.Layers;

public sealed class TimeAxisRendererTests
{
    [Fact]
    public void SelectLabelIntervalUs_ThreeHours_ReturnsOneHour()
    {
        long durationUs = 3L * 60 * 60 * 1_000_000;
        long intervalUs = TimeAxisRenderer.SelectLabelIntervalUs(durationUs);
        Assert.Equal(60L * 60 * 1_000_000, intervalUs);
    }

    [Fact]
    public void SelectLabelIntervalUs_TwelveHours_ReturnsTwoHours()
    {
        long durationUs = 12L * 60 * 60 * 1_000_000;
        long intervalUs = TimeAxisRenderer.SelectLabelIntervalUs(durationUs);
        Assert.Equal(2L * 60 * 60 * 1_000_000, intervalUs);
    }

    [Fact]
    public void AlignToIntervalStart_FloorsToBoundary()
    {
        long intervalUs = 60L * 1_000_000;
        long tsUs = 7L * 60 * 1_000_000 + 23L * 1_000_000; // 00:07:23
        long alignedUs = TimeAxisRenderer.AlignToIntervalStart(tsUs, intervalUs);
        Assert.Equal(7L * 60 * 1_000_000, alignedUs);
    }

    [Fact]
    public void FormatTimestamp_Returns_HH_mm_ss()
    {
        long tsUs = 1L * 3600 * 1_000_000 + 2L * 60 * 1_000_000 + 3L * 1_000_000;
        Assert.Equal("01:02:03", TimeAxisRenderer.FormatTimestamp(tsUs));
    }
}
