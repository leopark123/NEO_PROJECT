using Neo.Rendering.AEEG;
using Vortice.Mathematics;
using Xunit;

namespace Neo.Rendering.Tests.Waveform;

public sealed class AeegSemiLogVisualizerTests
{
    [Fact]
    public void GetBoundaryY_HalfHeight_WhenTopIsZero()
    {
        var area = new Rect(0, 0, 100, 200);
        float y = AeegSemiLogVisualizer.GetBoundaryY(area);
        Assert.Equal(100f, y, 0.01f);
    }

    [Fact]
    public void GetBoundaryY_RespectsTopOffset()
    {
        var area = new Rect(10, 20, 100, 300);
        float y = AeegSemiLogVisualizer.GetBoundaryY(area);
        Assert.Equal(170f, y, 0.01f); // 20 + 300/2
    }

    [Fact]
    public void DefaultOptions_EnableBoundaryAndBackground()
    {
        var options = AeegSemiLogVisualizer.VisualizationOptions.Default;
        Assert.True(options.ShowRegionBackground);
        Assert.True(options.ShowBoundaryLine);
    }
}
