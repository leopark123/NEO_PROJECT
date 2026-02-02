# 📐 API_STYLE.md - 代码风格约定

> **版本**: v1.0  
> **更新日期**: 2025-01-21  
> **作用**: 统一 Codex 和 Claude Code 生成代码的风格，避免分裂

---

## 1. 命名规范

### 1.1 通用规则

| 类型 | 风格 | 示例 |
|------|------|------|
| 类/结构体 | PascalCase | `EegDataPacket`, `FilterChain` |
| 接口 | I + PascalCase | `ITimeSeriesSource`, `IFilterChain` |
| 方法 | PascalCase | `ProcessSample()`, `GetTimestamp()` |
| 属性 | PascalCase | `SampleRateHz`, `ChannelCount` |
| 私有字段 | _camelCase | `_buffer`, `_isInitialized` |
| 参数 | camelCase | `sampleData`, `timestampUs` |
| 常量 | PascalCase | `MaxChannels`, `DefaultSampleRate` |
| 枚举值 | PascalCase | `ClockDomain.Device` |

### 1.2 时间相关命名

```csharp
// ✅ 正确：明确单位
long timestampUs;           // 微秒
int durationMs;             // 毫秒
double timeRangeSec;        // 秒
int SampleRateHz { get; }   // 赫兹

// ❌ 错误：单位不明
long timestamp;             // 什么单位？
int duration;               // 什么单位？
```

### 1.3 通道相关命名

```csharp
// ✅ 正确
int channelIndex;           // 0-based 索引
int channelNumber;          // 1-based 编号（用户可见）
int ChannelCount { get; }   // 通道总数

// ❌ 错误
int channel;                // 是索引还是编号？
int ch;                     // 太短，不清晰
```

---

## 2. 类型规范

### 2.1 时间戳

```csharp
// 时间戳统一使用 long (int64)，单位微秒
public long TimestampUs { get; }

// 时间间隔可用 TimeSpan（仅限 UI/日志）
public TimeSpan Duration => TimeSpan.FromMicroseconds(durationUs);
```

### 2.2 采样数据

```csharp
// DSP 处理：double
double[] filteredData = filter.Process(rawData);

// 存储/传输：可用 float 或 int16
float[] displayData;
short[] rawAdcValues;

// 滤波器系数/状态：必须 double
double[] b, a, z;  // IIR 系数和状态
```

### 2.3 Nullable 标注

```csharp
// 启用 nullable
#nullable enable

// 明确标注可空
public string? ErrorMessage { get; }
public IFilterChain? OptionalFilter { get; set; }

// 非空参数不加 ?
public void Process(double[] data)  // data 不可为 null
```

---

## 3. 线程安全标注

### 3.1 XML 文档注释

```csharp
/// <summary>
/// 处理单个 EEG 样本
/// </summary>
/// <param name="sample">输入样本值 (μV)</param>
/// <returns>滤波后的样本值 (μV)</returns>
/// <remarks>
/// <para><b>线程安全</b>: 非线程安全，每个通道需独立实例</para>
/// <para><b>时间戳语义</b>: 输入/输出为样本中心时间</para>
/// </remarks>
public double ProcessSample(double sample);
```

### 3.2 线程模型标注模板

```csharp
/// <remarks>
/// <para><b>线程模型</b>:</para>
/// <list type="bullet">
///   <item>写入线程: DSP 线程（单一）</item>
///   <item>读取线程: 渲染线程（可多个）</item>
///   <item>线程安全: 读写分离，无需外部锁</item>
/// </list>
/// </remarks>
```

---

## 4. 接口设计规范

### 4.1 数据源接口模板

```csharp
public interface ITimeSeriesSource
{
    /// <summary>数据源名称</summary>
    string Name { get; }
    
    /// <summary>采样率 (Hz)</summary>
    int SampleRateHz { get; }
    
    /// <summary>通道数</summary>
    int ChannelCount { get; }
    
    /// <summary>时钟域</summary>
    ClockDomain ClockDomain { get; }
    
    /// <summary>估计时间戳精度 (μs)</summary>
    int EstimatedPrecisionUs { get; }
    
    /// <summary>数据到达事件</summary>
    event EventHandler<DataReceivedEventArgs> DataReceived;
}
```

