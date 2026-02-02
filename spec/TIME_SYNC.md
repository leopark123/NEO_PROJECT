# ⏱️ TIME_SYNC.md - 时间同步策略

> **版本**: v1.1  
> **状态**: ⚠️ MVP临时方案（待设备信息确认后升级）  
> **更新日期**: 2025-01-21  
> **v1.1变更**: 补充EEG时间戳策略（设备无硬件时间戳）、Monotonic→WallClock映射

---

## 1. 概述

本文档定义 EEG / NIRS / Video 三个数据源的时间对齐策略。

**当前状态**：视频设备信息尚未确定，采用"主机时间对齐"临时方案。

---

## 2. 时钟域定义（ClockDomain）

```csharp
public enum ClockDomain
{
    /// <summary>设备硬件时钟（最高精度）</summary>
    Device,
    
    /// <summary>主机单调时钟（Stopwatch/QueryPerformanceCounter）</summary>
    Host,
    
    /// <summary>时钟来源未知或不可靠</summary>
    Unknown
}
```

### 各数据源时钟域

| 数据源 | 当前ClockDomain | 说明 |
|--------|-----------------|------|
| EEG | Host（临时） | 主机接收时打时间戳，待确认设备是否提供 |
| NIRS | Host（临时） | 同上 |
| Video | Host（临时） | 主机采集时打时间戳，待确认设备PTS |

---

## 3. 临时方案：主机时间对齐

### 3.1 方案描述

```
┌─────────────────────────────────────────────────────────────────┐
│                      主机单调时钟                                │
│                  (Stopwatch.GetTimestamp)                       │
├─────────────────────────────────────────────────────────────────┤
│                              │                                  │
│         ┌────────────────────┼────────────────────┐            │
│         ▼                    ▼                    ▼            │
│   ┌───────────┐        ┌───────────┐        ┌───────────┐     │
│   │ EEG 接收  │        │ NIRS 接收 │        │ Video 采集│     │
│   │ t_us = Now│        │ t_us = Now│        │ t_us = Now│     │
│   └───────────┘        └───────────┘        └───────────┘     │
│                              │                                  │
│                              ▼                                  │
│                    ┌─────────────────┐                         │
│                    │  统一时间轴     │                         │
│                    │  int64 微秒     │                         │
│                    └─────────────────┘                         │
└─────────────────────────────────────────────────────────────────┘
```

### 3.2 时间戳获取方法

```csharp
public static class HostClock
{
    private static readonly long _startTicks = Stopwatch.GetTimestamp();
    private static readonly double _ticksPerMicrosecond = 
        Stopwatch.Frequency / 1_000_000.0;
    
    /// <summary>
    /// 获取当前主机单调时间（微秒）
    /// </summary>
    public static long NowMicroseconds()
    {
        long elapsed = Stopwatch.GetTimestamp() - _startTicks;
        return (long)(elapsed / _ticksPerMicrosecond);
    }
}
```

### 3.3 EEG 数据包时间戳策略（重要！）

**关键事实**：EEG 设备**不提供硬件时间戳**，数据包中仅有：
- `data[16]` = GS counter (0-229)，用于 aEEG 直方图，**不是样本时间戳**
- 包序号可用于检测丢包，但不作为时间源

**时间戳生成策略**：

```csharp
// 常量
const int SAMPLE_RATE_HZ = 160;
const long SAMPLE_INTERVAL_US = 1_000_000 / SAMPLE_RATE_HZ;  // 6250 μs

// EEG 数据接收时
void OnEegPacketReceived(byte[] rawPacket)
{
    // 1. 立即打时间戳（包到达时刻）
    long packetTimestampUs = HostClock.NowMicroseconds();
    
    // 2. 解析数据包
    var packet = ParseEegPacket(rawPacket);
    
    // 3. 检测丢包（通过包序号）
    if (packet.SequenceNumber != _expectedSequence)
    {
        int lostPackets = (packet.SequenceNumber - _expectedSequence) & 0xFF;
        MarkGap(lostPackets * SAMPLE_INTERVAL_US);
    }
    _expectedSequence = (packet.SequenceNumber + 1) & 0xFF;
    
    // 4. 为包内各样本分配时间戳（如果包内有多个样本）
    // 当前设备：每包1样本，所以 sample.ts = packet.ts
    var sample = new EegSample
    {
        TimestampUs = packetTimestampUs,  // 样本中心时间
        Ch1 = packet.Data[0] * SCALE_FACTOR,
        Ch2 = packet.Data[1] * SCALE_FACTOR,
        Ch3 = packet.Data[2] * SCALE_FACTOR,
        Ch4 = packet.Data[3] * SCALE_FACTOR,
        Quality = QualityFlags.None
    };
    
    _buffer.Enqueue(sample);
}
```

### 3.4 NIRS 数据包时间戳策略

