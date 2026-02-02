// PrintService.cs
// 打印预览与打印服务 - S4-04
//
// 依据: CONSENSUS_BASELINE.md §12.7 (打印功能)
//       00_CONSTITUTION.md 铁律1 (原始数据不可修改)

using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.Drawing.Printing;
using Neo.Storage;

namespace Neo.Host.Services;

/// <summary>
/// 打印服务：提供打印预览对话框（含可编辑结论文本）和打印输出。
/// 打印内容包含：截图图像、时间范围、患者信息占位、结论文本。
/// </summary>
public sealed class PrintService
{
    private readonly AuditLog? _auditLog;

    public PrintService(AuditLog? auditLog = null)
    {
        _auditLog = auditLog;
    }

    /// <summary>
    /// 显示打印预览对话框。
    /// </summary>
    /// <param name="owner">父窗口。</param>
    /// <param name="screenshot">要打印的截图。调用者保留所有权。</param>
    /// <param name="timeRangeText">时间范围文本（如 "08:16:58 - 11:16:58"）。</param>
    /// <param name="patientInfo">患者信息文本（如 "床位: 41 / 姓名: ---"）。</param>
    public void ShowPrintPreview(Form owner, Bitmap screenshot, string timeRangeText, string patientInfo)
    {
        using var previewForm = new PrintPreviewForm(screenshot, timeRangeText, patientInfo, _auditLog);
        previewForm.ShowDialog(owner);
    }
}

/// <summary>
/// 打印预览窗口：显示截图、时间范围、患者信息、可编辑结论文本。
/// </summary>
internal sealed class PrintPreviewForm : Form
{
    private readonly Bitmap _screenshot;
    private readonly string _timeRangeText;
    private readonly string _patientInfo;
    private readonly AuditLog? _auditLog;

    private TextBox _conclusionTextBox = null!;
    private PrintPreviewControl _previewControl = null!;
    private PrintDocument _printDocument = null!;

    public PrintPreviewForm(Bitmap screenshot, string timeRangeText, string patientInfo, AuditLog? auditLog = null)
    {
        _screenshot = screenshot;
        _timeRangeText = timeRangeText;
        _patientInfo = patientInfo;
        _auditLog = auditLog;

        InitializeComponents();
        SetupPrintDocument();
    }

    private void InitializeComponents()
    {
        Text = "NEO 打印预览";
        Size = new Size(900, 700);
        StartPosition = FormStartPosition.CenterParent;
        MinimizeBox = false;
        MaximizeBox = true;
        FormBorderStyle = FormBorderStyle.Sizable;

        // === 顶部面板：结论编辑区 ===
        var topPanel = new Panel
        {
            Dock = DockStyle.Top,
            Height = 130,
            Padding = new Padding(10)
        };

        var conclusionLabel = new Label
        {
            Text = "结论 / 备注（可编辑，将打印在截图下方）：",
            Dock = DockStyle.Top,
            Height = 22,
            Font = new Font("Microsoft YaHei UI", 9f)
        };

        _conclusionTextBox = new TextBox
        {
            Dock = DockStyle.Fill,
            Multiline = true,
            ScrollBars = ScrollBars.Vertical,
            Font = new Font("Microsoft YaHei UI", 10f),
            Text = ""
        };

        topPanel.Controls.Add(_conclusionTextBox);
        topPanel.Controls.Add(conclusionLabel);

        // === 底部面板：按钮 ===
        var bottomPanel = new Panel
        {
            Dock = DockStyle.Bottom,
            Height = 50,
            Padding = new Padding(10, 8, 10, 8)
        };

        var printButton = new Button
        {
            Text = "打印(&P)",
            Width = 100,
            Height = 34,
            Dock = DockStyle.Right,
            Font = new Font("Microsoft YaHei UI", 9f)
        };
        printButton.Click += OnPrintClick;

        var cancelButton = new Button
        {
            Text = "关闭(&C)",
            Width = 100,
            Height = 34,
            Dock = DockStyle.Right,
            Font = new Font("Microsoft YaHei UI", 9f)
        };
        cancelButton.Click += (_, _) => Close();

        var refreshButton = new Button
        {
            Text = "刷新预览(&R)",
            Width = 110,
            Height = 34,
            Dock = DockStyle.Right,
            Font = new Font("Microsoft YaHei UI", 9f)
        };
        refreshButton.Click += (_, _) => RefreshPreview();

        bottomPanel.Controls.Add(printButton);
        bottomPanel.Controls.Add(refreshButton);
        bottomPanel.Controls.Add(cancelButton);

        // === 中间：打印预览控件 ===
        _previewControl = new PrintPreviewControl
        {
            Dock = DockStyle.Fill,
            Zoom = 1.0,
            AutoZoom = true
        };

        Controls.Add(_previewControl);
        Controls.Add(bottomPanel);
        Controls.Add(topPanel);
    }