### 4.2 处理器接口模板

```csharp
public interface IProcessor<TInput, TOutput>
{
    /// <summary>处理单个输入</summary>
    TOutput Process(TInput input);
    
    /// <summary>批量处理</summary>
    void ProcessBlock(ReadOnlySpan<TInput> input, Span<TOutput> output);
    
    /// <summary>重置状态</summary>
    void Reset();
    
    /// <summary>是否已预热完成</summary>
    bool IsWarmedUp { get; }
}
```

---

## 5. 错误处理规范

### 5.1 参数验证

```csharp
public void SetFilter(double cutoffHz, int order)
{
    // 使用 ArgumentOutOfRangeException
    if (cutoffHz <= 0 || cutoffHz >= SampleRateHz / 2)
        throw new ArgumentOutOfRangeException(nameof(cutoffHz), 
            $"Cutoff must be between 0 and Nyquist ({SampleRateHz / 2} Hz)");
    
    if (order < 1 || order > 8)
        throw new ArgumentOutOfRangeException(nameof(order),
            "Order must be between 1 and 8");
}
```

### 5.2 状态验证

```csharp
public double ProcessSample(double sample)
{
    // 使用 InvalidOperationException
    if (!_isInitialized)
        throw new InvalidOperationException("Filter not initialized. Call Initialize() first.");
    
    return DoProcess(sample);
}
```

---

## 6. 性能相关规范

### 6.1 避免分配

```csharp
// ✅ 正确：使用 Span，避免分配
public void ProcessBlock(ReadOnlySpan<double> input, Span<double> output)
{
    for (int i = 0; i < input.Length; i++)
        output[i] = ProcessSample(input[i]);
}

// ❌ 错误：每次调用分配新数组
public double[] ProcessBlock(double[] input)
{
    var output = new double[input.Length];  // 每次分配！
    // ...
    return output;
}
```

### 6.2 缓冲区复用

```csharp
// ✅ 正确：预分配缓冲区
private readonly double[] _workBuffer = new double[MaxBlockSize];

public void Process(ReadOnlySpan<double> input)
{
    // 使用预分配缓冲区
    input.CopyTo(_workBuffer);
    // ...
}
```

---

## 7. 文件组织规范

### 7.1 目录结构

```
src/
├── Core/
│   ├── Interfaces/           # 接口定义
│   │   ├── ITimeSeriesSource.cs
│   │   └── IFilterChain.cs
│   ├── Models/               # 数据模型
│   │   ├── GlobalTime.cs
│   │   └── EegDataPacket.cs
│   └── Enums/                # 枚举
│       └── ClockDomain.cs
├── DSP/
│   ├── Filters/              # 滤波器实现
│   └── Processing/           # 处理链
├── Rendering/
│   ├── Device/               # 设备管理
│   └── Layers/               # 渲染层
└── Infrastructure/
    ├── Buffers/              # 缓冲区
    └── Threading/            # 线程工具
```

### 7.2 单文件单类型

```csharp
// ✅ 正确：IFilterChain.cs 只包含 IFilterChain
public interface IFilterChain { ... }

// ❌ 错误：一个文件多个公开类型
public interface IFilterChain { ... }
public class FilterChain { ... }  // 应该单独文件
public enum FilterType { ... }    // 应该单独文件
```

---

## 8. 测试规范

### 8.1 测试命名

```csharp
// 格式: MethodName_Scenario_ExpectedResult
[Fact]
public void ProcessSample_WithValidInput_ReturnsFilteredValue()

[Fact]
public void ProcessSample_WithNaN_ThrowsArgumentException()

[Fact]
public void Reset_AfterProcessing_ClearsState()
```

### 8.2 测试组织

```
tests/
├── DSP.Tests/
│   ├── Filters/
│   │   ├── IirFilterTests.cs
│   │   └── NotchFilterTests.cs
│   └── Processing/
│       └── AeegProcessorTests.cs
└── Rendering.Tests/
    └── Device/
        └── DeviceManagerTests.cs
```

---

**文档结束**
