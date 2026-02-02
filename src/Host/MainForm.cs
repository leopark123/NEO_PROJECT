// MainForm.cs
// NEO 主窗口 - S1-06 系统集成

using System.Diagnostics;
using System.Drawing;
using Microsoft.Data.Sqlite;
using Neo.Core.Enums;
using Neo.Core.Interfaces;
using Neo.Core.Models;
using Neo.DSP.Filters;
using Neo.Host.Services;
using Neo.Infrastructure.Buffers;
using Neo.Mock;
using Neo.Rendering.Core;
using Neo.Rendering.EEG;
using Neo.Rendering.Layers;
using Neo.Storage;
using Vortice.Mathematics;

namespace Neo.Host;

/// <summary>
/// NEO 主窗口。
/// 集成所有 S1 模块的最小可运行系统。
/// </summary>
/// <remarks>
/// S1-06 系统集成 (TASK-S1-06)：
/// - EEG 数据源 → EegRingBuffer → 渲染管线 → 屏幕显示
/// - 只做"接线"，不新增功能
///
/// 初始化顺序 (TASK-S1-06 §4.1):
/// 1. Clock（Stopwatch）
/// 2. Buffer（EegRingBuffer）
/// 3. DataSource（src/Mock/MockEegSource，注入 Host 时间基准）
/// 4. Renderer（D2DRenderTarget + LayeredRenderer）
/// 5. Window（MainForm）
/// </remarks>
public partial class MainForm : Form
{
    // === 时钟 ===
    private readonly Stopwatch _clock = new();

    // === 数据源 ===
    private readonly MockEegSource _eegSource;

    // === 缓冲区 ===
    private readonly EegRingBuffer _eegBuffer;

    // === 渲染 ===
    private D2DRenderTarget? _renderTarget;
    private LayeredRenderer? _layeredRenderer;
    private readonly System.Windows.Forms.Timer _renderTimer;

    // === NIRS 集成壳 (S3-01) ===
    private readonly NirsWiring _nirsWiring;

    // === 视频采集 (S3-02) ===
    private readonly VideoWiring _videoWiring;

    // === 审计日志 (AT-21) ===
    private AuditLog? _auditLog;
    private SqliteConnection? _auditConn;

    // === 截图/打印/导出 (S4-04) ===
    private ScreenshotService? _screenshotService;
    private PrintService? _printService;
    private UsbExportService? _usbExportService;

    // === DSP 滤波链 (AT-21 审计) ===
    private EegFilterChain? _filterChain;
    private LowPassCutoff _currentLpfCutoff = LowPassCutoff.Hz35;
    private HighPassCutoff _currentHpfCutoff = HighPassCutoff.Hz0_5;
    private EegGainSetting _currentGain = EegGainScaler.DefaultGain;

    // === 状态 ===
    private long _frameNumber;
    private long _sessionStartUs;
    private bool _isRunning;

    // 常量
    private const int RenderFps = 60;
    private const int BufferCapacity = 1600;  // 10 秒 @ 160Hz
    private const double SecondsPerScreen = 10.0;

    public MainForm()
    {
        // 窗口设置
        Text = "NEO EEG Monitor - S1-06 Integration";
        Size = new System.Drawing.Size(1280, 720);
        DoubleBuffered = false;  // 使用 D2D 渲染

        // 初始化时钟
        _clock.Start();
        _sessionStartUs = GetTimestampUs();

        // 初始化缓冲区 (10秒 @ 160Hz)
        _eegBuffer = EegRingBuffer.CreateForSeconds(10);

        // 初始化数据源（注入统一时间基准）
        _eegSource = new MockEegSource(GetTimestampUs);
        _eegSource.SampleReceived += OnEegSampleReceived;

        // S3-01: 注册 NIRS 集成壳（当前 Blocked）
        _nirsWiring = new NirsWiring();

        // S3-02: 注册视频采集模块（优雅降级：无摄像头时正常运行）
        _videoWiring = new VideoWiring(GetTimestampUs);

        // 初始化渲染定时器
        _renderTimer = new System.Windows.Forms.Timer
        {
            Interval = 1000 / RenderFps
        };
        _renderTimer.Tick += OnRenderTick;

        // 窗口事件
        Load += OnFormLoad;
        FormClosing += OnFormClosing;
        Resize += OnFormResize;

        // S4-04: 键盘快捷键
        KeyPreview = true;
        KeyDown += OnKeyDown;
    }

