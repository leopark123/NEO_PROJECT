// UsbExportService.cs
// USB 导出服务 - S4-04
//
// 依据: CONSENSUS_BASELINE.md §12.7 (数据导出功能)
//       00_CONSTITUTION.md 铁律1 (原始数据不可修改)
//
// 安全约束:
// - 不写入系统盘
// - 不静默失败
// - 同名文件自动编号，不覆盖

using System.Diagnostics;
using Neo.Storage;

namespace Neo.Host.Services;

/// <summary>
/// USB 导出服务：检测可移动驱动器，安全复制文件。
/// </summary>
public sealed class UsbExportService
{
    private readonly AuditLog? _auditLog;

    public UsbExportService(AuditLog? auditLog = null)
    {
        _auditLog = auditLog;
    }

    /// <summary>
    /// 导出结果。
    /// </summary>
    public sealed record ExportResult(bool Success, string Message, string? DestinationPath = null);

    /// <summary>
    /// 将文件导出到 USB 驱动器。
    /// 如果有多个可移动驱动器，弹出选择对话框。
    /// 如果没有可移动驱动器，弹出文件夹浏览器（仅允许非系统盘）。
    /// </summary>
    /// <param name="owner">父窗口。</param>
    /// <param name="sourceFilePath">要导出的源文件路径。</param>
    /// <returns>导出结果。</returns>
    public ExportResult ExportFile(Form owner, string sourceFilePath)
    {
        if (!File.Exists(sourceFilePath))
            return new ExportResult(false, $"源文件不存在: {sourceFilePath}");

        // 检测可移动驱动器
        var removableDrives = GetRemovableDrives();

        string? targetDir = null;

        if (removableDrives.Count == 1)
        {
            // 单个 USB 驱动器：直接使用
            targetDir = removableDrives[0].RootDirectory.FullName;
        }
        else if (removableDrives.Count > 1)
        {
            // 多个 USB 驱动器：弹出选择对话框
            targetDir = ShowDriveSelectionDialog(owner, removableDrives);
        }

        if (targetDir == null)
        {
            // 无可移动驱动器或用户未选择：弹出文件夹浏览器
            targetDir = ShowFolderBrowser(owner);
        }

        if (targetDir == null)
            return new ExportResult(false, "导出已取消");

        // 安全检查：不写入系统盘
        if (IsSystemDrive(targetDir))
            return new ExportResult(false, "不允许导出到系统盘。请选择 USB 驱动器或其他非系统盘。");

        return CopyFileToTarget(sourceFilePath, targetDir);
    }

    /// <summary>
    /// 获取所有就绪的可移动驱动器。
    /// </summary>
    private static List<DriveInfo> GetRemovableDrives()
    {
        var drives = new List<DriveInfo>();
        try
        {
            foreach (var drive in DriveInfo.GetDrives())
            {
                if (drive.DriveType == DriveType.Removable && drive.IsReady)
                    drives.Add(drive);
            }
        }
        catch (Exception ex)
        {
            Trace.TraceWarning("[UsbExportService] Failed to enumerate drives: {0}", ex.Message);
        }
        return drives;
    }