```csharp
// NIRS 采样率较低 (1-4 Hz)，时间戳策略相同
void OnNirsPacketReceived(byte[] rawPacket)
{
    long timestampUs = HostClock.NowMicroseconds();
    var sample = ParseNirsSample(rawPacket, timestampUs);
    _nirsBuffer.Enqueue(sample);
}
```

### 3.5 Video 帧时间戳策略（临时）

```csharp
// 视频设备信息待确认，当前使用主机时间
void OnVideoFrameCaptured(VideoFrame frame)
{
    frame.TimestampUs = HostClock.NowMicroseconds();
    _frameBuffer.Enqueue(frame);
}
```

### 3.6 Monotonic → Wall Clock 映射

```csharp
/// <summary>
/// 单调时间到墙钟时间的映射
/// </summary>
public class TimeMapping
{
    private readonly DateTime _wallClockStart;
    private readonly long _monotonicStartUs;
    
    public TimeMapping()
    {
        // 会话开始时同时记录两个时钟
        _wallClockStart = DateTime.UtcNow;
        _monotonicStartUs = HostClock.NowMicroseconds();
    }
    
    /// <summary>
    /// 将单调时间转换为 UTC 时间
    /// </summary>
    public DateTime ToUtc(long monotonicUs)
    {
        long elapsedUs = monotonicUs - _monotonicStartUs;
        return _wallClockStart.AddTicks(elapsedUs * 10); // 1μs = 10 ticks
    }
    
    /// <summary>
    /// 将 UTC 时间转换为单调时间
    /// </summary>
    public long ToMonotonic(DateTime utc)
    {
        TimeSpan elapsed = utc - _wallClockStart;
        return _monotonicStartUs + (long)(elapsed.TotalMilliseconds * 1000);
    }
}

// 漂移处理策略：
// - 短期监护 (<72h)：累计漂移约 1-10 秒，对临床可接受
// - 长期监护 (>72h)：需定期与 NTP 校准（未来 Sprint 实现）
```

### 3.7 精度与误差分析

| 因素 | 预估误差 | 说明 |
|------|----------|------|
| 主机时钟精度 | < 1 μs | Windows QPC 精度 |
| 接收处理延迟 | 1-10 ms | 串口/USB 缓冲 + 解析 |
| 线程调度抖动 | 0-5 ms | 取决于系统负载 |
| **总体同步精度** | **±10-50 ms** | 临床可接受但非最优 |

---

## 4. 升级路径

### 4.1 设备时钟对齐（目标方案）

当获得以下信息后，可升级到更高精度方案：

| 需要确认 | 影响 |
|----------|------|
| EEG设备是否提供采样计数器/时间戳 | 可重建设备时间轴 |
| Video设备是否提供PTS | 可使用设备时间戳 |
| EEG与Video是否共用时钟 | 决定是否需要时钟同步算法 |

### 4.2 时钟同步算法（如需跨设备对齐）

```
方案A: 线性回归拟合
- 定期采集 (t_device, t_host) 对
- 拟合 t_host = a * t_device + b
- 支持 drift 校正

方案B: PTP/NTP 同步
- 如果设备支持网络时间同步
- 可达到 μs 级精度
```

### 4.3 升级触发条件

当满足以下任一条件时，必须评估升级：

- [ ] 临床验证发现 ±50ms 同步精度不满足需求
- [ ] 获得设备硬件时间戳能力
- [ ] 需要跨设备精确对齐（如多台 EEG 设备）

---

## 5. 接口设计预留

### 5.1 时间戳元数据

```csharp
public readonly struct TimestampInfo
{
    /// <summary>时间戳值（微秒）</summary>
    public long TimestampUs { get; }
    
    /// <summary>时钟域</summary>
    public ClockDomain ClockDomain { get; }
    
    /// <summary>估计精度（微秒）</summary>
    public int EstimatedPrecisionUs { get; }
    
    /// <summary>是否为插值/推算值</summary>
    public bool IsInterpolated { get; }
}
```

### 5.2 数据源接口（预留时钟信息）

```csharp
public interface ITimeSeriesSource
{
    /// <summary>数据源名称</summary>
    string Name { get; }
    
    /// <summary>采样率 (Hz)</summary>
    int SampleRateHz { get; }
    
    /// <summary>时钟域</summary>
    ClockDomain ClockDomain { get; }
    
    /// <summary>估计时间戳精度（微秒）</summary>
    int EstimatedPrecisionUs { get; }
}
```

---

## 6. 验收标准

### MVP 阶段（当前）

- [ ] 所有数据源使用 HostClock 打时间戳
- [ ] 时间戳单位统一为 μs
- [ ] UI 游标可同时定位 EEG/NIRS/Video
- [ ] 同步误差 < 100ms（目视检查）

### 升级后

- [ ] 使用设备时间戳（如可用）
- [ ] 同步误差 < 10ms
- [ ] 支持 drift 校正

---

## 附录：相关 ADR

- **ADR-001**: 统一时间轴（int64 微秒）
- **ADR-010**: 时间同步策略（临时方案）

---

**文档结束**
