# Playback Sync API Handoff Document

> **Version**: v1.0
> **Author**: Claude Code
> **Date**: 2026-01-29
> **Task**: S3-03 Video + EEG Synchronized Playback

---

## 1. Overview

Unified timeline service for synchronized playback of EEG and Video streams. Provides a pausable/seekable virtual clock, EEG replay from ring buffer, and multi-stream coordination with ±100ms sync tolerance.

---

## 2. Public Interfaces

### 2.1 Core Types (Neo.Core)

```csharp
namespace Neo.Core;

public enum PlaybackState
{
    Live,       // Real-time monitoring
    Playing,    // Playing back recorded data
    Paused,     // Paused at current position
    Seeking     // Seeking to new position
}

public sealed class TimelinePositionEventArgs : EventArgs
{
    public required long PositionUs { get; init; }
    public required PlaybackState State { get; init; }
}

public interface ITimelineService
{
    long CurrentPositionUs { get; }
    DateTimeOffset SessionStart { get; }
    PlaybackState State { get; }
    double PlaybackRate { get; set; }
    void SeekTo(long positionUs);
    void Play();
    void Pause();
    event EventHandler<TimelinePositionEventArgs>? PositionChanged;
}
```

### 2.2 Playback Module (Neo.Playback)

```csharp
namespace Neo.Playback;

// Virtual clock: pausable, seekable, rate-adjustable
public sealed class PlaybackClock
{
    public bool IsRunning { get; }
    public double Rate { get; set; }        // 1.0 = real-time
    public long GetCurrentUs();
    public void Start();
    public void Pause();
    public void SeekTo(long positionUs);
    public void Reset();
}

// EEG replay from ring buffer
public sealed class EegPlaybackSource : ITimeSeriesSource<EegSample>, IDisposable
{
    public EegPlaybackSource(EegRingBuffer buffer, PlaybackClock clock);
    public void Start();
    public void Stop();
    public void NotifySeek(long newPositionUs);
    // Inherits: SampleReceived, SampleRate, ChannelCount
}

// Multi-stream coordinator (implements ITimelineService)
public sealed class MultiStreamCoordinator : ITimelineService, IDisposable
{
    public MultiStreamCoordinator(
        PlaybackClock clock,
        EegPlaybackSource? eegSource = null,
        VideoPlaybackSource? videoSource = null);
    public void SetSessionStart(DateTimeOffset sessionStart);
    // Inherits: Play, Pause, SeekTo, CurrentPositionUs, State, etc.
}
```

---

## 3. Threading Model

| Thread | Role | Owner |
|--------|------|-------|
| EegPlayback | Reads EegRingBuffer, fires SampleReceived | EegPlaybackSource |
| PlaybackTick | 20Hz position update events | MultiStreamCoordinator |
| Caller thread | Play/Pause/SeekTo commands | Any |

**Thread safety**: PlaybackClock uses lock-based synchronization. MultiStreamCoordinator coordinates via shared clock.

---

## 4. Timestamp Semantics

| Component | Timestamp Source | Unit | Domain |
|-----------|-----------------|------|--------|
| PlaybackClock | Virtual (wall-clock scaled) | μs | Host |
| EegPlaybackSource | Original EegSample.TimestampUs | μs | Host |
| VideoPlaybackSource | .tsidx index (Host timestamps) | μs | Host |
| MultiStreamCoordinator | PlaybackClock.GetCurrentUs() | μs | Host |

**Iron Law 2 compliance**: No timestamps are fabricated. EEG samples come from the original ring buffer. Video timestamps come from the .tsidx index. The PlaybackClock provides a virtual time reference but never stamps data.

---

## 5. Sync Strategy

```
PlaybackClock (master time)
    │
    ├── EegPlaybackSource
    │     └── Queries EegRingBuffer.GetRange(lastEmitted+1, currentUs)
    │         → emits original EegSample with original TimestampUs
    │
    ├── VideoPlaybackSource (existing S3-02)
    │     └── SeekToTimestamp(targetUs) via .tsidx binary search
    │         → emits VideoFrame with original TimestampUs from index
    │
    └── MultiStreamCoordinator
          └── 20Hz tick → PositionChanged events
              → UI updates time display / scrolls waveform view
```

**Sync tolerance**: ±100ms (AT-17). Validated by `SyncTolerance_EegTimestampsWithin100ms` test.

---

## 6. Seek Flow