    /// <summary>
    /// 获取当前时间戳（微秒）。
    /// 使用 Stopwatch 确保单调递增。
    /// </summary>
    private long GetTimestampUs()
    {
        return _clock.ElapsedTicks * 1_000_000 / Stopwatch.Frequency;
    }

    /// <summary>
    /// 窗口加载事件。
    /// 初始化渲染目标和开始采集。
    /// </summary>
    private void OnFormLoad(object? sender, EventArgs e)
    {
        // 初始化渲染目标
        _renderTarget = new D2DRenderTarget();
        if (!_renderTarget.Initialize(Handle, new System.Drawing.Size(ClientSize.Width, ClientSize.Height)))
        {
            MessageBox.Show("Failed to initialize render target", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            Close();
            return;
        }

        _renderTarget.DeviceLost += OnDeviceLost;
        _renderTarget.DeviceRestored += OnDeviceRestored;

        // 创建分层渲染器
        _layeredRenderer = LayeredRenderer.CreateDefault();

        // AT-21: 初始化审计日志
        try
        {
            var auditDbPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "neo_audit.db");
            _auditConn = new SqliteConnection($"Data Source={auditDbPath}");
            _auditConn.Open();
            using var createCmd = _auditConn.CreateCommand();
            createCmd.CommandText = """
                CREATE TABLE IF NOT EXISTS audit_log (
                    id INTEGER PRIMARY KEY,
                    timestamp_us INTEGER NOT NULL,
                    event_type TEXT NOT NULL,
                    session_id INTEGER,
                    old_value TEXT,
                    new_value TEXT,
                    details TEXT
                );
                CREATE INDEX IF NOT EXISTS idx_audit_t ON audit_log(timestamp_us);
                CREATE INDEX IF NOT EXISTS idx_audit_type ON audit_log(event_type);
                """;
            createCmd.ExecuteNonQuery();
            _auditLog = new AuditLog(_auditConn);
        }
        catch (Exception ex)
        {
            Trace.TraceWarning("[MainForm] Failed to initialize audit log: {0}", ex.Message);
        }

        // S4-04: 初始化截图/打印/导出服务（注入审计日志）
        string screenshotDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "screenshots");
        _screenshotService = new ScreenshotService(_renderTarget, screenshotDir, _auditLog);
        _printService = new PrintService(_auditLog);
        _usbExportService = new UsbExportService(_auditLog);

        // 初始化 DSP 滤波链（AT-21: 可审计的滤波参数管理）
        _filterChain = new EegFilterChain(new EegFilterChainConfig
        {
            NotchFrequency = NotchFrequency.Hz50,
            HighPassCutoff = _currentHpfCutoff,
            LowPassCutoff = _currentLpfCutoff,
            ChannelCount = 4
        });

        // 开始数据采集
        _eegSource.Start();

        // S3-01: 启动 NIRS 集成壳（当前仅记录 Blocked 状态）
        _nirsWiring.Start();

        // S3-02: 启动视频采集（无摄像头时记录警告，不阻塞）
        _videoWiring.Start();

        _isRunning = true;

        // AT-21: 记录监控启动事件
        try
        {
            _auditLog?.Log("MONITORING_START", null, null, null,
                "{\"source\":\"MockEegSource\",\"sampleRate\":160,\"channels\":4}");
        }
        catch (Exception ex)
        {
            Trace.TraceWarning("[MainForm] Failed to log MONITORING_START: {0}", ex.Message);
        }

