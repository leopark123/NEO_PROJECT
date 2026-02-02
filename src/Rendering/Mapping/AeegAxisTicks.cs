// AeegAxisTicks.cs
// aEEG Y 轴刻度定义 - 来源: DSP_SPEC.md, CONSENSUS_BASELINE.md §6.4

namespace Neo.Rendering.Mapping;

/// <summary>
/// aEEG Y 轴刻度信息。
/// </summary>
public readonly struct AeegAxisTick
{
    /// <summary>
    /// 电压值 (μV)。
    /// </summary>
    public double VoltageUv { get; init; }

    /// <summary>
    /// Y 坐标 (像素)。
    /// </summary>
    public double Y { get; init; }

    /// <summary>
    /// 刻度标签。
    /// </summary>
    public string Label { get; init; }

    /// <summary>
    /// 是否为主刻度（用于样式区分）。
    /// </summary>
    public bool IsMajor { get; init; }
}

/// <summary>
/// aEEG Y 轴刻度生成器。
/// </summary>
/// <remarks>
/// 职责: 生成 aEEG 显示的固定刻度点
///
/// ⚠️ 这是显示刻度，不是信号处理
/// ⚠️ 刻度点固定，不可增删
///
/// 标准刻度（医学冻结）:
/// - 线性段: 0, 1, 2, 3, 4, 5 μV
/// - 分界点: 10 μV
/// - 对数段: 25, 50, 100, 200 μV
///
/// 禁止事项:
/// ❌ 新增刻度
/// ❌ 删除刻度
/// ❌ 根据屏幕大小调整刻度
/// </remarks>
public static class AeegAxisTicks
{
    // ============================================
    // 冻结刻度值（禁止修改）
    // ============================================

    /// <summary>
    /// 标准刻度电压值 (μV)。
    /// </summary>
    /// <remarks>
    /// 医学冻结，不可增删。
    /// </remarks>
    public static readonly double[] StandardTicksUv =
    [
        0,      // 底部
        1,
        2,
        3,
        4,
        5,
        10,     // 线性/对数分界
        25,
        50,
        100,
        200     // 顶部
    ];

    /// <summary>
    /// 主刻度电压值 (μV)。
    /// </summary>
    /// <remarks>
    /// 用于视觉强调的刻度点。
    /// </remarks>
    public static readonly double[] MajorTicksUv =
    [
        0,
        5,
        10,     // 分界点（关键刻度）
        50,
        100,
        200
    ];

    /// <summary>
    /// 刻度数量。
    /// </summary>
    public const int TickCount = 11;

    /// <summary>
    /// 生成指定高度的刻度列表。
    /// </summary>
    /// <param name="totalHeightPx">显示区域总高度 (像素)</param>
    /// <returns>刻度列表</returns>
    public static AeegAxisTick[] GetTicks(double totalHeightPx)
    {
        if (totalHeightPx <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(totalHeightPx), "Height must be positive");
        }

        var mapper = new AeegSemiLogMapper(totalHeightPx);
        var ticks = new AeegAxisTick[StandardTicksUv.Length];

        for (int i = 0; i < StandardTicksUv.Length; i++)
        {
            double voltage = StandardTicksUv[i];
            ticks[i] = new AeegAxisTick
            {
                VoltageUv = voltage,
                Y = mapper.MapVoltageToY(voltage),
                Label = FormatTickLabel(voltage),
                IsMajor = IsMajorTick(voltage)
            };
        }

        return ticks;
    }

    /// <summary>
    /// 获取指定电压值的 Y 坐标。
    /// </summary>
    /// <param name="voltageUv">电压值 (μV)</param>
    /// <param name="totalHeightPx">显示区域总高度 (像素)</param>
    /// <returns>Y 坐标 (像素)</returns>
    public static double GetTickY(double voltageUv, double totalHeightPx)
    {
        var mapper = new AeegSemiLogMapper(totalHeightPx);
        return mapper.MapVoltageToY(voltageUv);
    }

    /// <summary>
    /// 格式化刻度标签。
    /// </summary>
    /// <param name="voltageUv">电压值 (μV)</param>
    /// <returns>标签字符串</returns>
    public static string FormatTickLabel(double voltageUv)
    {
        // 整数显示（无小数）
        return $"{(int)voltageUv}";
    }

    /// <summary>
    /// 判断是否为主刻度。
    /// </summary>
    /// <param name="voltageUv">电压值 (μV)</param>
    /// <returns>是否为主刻度</returns>
    public static bool IsMajorTick(double voltageUv)
    {
        foreach (double major in MajorTicksUv)
        {
            if (Math.Abs(voltageUv - major) < 0.001)
            {
                return true;
            }
        }
        return false;
    }

    /// <summary>
    /// 获取线性/对数分界点的 Y 坐标。
    /// </summary>
    /// <param name="totalHeightPx">显示区域总高度 (像素)</param>
    /// <returns>10 μV 对应的 Y 坐标 (像素)</returns>
    public static double GetBoundaryY(double totalHeightPx)
    {
        return GetTickY(AeegSemiLogMapper.LinearLogBoundaryUv, totalHeightPx);
    }
}
