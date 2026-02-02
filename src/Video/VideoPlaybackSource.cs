// VideoPlaybackSource.cs
// 视频回放源（MP4 + .tsidx 索引）- S3-02 Video Capture & Playback
//
// 依据: ADR-011 (Playback with timestamp-based seeking)
//       ADR-012 (Host clock alignment for EEG sync)

using System.Diagnostics;
using System.Runtime.InteropServices;
using Vortice.MediaFoundation;

namespace Neo.Video;

/// <summary>
/// .tsidx 索引条目。
/// </summary>
internal readonly record struct TsidxEntry
{
    public long TimestampUs { get; init; }
    public long PresentationTime100ns { get; init; }
    public int FrameIndex { get; init; }
}

/// <summary>
/// 视频回放源。
/// 读取 MP4 文件 + .tsidx 索引，支持按时间戳定位。
/// </summary>
/// <remarks>
/// 线程模型:
/// - 回放线程: 内部专用线程，按帧率逐帧读取
/// - SampleReceived 事件在回放线程触发
/// - 通过 .tsidx 索引实现毫秒级时间戳定位
///
/// .tsidx 格式:
/// - Header (16 bytes): magic "TSIX" | version 1 | entrySize 20 | reserved
/// - Entry  (20 bytes): int64 TimestampUs | int64 PresentationTime100ns | int32 FrameIndex
///
/// 内存占用: .tsidx 全量加载，~2MB/小时
/// </remarks>
public sealed class VideoPlaybackSource : IVideoSource, IDisposable
{
    private readonly string _videoPath;
    private readonly string _indexPath;
    private readonly List<TsidxEntry> _index = [];
    private readonly object _bufferLock = new();

    // MF 对象
    private IMFSourceReader? _sourceReader;
    private bool _mfInitialized;

    // 双缓冲
    private byte[]? _frontBuffer;
    private byte[]? _backBuffer;
    private volatile bool _hasFrame;

    // 回放状态
    private Thread? _playbackThread;
    private volatile bool _playing;
    private volatile bool _stopRequested;
    private bool _disposed;
    private long _frameNumber;

    // 格式信息
    private int _width;
    private int _height;
    private int _fps = 30;
    private int _strideBytes;

    // 常量
    private static readonly byte[] TsidxMagic = "TSIX"u8.ToArray();

    public int SampleRate => _fps;
    public int ChannelCount => 1;
    public event Action<VideoFrame>? SampleReceived;

    public IReadOnlyList<CameraDeviceInfo> AvailableCameras => [];
    public CameraDeviceInfo? SelectedCamera => null;
    public bool IsCapturing => _playing;
    public (int Width, int Height) Resolution => (_width, _height);

    /// <summary>
    /// 总帧数。
    /// </summary>
    public int TotalFrames => _index.Count;

    /// <summary>
    /// 索引是否已加载。
    /// </summary>
    public bool IsIndexLoaded => _index.Count > 0;

    /// <summary>
    /// 创建视频回放源。
    /// </summary>
    /// <param name="videoPath">MP4 文件路径。</param>
    public VideoPlaybackSource(string videoPath)
    {
        _videoPath = videoPath ?? throw new ArgumentNullException(nameof(videoPath));
        _indexPath = Path.ChangeExtension(videoPath, ".tsidx");
    }

    /// <summary>
    /// 加载 .tsidx 索引（全量加载到内存）。
    /// </summary>
    public void LoadIndex()
    {
        _index.Clear();

        if (!File.Exists(_indexPath))
        {
            Trace.TraceWarning("[VideoPlaybackSource] Index file not found: {0}", _indexPath);
            return;
        }

        using var stream = new FileStream(_indexPath, FileMode.Open, FileAccess.Read, FileShare.Read);
        using var reader = new BinaryReader(stream);

        // 读取并验证头部
        byte[] magic = reader.ReadBytes(4);
        if (!magic.AsSpan().SequenceEqual(TsidxMagic))
        {
            Trace.TraceError("[VideoPlaybackSource] Invalid .tsidx magic");
            return;
        }

        int version = reader.ReadInt32();
        int entrySize = reader.ReadInt32();
        _ = reader.ReadInt32(); // reserved

        if (version != 1 || entrySize != 20)
        {
            Trace.TraceError("[VideoPlaybackSource] Unsupported .tsidx version {0} or entry size {1}",
                version, entrySize);
            return;
        }

        // 读取所有条目
        while (stream.Position + entrySize <= stream.Length)
        {
            _index.Add(new TsidxEntry
            {
                TimestampUs = reader.ReadInt64(),
                PresentationTime100ns = reader.ReadInt64(),
                FrameIndex = reader.ReadInt32()
            });
        }

        Trace.TraceInformation("[VideoPlaybackSource] Loaded {0} index entries from {1}",
            _index.Count, _indexPath);
    }