        // 开始渲染循环
        _renderTimer.Start();
    }

    /// <summary>
    /// EEG 样本接收事件。
    /// 将样本写入环形缓冲区。
    /// </summary>
    private void OnEegSampleReceived(EegSample sample)
    {
        if (!_isRunning)
            return;

        // 写入环形缓冲区
        _eegBuffer.Write(in sample);
    }

    /// <summary>
    /// 渲染定时器事件。
    /// 执行渲染循环。
    /// </summary>
    private void OnRenderTick(object? sender, EventArgs e)
    {
        if (!_isRunning || _renderTarget == null || _layeredRenderer == null)
            return;

        if (!_renderTarget.IsValid)
            return;

        // 获取当前时间
        long currentUs = GetTimestampUs();
        long relativeUs = currentUs - _sessionStartUs;

        // 计算可见时间范围
        long durationUs = (long)(SecondsPerScreen * 1_000_000);
        long endUs = relativeUs;
        long startUs = endUs - durationUs;
        if (startUs < 0) startUs = 0;

        // 从环形缓冲区获取可见范围内的样本
        var sampleBuffer = new EegSample[BufferCapacity];
        int sampleCount = _eegBuffer.GetRange(startUs, endUs, sampleBuffer);

        // 构建通道渲染数据
        var channels = BuildChannelRenderData(sampleBuffer.AsSpan(0, sampleCount));

        // 创建渲染上下文
        var renderContext = new RenderContext
        {
            CurrentTimestampUs = relativeUs,
            VisibleRange = new TimeRange(startUs, endUs),
            Zoom = new ZoomLevel(SecondsPerScreen, 0),
            Channels = channels,
            FrameNumber = _frameNumber++,
            ViewportWidth = _renderTarget.Width,
            ViewportHeight = _renderTarget.Height,
            Dpi = _renderTarget.Dpi
        };

        // 执行渲染
        _renderTarget.BeginDraw();

        // 清除背景
        _renderTarget.D2DContext?.Clear(new Color4(0.1f, 0.1f, 0.1f, 1.0f));

        // 渲染所有层
        _layeredRenderer.RenderFrame(
            _renderTarget.D2DContext!,
            _renderTarget.Resources,
            renderContext);

        _renderTarget.EndDraw();
    }

    /// <summary>
    /// 从样本数组构建通道渲染数据。
    /// </summary>
    private static List<ChannelRenderData> BuildChannelRenderData(ReadOnlySpan<EegSample> samples)
    {
        var channels = new List<ChannelRenderData>();

        if (samples.Length == 0)
            return channels;

        // 为每个通道创建渲染数据
        string[] channelNames = ["CH1 (C3-P3)", "CH2 (C4-P4)", "CH3 (P3-P4)", "CH4 (C3-C4)"];

        for (int ch = 0; ch < 4; ch++)
        {
            var dataPoints = new float[samples.Length];
            var qualityFlags = new byte[samples.Length];

            for (int i = 0; i < samples.Length; i++)
            {
                ref readonly var sample = ref samples[i];
                dataPoints[i] = ch switch
                {
                    0 => (float)sample.Ch1Uv,
                    1 => (float)sample.Ch2Uv,
                    2 => (float)sample.Ch3Uv,
                    3 => (float)sample.Ch4Uv,
                    _ => 0f
                };
                qualityFlags[i] = (byte)sample.QualityFlags;
            }

            channels.Add(new ChannelRenderData
            {
                ChannelIndex = ch,
                ChannelName = channelNames[ch],
                DataPoints = dataPoints,
                StartTimestampUs = samples[0].TimestampUs,
                SampleIntervalUs = 6250,  // 160 Hz
                QualityFlags = qualityFlags
            });
        }

        return channels;
    }

    /// <summary>
    /// 窗口大小变化事件。
    /// </summary>
    private void OnFormResize(object? sender, EventArgs e)
    {
        if (_renderTarget != null && ClientSize.Width > 0 && ClientSize.Height > 0)
        {
            _renderTarget.Resize(new System.Drawing.Size(ClientSize.Width, ClientSize.Height));
            _layeredRenderer?.InvalidateAll();
        }
    }

    /// <summary>
    /// 设备丢失事件。
    /// </summary>
    private void OnDeviceLost()
    {
        // 设备丢失时暂停渲染
        _renderTimer.Stop();

        // AT-21: 记录设备丢失事件
        try
        {
            _auditLog?.Log("DEVICE_LOST", null, null, null,
                $"{{\"timestampUs\":{GetTimestampUs()}}}");
        }
        catch (Exception ex)
        {
            Trace.TraceWarning("[MainForm] Failed to log DEVICE_LOST: {0}", ex.Message);
        }
    }

    /// <summary>
    /// 设备恢复事件。
    /// </summary>
    private void OnDeviceRestored()
    {
        // 设备恢复后继续渲染
        _renderTimer.Start();

        // AT-21: 记录设备恢复事件
        try
        {
            _auditLog?.Log("DEVICE_RESTORED", null, null, null,
                $"{{\"timestampUs\":{GetTimestampUs()}}}");
        }
        catch (Exception ex)
        {
            Trace.TraceWarning("[MainForm] Failed to log DEVICE_RESTORED: {0}", ex.Message);
        }
    }

    // ========== S4-04: 截图/打印/导出 ==========

    /// <summary>
    /// 键盘快捷键处理。
    /// Ctrl+P: 截图  |  Ctrl+Shift+P: 打印预览  |  Ctrl+E: USB 导出
    /// F5: 增益降低（灵敏度提高）  |  F6: 增益增加（灵敏度降低）
    /// F7: LPF 截止频率降低  |  F8: LPF 截止频率增加
    /// </summary>
    private void OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Control && !e.Shift && e.KeyCode == Keys.P)
        {
            e.Handled = true;
            e.SuppressKeyPress = true;
            DoScreenshot();
        }
        else if (e.Control && e.Shift && e.KeyCode == Keys.P)
        {
            e.Handled = true;
            e.SuppressKeyPress = true;
            DoPrintPreview();
        }
        else if (e.Control && e.KeyCode == Keys.E)
        {
            e.Handled = true;
            e.SuppressKeyPress = true;
            DoUsbExport();
        }
        else if (e.KeyCode == Keys.F5)
        {
            e.Handled = true;
            DoCycleGain(-1);
        }
        else if (e.KeyCode == Keys.F6)
        {
            e.Handled = true;
            DoCycleGain(+1);
        }
        else if (e.KeyCode == Keys.F7)
        {
            e.Handled = true;
            DoCycleLpfCutoff(-1);
        }
        else if (e.KeyCode == Keys.F8)
        {
            e.Handled = true;
            DoCycleLpfCutoff(+1);
        }
    }

    /// <summary>
    /// 执行截图并保存。
    /// </summary>
    private void DoScreenshot()
    {
        if (_screenshotService == null) return;

        var path = _screenshotService.CaptureAndSave();
        if (path != null)
            MessageBox.Show(this, $"截图已保存:\n{path}", "截图", MessageBoxButtons.OK, MessageBoxIcon.Information);
        else
            MessageBox.Show(this, "截图失败。请确认渲染已启动。", "截图", MessageBoxButtons.OK, MessageBoxIcon.Warning);
    }

    /// <summary>
    /// 打开打印预览（含可编辑结论文本）。
    /// </summary>
    private void DoPrintPreview()
    {
        if (_screenshotService == null) return;

        using var screenshot = _screenshotService.Capture();
        if (screenshot == null)
        {
            MessageBox.Show(this, "无法捕获当前画面用于打印。", "打印", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        // 构建时间范围和患者信息
        long currentUs = GetTimestampUs();
        long relativeUs = currentUs - _sessionStartUs;
        long durationUs = (long)(SecondsPerScreen * 1_000_000);
        long startUs = Math.Max(0, relativeUs - durationUs);

        string startTime = FormatTimeFromUs(startUs);
        string endTime = FormatTimeFromUs(relativeUs);
        string timeRange = $"{startTime} - {endTime}";
        string patientInfo = "床位: --- / 姓名: --- / ID: ---";

        _printService?.ShowPrintPreview(this, screenshot, timeRange, patientInfo);
    }

    /// <summary>
    /// 导出最近截图到 USB。如果没有截图，先执行截图。
    /// </summary>
    private void DoUsbExport()
    {
        if (_screenshotService == null) return;

        // 如果没有已保存的截图，先执行截图
        string? filePath = _screenshotService.LastScreenshotPath;
        if (filePath == null || !File.Exists(filePath))
        {
            filePath = _screenshotService.CaptureAndSave();
            if (filePath == null)
            {
                MessageBox.Show(this, "无法捕获截图用于导出。", "导出", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
        }

        if (_usbExportService == null) return;
        var result = _usbExportService.ExportFile(this, filePath);
        if (result.Success)
            MessageBox.Show(this, result.Message, "USB 导出", MessageBoxButtons.OK, MessageBoxIcon.Information);
        else
            MessageBox.Show(this, result.Message, "USB 导出", MessageBoxButtons.OK, MessageBoxIcon.Warning);
    }

    // ========== AT-21: 审计日志 - 滤波器/增益变更 ==========

    /// <summary>
    /// 切换增益设置（F5 降低 / F6 增加）。
    /// 每次变更记录审计日志。
    /// </summary>
    private void DoCycleGain(int direction)
    {
        var gains = EegGainScaler.AvailableGains;
        int idx = Array.IndexOf(gains, _currentGain);
        int newIdx = Math.Clamp(idx + direction, 0, gains.Length - 1);

        if (newIdx == idx) return; // 已在边界

        var oldGain = _currentGain;
        _currentGain = gains[newIdx];

        // AT-21: 记录增益变更
        try
        {
            _auditLog?.Log("GAIN_CHANGE", null,
                ((int)oldGain).ToString(),
                ((int)_currentGain).ToString(),
                $"{{\"oldUvPerCm\":{(int)oldGain},\"newUvPerCm\":{(int)_currentGain}}}");
        }
        catch (Exception ex)
        {
            Trace.TraceWarning("[MainForm] Failed to log GAIN_CHANGE: {0}", ex.Message);
        }

        Text = $"NEO EEG Monitor - Gain: {EegGainScaler.GetDisplayText(_currentGain)}";
        Trace.TraceInformation("[MainForm] Gain changed: {0} → {1}",
            EegGainScaler.GetDisplayText(oldGain), EegGainScaler.GetDisplayText(_currentGain));
    }

    /// <summary>
    /// 切换 LPF 截止频率（F7 降低 / F8 增加）。
    /// 每次变更重建滤波链并记录审计日志。
    /// </summary>
    private void DoCycleLpfCutoff(int direction)
    {
        var cutoffs = new[] { LowPassCutoff.Hz15, LowPassCutoff.Hz35, LowPassCutoff.Hz50, LowPassCutoff.Hz70 };
        int idx = Array.IndexOf(cutoffs, _currentLpfCutoff);
        if (idx < 0) idx = 1; // default to Hz35
        int newIdx = Math.Clamp(idx + direction, 0, cutoffs.Length - 1);

        if (newIdx == idx) return;

        var oldCutoff = _currentLpfCutoff;
        _currentLpfCutoff = cutoffs[newIdx];

        // 重建滤波链
        _filterChain?.Dispose();
        _filterChain = new EegFilterChain(new EegFilterChainConfig
        {
            NotchFrequency = NotchFrequency.Hz50,
            HighPassCutoff = _currentHpfCutoff,
            LowPassCutoff = _currentLpfCutoff,
            ChannelCount = 4
        });

        // AT-21: 记录滤波器参数变更
        try
        {
            _auditLog?.Log("FILTER_CHANGE", null,
                oldCutoff.ToString(),
                _currentLpfCutoff.ToString(),
                $"{{\"parameter\":\"LowPassCutoff\"}}");
        }
        catch (Exception ex)
        {
            Trace.TraceWarning("[MainForm] Failed to log FILTER_CHANGE: {0}", ex.Message);
        }

        Trace.TraceInformation("[MainForm] LPF cutoff changed: {0} → {1}", oldCutoff, _currentLpfCutoff);
    }

    /// <summary>
    /// 将微秒时间戳格式化为 HH:mm:ss。
    /// </summary>
    private static string FormatTimeFromUs(long us)
    {
        var ts = TimeSpan.FromMicroseconds(us);
        return ts.ToString(@"hh\:mm\:ss");
    }

    /// <summary>
    /// 窗口关闭事件。
    /// </summary>
    private void OnFormClosing(object? sender, FormClosingEventArgs e)
    {
        // AT-21: 记录监控停止事件
        try
        {
            long uptimeUs = GetTimestampUs() - _sessionStartUs;
            _auditLog?.Log("MONITORING_STOP", null, null, null,
                $"{{\"uptimeUs\":{uptimeUs},\"frames\":{_frameNumber}}}");
        }
        catch (Exception ex)
        {
            Trace.TraceWarning("[MainForm] Failed to log MONITORING_STOP: {0}", ex.Message);
        }

        _isRunning = false;
        _renderTimer.Stop();

        _eegSource.Stop();
        _eegSource.Dispose();

        // S3-01: 停止 NIRS 集成壳
        _nirsWiring.Dispose();

        // S3-02: 停止视频采集
        _videoWiring.Dispose();

        _filterChain?.Dispose();
        _layeredRenderer?.Dispose();
        _renderTarget?.Dispose();

        // AT-21: 关闭审计日志连接
        _auditConn?.Dispose();
    }
}
