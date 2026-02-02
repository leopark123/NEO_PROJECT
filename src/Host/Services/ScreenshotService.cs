// ScreenshotService.cs
// 截图服务 - S4-04
//
// 依据: CONSENSUS_BASELINE.md §12.7 (截图功能)
//       00_CONSTITUTION.md 铁律1 (原始数据不可修改 — 截图为 WYSIWYG)
//       ARCHITECTURE.md §8.2 (screenshots/ 目录)

using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using Neo.Rendering.Core;
using Neo.Storage;

namespace Neo.Host.Services;

/// <summary>
/// 截图服务：捕获当前渲染画面并保存为 PNG。
/// 文件名包含精确到秒的日期时间。
/// </summary>
public sealed class ScreenshotService
{
    private readonly D2DRenderTarget _renderTarget;
    private string _screenshotDirectory;
    private readonly AuditLog? _auditLog;

    /// <summary>
    /// 最近一次截图的完整路径。
    /// </summary>
    public string? LastScreenshotPath { get; private set; }

    public ScreenshotService(D2DRenderTarget renderTarget, string screenshotDirectory, AuditLog? auditLog = null)
    {
        _renderTarget = renderTarget ?? throw new ArgumentNullException(nameof(renderTarget));
        _screenshotDirectory = screenshotDirectory;
        _auditLog = auditLog;
    }

    /// <summary>
    /// 捕获当前画面并保存为 PNG。
    /// </summary>
    /// <returns>保存的文件完整路径，失败返回 null。</returns>
    public string? CaptureAndSave()
    {
        try
        {
            // 确保目录存在
            Directory.CreateDirectory(_screenshotDirectory);

            using var bitmap = _renderTarget.CaptureScreenshot();
            if (bitmap == null)
            {
                Trace.TraceWarning("[ScreenshotService] Failed to capture screenshot: render target returned null");
                return null;
            }

            // 生成文件名：NEO_截图_yyyyMMdd_HHmmss.png
            string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            string baseName = $"NEO_截图_{timestamp}";
            string filePath = GetUniqueFilePath(baseName, ".png");

            bitmap.Save(filePath, ImageFormat.Png);

            _auditLog?.Log("SCREENSHOT", null, null, filePath,
                $"{{\"width\":{bitmap.Width},\"height\":{bitmap.Height},\"format\":\"PNG\"}}");

            LastScreenshotPath = filePath;
            Trace.TraceInformation("[ScreenshotService] Screenshot saved: {0} ({1}x{2})",
                filePath, bitmap.Width, bitmap.Height);

            return filePath;
        }
        catch (Exception ex)
        {
            Trace.TraceError("[ScreenshotService] Screenshot failed: {0}", ex.Message);
            return null;
        }
    }

    /// <summary>
    /// 捕获当前画面并返回 Bitmap（不保存到磁盘）。
    /// 调用者负责 Dispose。
    /// </summary>
    public Bitmap? Capture()
    {
        try
        {
            return _renderTarget.CaptureScreenshot();
        }
        catch (Exception ex)
        {
            Trace.TraceError("[ScreenshotService] Capture failed: {0}", ex.Message);
            return null;
        }
    }

    /// <summary>
    /// 获取不冲突的文件路径（同名文件自动追加序号）。
    /// </summary>
    private string GetUniqueFilePath(string baseName, string extension)
    {
        string path = Path.Combine(_screenshotDirectory, baseName + extension);
        if (!File.Exists(path))
            return path;

        for (int i = 2; i < 10000; i++)
        {
            path = Path.Combine(_screenshotDirectory, $"{baseName}_{i}{extension}");
            if (!File.Exists(path))
                return path;
        }

        // 极端情况：使用 GUID
        return Path.Combine(_screenshotDirectory, $"{baseName}_{Guid.NewGuid():N}{extension}");
    }
}
