# 📋 TASK-S1-02: SafeDoubleBuffer 无锁双缓冲

> **Sprint**: 1  
> **负责方**: Codex  
> **优先级**: 🔴 P0  
> **预估工时**: 6h  
> **状态**: ⏳ 待开始

---

## 1. 目标

实现线程安全的无锁双缓冲机制，用于 DSP 线程与渲染线程之间的数据交换。

---

## 2. 输入（必读文件）

| 文件 | 重点章节 |
|------|----------|
| `spec/00_CONSTITUTION.md` | 铁律6（渲染线程只Draw）、铁律11（时间轴） |
| `spec/ARCHITECTURE.md` | §3（数据交换层）、ADR-007 |
| `spec/DECISIONS.md` | ADR-007（无锁双缓冲） |
| `handoff/interfaces-api.md` | 数据结构定义（S1-01产出） |

---

## 3. 输出

### 3.1 代码文件

```
src/Infrastructure/Buffers/
├── SafeDoubleBuffer.cs           # 泛型双缓冲实现
├── EegRingBuffer.cs              # EEG专用环形缓冲
└── BufferSnapshot.cs             # 快照结构

tests/Infrastructure.Tests/Buffers/
├── SafeDoubleBufferTests.cs      # 功能测试
└── SafeDoubleBufferStressTests.cs # 并发压力测试
```

### 3.2 交接文档

```
handoff/double-buffer-api.md
```

---

## 4. 设计规格

### 4.1 SafeDoubleBuffer<T>

```csharp
/// <summary>
/// 无锁双缓冲，支持单生产者-单消费者模式
/// </summary>
/// <remarks>
/// <para><b>线程模型</b>:</para>
/// <list type="bullet">
///   <item>写入线程: 单一（DSP线程）</item>
///   <item>读取线程: 单一（渲染线程）</item>
///   <item>线程安全: 是（无锁设计）</item>
/// </list>
/// </remarks>
public class SafeDoubleBuffer<T> where T : struct
{
    /// <summary>缓冲区容量</summary>
    public int Capacity { get; }
    
    /// <summary>
    /// 获取写入缓冲区（生产者调用）
    /// </summary>
    public Span<T> AcquireWriteBuffer();
    
    /// <summary>
    /// 发布写入内容（生产者调用）
    /// </summary>
    /// <param name="count">实际写入的元素数量</param>
    /// <param name="timestampUs">数据时间戳</param>
    public void Publish(int count, long timestampUs);
    
    /// <summary>
    /// 获取最新快照（消费者调用）
    /// </summary>
    public BufferSnapshot<T> GetSnapshot();
}

public readonly struct BufferSnapshot<T> where T : struct
{
    public ReadOnlySpan<T> Data { get; }
    public int Count { get; }
    public long TimestampUs { get; }
    public int Version { get; }  // 用于检测是否有更新
}
```

### 4.2 实现要求

```csharp
// 核心机制：Interlocked 交换索引
private T[] _bufferA, _bufferB;
private volatile int _publishedIndex;  // 0 = A, 1 = B
private volatile int _version;

public void Publish(int count, long timestampUs)
{
    // 写入完成后，原子交换索引
    Interlocked.Increment(ref _version);
    Interlocked.Exchange(ref _publishedIndex, 
        _publishedIndex == 0 ? 1 : 0);
}
```

---

## 5. 验收标准

### 5.1 功能验收

- [ ] 单生产者写入不阻塞
- [ ] 单消费者读取不阻塞
- [ ] 读写可并发执行
- [ ] 数据不丢失、不重复
- [ ] 支持泛型（EegSample, NirsSample 等）

### 5.2 性能验收

| 指标 | 目标 | 测试方法 |
|------|------|----------|
| 写入延迟 P99 | < 10 μs | Stopwatch 计时 |
| 读取延迟 P99 | < 10 μs | Stopwatch 计时 |
| 吞吐量 | > 10,000 ops/sec | 压力测试 |

### 5.3 压力测试

```csharp
[Fact]
public void StressTest_ConcurrentReadWrite_NoDataLoss()
{
    var buffer = new SafeDoubleBuffer<int>(1000);
    int writeCount = 0, readCount = 0;
    
    // 生产者线程：160Hz 写入，持续60秒
    var producer = Task.Run(() => {
        for (int i = 0; i < 160 * 60; i++)
        {
            var span = buffer.AcquireWriteBuffer();
            span[0] = i;
            buffer.Publish(1, i * 6250); // 160Hz = 6250μs/sample
            Interlocked.Increment(ref writeCount);
            Thread.Sleep(6); // ~160Hz
        }
    });
    
    // 消费者线程：120Hz 读取
    var consumer = Task.Run(() => {
        int lastVersion = -1;
        while (writeCount < 160 * 60)
        {
            var snapshot = buffer.GetSnapshot();
            if (snapshot.Version != lastVersion)
            {
                lastVersion = snapshot.Version;
                Interlocked.Increment(ref readCount);
            }
            Thread.Sleep(8); // ~120Hz
        }
    });
    
    Task.WaitAll(producer, consumer);
    
    // 验证：无死锁、无异常
    Assert.True(writeCount > 0);
    Assert.True(readCount > 0);
}
```

### 5.4 编译验收

- [ ] `dotnet build` 通过
- [ ] `dotnet test` 全部通过
- [ ] 无 lock 关键字（使用 Interlocked）

---

## 6. 约束（不可违反）

```
❌ 禁止使用 lock / Monitor / Mutex
❌ 禁止在读取路径分配内存（GC压力）
❌ 禁止阻塞操作
✅ 必须使用 Interlocked 原子操作
✅ 必须支持版本号检测更新
```

---

## 7. 依赖与被依赖

### 依赖
- S1-01: 核心接口定义（EegSample, NirsSample 结构）

### 被依赖
- S1-04: 三层渲染框架（使用双缓冲接收数据）
- S2-xx: DSP 链路（输出到双缓冲）

---

## 8. 启动指令（给 Codex）

```
请先阅读以下文件：
1. spec/00_CONSTITUTION.md（铁律6、铁律11）
2. spec/ARCHITECTURE.md §3
3. spec/DECISIONS.md ADR-007
4. handoff/interfaces-api.md（S1-01产出）

然后执行任务 TASK-S1-02：
- 实现 SafeDoubleBuffer<T> 无锁双缓冲
- 禁止使用 lock，只用 Interlocked
- 编写并发压力测试
- 完成后生成 handoff/double-buffer-api.md
```

---

**任务卡结束**
