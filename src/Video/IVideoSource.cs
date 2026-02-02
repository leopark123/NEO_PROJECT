// IVideoSource.cs
// 视频源接口 - S3-02 Video Capture & Playback

using Neo.Core.Interfaces;

namespace Neo.Video;

/// <summary>
/// USB UVC 摄像头设备信息。
/// </summary>
public readonly record struct CameraDeviceInfo
{
    /// <summary>
    /// 设备友好名称。
    /// </summary>
    public string Name { get; init; }

    /// <summary>
    /// 设备符号链接路径（唯一标识）。
    /// </summary>
    public string SymbolicLink { get; init; }
}

/// <summary>
/// 视频源接口，扩展 ITimeSeriesSource&lt;VideoFrame&gt;。
/// </summary>
/// <remarks>
/// 依据: ADR-011, ADR-012
///
/// 设计:
/// - 继承 ITimeSeriesSource&lt;VideoFrame&gt;，提供统一的时间序列接口
/// - 额外提供摄像头枚举、选择、像素数据访问
/// - 像素数据通过 CopyLatestFramePixels 方法获取（不在 VideoFrame 结构中）
/// - SampleRate = FPS (15-30), ChannelCount = 1
/// </remarks>
public interface IVideoSource : ITimeSeriesSource<VideoFrame>
{
    /// <summary>
    /// 可用摄像头列表。
    /// </summary>
    IReadOnlyList<CameraDeviceInfo> AvailableCameras { get; }

    /// <summary>
    /// 当前选中的摄像头（null 表示无摄像头）。
    /// </summary>
    CameraDeviceInfo? SelectedCamera { get; }

    /// <summary>
    /// 是否正在采集。
    /// </summary>
    bool IsCapturing { get; }

    /// <summary>
    /// 当前分辨率 (Width, Height)。
    /// </summary>
    (int Width, int Height) Resolution { get; }

    /// <summary>
    /// 拷贝最新帧像素数据到目标缓冲区。
    /// </summary>
    /// <param name="destination">目标缓冲区（长度必须 >= Width * Height * 4 for BGRA）。</param>
    /// <returns>实际拷贝的字节数，0 表示无可用帧。</returns>
    int CopyLatestFramePixels(Span<byte> destination);
}
