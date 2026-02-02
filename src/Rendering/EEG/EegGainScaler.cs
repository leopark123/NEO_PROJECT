// EegGainScaler.cs
// EEG 增益缩放器 - 来源: DSP_SPEC.md, CONSENSUS_BASELINE.md §6.3

namespace Neo.Rendering.EEG;

/// <summary>
/// EEG 增益设置（μV/cm）。
/// </summary>
/// <remarks>
/// 来源: CONSENSUS_BASELINE.md §6.3, ARCHITECTURE.md
///
/// 1000 μV/cm 是 S2-05 新增的必选项。
/// </remarks>
public enum EegGainSetting
{
    /// <summary>10 μV/cm - 最高灵敏度</summary>
    Gain10 = 10,

    /// <summary>20 μV/cm</summary>
    Gain20 = 20,

    /// <summary>50 μV/cm - 默认</summary>
    Gain50 = 50,

    /// <summary>70 μV/cm</summary>
    Gain70 = 70,

    /// <summary>100 μV/cm</summary>
    Gain100 = 100,

    /// <summary>200 μV/cm</summary>
    Gain200 = 200,

    /// <summary>1000 μV/cm - 最低灵敏度（S2-05 必选）</summary>
    Gain1000 = 1000
}

/// <summary>
/// EEG 增益缩放器。
/// 将 μV 值转换为像素偏移，基于增益设置和屏幕 DPI。
/// </summary>
/// <remarks>
/// 依据: DSP_SPEC.md, CONSENSUS_BASELINE.md §6.3
///
/// 设计原则:
/// - 纯函数映射，不含任何 DSP 处理
/// - 增益单位: μV/cm
/// - 1 cm 对应的像素数由 DPI 决定 (1 inch = 2.54 cm, 96 DPI = 37.8 px/cm)
///
/// 铁律约束:
/// - 铁律6: 渲染只做 Draw
/// - 铁律2: 不伪造波形
/// </remarks>
public sealed class EegGainScaler
{
    // 常量
    private const double InchesPerCm = 1.0 / 2.54;
    private const double DefaultDpi = 96.0;

    /// <summary>
    /// 可用增益设置列表。
    /// </summary>
    public static readonly EegGainSetting[] AvailableGains =
    [
        EegGainSetting.Gain10,
        EegGainSetting.Gain20,
        EegGainSetting.Gain50,
        EegGainSetting.Gain70,
        EegGainSetting.Gain100,
        EegGainSetting.Gain200,
        EegGainSetting.Gain1000
    ];

    /// <summary>
    /// 默认增益设置。
    /// </summary>
    public const EegGainSetting DefaultGain = EegGainSetting.Gain50;

    private readonly EegGainSetting _gain;
    private readonly double _dpi;
    private readonly double _pixelsPerCm;
    private readonly double _uvToPixelScale;

    /// <summary>
    /// 创建增益缩放器。
    /// </summary>
    /// <param name="gain">增益设置 (μV/cm)。</param>
    /// <param name="dpi">屏幕 DPI（默认 96）。</param>
    public EegGainScaler(EegGainSetting gain = DefaultGain, double dpi = DefaultDpi)
    {
        if (dpi <= 0)
            throw new ArgumentOutOfRangeException(nameof(dpi), "DPI must be positive");

        _gain = gain;
        _dpi = dpi;

        // 计算每厘米像素数
        // 1 inch = 2.54 cm
        // pixelsPerCm = dpi / 2.54
        _pixelsPerCm = dpi * InchesPerCm;

        // 计算 μV 到像素的缩放因子
        // gain (μV/cm) 表示 1 cm 对应多少 μV
        // 因此: 1 μV = (1 / gain) cm = (pixelsPerCm / gain) pixels
        _uvToPixelScale = _pixelsPerCm / (int)gain;
    }

    /// <summary>
    /// 当前增益设置。
    /// </summary>
    public EegGainSetting Gain => _gain;

    /// <summary>
    /// 增益值 (μV/cm)。
    /// </summary>
    public int GainValue => (int)_gain;

    /// <summary>
    /// 屏幕 DPI。
    /// </summary>
    public double Dpi => _dpi;

    /// <summary>
    /// 每厘米像素数。
    /// </summary>
    public double PixelsPerCm => _pixelsPerCm;

    /// <summary>
    /// μV 到像素的缩放因子。
    /// </summary>
    /// <remarks>
    /// 计算公式: pixelOffset = uV * UvToPixelScale
    /// </remarks>
    public double UvToPixelScale => _uvToPixelScale;

    /// <summary>
    /// 将 μV 值转换为像素偏移。
    /// </summary>
    /// <param name="uv">电压值 (μV)。</param>
    /// <returns>像素偏移。</returns>
    /// <remarks>
    /// 纯函数，无状态依赖。
    /// 正电压向上（负偏移），负电压向下（正偏移）。
    /// </remarks>
    public double UvToPixels(double uv)
    {
        return uv * _uvToPixelScale;
    }

    /// <summary>
    /// 将像素偏移转换为 μV 值。
    /// </summary>
    /// <param name="pixels">像素偏移。</param>
    /// <returns>电压值 (μV)。</returns>
    public double PixelsToUv(double pixels)
    {
        if (Math.Abs(_uvToPixelScale) < 1e-10)
            return 0.0;

        return pixels / _uvToPixelScale;
    }

    /// <summary>
    /// 计算指定高度下的显示范围 (μV)。
    /// </summary>
    /// <param name="heightPx">显示区域高度（像素）。</param>
    /// <returns>从基线到边缘的最大电压 (μV)。</returns>
    /// <remarks>
    /// 返回值为单边范围，总范围为 ±返回值。
    /// </remarks>
    public double GetDisplayRangeUv(double heightPx)
    {
        // 高度的一半对应最大振幅
        double halfHeightPx = heightPx / 2.0;
        return PixelsToUv(halfHeightPx);
    }

    /// <summary>
    /// 创建指定增益设置的缩放器（静态工厂方法）。
    /// </summary>
    /// <param name="gain">增益设置。</param>
    /// <param name="dpi">屏幕 DPI。</param>
    /// <returns>增益缩放器实例。</returns>
    public static EegGainScaler Create(EegGainSetting gain, double dpi = DefaultDpi)
    {
        return new EegGainScaler(gain, dpi);
    }

    /// <summary>
    /// 获取增益设置的显示文本。
    /// </summary>
    /// <param name="gain">增益设置。</param>
    /// <returns>显示文本（如 "50 μV/cm"）。</returns>
    public static string GetDisplayText(EegGainSetting gain)
    {
        return $"{(int)gain} μV/cm";
    }
}
