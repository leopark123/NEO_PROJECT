// IDataSink.cs
// 数据接收器接口 - 来源: ARCHITECTURE.md, CONSENSUS_BASELINE.md 铁律1/12

namespace Neo.Core.Interfaces;

/// <summary>
/// 数据接收器接口。
/// 用于接收和消费时间序列样本。
/// </summary>
/// <remarks>
/// 依据: ARCHITECTURE.md §3
///
/// 职责:
/// - 接收带时间戳的样本
/// - 按顺序消费数据
///
/// 本接口不负责:
/// - 数据采集（由 ITimeSeriesSource 负责）
/// - DSP 处理（由 IFilterChain 负责）
/// - 持久化存储（由具体实现决定，当前 Sprint 禁止）
/// - 渲染显示（由 IRenderTarget 负责）
///
/// 铁律约束:
/// - 铁律1: Raw 数据永不修改
/// - 铁律12: Raw 数据只追加
/// </remarks>
/// <typeparam name="TSample">样本类型</typeparam>
public interface IDataSink<TSample> where TSample : struct
{
    /// <summary>
    /// 写入单个样本。
    /// </summary>
    /// <param name="sample">样本数据（含时间戳）</param>
    void Write(in TSample sample);

    /// <summary>
    /// 批量写入样本。
    /// </summary>
    /// <param name="samples">样本数组</param>
    void WriteBatch(ReadOnlySpan<TSample> samples);

    /// <summary>
    /// 刷新缓冲区。
    /// </summary>
    void Flush();
}