```
SeekTo(positionUs)
    │
    ├── 1. Set State = Seeking
    ├── 2. clock.SeekTo(positionUs)
    ├── 3. eegSource.NotifySeek(positionUs)  → reset emission cursor
    ├── 4. videoSource.SeekToTimestamp(positionUs)  → MF seek via .tsidx
    ├── 5. Restore previous State (Playing or Paused)
    └── 6. Fire PositionChanged
```

---

## 7. Data Contracts

| Property | Value |
|----------|-------|
| EEG sample rate | 160 Hz |
| EEG channels | 4 |
| Video frame rate | 15-30 fps |
| Position update rate | 20 Hz |
| Sync tolerance | ±100ms |
| Clock rate range | 0.1x - 10x |
| Timestamp unit | μs (int64) |
| Timestamp domain | Host monotonic clock |

---

## 8. Usage Example

```csharp
// Setup
var clock = new PlaybackClock();
var eegBuffer = EegRingBuffer.CreateForSeconds(600); // 10 min
// ... fill buffer with recorded data ...

var eegPlayback = new EegPlaybackSource(eegBuffer, clock);
var videoPlayback = new VideoPlaybackSource(@"C:\recordings\session.mp4");
videoPlayback.LoadIndex();

var coordinator = new MultiStreamCoordinator(clock, eegPlayback, videoPlayback);
coordinator.PositionChanged += (_, e) => UpdateTimeDisplay(e.PositionUs);

// Playback
coordinator.Play();
// ... user watches replay ...

coordinator.SeekTo(120_000_000); // Jump to 2 minutes
coordinator.Pause();
coordinator.PlaybackRate = 2.0;  // 2x speed
coordinator.Play();

// Cleanup
coordinator.Dispose();
videoPlayback.Dispose();
```

---

## 9. Graceful Degradation

| Scenario | Behavior |
|----------|----------|
| No EEG data in buffer | EegPlaybackSource.Start() logs warning, no events |
| No video file | MultiStreamCoordinator works with EEG only |
| No .tsidx index | VideoPlaybackSource refuses to start (Iron Law) |
| Video seek failure | Logs warning, EEG continues normally |
| Empty coordinator (no sources) | Clock runs, position events fire, no data |

---

## 10. Test Coverage

| Test | Type | Validates |
|------|------|-----------|
| PlaybackClock: 12 tests | Unit | Start/Pause/Seek/Rate/Reset/Idempotency |
| MultiStreamCoordinator: 18 tests | Integration | State transitions, EEG emission, seek, sync tolerance, gap markers |
| VideoIndexTests: 10 tests | Unit+Integration | .tsidx binary format, load/parse, validation, joint EEG+Video |
| SyncTolerance_EegTimestampsWithin100ms | AT-17 | ±100ms sync between clock and EEG timestamps |
| SyncMonitor_CountsViolations | AT-17 | Runtime sync drift monitoring via SyncToleranceUs |
| EegPlayback_GapMarker_EmittedForMissingData | IL-5 | Gap markers with QualityFlag.Missing + NaN values |
| JointPlayback_EegAndVideoIndex_CoordinatorManagesBoth | Integration | Coordinator with real EEG buffer + real .tsidx index |
| JointPlayback_SeekBeyondEegRange_VideoIndexStillValid | Integration | Seek with mismatched stream durations |

**Total: 40 tests, all passing.**

---

## 11. Dependencies

| Module | Interface | Usage |
|--------|-----------|-------|
| Neo.Core | ITimeSeriesSource<T>, PlaybackState, ITimelineService | Interfaces & types |
| Neo.Infrastructure | EegRingBuffer | EEG data storage |
| Neo.Video | VideoPlaybackSource | Video playback with .tsidx seek |

---

## 12. Known Limitations

1. EegPlaybackSource uses Thread.Sleep for rate control (not high-precision timer)
2. PlaybackClock lock-based (acceptable for single coordinator pattern)
3. No NIRS playback source yet (S3-00 blocked on protocol spec)
4. Video playback rate fixed to recorded FPS (no variable speed video)
5. EegRingBuffer.GetRange is O(n) scan — acceptable for current buffer sizes

---

## 13. Change History

| Version | Date | Changes |
|---------|------|---------|
| v1.0 | 2026-01-29 | Initial — PlaybackClock, EegPlaybackSource, MultiStreamCoordinator |
| v1.1 | 2026-01-29 | Audit fix: SyncToleranceUs runtime monitoring, gap markers (IL-5), O(N) justification, +4 tests |
| v1.2 | 2026-01-29 | Audit fix: VideoIndexTests with actual .tsidx binary files, joint EEG+Video integration tests, DoD evidence file |

---

**End of Document**
