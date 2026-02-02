// AeegSemiLogMapper.cs
// aEEG 半对数显示映射器 - 来源: DSP_SPEC.md §3, CONSENSUS_BASELINE.md §6.4

namespace Neo.Rendering.Mapping;

/// <summary>
/// aEEG 半对数 Y 轴映射器。
/// </summary>
/// <remarks>
/// 职责: 把 μV 值映射为 UI 垂直坐标 (Y 像素)
///
/// ⚠️ 这是显示映射，不是信号处理
/// ⚠️ 不产生新数据
/// ⚠️ 不修改 GS
/// ⚠️ 不解释医学含义
///
/// 冻结规格（不可修改）:
/// - 显示范围: 0-200 μV
/// - 线性段: 0-10 μV → 下半区 (50%)
/// - 对数段: 10-200 μV → 上半区 (50%)
/// - 分界点: 10 μV（医学冻结，不可优化）
///
/// 禁止事项:
/// ❌ 对 GS bin 做任何"视觉平滑"
/// ❌ 做 anti-alias 数据插值
/// ❌ 根据屏幕比例改变线性/对数分界
/// ❌ "自动适配"不同设备
/// ❌ 修改 GS 直方图
/// </remarks>
public sealed class AeegSemiLogMapper
{
    // ============================================
    // 冻结常量（医学规格，禁止修改）
    // ============================================

    /// <summary>
    /// 显示范围最小值 (μV)。
    /// </summary>
    public const double MinVoltageUv = 0.0;

    /// <summary>
    /// 显示范围最大值 (μV)。
    /// </summary>
    public const double MaxVoltageUv = 200.0;

    /// <summary>
    /// 线性/对数分界点 (μV)。
    /// </summary>
    /// <remarks>
    /// 医学冻结值，禁止"优化"。
    /// </remarks>
    public const double LinearLogBoundaryUv = 10.0;

    /// <summary>
    /// 线性段占显示高度比例。
    /// </summary>
    public const double LinearHeightRatio = 0.5;

    /// <summary>
    /// 对数段占显示高度比例。
    /// </summary>
    public const double LogHeightRatio = 0.5;

    // log10(200) - log10(10) = log10(200) - 1
    private static readonly double Log10Max = Math.Log10(MaxVoltageUv);  // ≈ 2.30103
    private static readonly double Log10Boundary = Math.Log10(LinearLogBoundaryUv);  // = 1.0
    private static readonly double LogRange = Log10Max - Log10Boundary;  // ≈ 1.30103

    // ============================================
    // 实例状态
    // ============================================

    private readonly double _totalHeightPx;
    private readonly double _linearHeightPx;
    private readonly double _logHeightPx;

    /// <summary>
    /// 创建 aEEG 半对数映射器。
    /// </summary>
    /// <param name="totalHeightPx">显示区域总高度 (像素)</param>
    /// <exception cref="ArgumentOutOfRangeException">高度必须 > 0</exception>
    public AeegSemiLogMapper(double totalHeightPx)
    {
        if (totalHeightPx <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(totalHeightPx), "Height must be positive");
        }

        _totalHeightPx = totalHeightPx;
        _linearHeightPx = totalHeightPx * LinearHeightRatio;
        _logHeightPx = totalHeightPx * LogHeightRatio;
    }

    /// <summary>
    /// 显示区域总高度 (像素)。
    /// </summary>
    public double TotalHeightPx => _totalHeightPx;

    /// <summary>
    /// 线性段高度 (像素)。
    /// </summary>
    public double LinearHeightPx => _linearHeightPx;

    /// <summary>
    /// 对数段高度 (像素)。
    /// </summary>
    public double LogHeightPx => _logHeightPx;

    /// <summary>
    /// 将电压值映射到 Y 坐标。
    /// </summary>
    /// <param name="voltageUv">电压值 (μV)</param>
    /// <returns>Y 坐标 (像素)，0 在顶部，totalHeight 在底部。NaN/Invalid 返回 NaN。</returns>
    /// <remarks>
    /// 纯函数: 相同输入 → 相同输出
    ///
    /// 坐标系:
    /// - Y = 0: 顶部 (200 μV)
    /// - Y = totalHeight: 底部 (0 μV)
    ///
    /// 映射规则:
    /// - 0-10 μV: 线性映射到下半区
    /// - 10-200 μV: log10 映射到上半区
    /// - 负值: NaN
    /// - > 200 μV: clamp 到 0 (顶部)
    /// - NaN: NaN
    /// </remarks>
    public double MapVoltageToY(double voltageUv)
    {
        // NaN 检查
        if (double.IsNaN(voltageUv))
        {
            return double.NaN;
        }

        // 负值无效
        if (voltageUv < 0)
        {
            return double.NaN;
        }

        // 线性段: 0-10 μV → 下半区
        if (voltageUv <= LinearLogBoundaryUv)
        {
            // 0 μV → Y = totalHeight (底部)
            // 10 μV → Y = logHeightPx (中间)
            double normalizedLinear = voltageUv / LinearLogBoundaryUv;  // 0 到 1
            return _totalHeightPx - (normalizedLinear * _linearHeightPx);
        }

        // 对数段: 10-200 μV → 上半区
        // clamp 到 200 μV
        double clampedVoltage = Math.Min(voltageUv, MaxVoltageUv);
        double log10Value = Math.Log10(clampedVoltage);

        // 10 μV → Y = logHeightPx (中间)
        // 200 μV → Y = 0 (顶部)
        double normalizedLog = (log10Value - Log10Boundary) / LogRange;  // 0 到 1
        return _logHeightPx * (1.0 - normalizedLog);
    }

    /// <summary>
    /// 将 Y 坐标映射回电压值（逆映射）。
    /// </summary>
    /// <param name="y">Y 坐标 (像素)</param>
    /// <returns>电压值 (μV)。超出范围返回 NaN。</returns>
    /// <remarks>
    /// 纯函数: 相同输入 → 相同输出
    /// </remarks>
    public double MapYToVoltage(double y)
    {
        // NaN 检查
        if (double.IsNaN(y))
        {
            return double.NaN;
        }

        // 范围检查
        if (y < 0 || y > _totalHeightPx)
        {
            return double.NaN;
        }

        // 对数段: Y < logHeightPx
        if (y <= _logHeightPx)
        {
            // Y = 0 → 200 μV
            // Y = logHeightPx → 10 μV
            double normalizedLog = 1.0 - (y / _logHeightPx);  // 0 到 1
            double log10Value = Log10Boundary + (normalizedLog * LogRange);
            return Math.Pow(10, log10Value);
        }

        // 线性段: Y > logHeightPx
        // Y = logHeightPx → 10 μV
        // Y = totalHeight → 0 μV
        double normalizedLinear = (_totalHeightPx - y) / _linearHeightPx;  // 0 到 1
        return normalizedLinear * LinearLogBoundaryUv;
    }

    /// <summary>
    /// 获取指定电压值的 Y 坐标（静态便捷方法）。
    /// </summary>
    /// <param name="voltageUv">电压值 (μV)</param>
    /// <param name="totalHeightPx">显示区域总高度 (像素)</param>
    /// <returns>Y 坐标 (像素)</returns>
    public static double GetY(double voltageUv, double totalHeightPx)
    {
        var mapper = new AeegSemiLogMapper(totalHeightPx);
        return mapper.MapVoltageToY(voltageUv);
    }
}
