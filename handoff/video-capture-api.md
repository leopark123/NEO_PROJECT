# Video Capture & Playback API 交接文档

> **版本**: v1.0
> **负责方**: Claude Code
> **创建日期**: 2026-01-29
> **关联任务**: S3-02

---

## 1. 概述

USB UVC 摄像头采集与回放模块。支持 Host 时钟打点帧采集、H.264/MP4 录制、.tsidx 索引文件生成、基于时间戳的毫秒级定位回放。

---

## 2. 公开接口

```csharp
namespace Neo.Video
{
    // 帧元数据（不含像素数据）
    public readonly record struct VideoFrame
    {
        public long TimestampUs { get; init; }
        public int Width { get; init; }
        public int Height { get; init; }
        public int StrideBytes { get; init; }
        public long FrameNumber { get; init; }
    }

    // 摄像头设备信息
    public readonly record struct CameraDeviceInfo
    {
        public string Name { get; init; }
        public string SymbolicLink { get; init; }
    }

    // 视频源接口
    public interface IVideoSource : ITimeSeriesSource<VideoFrame>
    {
        IReadOnlyList<CameraDeviceInfo> AvailableCameras { get; }
        CameraDeviceInfo? SelectedCamera { get; }
        bool IsCapturing { get; }
        (int Width, int Height) Resolution { get; }
        int CopyLatestFramePixels(Span<byte> destination);
    }

    // USB 摄像头采集源
    public sealed class UsbCameraSource : IVideoSource, IDisposable
    {
        public UsbCameraSource(Func<long> timestampProvider, int width = 640, int height = 480, int fps = 30);
    }

    // 视频录制器
    public sealed class VideoRecorder : IDisposable
    {
        public VideoRecorder(int width, int height, int fps, int bitrateBps = 1_500_000);
        public bool IsRecording { get; }
        public void StartRecording(string outputPath);
        public void WriteFrame(in VideoFrame frame, ReadOnlySpan<byte> pixelData);
        public void StopRecording();
    }

    // 视频回放源
    public sealed class VideoPlaybackSource : IVideoSource, IDisposable
    {
        public VideoPlaybackSource(string videoPath);
        public int TotalFrames { get; }
        public bool IsIndexLoaded { get; }
        public void LoadIndex();
        public bool SeekToTimestamp(long targetTimestampUs);
    }
}

namespace Neo.Host
{
    // 视频模块装配
    public sealed class VideoWiring : IDisposable
    {
        public VideoWiring(Func<long> timestampProvider, int width = 640, int height = 480, int fps = 30);
        public IVideoSource Source { get; }
        public bool IsCameraAvailable { get; }
        public bool IsCapturing { get; }
        public bool IsRecording { get; }
        public void Start();
        public void StartRecording(string outputPath);
        public void StopRecording();
        public void Stop();
    }
}
```

---

## 3. 数据模型

```csharp
// 帧元数据 (~40 bytes)
public readonly record struct VideoFrame
{
    public long TimestampUs { get; init; }   // Host 单调时钟 (μs)
    public int Width { get; init; }          // 像素宽度
    public int Height { get; init; }         // 像素高度
    public int StrideBytes { get; init; }    // 每行字节跨度
    public long FrameNumber { get; init; }   // 帧序号（单调递增）
}

// 像素数据不在 struct 中，通过 CopyLatestFramePixels() 获取
// 格式: BGRA32, 每像素 4 bytes
// 缓冲区大小: Width * Height * 4 bytes (~1.2MB @ 640x480)
```

---

## 4. 线程模型

| 角色 | 线程 | 说明 |
|------|------|------|
| MF 采集 | VideoCapture (专用) | IMFSourceReader.ReadSample 同步读取 |
| 时间戳打点 | VideoCapture | 每帧读取后立即调用 _getTimestampUs() |
| 像素双缓冲 | VideoCapture (写) / Render (读) | lock-based swap |
| SampleReceived 事件 | VideoCapture | 帧元数据回调 |
| VideoRecorder.WriteFrame | VideoCapture | 同步写入 MF SinkWriter |
| 回放 | VideoPlayback (专用) | 按帧率逐帧读取 MP4 |

**线程安全性**: 读写分离（双缓冲 + lock swap）

**锁策略**: object lock 保护双缓冲交换

---

## 5. 时间戳语义

| 输入/输出 | 时间戳含义 | 单位 |
|-----------|-----------|------|
| 采集输出 | Host 单调时钟打点（帧到达时刻） | μs |
| 录制写入 | 同采集输出 | μs |
| .tsidx 索引 | TimestampUs + PresentationTime100ns | μs / 100ns |
| 回放输出 | 从 .tsidx 还原的原始 Host 时间戳 | μs |

**时钟域**: Host (Stopwatch)

