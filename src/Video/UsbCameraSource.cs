// UsbCameraSource.cs
// USB UVC 摄像头采集源 - S3-02 Video Capture & Playback
//
// 依据: ADR-011 (Camera API: Vortice.MediaFoundation)
//       ADR-012 (Host Monotonic Clock timestamping)

using System.Diagnostics;
using System.Runtime.InteropServices;
using Vortice.MediaFoundation;

namespace Neo.Video;

/// <summary>
/// USB UVC 摄像头采集源。
/// 使用 MediaFoundation SourceReader 进行帧采集，Host 时钟打点。
/// </summary>
/// <remarks>
/// 线程模型:
/// - 采集线程: 内部专用线程，MF SourceReader 同步读取
/// - 时间戳: 每帧在采集线程中立即调用 _getTimestampUs()
/// - 像素数据: 通过双缓冲（两个预分配 byte[]，原子交换）流转
/// - SampleReceived 事件在采集线程触发
///
/// 优雅降级:
/// - 无摄像头时 AvailableCameras 为空列表
/// - Start() 在无摄像头时记录警告并返回（不抛异常）
/// </remarks>
public sealed class UsbCameraSource : IVideoSource, IDisposable
{
    private readonly Func<long> _getTimestampUs;
    private readonly List<CameraDeviceInfo> _cameras = [];
    private readonly object _bufferLock = new();

    // 双缓冲（像素数据）
    private byte[]? _frontBuffer;
    private byte[]? _backBuffer;
    private volatile bool _hasFrame;

    // MF 对象
    private IMFSourceReader? _sourceReader;
    private IMFMediaSource? _mediaSource;
    private bool _mfInitialized;

    // 采集状态
    private Thread? _captureThread;
    private volatile bool _capturing;
    private volatile bool _stopRequested;
    private bool _disposed;
    private long _frameNumber;

    // 配置
    private int _width = 640;
    private int _height = 480;
    private int _fps = 30;
    private int _strideBytes;

    public int SampleRate => _fps;
    public int ChannelCount => 1;
    public event Action<VideoFrame>? SampleReceived;

    public IReadOnlyList<CameraDeviceInfo> AvailableCameras => _cameras;
    public CameraDeviceInfo? SelectedCamera { get; private set; }
    public bool IsCapturing => _capturing;
    public (int Width, int Height) Resolution => (_width, _height);

    /// <summary>
    /// 创建 USB 摄像头采集源。
    /// </summary>
    /// <param name="timestampProvider">Host 时间戳提供者（微秒）。</param>
    /// <param name="width">分辨率宽度（默认640）。</param>
    /// <param name="height">分辨率高度（默认480）。</param>
    /// <param name="fps">目标帧率（默认30）。</param>
    public UsbCameraSource(Func<long> timestampProvider, int width = 640, int height = 480, int fps = 30)
    {
        _getTimestampUs = timestampProvider ?? throw new ArgumentNullException(nameof(timestampProvider));
        _width = width;
        _height = height;
        _fps = fps;
        _strideBytes = _width * 4; // BGRA32

        // 初始化 MF
        MediaFactory.MFStartup(true);
        _mfInitialized = true;

        // 枚举摄像头
        EnumerateCameras();
    }

    /// <summary>
    /// 枚举系统中可用的 UVC 摄像头。
    /// </summary>
    private void EnumerateCameras()
    {
        _cameras.Clear();

        try
        {
            using var devices = MediaFactory.MFEnumVideoDeviceSources();

            foreach (var device in devices)
            {
                try
                {
                    string name = device.FriendlyName ?? "Unknown Camera";
                    string link = device.SymbolicLink ?? "";

                    _cameras.Add(new CameraDeviceInfo
                    {
                        Name = name,
                        SymbolicLink = link
                    });
                }
                catch (Exception ex)
                {
                    Trace.TraceWarning("[UsbCameraSource] Failed to read device info: {0}", ex.Message);
                }
            }

            Trace.TraceInformation("[UsbCameraSource] Found {0} camera(s)", _cameras.Count);
        }
        catch (Exception ex)
        {
            Trace.TraceWarning("[UsbCameraSource] Camera enumeration failed: {0}", ex.Message);
        }
    }

