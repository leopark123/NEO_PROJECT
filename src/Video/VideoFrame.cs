// VideoFrame.cs
// 视频帧元数据 - S3-02 Video Capture & Playback

namespace Neo.Video;

/// <summary>
/// 视频帧元数据（不含像素数据）。
/// </summary>
/// <remarks>
/// 依据: ADR-011, ADR-012
///
/// 设计:
/// - readonly record struct (~40 bytes)，纯元数据
/// - 像素数据 (~900KB/帧 @ 640x480) 通过 VideoFrameDoubleBuffer 流转
/// - 时间戳语义: Host 单调时钟打点，样本中心时间 (CONSENSUS_BASELINE §5.3)
/// - 单位: 微秒 (μs)
/// </remarks>
public readonly record struct VideoFrame
{
    /// <summary>
    /// Host 单调时钟时间戳（微秒）。
    /// </summary>
    public long TimestampUs { get; init; }

    /// <summary>
    /// 帧宽度（像素）。
    /// </summary>
    public int Width { get; init; }

    /// <summary>
    /// 帧高度（像素）。
    /// </summary>
    public int Height { get; init; }

    /// <summary>
    /// 每行字节跨度。
    /// </summary>
    public int StrideBytes { get; init; }

    /// <summary>
    /// 帧序号（从0开始单调递增）。
    /// </summary>
    public long FrameNumber { get; init; }
}
