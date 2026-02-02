// EegRingBuffer.cs
// EEG 专用环形缓冲 - 来源: ARCHITECTURE.md §3, ADR-007

using Neo.Core.Models;

namespace Neo.Infrastructure.Buffers;

/// <summary>
/// EEG 专用环形缓冲，用于存储滑动窗口数据。
/// </summary>
/// <remarks>
/// 依据: ARCHITECTURE.md §3, ADR-007 (无锁双缓冲)
///
/// 用途:
/// - 存储最近 N 秒的 EEG 数据
/// - 支持滑动窗口视图
/// - 与 SafeDoubleBuffer 配合使用
///
/// 线程模型:
/// - 单生产者（DSP线程写入）
/// - 单消费者（渲染线程读取快照后处理）
/// </remarks>
public sealed class EegRingBuffer
{
    private readonly EegSample[] _buffer;
    private readonly int _capacity;
    private int _head;  // 下一个写入位置
    private int _count; // 当前元素数量

    /// <summary>
    /// 缓冲区容量。
    /// </summary>
    public int Capacity => _capacity;

    /// <summary>
    /// 当前元素数量。
    /// </summary>
    public int Count => _count;

    /// <summary>
    /// 缓冲区是否已满。
    /// </summary>
    public bool IsFull => _count >= _capacity;

    /// <summary>
    /// 缓冲区是否为空。
    /// </summary>
    public bool IsEmpty => _count == 0;

    /// <summary>
    /// 最早数据的时间戳。
    /// </summary>
    public long OldestTimestampUs
    {
        get
        {
            if (_count == 0) return 0;
            int tail = (_head - _count + _capacity) % _capacity;
            return _buffer[tail].TimestampUs;
        }
    }

    /// <summary>
    /// 最新数据的时间戳。
    /// </summary>
    public long NewestTimestampUs
    {
        get
        {
            if (_count == 0) return 0;
            int last = (_head - 1 + _capacity) % _capacity;
            return _buffer[last].TimestampUs;
        }
    }

    /// <summary>
    /// 创建 EegRingBuffer 实例。
    /// </summary>
    /// <param name="capacity">缓冲区容量（样本数）。</param>
    /// <exception cref="ArgumentOutOfRangeException">容量必须大于0。</exception>
    public EegRingBuffer(int capacity)
    {
        if (capacity <= 0)
            throw new ArgumentOutOfRangeException(nameof(capacity), "Capacity must be greater than 0.");

        _capacity = capacity;
        _buffer = new EegSample[capacity];
        _head = 0;
        _count = 0;
    }

    /// <summary>
    /// 创建指定秒数的缓冲区（基于 160Hz 采样率）。
    /// </summary>
    /// <param name="seconds">秒数。</param>
    /// <returns>EegRingBuffer 实例。</returns>
    public static EegRingBuffer CreateForSeconds(int seconds)
    {
        const int SampleRate = 160; // Hz
        return new EegRingBuffer(seconds * SampleRate);
    }

    /// <summary>
    /// 写入单个样本。
    /// </summary>
    /// <param name="sample">EEG 样本。</param>
    public void Write(in EegSample sample)
    {
        _buffer[_head] = sample;
        _head = (_head + 1) % _capacity;

        if (_count < _capacity)
            _count++;
    }

    /// <summary>
    /// 批量写入样本。
    /// </summary>
    /// <param name="samples">样本数组。</param>
    public void WriteBatch(ReadOnlySpan<EegSample> samples)
    {
        foreach (ref readonly var sample in samples)
        {
            Write(in sample);
        }
    }

    /// <summary>
    /// 获取指定时间范围内的样本。
    /// </summary>
    /// <param name="startUs">起始时间（微秒）。</param>
    /// <param name="endUs">结束时间（微秒）。</param>
    /// <param name="output">输出数组。</param>
    /// <returns>实际复制的样本数。</returns>
    /// <remarks>
    /// Performance: O(N) linear scan where N = Count.
    /// Justification: Maximum buffer size is bounded by CreateForSeconds() — typically 10-600 seconds
    /// at 160 Hz = 1,600-96,000 elements. At 160 Hz, each tick queries ~6ms worth = ~1 sample.
    /// Worst case for 600s buffer: ~96K iterations of integer comparison, completing in &lt;1ms.
    /// Binary search is not used because timestamps may have gaps (non-uniform spacing).
    /// </remarks>
    public int GetRange(long startUs, long endUs, Span<EegSample> output)
    {
        if (_count == 0 || output.Length == 0)
            return 0;

        int tail = (_head - _count + _capacity) % _capacity;
        int copied = 0;

        for (int i = 0; i < _count && copied < output.Length; i++)
        {
            int idx = (tail + i) % _capacity;
            ref readonly var sample = ref _buffer[idx];

            if (sample.TimestampUs >= startUs && sample.TimestampUs <= endUs)
            {
                output[copied++] = sample;
            }
        }

        return copied;
    }

    /// <summary>
    /// 获取最近 N 个样本。
    /// </summary>
    /// <param name="count">样本数量。</param>
    /// <param name="output">输出数组。</param>
    /// <returns>实际复制的样本数。</returns>
    public int GetLatest(int count, Span<EegSample> output)
    {
        int toCopy = Math.Min(count, Math.Min(_count, output.Length));
        if (toCopy == 0)
            return 0;

        int start = (_head - toCopy + _capacity) % _capacity;

        for (int i = 0; i < toCopy; i++)
        {
            int idx = (start + i) % _capacity;
            output[i] = _buffer[idx];
        }

        return toCopy;
    }

    /// <summary>
    /// 清空缓冲区。
    /// </summary>
    public void Clear()
    {
        _head = 0;
        _count = 0;
        Array.Clear(_buffer);
    }

    /// <summary>
    /// 按索引访问（0 = 最旧，Count-1 = 最新）。
    /// </summary>
    /// <param name="index">索引。</param>
    /// <returns>样本引用。</returns>
    /// <exception cref="IndexOutOfRangeException">索引超出范围。</exception>
    public ref readonly EegSample this[int index]
    {
        get
        {
            if (index < 0 || index >= _count)
                throw new IndexOutOfRangeException($"Index {index} is out of range [0, {_count}).");

            int tail = (_head - _count + _capacity) % _capacity;
            int idx = (tail + index) % _capacity;
            return ref _buffer[idx];
        }
    }
}