    /// <summary>
    /// 开始回放。
    /// </summary>
    public void Start()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (_playing)
            return;

        if (!File.Exists(_videoPath))
        {
            Trace.TraceWarning("[VideoPlaybackSource] Video file not found: {0}", _videoPath);
            return;
        }

        try
        {
            // 加载索引（如果尚未加载）
            if (_index.Count == 0)
                LoadIndex();

            // 铁律：无 .tsidx 索引时无法提供 Host 时间戳
            if (_index.Count == 0)
            {
                Trace.TraceWarning(
                    "[VideoPlaybackSource] No .tsidx index available for {0}. " +
                    "Playback blocked: cannot provide Host timestamps without index.",
                    _videoPath);
                return;
            }

            // 初始化 MF
            MediaFactory.MFStartup(true);
            _mfInitialized = true;

            // 创建 SourceReader 读取 MP4
            _sourceReader = MediaFactory.MFCreateSourceReaderFromURL(_videoPath, null);

            // 配置输出格式为 RGB32
            using var outputType = MediaFactory.MFCreateMediaType();
            outputType.Set(MediaTypeAttributeKeys.MajorType, MediaTypeGuids.Video);
            outputType.Set(MediaTypeAttributeKeys.Subtype, VideoFormatGuids.Rgb32);

            _sourceReader.SetCurrentMediaType(
                (int)SourceReaderIndex.FirstVideoStream,
                outputType);

            // 读取实际格式
            using var actualType = _sourceReader.GetCurrentMediaType(
                (int)SourceReaderIndex.FirstVideoStream);

            MediaFactory.MFGetAttributeSize(actualType, MediaTypeAttributeKeys.FrameSize,
                out uint actualWidth, out uint actualHeight);
            _width = (int)actualWidth;
            _height = (int)actualHeight;
            _strideBytes = _width * 4;

            MediaFactory.MFGetAttributeRatio(actualType, MediaTypeAttributeKeys.FrameRate,
                out uint fpsNum, out uint fpsDen);
            if (fpsDen > 0)
                _fps = (int)(fpsNum / fpsDen);

            // 预分配双缓冲
            int bufferSize = _strideBytes * _height;
            _frontBuffer = new byte[bufferSize];
            _backBuffer = new byte[bufferSize];
            _hasFrame = false;
            _frameNumber = 0;

            // 启动回放线程
            _stopRequested = false;
            _playing = true;
            _playbackThread = new Thread(PlaybackLoop)
            {
                Name = "VideoPlayback",
                IsBackground = true
            };
            _playbackThread.Start();

            Trace.TraceInformation("[VideoPlaybackSource] Playback started: {0} ({1}x{2} @ {3}fps)",
                _videoPath, _width, _height, _fps);
        }
        catch (Exception ex)
        {
            Trace.TraceWarning("[VideoPlaybackSource] Failed to start playback: {0}", ex.Message);
            CleanupMfObjects();
            _playing = false;
        }
    }

    /// <summary>
    /// 按 Host 时间戳定位到最近帧。
    /// 使用 .tsidx 索引进行二分查找。
    /// </summary>
    /// <param name="targetTimestampUs">目标时间戳（微秒）。</param>
    /// <returns>是否成功定位。</returns>
    public bool SeekToTimestamp(long targetTimestampUs)
    {
        if (_sourceReader == null || _index.Count == 0)
            return false;

        // 二分查找最近的索引条目
        int entryIndex = BinarySearchClosest(targetTimestampUs);
        if (entryIndex < 0)
            return false;

        var entry = _index[entryIndex];

        try
        {
            // 使用 PresentationTime 进行 MF seek (100ns 单位)
            _sourceReader.SetCurrentPosition(entry.PresentationTime100ns);
            _frameNumber = entry.FrameIndex;

            Trace.TraceInformation("[VideoPlaybackSource] Seeked to frame {0} (ts={1}us)",
                entry.FrameIndex, entry.TimestampUs);
            return true;
        }
        catch (Exception ex)
        {
            Trace.TraceError("[VideoPlaybackSource] Seek failed: {0}", ex.Message);
            return false;
        }
    }

    /// <summary>
    /// 在 .tsidx 索引中二分查找最接近的条目。
    /// </summary>
    private int BinarySearchClosest(long targetTimestampUs)
    {
        if (_index.Count == 0) return -1;

        int lo = 0, hi = _index.Count - 1;

        while (lo <= hi)
        {
            int mid = lo + (hi - lo) / 2;
            long midTs = _index[mid].TimestampUs;

            if (midTs == targetTimestampUs)
                return mid;
            else if (midTs < targetTimestampUs)
                lo = mid + 1;
            else
                hi = mid - 1;
        }

        // 返回最接近的条目
        if (lo >= _index.Count) return _index.Count - 1;
        if (hi < 0) return 0;

        long diffLo = Math.Abs(_index[lo].TimestampUs - targetTimestampUs);
        long diffHi = Math.Abs(_index[hi].TimestampUs - targetTimestampUs);
        return diffLo <= diffHi ? lo : hi;
    }

    /// <summary>
    /// 回放线程主循环。
    /// </summary>
    private void PlaybackLoop()
    {
        int frameIntervalMs = 1000 / _fps;

        try
        {
            while (!_stopRequested)
            {
                if (!ReadOneFrame())
                    break; // 到达文件末尾

                // 控制回放速率
                Thread.Sleep(frameIntervalMs);
            }
        }
        catch (Exception ex)
        {
            if (!_stopRequested)
            {
                Trace.TraceError("[VideoPlaybackSource] Playback loop error: {0}", ex.Message);
            }
        }
        finally
        {
            _playing = false;
        }
    }

    /// <summary>
    /// 读取一帧。
    /// </summary>
    private bool ReadOneFrame()
    {
        if (_sourceReader == null) return false;

        using IMFSample? sample = _sourceReader.ReadSample(
            (int)SourceReaderIndex.FirstVideoStream,
            SourceReaderControlFlag.None,
            out _,
            out SourceReaderFlag flags,
            out long timestamp100ns);

        if ((flags & SourceReaderFlag.EndOfStream) != 0)
            return false;

        if (sample == null)
            return true; // skip but continue

        // 获取像素数据
        using var buffer = sample.ConvertToContiguousBuffer();
        buffer.Lock(out nint dataPtr, out _, out int currentLength);

        try
        {
            int copyLength = Math.Min(currentLength, _backBuffer!.Length);
            Marshal.Copy(dataPtr, _backBuffer, 0, copyLength);

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

        // 从索引查找原始 Host 时间戳（铁律：所有时间戳必须来自 Host 时钟）
        long frameNum = Interlocked.Increment(ref _frameNumber) - 1;
        long? hostTimestampUs = LookupHostTimestamp(frameNum, timestamp100ns);

        if (hostTimestampUs == null)
        {
            // 无法恢复 Host 时间戳 → 丢弃帧，不发射非 Host 时间戳
            Trace.TraceWarning(
                "[VideoPlaybackSource] Frame {0} dropped: no host timestamp in .tsidx index",
                frameNum);
            return true;
        }

        var frame = new VideoFrame
        {
            TimestampUs = hostTimestampUs.Value,
            Width = _width,
            Height = _height,
            StrideBytes = _strideBytes,
            FrameNumber = frameNum
        };

        SampleReceived?.Invoke(frame);
        return true;
    }

    /// <summary>
    /// 根据帧号或 PresentationTime 查找原始 Host 时间戳。
    /// 铁律：不可从 MF PresentationTime 伪造 Host 时间戳。
    /// 无法恢复时返回 null，调用方必须丢弃该帧。
    /// </summary>
    /// <remarks>
    /// Performance: O(1) for frame-number hit (primary path), O(N) fallback for PresentationTime scan.
    /// Justification: The O(N) fallback only triggers after seek (frame numbers may desync with MF).
    /// Index size is bounded by recording duration: ~108K entries/hour @ 30fps, ~2MB/hour in memory.
    /// The linear scan of 108K int64 comparisons completes in &lt;1ms. This path is rarely hit during
    /// normal sequential playback (frame-number direct lookup is O(1)).
    /// </remarks>
    private long? LookupHostTimestamp(long frameNumber, long presentationTime100ns)
    {
        if (_index.Count == 0)
            return null; // 无索引 → 无 Host 时间戳

        // 先尝试帧号直接索引
        if (frameNumber >= 0 && frameNumber < _index.Count)
        {
            var entry = _index[(int)frameNumber];
            if (entry.FrameIndex == frameNumber)
                return entry.TimestampUs;
        }

        // 回退到 PresentationTime 查找（仍从索引中恢复 Host 时间戳）
        for (int i = 0; i < _index.Count; i++)
        {
            if (Math.Abs(_index[i].PresentationTime100ns - presentationTime100ns) < 10_000) // 1ms tolerance
                return _index[i].TimestampUs;
        }

        // 索引存在但未命中 → 无法恢复该帧的 Host 时间戳
        return null;
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
    /// 停止回放。
    /// </summary>
    public void Stop()
    {
        if (!_playing)
            return;

        _stopRequested = true;
        _playbackThread?.Join(timeout: TimeSpan.FromSeconds(3));
        _playbackThread = null;

        CleanupMfObjects();
        _playing = false;

        Trace.TraceInformation("[VideoPlaybackSource] Playback stopped.");
    }

    private void CleanupMfObjects()
    {
        _sourceReader?.Dispose();
        _sourceReader = null;
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
