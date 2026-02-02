// VideoRecorder.cs
// 视频录制（H.264/MP4 + .tsidx 索引）- S3-02 Video Capture & Playback
//
// 依据: ADR-011 (Codec: H.264/MP4, Bitrate: 1-2 Mbps)
//       ADR-012 (Timestamp Index for seek)

using System.Diagnostics;
using Vortice.MediaFoundation;

namespace Neo.Video;

/// <summary>
/// 视频录制器。
/// 使用 MediaFoundation SinkWriter 进行 H.264/MP4 编码，
/// 同时写入 .tsidx 时间戳索引文件。
/// </summary>
/// <remarks>
/// 线程模型:
/// - WriteFrame() 在 UsbCameraSource 的采集线程中调用（同步）
/// - 不可并发调用 WriteFrame
///
/// .tsidx 格式:
/// - Header (16 bytes): magic "TSIX" | version 1 | entrySize 20 | reserved
/// - Entry  (20 bytes): int64 TimestampUs | int64 PresentationTime100ns | int32 FrameIndex
/// </remarks>
public sealed class VideoRecorder : IDisposable
{
    private IMFSinkWriter? _sinkWriter;
    private int _videoStreamIndex;
    private FileStream? _indexStream;
    private BinaryWriter? _indexWriter;
    private bool _isRecording;
    private bool _disposed;
    private long _startTimestamp100ns;
    private int _frameIndex;
    private readonly int _width;
    private readonly int _height;
    private readonly int _fps;
    private readonly int _bitrateBps;
    private readonly int _strideBytes;

    // .tsidx 常量
    private static readonly byte[] TsidxMagic = "TSIX"u8.ToArray();
    private const int TsidxVersion = 1;
    private const int TsidxEntrySize = 20;

    /// <summary>
    /// 是否正在录制。
    /// </summary>
    public bool IsRecording => _isRecording;

    /// <summary>
    /// 创建视频录制器。
    /// </summary>
    /// <param name="width">帧宽度。</param>
    /// <param name="height">帧高度。</param>
    /// <param name="fps">帧率。</param>
    /// <param name="bitrateBps">目标码率（bps），默认 1.5 Mbps。</param>
    public VideoRecorder(int width, int height, int fps, int bitrateBps = 1_500_000)
    {
        _width = width;
        _height = height;
        _fps = fps;
        _bitrateBps = bitrateBps;
        _strideBytes = width * 4; // BGRA32
    }

    /// <summary>
    /// 开始录制到指定 MP4 文件。
    /// </summary>
    /// <param name="outputPath">输出 MP4 文件路径。</param>
    public void StartRecording(string outputPath)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (_isRecording)
            throw new InvalidOperationException("Already recording.");

