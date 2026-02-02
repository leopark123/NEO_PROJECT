// BufferSnapshot.cs
// 缓冲区快照结构 - 来源: ARCHITECTURE.md §3, ADR-007

namespace Neo.Infrastructure.Buffers;

/// <summary>
/// 缓冲区快照，用于无锁读取。
/// </summary>
/// <typeparam name="T">元素类型。</typeparam>
/// <remarks>
/// 依据: ARCHITECTURE.md §3, ADR-007 (无锁双缓冲)
///
/// 快照特性:
/// - 只读视图
/// - 版本号用于检测更新
/// - 零拷贝（直接引用内部数组）
/// </remarks>
public readonly struct BufferSnapshot<T> where T : struct
{
    private readonly T[] _data;
    private readonly int _count;
    private readonly long _timestampUs;
    private readonly int _version;

    /// <summary>
    /// 创建缓冲区快照。
    /// </summary>
    /// <param name="data">数据数组。</param>
    /// <param name="count">有效元素数量。</param>
    /// <param name="timestampUs">时间戳（微秒）。</param>
    /// <param name="version">版本号。</param>
    public BufferSnapshot(T[] data, int count, long timestampUs, int version)
    {
        _data = data;
        _count = count;
        _timestampUs = timestampUs;
        _version = version;
    }

    /// <summary>
    /// 获取数据的只读视图。
    /// </summary>
    public ReadOnlySpan<T> Data => new(_data, 0, _count);

    /// <summary>
    /// 有效元素数量。
    /// </summary>
    public int Count => _count;

    /// <summary>
    /// 数据时间戳（微秒）。
    /// </summary>
    public long TimestampUs => _timestampUs;

    /// <summary>
    /// 版本号，用于检测是否有更新。
    /// </summary>
    public int Version => _version;

    /// <summary>
    /// 快照是否为空。
    /// </summary>
    public bool IsEmpty => _count == 0 || _data == null;

    /// <summary>
    /// 空快照实例。
    /// </summary>
    public static BufferSnapshot<T> Empty => new(Array.Empty<T>(), 0, 0, 0);
}