**铁律合规**: 回放路径仅从 .tsidx 索引恢复 Host 时间戳。无索引时拒绝回放；索引未命中帧直接丢弃，不从 MF PresentationTime 伪造时间戳。

**精度**: ±50-100ms with EEG (ADR-011)

---

## 6. 数据契约

| 属性 | 值 | 说明 |
|------|-----|------|
| 采样率 | 15-30 fps | 取决于摄像头实际能力 |
| 通道数 | 1 | 单视频流 |
| 像素格式 | BGRA32 | 4 bytes/pixel |
| 分辨率 | 640x480 (默认) | 可配置 |
| 编码 | H.264 | MF 硬件编码 |
| 容器 | MP4 | |
| 码率 | 1.5 Mbps (默认) | 可配置 (1-2 Mbps) |
| 时间戳单位 | μs | 微秒 |

---

## 7. 使用示例

```csharp
// 初始化（在 MainForm 中）
var videoWiring = new VideoWiring(GetTimestampUs);

// 启动采集（无摄像头时自动降级）
videoWiring.Start();

// 检查状态
if (videoWiring.IsCameraAvailable && videoWiring.IsCapturing)
{
    // 开始录制
    videoWiring.StartRecording(@"C:\recordings\session.mp4");
    // ... 录制中 ...
    videoWiring.StopRecording();
}

// 获取最新帧像素（未来渲染用）
byte[] pixels = new byte[640 * 480 * 4];
int copied = videoWiring.Source.CopyLatestFramePixels(pixels);

// 回放
var playback = new VideoPlaybackSource(@"C:\recordings\session.mp4");
playback.LoadIndex();
playback.Start();
playback.SeekToTimestamp(targetUs); // 毫秒级定位

// 清理
videoWiring.Dispose();
playback.Dispose();
```

---

## 8. 性能特征

| 指标 | 值 | 测试条件 |
|------|-----|----------|
| 帧采集延迟 | <1ms (Host stamp) | 640x480 @ 30fps |
| 像素拷贝 | ~0.3ms | 1.2MB BGRA32 |
| 索引查找 | O(log n) | 二分查找 |
| 内存 (采集) | ~2.4MB | 双缓冲 + 元数据 |
| 内存 (索引) | ~2MB/小时 | 20 bytes/frame @ 30fps |
| 磁盘 (MP4) | ~11MB/分钟 | 1.5 Mbps H.264 |

---

## 9. 已知限制

1. 像素双缓冲使用 lock，非无锁设计（可接受：单生产者单消费者，争用极低）
2. 仅支持第一个检测到的 USB 摄像头（不支持多摄像头选择）
3. 回放速率固定为录制帧率（不支持变速）
4. 无摄像头热插拔检测（启动时枚举一次）
5. MFStartup/MFShutdown 在 UsbCameraSource 生命周期内调用一次

---

## 10. 依赖项

| 依赖模块 | 接口/类 | 用途 |
|----------|---------|------|
| Neo.Core | ITimeSeriesSource<T> | 统一时间序列接口 |
| Vortice.MediaFoundation 3.8.1 | MediaFactory, IMFSourceReader, IMFSinkWriter | 摄像头采集 + H.264 编码 |

---

## 11. .tsidx 索引文件格式

```
Header (16 bytes):
  [0..3]   byte[4]  magic = "TSIX"
  [4..7]   int32    version = 1
  [8..11]  int32    entrySize = 20
  [12..15] int32    reserved = 0

Entry (20 bytes, repeated):
  [0..7]   int64    TimestampUs          (Host 单调时钟 μs)
  [8..15]  int64    PresentationTime100ns (MF 时间轴 100ns)
  [16..19] int32    FrameIndex           (帧序号)
```

全量加载到内存用于二分查找。约 2MB/小时 @ 30fps。

---

## 12. 优雅降级行为

| 场景 | 行为 |
|------|------|
| 无 USB 摄像头 | 记录警告，IsCameraAvailable=false，系统正常运行 |
| 摄像头枚举失败 | 记录警告，空列表 |
| 采集初始化失败 | 记录警告，IsCapturing=false |
| 录制时无采集 | 记录警告，不启动录制 |
| 回放文件不存在 | 记录警告，不启动回放 |
| .tsidx 缺失 | 记录警告，拒绝回放（无法提供 Host 时间戳） |
| 索引帧未命中 | 记录警告，丢弃该帧（不伪造时间戳） |

---

## 13. 变更历史

| 版本 | 日期 | 变更内容 |
|------|------|----------|
| v1.0 | 2026-01-29 | 初始版本 - S3-02 Video Capture & Playback |
| v1.1 | 2026-01-29 | 修复铁律违规：回放路径移除 PresentationTime→μs 伪造，无索引时拒绝回放，未命中帧丢弃 |

---

**文档结束**
