// GsBinMapper.cs
// GS 直方图 Bin 映射器 - 来源: DSP_SPEC.md §3.3

namespace Neo.DSP.GS;

/// <summary>
/// GS 直方图 Bin 映射器。
/// </summary>
/// <remarks>
/// 依据: DSP_SPEC.md §3.3
///
/// 冻结规格（不可修改）:
/// - 总 bin 数: 230 (index 0-229)
/// - 电压范围: 0-200 μV
/// - 分段映射:
///   - 0-10 μV: 线性映射 (100 bins, index 0-99)
///   - 10-200 μV: log10 映射 (130 bins, index 100-229)
///
/// 边界处理:
/// - uV &lt; 0: 返回 -1 (忽略，不计入任何 bin)
/// - uV &gt;= 200: 返回 229 (clamp 到最高 bin)
///
/// 禁止事项:
/// - ❌ 禁止修改 bin 数量
/// - ❌ 禁止修改分段点 (10 μV)
/// - ❌ 禁止引入平滑或插值
/// </remarks>
public static class GsBinMapper
{
    /// <summary>
    /// 总 bin 数。
    /// </summary>
    public const int TotalBins = 230;

    /// <summary>
    /// 最大有效 bin 索引。
    /// </summary>
    public const int MaxBinIndex = 229;

    /// <summary>
    /// 线性区域 bin 数 (0-10 μV)。
    /// </summary>
    public const int LinearBins = 100;

    /// <summary>
    /// 对数区域 bin 数 (10-200 μV)。
    /// </summary>
    public const int LogBins = 130;

    /// <summary>
    /// 线性区域上限 (μV)。
    /// </summary>
    public const double LinearUpperBoundUv = 10.0;

    /// <summary>
    /// 电压范围上限 (μV)。
    /// </summary>
    public const double MaxVoltageUv = 200.0;

    /// <summary>
    /// 电压范围下限 (μV)。
    /// </summary>
    public const double MinVoltageUv = 0.0;

    /// <summary>
    /// 无效 bin 索引（表示忽略）。
    /// </summary>
    public const int InvalidBin = -1;

    // 预计算常量
    // log10(10) = 1.0, log10(200) = 2.30103
    // log 范围 = log10(200) - log10(10) = 1.30103
    private static readonly double Log10Of10 = Math.Log10(10.0);  // 1.0
    private static readonly double Log10Of200 = Math.Log10(200.0);  // 2.30103
    private static readonly double LogRange = Log10Of200 - Log10Of10;  // 1.30103

    /// <summary>
    /// 将电压值映射到 bin 索引。
    /// </summary>
    /// <param name="voltageUv">电压值 (μV)</param>
    /// <returns>bin 索引 (0-229)，或 -1 表示忽略</returns>
    /// <remarks>
    /// 映射规则:
    /// - uV &lt; 0: 返回 -1 (忽略)
    /// - 0 &lt;= uV &lt; 10: 线性映射到 bin 0-99
    /// - 10 &lt;= uV &lt; 200: log10 映射到 bin 100-229
    /// - uV &gt;= 200: 返回 229 (clamp)
    ///
    /// 线性公式: bin = floor(uV * 10)
    /// Log 公式: bin = 100 + floor((log10(uV) - 1.0) / 1.30103 * 130)
    /// </remarks>
    public static int MapToBin(double voltageUv)
    {
        // 负值忽略
        if (voltageUv < MinVoltageUv)
        {
            return InvalidBin;
        }

        // 超过 200 μV clamp 到 bin 229
        if (voltageUv >= MaxVoltageUv)
        {
            return MaxBinIndex;
        }

        // 线性区域 [0, 10) μV → bin 0-99
        if (voltageUv < LinearUpperBoundUv)
        {
            int bin = (int)(voltageUv * 10.0);
            // 边界保护（理论上不应触发）
            return Math.Min(bin, LinearBins - 1);
        }

        // 对数区域 [10, 200) μV → bin 100-229
        // bin = 100 + floor((log10(uV) - 1.0) / LogRange * 130)
        double logValue = Math.Log10(voltageUv);
        double normalizedLog = (logValue - Log10Of10) / LogRange;
        int logBin = (int)(normalizedLog * LogBins);

        int binIndex = LinearBins + logBin;

        // 边界保护（确保不超过 229）
        return Math.Min(binIndex, MaxBinIndex);
    }

    /// <summary>
    /// 获取 bin 索引对应的电压中心值 (μV)。
    /// </summary>
    /// <param name="binIndex">bin 索引 (0-229)</param>
    /// <returns>该 bin 的电压中心值 (μV)</returns>
    /// <remarks>
    /// 用于测试验证。
    ///
    /// 线性区域: uV = (binIndex + 0.5) / 10
    /// Log 区域: uV = 10^(1.0 + (binIndex - 100 + 0.5) / 130 * LogRange)
    /// </remarks>
    public static double GetBinCenterVoltage(int binIndex)
    {
        if (binIndex < 0 || binIndex >= TotalBins)
        {
            throw new ArgumentOutOfRangeException(nameof(binIndex),
                $"Bin index must be in range [0, {MaxBinIndex}], got {binIndex}");
        }

        if (binIndex < LinearBins)
        {
            // 线性区域: 中心值
            return (binIndex + 0.5) / 10.0;
        }
        else
        {
            // 对数区域: 中心值
            int logBinOffset = binIndex - LinearBins;
            double normalizedLog = (logBinOffset + 0.5) / LogBins;
            double logValue = Log10Of10 + normalizedLog * LogRange;
            return Math.Pow(10.0, logValue);
        }
    }

    /// <summary>
    /// 获取 bin 索引对应的电压下界 (μV)。
    /// </summary>
    /// <param name="binIndex">bin 索引 (0-229)</param>
    /// <returns>该 bin 的电压下界 (μV)</returns>
    public static double GetBinLowerBound(int binIndex)
    {
        if (binIndex < 0 || binIndex >= TotalBins)
        {
            throw new ArgumentOutOfRangeException(nameof(binIndex));
        }

        if (binIndex < LinearBins)
        {
            return binIndex / 10.0;
        }
        else
        {
            int logBinOffset = binIndex - LinearBins;
            double normalizedLog = (double)logBinOffset / LogBins;
            double logValue = Log10Of10 + normalizedLog * LogRange;
            return Math.Pow(10.0, logValue);
        }
    }

    /// <summary>
    /// 获取 bin 索引对应的电压上界 (μV)。
    /// </summary>
    /// <param name="binIndex">bin 索引 (0-229)</param>
    /// <returns>该 bin 的电压上界 (μV)</returns>
    public static double GetBinUpperBound(int binIndex)
    {
        if (binIndex < 0 || binIndex >= TotalBins)
        {
            throw new ArgumentOutOfRangeException(nameof(binIndex));
        }

        if (binIndex < LinearBins - 1)
        {
            return (binIndex + 1) / 10.0;
        }
        else if (binIndex == LinearBins - 1)
        {
            // bin 99 的上界是 10.0 μV
            return LinearUpperBoundUv;
        }
        else if (binIndex < MaxBinIndex)
        {
            int logBinOffset = binIndex - LinearBins + 1;
            double normalizedLog = (double)logBinOffset / LogBins;
            double logValue = Log10Of10 + normalizedLog * LogRange;
            return Math.Pow(10.0, logValue);
        }
        else
        {
            // bin 229 的上界是 200 μV
            return MaxVoltageUv;
        }
    }
}
