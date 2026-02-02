// PlaybackClockTests.cs
// Unit tests for PlaybackClock - S3-03

using Neo.Playback;
using Xunit;

namespace Neo.Playback.Tests;

public class PlaybackClockTests
{
    [Fact]
    public void InitialState_IsNotRunning_PositionZero()
    {
        var clock = new PlaybackClock();

        Assert.False(clock.IsRunning);
        Assert.Equal(0, clock.GetCurrentUs());
    }

    [Fact]
    public void Start_SetsRunning()
    {
        var clock = new PlaybackClock();
        clock.Start();

        Assert.True(clock.IsRunning);
    }

    [Fact]
    public void Pause_StopsRunning()
    {
        var clock = new PlaybackClock();
        clock.Start();
        clock.Pause();

        Assert.False(clock.IsRunning);
    }

    [Fact]
    public void Pause_FreezesPosition()
    {
        var clock = new PlaybackClock();
        clock.Start();
        Thread.Sleep(50);
        clock.Pause();

        long pos1 = clock.GetCurrentUs();
        Thread.Sleep(50);
        long pos2 = clock.GetCurrentUs();

        Assert.Equal(pos1, pos2);
        Assert.True(pos1 > 0, "Position should have advanced before pause");
    }

    [Fact]
    public void SeekTo_SetsPosition()
    {
        var clock = new PlaybackClock();
        clock.SeekTo(5_000_000); // 5 seconds

        Assert.Equal(5_000_000, clock.GetCurrentUs());
    }

    [Fact]
    public void SeekTo_WhileRunning_ContinuesFromNewPosition()
    {
        var clock = new PlaybackClock();
        clock.Start();
        Thread.Sleep(20);

        clock.SeekTo(10_000_000); // 10 seconds
        long pos = clock.GetCurrentUs();

        // Should be at or slightly above 10 seconds
        Assert.True(pos >= 10_000_000, $"Expected >= 10000000, got {pos}");
        Assert.True(pos < 10_500_000, $"Expected < 10500000, got {pos}");
    }

    [Fact]
    public void Rate_DefaultIsOne()
    {
        var clock = new PlaybackClock();
        Assert.Equal(1.0, clock.Rate);
    }

    [Fact]
    public void Rate_HalfSpeed_AdvancesHalfAsQuickly()
    {
        var clock1 = new PlaybackClock();
        var clock2 = new PlaybackClock();

        clock1.Rate = 1.0;
        clock2.Rate = 0.5;

        clock1.Start();
        clock2.Start();
        Thread.Sleep(200);
        clock1.Pause();
        clock2.Pause();

        long pos1 = clock1.GetCurrentUs();
        long pos2 = clock2.GetCurrentUs();

        // clock2 should be roughly half of clock1
        double ratio = (double)pos2 / pos1;
        Assert.True(ratio > 0.3 && ratio < 0.7,
            $"Expected ratio ~0.5, got {ratio:F3} (pos1={pos1}, pos2={pos2})");
    }

    [Fact]
    public void Reset_ClearsEverything()
    {
        var clock = new PlaybackClock();
        clock.SeekTo(5_000_000);
        clock.Start();
        Thread.Sleep(20);

        clock.Reset();

        Assert.False(clock.IsRunning);
        Assert.Equal(0, clock.GetCurrentUs());
    }

    [Fact]
    public void Start_AfterPause_ResumesFromPausedPosition()
    {
        var clock = new PlaybackClock();
        clock.Start();
        Thread.Sleep(50);
        clock.Pause();

        long pausedPos = clock.GetCurrentUs();

        clock.Start();
        Thread.Sleep(50);
        clock.Pause();

        long resumedPos = clock.GetCurrentUs();

        Assert.True(resumedPos > pausedPos,
            $"Expected resumed > paused, got {resumedPos} vs {pausedPos}");
    }

    [Fact]
    public void DoubleStart_IsIdempotent()
    {
        var clock = new PlaybackClock();
        clock.Start();
        Thread.Sleep(20);
        long pos1 = clock.GetCurrentUs();

        clock.Start(); // second start should be no-op
        long pos2 = clock.GetCurrentUs();

        // pos2 should be >= pos1 (clock still running, not reset)
        Assert.True(pos2 >= pos1);
    }

    [Fact]
    public void DoublePause_IsIdempotent()
    {
        var clock = new PlaybackClock();
        clock.Start();
        Thread.Sleep(20);
        clock.Pause();
        long pos1 = clock.GetCurrentUs();

        clock.Pause(); // second pause should be no-op
        long pos2 = clock.GetCurrentUs();

        Assert.Equal(pos1, pos2);
    }
}