    /// <summary>
    /// 开始采集。如果无摄像头，记录警告并返回。
    /// </summary>
    public void Start()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (_capturing)
            return;

        if (_cameras.Count == 0)
        {
            Trace.TraceWarning("[UsbCameraSource] No camera available. Video capture disabled.");
            return;
        }

        try
        {
            // 选择第一个可用摄像头
            SelectedCamera = _cameras[0];

            // 创建 MF 对象
            InitializeCapture(SelectedCamera.Value);

            // 预分配双缓冲
            int bufferSize = _strideBytes * _height;
            _frontBuffer = new byte[bufferSize];
            _backBuffer = new byte[bufferSize];
            _hasFrame = false;
            _frameNumber = 0;

            // 启动采集线程
            _stopRequested = false;
            _capturing = true;
            _captureThread = new Thread(CaptureLoop)
            {
                Name = "VideoCapture",
                IsBackground = true,
                Priority = ThreadPriority.AboveNormal
            };
            _captureThread.Start();

            Trace.TraceInformation("[UsbCameraSource] Capture started: {0} @ {1}x{2} {3}fps",
                SelectedCamera.Value.Name, _width, _height, _fps);
        }
        catch (Exception ex)
        {
            Trace.TraceWarning("[UsbCameraSource] Failed to start capture: {0}", ex.Message);
            CleanupMfObjects();
            _capturing = false;
            SelectedCamera = null;
        }
    }

    /// <summary>
    /// 初始化 MF SourceReader 进行采集。
    /// </summary>
    private void InitializeCapture(CameraDeviceInfo camera)
    {
        // 重新枚举以获取 IMFActivate
        using var devices = MediaFactory.MFEnumVideoDeviceSources();
        IMFActivate? targetDevice = null;

        foreach (var device in devices)
        {
            string link = device.SymbolicLink ?? "";
            if (link == camera.SymbolicLink)
            {
                targetDevice = device;
                break;
            }
        }

        if (targetDevice == null)
            throw new InvalidOperationException($"Camera not found: {camera.Name}");

        // 激活媒体源
        _mediaSource = targetDevice.ActivateObject<IMFMediaSource>();

        // 创建 SourceReader
        using var readerAttrs = MediaFactory.MFCreateAttributes(1);
        readerAttrs.Set(SourceReaderAttributeKeys.EnableAdvancedVideoProcessing, true);

        _sourceReader = MediaFactory.MFCreateSourceReaderFromMediaSource(_mediaSource, readerAttrs);

        // 配置输出格式为 RGB32
        ConfigureOutputFormat();
    }

    /// <summary>
    /// 配置 SourceReader 输出格式。
    /// </summary>
    private void ConfigureOutputFormat()
    {
        if (_sourceReader == null) return;

        // 设置输出格式为 RGB32 (BGRA)
        using var outputType = MediaFactory.MFCreateMediaType();
        outputType.Set(MediaTypeAttributeKeys.MajorType, MediaTypeGuids.Video);
        outputType.Set(MediaTypeAttributeKeys.Subtype, VideoFormatGuids.Rgb32);

        _sourceReader.SetCurrentMediaType(
            (int)SourceReaderIndex.FirstVideoStream,
            outputType);

        // 读回实际生效的格式
        using var actualType = _sourceReader.GetCurrentMediaType(
            (int)SourceReaderIndex.FirstVideoStream);

        // 解析帧尺寸
        MediaFactory.MFGetAttributeSize(actualType, MediaTypeAttributeKeys.FrameSize,
            out uint actualWidth, out uint actualHeight);
        _width = (int)actualWidth;
        _height = (int)actualHeight;
        _strideBytes = _width * 4;

        // 解析帧率
        MediaFactory.MFGetAttributeRatio(actualType, MediaTypeAttributeKeys.FrameRate,
            out uint fpsNum, out uint fpsDen);
        if (fpsDen > 0)
            _fps = (int)(fpsNum / fpsDen);

        Trace.TraceInformation("[UsbCameraSource] Actual format: {0}x{1} @ {2}fps, stride={3}",
            _width, _height, _fps, _strideBytes);
    }

    /// <summary>
    /// 采集线程主循环。
    /// </summary>
    private void CaptureLoop()
    {
        try
        {
            while (!_stopRequested)
            {
                ReadOneFrame();
            }
        }
        catch (Exception ex)
        {
            if (!_stopRequested)
            {
                Trace.TraceError("[UsbCameraSource] Capture loop error: {0}", ex.Message);
            }
        }
        finally
        {
            _capturing = false;
        }
    }

    /// <summary>
    /// 从 SourceReader 读取一帧。
    /// </summary>
    private void ReadOneFrame()
    {
        if (_sourceReader == null) return;

        using IMFSample? sample = _sourceReader.ReadSample(
            (int)SourceReaderIndex.FirstVideoStream,
            SourceReaderControlFlag.None,
            out _,
            out SourceReaderFlag flags,
            out _);

        if ((flags & SourceReaderFlag.StreamTick) != 0)
            return;

        if ((flags & SourceReaderFlag.EndOfStream) != 0)
        {
            _stopRequested = true;
            return;
        }

        if (sample == null)
            return;

        // 立即打点 Host 时间戳（ADR-012）
        long timestampUs = _getTimestampUs();

        // 获取像素数据
        using var buffer = sample.ConvertToContiguousBuffer();
        buffer.Lock(out nint dataPtr, out _, out int currentLength);

        try
        {
            // 写入后缓冲
            int copyLength = Math.Min(currentLength, _backBuffer!.Length);
            Marshal.Copy(dataPtr, _backBuffer, 0, copyLength);

            // 原子交换双缓冲
            lock (_bufferLock)
            {
                (_frontBuffer, _backBuffer) = (_backBuffer, _frontBuffer);
                _hasFrame = true;
            }
        }
        finally
        {
            buffer.Unlock();
        }

        // 构建帧元数据并触发事件
        long frameNum = Interlocked.Increment(ref _frameNumber) - 1;

        var frame = new VideoFrame
        {
            TimestampUs = timestampUs,
            Width = _width,
            Height = _height,
            StrideBytes = _strideBytes,
            FrameNumber = frameNum
        };

        SampleReceived?.Invoke(frame);
    }

    /// <summary>
    /// 拷贝最新帧像素数据。
    /// </summary>
    public int CopyLatestFramePixels(Span<byte> destination)
    {
        if (!_hasFrame || _frontBuffer == null)
            return 0;

        int copyLength;
        lock (_bufferLock)
        {
            copyLength = Math.Min(_frontBuffer.Length, destination.Length);
            _frontBuffer.AsSpan(0, copyLength).CopyTo(destination);
        }
        return copyLength;
    }

    /// <summary>
    /// 停止采集。
    /// </summary>
    public void Stop()
    {
        if (!_capturing)
            return;

        _stopRequested = true;
        _captureThread?.Join(timeout: TimeSpan.FromSeconds(3));
        _captureThread = null;

        CleanupMfObjects();
        _capturing = false;

        Trace.TraceInformation("[UsbCameraSource] Capture stopped.");
    }

    private void CleanupMfObjects()
    {
        _sourceReader?.Dispose();
        _sourceReader = null;

        if (_mediaSource != null)
        {
            try { _mediaSource.Shutdown(); } catch { /* best-effort */ }
            _mediaSource.Dispose();
            _mediaSource = null;
        }
    }

    public void Dispose()
    {
        if (_disposed) return;

        Stop();

        if (_mfInitialized)
        {
            MediaFactory.MFShutdown();
            _mfInitialized = false;
        }

        _disposed = true;
    }
}