    /// <summary>
    /// 显示驱动器选择对话框。
    /// </summary>
    private static string? ShowDriveSelectionDialog(Form owner, List<DriveInfo> drives)
    {
        using var dialog = new Form
        {
            Text = "选择 USB 驱动器",
            Size = new System.Drawing.Size(400, 300),
            StartPosition = FormStartPosition.CenterParent,
            FormBorderStyle = FormBorderStyle.FixedDialog,
            MinimizeBox = false,
            MaximizeBox = false
        };

        var listBox = new ListBox
        {
            Dock = DockStyle.Fill,
            Font = new System.Drawing.Font("Microsoft YaHei UI", 10f)
        };

        foreach (var drive in drives)
        {
            string label = $"{drive.Name}";
            try
            {
                long freeGB = drive.AvailableFreeSpace / (1024 * 1024 * 1024);
                label += $" ({drive.VolumeLabel}) - 可用 {freeGB} GB";
            }
            catch
            {
                // VolumeLabel or AvailableFreeSpace may throw
            }
            listBox.Items.Add(label);
        }

        if (listBox.Items.Count > 0)
            listBox.SelectedIndex = 0;

        var okButton = new Button
        {
            Text = "确定",
            Dock = DockStyle.Bottom,
            Height = 36,
            DialogResult = DialogResult.OK
        };

        dialog.Controls.Add(listBox);
        dialog.Controls.Add(okButton);
        dialog.AcceptButton = okButton;

        if (dialog.ShowDialog(owner) == DialogResult.OK && listBox.SelectedIndex >= 0)
            return drives[listBox.SelectedIndex].RootDirectory.FullName;

        return null;
    }

    /// <summary>
    /// 显示文件夹浏览器（用户手动选择路径）。
    /// </summary>
    private static string? ShowFolderBrowser(Form owner)
    {
        using var dialog = new FolderBrowserDialog
        {
            Description = "选择导出目标文件夹（建议选择 USB 驱动器）",
            ShowNewFolderButton = true,
            UseDescriptionForTitle = true
        };

        if (dialog.ShowDialog(owner) == DialogResult.OK)
            return dialog.SelectedPath;

        return null;
    }

    /// <summary>
    /// 检查路径是否在系统盘上。
    /// </summary>
    private static bool IsSystemDrive(string path)
    {
        try
        {
            string systemRoot = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
            if (string.IsNullOrEmpty(systemRoot))
                return false;

            string systemDrive = Path.GetPathRoot(systemRoot) ?? "";
            string targetDrive = Path.GetPathRoot(Path.GetFullPath(path)) ?? "";

            return string.Equals(systemDrive, targetDrive, StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// 安全复制文件到目标目录。同名文件自动追加序号。
    /// </summary>
    private static ExportResult CopyFileToTarget(string sourceFilePath, string targetDir)
    {
        try
        {
            string fileName = Path.GetFileNameWithoutExtension(sourceFilePath);
            string ext = Path.GetExtension(sourceFilePath);

            string destPath = GetUniqueFilePath(targetDir, fileName, ext);

            File.Copy(sourceFilePath, destPath, overwrite: false);

            _auditLog?.Log("USB_EXPORT", null, null, destPath,
                $"{{\"source\":\"{sourceFilePath.Replace("\\", "\\\\")}\",\"destination\":\"{destPath.Replace("\\", "\\\\")}\"}}");

            Trace.TraceInformation("[UsbExportService] File exported: {0} → {1}", sourceFilePath, destPath);
            return new ExportResult(true, $"导出成功: {destPath}", destPath);
        }
        catch (IOException ex) when (ex.Message.Contains("space", StringComparison.OrdinalIgnoreCase)
                                     || ex.HResult == unchecked((int)0x80070070))
        {
            return new ExportResult(false, "目标驱动器空间不足");
        }
        catch (UnauthorizedAccessException)
        {
            return new ExportResult(false, "没有写入权限。请检查 USB 驱动器是否写保护。");
        }
        catch (Exception ex)
        {
            return new ExportResult(false, $"导出失败: {ex.Message}");
        }
    }

    /// <summary>
    /// 获取不冲突的文件路径（同名文件自动追加序号，绝不覆盖）。
    /// </summary>
    private static string GetUniqueFilePath(string directory, string baseName, string extension)
    {
        string path = Path.Combine(directory, baseName + extension);
        if (!File.Exists(path))
            return path;

        for (int i = 2; i < 10000; i++)
        {
            path = Path.Combine(directory, $"{baseName}_{i}{extension}");
            if (!File.Exists(path))
                return path;
        }

        return Path.Combine(directory, $"{baseName}_{Guid.NewGuid():N}{extension}");
    }
}
