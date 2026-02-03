// DpiHelper.cs
// DPI 处理工具 - 来源: ARCHITECTURE.md §5

using System.Runtime.InteropServices;

namespace Neo.Rendering.Device;

/// <summary>
/// DPI 处理工具类。
/// 提供 DPI 查询、DIP ↔ 像素转换功能。
/// </summary>
/// <remarks>
/// 依据: ARCHITECTURE.md §5
///
/// 使用 double 精度进行坐标转换，避免累积舍入误差。
/// 来源: ADR-006 精度要求
/// </remarks>
public static class DpiHelper
{
    /// <summary>
    /// 标准 DPI 值（96 DPI = 100% 缩放）。
    /// </summary>
    public const double StandardDpi = 96.0;

    /// <summary>
    /// 获取主监视器的系统 DPI。
    /// </summary>
    /// <returns>系统 DPI 值。</returns>
    public static double GetSystemDpi()
    {
        try
        {
            // 尝试使用 Per-Monitor DPI 感知
            var dpi = GetDpiForSystem();
            return dpi > 0 ? dpi : StandardDpi;
        }
        catch
        {
            return StandardDpi;
        }
    }

    /// <summary>
    /// 获取指定窗口的 DPI。
    /// </summary>
    /// <param name="hwnd">窗口句柄。</param>
    /// <returns>窗口 DPI 值。</returns>
    public static double GetWindowDpi(IntPtr hwnd)
    {
        if (hwnd == IntPtr.Zero)
            return GetSystemDpi();

        try
        {
            var dpi = GetDpiForWindow(hwnd);
            return dpi > 0 ? dpi : GetSystemDpi();
        }
        catch
        {
            return GetSystemDpi();
        }
    }

    /// <summary>
    /// 获取 DPI 缩放因子。
    /// </summary>
    /// <param name="dpi">DPI 值。</param>
    /// <returns>缩放因子（1.0 = 100%）。</returns>
    public static double GetScaleFactor(double dpi)
    {
        return dpi / StandardDpi;
    }

    /// <summary>
    /// 将设备无关像素（DIP）转换为物理像素。
    /// </summary>
    /// <param name="dip">DIP 值。</param>
    /// <param name="dpi">DPI 值。</param>
    /// <returns>物理像素值。</returns>
    /// <remarks>使用 double 精度避免累积舍入误差。</remarks>
    public static double DipToPixel(double dip, double dpi)
    {
        return dip * dpi / StandardDpi;
    }

    /// <summary>
    /// 将物理像素转换为设备无关像素（DIP）。
    /// </summary>
    /// <param name="pixel">物理像素值。</param>
    /// <param name="dpi">DPI 值。</param>
    /// <returns>DIP 值。</returns>
    /// <remarks>使用 double 精度避免累积舍入误差。</remarks>
    public static double PixelToDip(double pixel, double dpi)
    {
        return pixel * StandardDpi / dpi;
    }

    /// <summary>
    /// 将 DIP 转换为整数像素（向上取整）。
    /// </summary>
    /// <param name="dip">DIP 值。</param>
    /// <param name="dpi">DPI 值。</param>
    /// <returns>像素值（向上取整）。</returns>
    public static int DipToPixelCeiling(double dip, double dpi)
    {
        return (int)Math.Ceiling(DipToPixel(dip, dpi));
    }

    /// <summary>
    /// 将 DIP 转换为整数像素（四舍五入）。
    /// </summary>
    /// <param name="dip">DIP 值。</param>
    /// <param name="dpi">DPI 值。</param>
    /// <returns>像素值（四舍五入）。</returns>
    public static int DipToPixelRound(double dip, double dpi)
    {
        return (int)Math.Round(DipToPixel(dip, dpi), MidpointRounding.AwayFromZero);
    }

    #region Native Methods

    [DllImport("user32.dll")]
    private static extern uint GetDpiForSystem();

    [DllImport("user32.dll")]
    private static extern uint GetDpiForWindow(IntPtr hwnd);

    #endregion
}
