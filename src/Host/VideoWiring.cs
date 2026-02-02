// VideoWiring.cs
// 视频模块装配 - S3-02: 生命周期管理
//
// 依据: NirsWiring pattern (接线/DI/生命周期)

using System.Diagnostics;
using Neo.Video;

namespace Neo.Host;

/// <summary>
/// 视频模块装配（接线/DI/生命周期）。
/// </summary>
/// <remarks>
/// S3-02 范围:
/// - 创建并管理 UsbCameraSource 生命周期
/// - 管理 VideoRecorder 录制状态
/// - 优雅降级：无摄像头时系统正常运行
///
/// 模式: 与 NirsWiring 一致
/// - Start/Stop 控制采集
/// - StartRecording/StopRecording 控制录制
/// - Dispose 释放所有资源
/// </remarks>
public sealed class VideoWiring : IDisposable
{
    private readonly UsbCameraSource _source;
    private VideoRecorder? _recorder;
    private bool _disposed;

    /// <summary>
    /// 创建视频模块装配。
    /// </summary>
    /// <param name="timestampProvider">Host 时间戳提供者（微秒）。</param>
    /// <param name="width">分辨率宽度（默认640）。</param>
    /// <param name="height">分辨率高度（默认480）。</param>
    /// <param name="fps">目标帧率（默认30）。</param>
    public VideoWiring(Func<long> timestampProvider, int width = 640, int height = 480, int fps = 30)
    {
        _source = new UsbCameraSource(timestampProvider, width, height, fps);
    }

    /// <summary>
    /// 视频源实例。
    /// </summary>
    public IVideoSource Source => _source;

    /// <summary>
    /// 是否有可用摄像头。
    /// </summary>
    public bool IsCameraAvailable => _source.AvailableCameras.Count > 0;

    /// <summary>
    /// 是否正在采集。
    /// </summary>
    public bool IsCapturing => _source.IsCapturing;

    /// <summary>
    /// 是否正在录制。
    /// </summary>
    public bool IsRecording => _recorder?.IsRecording ?? false;

    /// <summary>
    /// 启动视频采集。
    /// 无摄像头时记录警告并正常返回。
    /// </summary>
    public void Start()
    {
        _source.Start();

        if (IsCameraAvailable)
        {
            Trace.TraceInformation("[VideoWiring] Video module started. Camera: {0}",
                _source.SelectedCamera?.Name ?? "none");
        }
        else
        {
            Trace.TraceWarning("[VideoWiring] No camera available. System continues in EEG-only mode.");
        }
    }

    /// <summary>
    /// 开始录制。
    /// </summary>
    /// <param name="outputPath">输出 MP4 文件路径。</param>
    public void StartRecording(string outputPath)
    {
        if (!IsCapturing)
        {
            Trace.TraceWarning("[VideoWiring] Cannot start recording: not capturing.");
            return;
        }

        var (width, height) = _source.Resolution;
        _recorder = new VideoRecorder(width, height, _source.SampleRate);

        // 订阅帧事件，将帧写入录制器
        _source.SampleReceived += OnFrameForRecording;

        _recorder.StartRecording(outputPath);

        Trace.TraceInformation("[VideoWiring] Recording started: {0}", outputPath);
    }

    /// <summary>
    /// 停止录制。
    /// </summary>
    public void StopRecording()
    {
        if (_recorder == null || !_recorder.IsRecording)
            return;

        _source.SampleReceived -= OnFrameForRecording;
        _recorder.StopRecording();
        _recorder.Dispose();
        _recorder = null;

        Trace.TraceInformation("[VideoWiring] Recording stopped.");
    }

    /// <summary>
    /// 录制帧回调。
    /// </summary>
    private void OnFrameForRecording(VideoFrame frame)
    {
        if (_recorder == null || !_recorder.IsRecording)
            return;

        // 从源获取最新像素数据
        int bufferSize = frame.StrideBytes * frame.Height;
        byte[] pixelBuffer = new byte[bufferSize];
        int copied = _source.CopyLatestFramePixels(pixelBuffer);

        if (copied > 0)
        {
            _recorder.WriteFrame(in frame, pixelBuffer.AsSpan(0, copied));
        }
    }

    /// <summary>
    /// 停止视频采集。
    /// </summary>
    public void Stop()
    {
        if (IsRecording)
            StopRecording();

        _source.Stop();

        Trace.TraceInformation("[VideoWiring] Video module stopped.");
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            Stop();
            _source.Dispose();
            _disposed = true;
        }
    }
}