        try
        {
            // 创建 SinkWriter
            using var writerAttrs = MediaFactory.MFCreateAttributes(1);
            writerAttrs.Set(SinkWriterAttributeKeys.ReadwriteEnableHardwareTransforms, true);

            _sinkWriter = MediaFactory.MFCreateSinkWriterFromURL(outputPath, null, writerAttrs);

            // 配置输出流（H.264）
            using var outputType = MediaFactory.MFCreateMediaType();
            outputType.Set(MediaTypeAttributeKeys.MajorType, MediaTypeGuids.Video);
            outputType.Set(MediaTypeAttributeKeys.Subtype, VideoFormatGuids.H264);
            outputType.Set(MediaTypeAttributeKeys.AvgBitrate, (uint)_bitrateBps);
            outputType.Set(MediaTypeAttributeKeys.InterlaceMode, (uint)2); // Progressive
            MediaFactory.MFSetAttributeSize(outputType, MediaTypeAttributeKeys.FrameSize,
                (uint)_width, (uint)_height);
            MediaFactory.MFSetAttributeRatio(outputType, MediaTypeAttributeKeys.FrameRate,
                (uint)_fps, 1);

            _videoStreamIndex = _sinkWriter.AddStream(outputType);

            // 配置输入流（RGB32）
            using var inputType = MediaFactory.MFCreateMediaType();
            inputType.Set(MediaTypeAttributeKeys.MajorType, MediaTypeGuids.Video);
            inputType.Set(MediaTypeAttributeKeys.Subtype, VideoFormatGuids.Rgb32);
            inputType.Set(MediaTypeAttributeKeys.InterlaceMode, (uint)2); // Progressive
            MediaFactory.MFSetAttributeSize(inputType, MediaTypeAttributeKeys.FrameSize,
                (uint)_width, (uint)_height);
            MediaFactory.MFSetAttributeRatio(inputType, MediaTypeAttributeKeys.FrameRate,
                (uint)_fps, 1);

            _sinkWriter.SetInputMediaType(_videoStreamIndex, inputType, null);

            // 开始写入
            _sinkWriter.BeginWriting();

            // 创建 .tsidx 索引文件
            string indexPath = Path.ChangeExtension(outputPath, ".tsidx");
            _indexStream = new FileStream(indexPath, FileMode.Create, FileAccess.Write, FileShare.Read);
            _indexWriter = new BinaryWriter(_indexStream);
            WriteTsidxHeader();

            _frameIndex = 0;
            _startTimestamp100ns = -1;
            _isRecording = true;

            Trace.TraceInformation("[VideoRecorder] Recording started: {0}", outputPath);
        }
        catch (Exception ex)
        {
            Trace.TraceError("[VideoRecorder] Failed to start recording: {0}", ex.Message);
            CleanupRecording();
            throw;
        }
    }

    /// <summary>
    /// 写入一帧视频数据。
    /// </summary>
    /// <param name="frame">帧元数据。</param>
    /// <param name="pixelData">BGRA32 像素数据。</param>
    public void WriteFrame(in VideoFrame frame, ReadOnlySpan<byte> pixelData)
    {
        if (!_isRecording || _sinkWriter == null)
            return;

        try
        {
            int bufferSize = _strideBytes * _height;
            int dataSize = Math.Min(pixelData.Length, bufferSize);

            // 计算 PresentationTime (100ns 单位)
            long presentationTime100ns;
            if (_startTimestamp100ns < 0)
            {
                _startTimestamp100ns = frame.TimestampUs * 10; // μs → 100ns
                presentationTime100ns = 0;
            }
            else
            {
                presentationTime100ns = (frame.TimestampUs * 10) - _startTimestamp100ns;
            }

            // 创建 MF 样本
            using var mfBuffer = MediaFactory.MFCreateMemoryBuffer(bufferSize);
            mfBuffer.Lock(out nint dataPtr, out _, out _);

            try
            {
                unsafe
                {
                    fixed (byte* srcPtr = pixelData)
                    {
                        Buffer.MemoryCopy(srcPtr, (void*)dataPtr, bufferSize, dataSize);
                    }
                }
                mfBuffer.CurrentLength = dataSize;
            }
            finally
            {
                mfBuffer.Unlock();
            }

            using var mfSample = MediaFactory.MFCreateSample();
            mfSample.AddBuffer(mfBuffer);
            mfSample.SampleTime = presentationTime100ns;
            mfSample.SampleDuration = 10_000_000L / _fps; // 100ns 单位

            _sinkWriter.WriteSample(_videoStreamIndex, mfSample);

            // 写入 .tsidx 索引条目
            WriteTsidxEntry(frame.TimestampUs, presentationTime100ns, _frameIndex);
            _frameIndex++;
        }
        catch (Exception ex)
        {
            Trace.TraceError("[VideoRecorder] WriteFrame error: {0}", ex.Message);
        }
    }

    /// <summary>
    /// 停止录制。
    /// </summary>
    public void StopRecording()
    {
        if (!_isRecording)
            return;

        try
        {
            _sinkWriter?.Finalize();
            Trace.TraceInformation("[VideoRecorder] Recording stopped. {0} frames written.", _frameIndex);
        }
        catch (Exception ex)
        {
            Trace.TraceError("[VideoRecorder] Finalize error: {0}", ex.Message);
        }
        finally
        {
            CleanupRecording();
            _isRecording = false;
        }
    }

    /// <summary>
    /// 写入 .tsidx 文件头。
    /// </summary>
    private void WriteTsidxHeader()
    {
        if (_indexWriter == null) return;

        _indexWriter.Write(TsidxMagic);          // 4 bytes: "TSIX"
        _indexWriter.Write(TsidxVersion);         // 4 bytes: version
        _indexWriter.Write(TsidxEntrySize);       // 4 bytes: entry size
        _indexWriter.Write(0);                    // 4 bytes: reserved
        _indexWriter.Flush();
    }

    /// <summary>
    /// 写入 .tsidx 索引条目。
    /// </summary>
    private void WriteTsidxEntry(long timestampUs, long presentationTime100ns, int frameIndex)
    {
        if (_indexWriter == null) return;

        _indexWriter.Write(timestampUs);           // 8 bytes
        _indexWriter.Write(presentationTime100ns);  // 8 bytes
        _indexWriter.Write(frameIndex);             // 4 bytes
        _indexWriter.Flush();
    }

    private void CleanupRecording()
    {
        _indexWriter?.Dispose();
        _indexWriter = null;

        _indexStream?.Dispose();
        _indexStream = null;

        _sinkWriter?.Dispose();
        _sinkWriter = null;
    }

    public void Dispose()
    {
        if (_disposed) return;

        if (_isRecording)
            StopRecording();

        _disposed = true;
    }
}