    private void SetupPrintDocument()
    {
        _printDocument = new PrintDocument();
        _printDocument.DocumentName = "NEO EEG Report";
        _printDocument.PrintPage += OnPrintPage;
        _previewControl.Document = _printDocument;
    }

    private void RefreshPreview()
    {
        _previewControl.InvalidatePreview();
    }

    private void OnPrintClick(object? sender, EventArgs e)
    {
        try
        {
            using var printDialog = new PrintDialog
            {
                Document = _printDocument,
                UseEXDialog = true
            };

            if (printDialog.ShowDialog(this) == DialogResult.OK)
            {
                _printDocument.Print();

                _auditLog?.Log("PRINT", null, null, null,
                    $"{{\"timeRange\":\"{_timeRangeText}\",\"patientInfo\":\"{_patientInfo}\"}}");

                Trace.TraceInformation("[PrintService] Print job sent successfully");
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, $"打印失败：{ex.Message}", "打印错误",
                MessageBoxButtons.OK, MessageBoxIcon.Error);
            Trace.TraceError("[PrintService] Print failed: {0}", ex.Message);
        }
    }

    private void OnPrintPage(object sender, PrintPageEventArgs e)
    {
        if (e.Graphics == null) return;

        var g = e.Graphics;
        var bounds = e.MarginBounds;

        float y = bounds.Top;
        float contentWidth = bounds.Width;

        // === Header ===
        using var headerFont = new Font("Microsoft YaHei UI", 14f, FontStyle.Bold);
        using var subFont = new Font("Microsoft YaHei UI", 10f);
        using var bodyFont = new Font("Microsoft YaHei UI", 10f);
        using var blackBrush = new SolidBrush(Color.Black);
        using var grayBrush = new SolidBrush(Color.Gray);
        using var linePen = new Pen(Color.Black, 1.5f);

        // 标题
        g.DrawString("NEO 新生儿脑功能监护报告", headerFont, blackBrush, bounds.Left, y);
        y += headerFont.GetHeight(g) + 4;

        // 分隔线
        g.DrawLine(linePen, bounds.Left, y, bounds.Right, y);
        y += 8;

        // 患者信息 + 时间范围
        g.DrawString(_patientInfo, subFont, blackBrush, bounds.Left, y);
        y += subFont.GetHeight(g) + 2;

        g.DrawString($"记录时段: {_timeRangeText}", subFont, blackBrush, bounds.Left, y);
        y += subFont.GetHeight(g) + 2;

        g.DrawString($"打印时间: {DateTime.Now:yyyy-MM-dd HH:mm:ss}", subFont, grayBrush, bounds.Left, y);
        y += subFont.GetHeight(g) + 10;

        // === Screenshot Image ===
        float imageMaxHeight = bounds.Height * 0.55f;  // 截图占最多55%页面高度
        float imageWidth = contentWidth;
        float imageHeight = (float)_screenshot.Height / _screenshot.Width * imageWidth;

        if (imageHeight > imageMaxHeight)
        {
            imageHeight = imageMaxHeight;
            imageWidth = (float)_screenshot.Width / _screenshot.Height * imageHeight;
        }

        float imageX = bounds.Left + (contentWidth - imageWidth) / 2;
        g.DrawImage(_screenshot, imageX, y, imageWidth, imageHeight);
        y += imageHeight + 12;

        // 分隔线
        g.DrawLine(linePen, bounds.Left, y, bounds.Right, y);
        y += 8;

        // === 结论文本 ===
        string conclusion = _conclusionTextBox.Text.Trim();
        if (!string.IsNullOrEmpty(conclusion))
        {
            g.DrawString("结论 / 备注（用户输入）:", subFont, blackBrush, bounds.Left, y);
            y += subFont.GetHeight(g) + 4;

            var conclusionRect = new RectangleF(bounds.Left, y, contentWidth, bounds.Bottom - y - 40);
            g.DrawString(conclusion, bodyFont, blackBrush, conclusionRect);
        }

        // === Footer ===
        using var footerFont = new Font("Microsoft YaHei UI", 8f);
        string footer = $"NEO 新生儿脑功能监护系统  |  第 1 页  |  {DateTime.Now:yyyy-MM-dd}";
        var footerSize = g.MeasureString(footer, footerFont);
        g.DrawString(footer, footerFont, grayBrush,
            bounds.Left + (contentWidth - footerSize.Width) / 2,
            bounds.Bottom - footerSize.Height);

        e.HasMorePages = false;
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _printDocument?.Dispose();
        }
        base.Dispose(disposing);
    }
}
