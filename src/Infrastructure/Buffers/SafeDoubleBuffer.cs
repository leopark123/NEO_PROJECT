// SafeDoubleBuffer.cs
// 无锁双缓冲 - 来源: ARCHITECTURE.md §3, ADR-007

using System.Runtime.CompilerServices;

namespace Neo.Infrastructure.Buffers;

/// <summary>
/// 无锁双缓冲，支持单生产者-单消费者模式。
/// </summary>
/// <typeparam name="T">元素类型（必须为值类型）。</typeparam>
/// <remarks>
/// 依据: ARCHITECTURE.md §3, ADR-007 (无锁双缓冲)
///
/// 线程模型:
/// - 写入线程: 单一（DSP线程）
/// - 读取线程: 单一（渲染线程）
/// - 线程安全: 是（无锁设计）
///
/// 约束:
/// - 禁止使用 lock / Monitor / Mutex
/// - 禁止在读取路径分配内存
/// - 禁止阻塞操作
/// - 必须使用 Interlocked 原子操作
/// </remarks>
public sealed class SafeDoubleBuffer<T> where T : struct
{
    private readonly T[] _bufferA;
    private readonly T[] _bufferB;
    private readonly int _capacity;

    // 原子状态
    private volatile int _publishedIndex;    // 0 = A, 1 = B
    private volatile int _version;
    private volatile int _publishedCount;
    private long _publishedTimestamp;  // 使用 Interlocked/Volatile 访问

    // 写入状态（仅生产者线程访问）
    private int _writeIndex;  // 当前写入的缓冲区索引
    private int _writeCount;  // 当前写入的元素数量

    /// <summary>
    /// 缓冲区容量。
    /// </summary>
    public int Capacity => _capacity;

    /// <summary>
    /// 当前版本号。
    /// </summary>
    public int Version => _version;

    /// <summary>
    /// 创建 SafeDoubleBuffer 实例。
    /// </summary>
    /// <param name="capacity">缓冲区容量。</param>
    /// <exception cref="ArgumentOutOfRangeException">容量必须大于0。</exception>
    public SafeDoubleBuffer(int capacity)
    {
        if (capacity <= 0)
            throw new ArgumentOutOfRangeException(nameof(capacity), "Capacity must be greater than 0.");

        _capacity = capacity;
        _bufferA = new T[capacity];
        _bufferB = new T[capacity];
        _publishedIndex = 0;
        _writeIndex = 1;  // 初始写入 B，发布 A
        _version = 0;
        _writeCount = 0;
    }

    /// <summary>
    /// 获取写入缓冲区（生产者调用）。
    /// </summary>
    /// <returns>可写入的 Span。</returns>
    /// <remarks>
    /// 必须在同一线程中调用，不可并发。
    /// 调用后必须调用 Publish 完成发布。
    /// </remarks>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Span<T> AcquireWriteBuffer()
    {
        var buffer = _writeIndex == 0 ? _bufferA : _bufferB;
        _writeCount = 0;
        return buffer.AsSpan();
    }

    /// <summary>
    /// 发布写入内容（生产者调用）。
    /// </summary>
    /// <param name="count">实际写入的元素数量。</param>
    /// <param name="timestampUs">数据时间戳（微秒）。</param>
    /// <exception cref="ArgumentOutOfRangeException">count 超出容量范围。</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Publish(int count, long timestampUs)
    {
        if (count < 0 || count > _capacity)
            throw new ArgumentOutOfRangeException(nameof(count), $"Count must be between 0 and {_capacity}.");

        _writeCount = count;

        // 更新发布状态
        Interlocked.Exchange(ref _publishedTimestamp, timestampUs);
        Interlocked.Exchange(ref _publishedCount, count);

        // 原子交换索引：发布当前写入缓冲区
        int newPublished = _writeIndex;
        Interlocked.Exchange(ref _publishedIndex, newPublished);

        // 切换写入目标
        _writeIndex = newPublished == 0 ? 1 : 0;

        // 递增版本号
        Interlocked.Increment(ref _version);
    }

    /// <summary>
    /// 获取最新快照（消费者调用）。
    /// </summary>
    /// <returns>当前发布的数据快照。</returns>
    /// <remarks>
    /// 快照是只读视图，不拷贝数据。
    /// 消费者应尽快处理，避免持有过久。
    /// </remarks>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public BufferSnapshot<T> GetSnapshot()
    {
        // 读取发布状态（原子读取）
        int publishedIndex = Volatile.Read(ref _publishedIndex);
        int count = Volatile.Read(ref _publishedCount);
        long timestamp = Volatile.Read(ref _publishedTimestamp);
        int version = Volatile.Read(ref _version);

        var buffer = publishedIndex == 0 ? _bufferA : _bufferB;
        return new BufferSnapshot<T>(buffer, count, timestamp, version);
    }

    /// <summary>
    /// 尝试获取更新的快照（消费者调用）。
    /// </summary>
    /// <param name="lastVersion">上次读取的版本号。</param>
    /// <param name="snapshot">输出的快照。</param>
    /// <returns>如果有新数据返回 true。</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool TryGetSnapshot(int lastVersion, out BufferSnapshot<T> snapshot)
    {
        int currentVersion = Volatile.Read(ref _version);
        if (currentVersion == lastVersion)
        {
            snapshot = default;
            return false;
        }

        snapshot = GetSnapshot();
        return true;
    }

    /// <summary>
    /// 重置缓冲区状态。
    /// </summary>
    /// <remarks>
    /// 仅在无读写活动时调用。
    /// </remarks>
    public void Reset()
    {
        _publishedIndex = 0;
        _writeIndex = 1;
        _version = 0;
        _publishedCount = 0;
        _publishedTimestamp = 0;
        _writeCount = 0;

        Array.Clear(_bufferA);
        Array.Clear(_bufferB);
    }
}
