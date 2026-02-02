// IFilterChain.cs
// 滤波链接口（空壳） - 来源: ARCHITECTURE.md §4.1

namespace Neo.Core.Interfaces;

/// <summary>
/// 滤波链接口。
/// DSP 处理链的抽象定义。
/// </summary>
/// <remarks>
/// 依据: ARCHITECTURE.md §4.1
///
/// 职责:
/// - 定义滤波处理的契约
/// - 支持实时和批量处理模式
///
/// 本接口不负责:
/// - 具体滤波算法实现（S2 实现）
/// - 数据采集（由 ITimeSeriesSource 负责）
/// - 数据存储（由 IDataSink 负责）
/// - 渲染显示（由 IRenderTarget 负责）
///
/// 铁律约束:
/// - 铁律4: 滤波器系数、状态变量必须使用 double
///
/// 注意: 这是空壳接口，具体方法签名将在 S2-01 定义。
/// 当前仅声明接口存在性，不定义任何成员。
/// </remarks>
public interface IFilterChain
{
    /// <summary>
    /// Zero-phase 批量滤波（回放模式用）。
    /// 消除相位延迟，不影响实时滤波状态。
    /// </summary>
    /// <param name="channelIndex">通道索引</param>
    /// <param name="inputUv">输入信号 (μV)</param>
    /// <param name="outputUv">输出信号 (μV)</param>
    void ProcessBlockZeroPhase(int channelIndex, ReadOnlySpan<double> inputUv, Span<double> outputUv);
}
