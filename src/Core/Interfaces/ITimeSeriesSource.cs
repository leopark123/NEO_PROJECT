// ITimeSeriesSource.cs
// 时间序列数据源接口 - 来源: ARCHITECTURE.md §2, CONSENSUS_BASELINE.md §4

namespace Neo.Core.Interfaces;

using Neo.Core.Models;

/// <summary>
/// 时间序列数据源接口。
/// 所有数据源（EEG、NIRS、Video）的公共抽象。
/// </summary>
/// <remarks>
/// 依据: ARCHITECTURE.md §2
///
/// 职责:
/// - 提供带时间戳的样本流
/// - 报告采样率和通道数
/// - 提供启动/停止控制
///
/// 本接口不负责:
/// - 具体协议解析（由具体 Adapter 实现）
/// - DSP 处理（由 IFilterChain 负责）
/// - 数据存储（由 IDataSink 负责）
/// - 渲染显示（由 IRenderTarget 负责）
///
/// 时间戳规则（CONSENSUS_BASELINE §5）：
/// - 单位：微秒 (μs)
/// - 来源：Host 打点（ADR-012）
/// - 语义：样本中心时间
/// </remarks>
/// <typeparam name="TSample">样本类型</typeparam>
public interface ITimeSeriesSource<TSample> where TSample : struct
{
    /// <summary>
    /// 采样率（Hz）。
    /// EEG: 160 Hz, NIRS: 1-4 Hz, Video: 30 fps。
    /// </summary>
    int SampleRate { get; }

    /// <summary>
    /// 通道数。
    /// EEG: 4, NIRS: 6。
    /// </summary>
    int ChannelCount { get; }

    /// <summary>
    /// 样本到达事件。
    /// 时间戳已由 Host 打点。
    /// </summary>
    event Action<TSample>? SampleReceived;

    /// <summary>
    /// 启动数据采集。
    /// </summary>
    void Start();

    /// <summary>
    /// 停止数据采集。
    /// </summary>
    void Stop();
}
