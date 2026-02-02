# 📋 TASK-S1-05: 模拟数据源

> **Sprint**: 1  
> **负责方**: Claude Code  
> **优先级**: 🟡 P1  
> **预估工时**: 4h  
> **状态**: ⏳ 待开始

---

## 1. 目标

实现用于测试和验收的模拟 EEG/NIRS 数据源，支持可控的波形生成和伪迹注入。

---

## 2. 输入（必读文件）

| 文件 | 重点章节 |
|------|----------|
| `spec/00_CONSTITUTION.md` | 铁律11（时间轴）、铁律2（不伪造波形） |
| `spec/DSP_SPEC.md` | §1（采集参数）、§6（伪迹检测） |
| `spec/TIME_SYNC.md` | §3（主机时间对齐） |
| `handoff/interfaces-api.md` | ITimeSeriesSource 接口定义 |

---

## 3. 输出

### 3.1 代码文件

```
src/Mock/
├── MockEegSource.cs              # EEG 模拟数据源
├── MockNirsSource.cs             # NIRS 模拟数据源
├── WaveformGenerators/
│   ├── SineWaveGenerator.cs      # 正弦波生成器
│   ├── AlphaWaveGenerator.cs     # Alpha 波生成器
│   └── NoiseGenerator.cs         # 噪声生成器
└── ArtifactInjectors/
    ├── GapInjector.cs            # Gap 注入器
    ├── ClipInjector.cs           # 饱和注入器
    └── OutlierInjector.cs        # 离群值注入器

tests/Mock.Tests/
├── MockEegSourceTests.cs
├── MockNirsSourceTests.cs
└── ArtifactInjectorTests.cs
```

### 3.2 交接文档

```
handoff/mock-data-api.md
```

---

## 4. 设计规格

### 4.1 MockEegSource

```csharp
/// <summary>
/// EEG 模拟数据源
/// </summary>
/// <remarks>
/// <para><b>线程模型</b>: 内部定时器线程生成数据</para>
/// <para><b>时间戳</b>: 使用 HostClock，样本中心时间</para>
/// </remarks>
public class MockEegSource : ITimeSeriesSource, IDisposable
{
    // ITimeSeriesSource 实现
    public string Name => "MockEEG";
    public int SampleRateHz => 160;
    public int ChannelCount => 4;
    public ClockDomain ClockDomain => ClockDomain.Host;
    public int EstimatedPrecisionUs => 1000;  // 1ms 精度
    
    public event EventHandler<DataReceivedEventArgs> DataReceived;
    
    /// <summary>配置波形生成</summary>
    public MockEegConfig Config { get; set; }
    
    /// <summary>启动数据生成</summary>
    public void Start();
    
    /// <summary>停止数据生成</summary>
    public void Stop();
    
    /// <summary>注入伪迹（测试用）</summary>
    public void InjectArtifact(ArtifactType type, int durationSamples);
}

public class MockEegConfig
{
    /// <summary>基础振幅 (μV)</summary>
    public double BaseAmplitude { get; set; } = 50;
    
    /// <summary>Alpha 波频率 (Hz)</summary>
    public double AlphaFrequency { get; set; } = 10;
    
    /// <summary>Alpha 波幅度 (μV)</summary>
    public double AlphaAmplitude { get; set; } = 30;
    
    /// <summary>噪声标准差 (μV)</summary>
    public double NoiseStdDev { get; set; } = 5;
    
    /// <summary>各通道差异因子</summary>
    public double[] ChannelFactors { get; set; } = { 1.0, 0.9, 1.1, 0.95 };
}
```

### 4.2 波形生成算法

```csharp
// 每个样本的生成（160Hz = 每6.25ms一个样本）
private EegSample GenerateSample(long timestampUs, int sampleIndex)
{
    double t = sampleIndex / (double)SampleRateHz;  // 时间（秒）
    
    // 基础波形：Alpha 波
    double alpha = Config.AlphaAmplitude * 
        Math.Sin(2 * Math.PI * Config.AlphaFrequency * t);
    
    // 添加噪声
    double noise = _random.NextGaussian() * Config.NoiseStdDev;
    
    // 各通道略有差异
    return new EegSample
    {
        TimestampUs = timestampUs,
        Ch1 = (alpha + noise) * Config.ChannelFactors[0],
        Ch2 = (alpha + noise) * Config.ChannelFactors[1],
        Ch3 = (alpha + noise) * Config.ChannelFactors[2],
        Ch4 = (alpha + noise) * Config.ChannelFactors[3],
        Quality = QualityFlags.None
    };
}
```

### 4.3 伪迹注入

```csharp
public enum ArtifactType
{
    Gap,      // 数据缺失
    Clip,     // 信号饱和
    Outlier   // 离群值
}

/// <summary>Gap 注入器：跳过指定数量的样本</summary>
public class GapInjector
{
    public void Inject(int gapSamples)
    {
        // 跳过 gapSamples 个样本，不生成数据
        // 下一个样本的时间戳会有跳变
    }
}

/// <summary>Clip 注入器：将值钳位到饱和值</summary>
public class ClipInjector
{
    public double Inject(double value, int durationSamples)
    {
        if (_clipRemaining > 0)
        {
            _clipRemaining--;
            return Math.Sign(value) * ClipThreshold;  // ±2400 μV
        }
        return value;
    }
}

/// <summary>Outlier 注入器：产生异常大值</summary>
public class OutlierInjector
{
    public double Inject(double value)
    {
        if (_shouldInject)
        {
            return value * 10;  // 10倍异常值
        }
        return value;
    }
}
```

### 4.4 MockNirsSource

```csharp
public class MockNirsSource : ITimeSeriesSource, IDisposable
{
    public string Name => "MockNIRS";
    public int SampleRateHz => 4;
    public int ChannelCount => 6;
    public ClockDomain ClockDomain => ClockDomain.Host;
    public int EstimatedPrecisionUs => 10000;  // 10ms 精度
    
    // NIRS 数据：基线 70%，缓慢波动 ±5%
    private NirsSample GenerateSample(long timestampUs, int sampleIndex)
    {
        double t = sampleIndex / (double)SampleRateHz;
        
        return new NirsSample
        {
            TimestampUs = timestampUs,
            Values = new double[6]
            {
                70 + 5 * Math.Sin(2 * Math.PI * 0.05 * t),
                70 + 5 * Math.Sin(2 * Math.PI * 0.05 * t + 0.5),
                70 + 5 * Math.Sin(2 * Math.PI * 0.05 * t + 1.0),
                70 + 5 * Math.Sin(2 * Math.PI * 0.05 * t + 1.5),
                70 + 5 * Math.Sin(2 * Math.PI * 0.05 * t + 2.0),
                70 + 5 * Math.Sin(2 * Math.PI * 0.05 * t + 2.5),
            }
        };
    }
}
```

---

## 5. 验收标准

### 5.1 功能验收

- [ ] 实现 ITimeSeriesSource 接口
- [ ] 160Hz EEG 数据生成（4通道）
- [ ] 4Hz NIRS 数据生成（6通道）
- [ ] 时间戳使用 HostClock（μs）
- [ ] 支持 Gap/Clip/Outlier 伪迹注入

### 5.2 波形验收

```
EEG 波形特征：
- [ ] Alpha 波（8-12Hz）明显可见
- [ ] 各通道有差异但相似
- [ ] 噪声适中，不掩盖主波形

NIRS 波形特征：
- [ ] 基线稳定在 70% 附近
- [ ] 缓慢波动周期约 20 秒
- [ ] 6 通道有相位差
```

### 5.3 伪迹验收

```
- [ ] Gap 注入后时间戳跳变正确
- [ ] Clip 注入后值钳位到 ±2400 μV
- [ ] Outlier 注入后值异常放大
- [ ] Quality 标志正确设置
```

### 5.4 编译验收

- [ ] `dotnet build` 通过
- [ ] `dotnet test` 全部通过

---

## 6. 约束（不可违反）

```
❌ 禁止生成无时间戳的数据
❌ 禁止使用 DateTime，必须用 HostClock
❌ 禁止在 Gap 期间生成数据（真正跳过）
✅ 必须实现 ITimeSeriesSource 接口
✅ 必须支持可配置的波形参数
```

---

## 7. 依赖与被依赖

### 依赖
- S1-01: 核心接口定义（ITimeSeriesSource, EegSample, NirsSample）

### 被依赖
- S1-04: 三层渲染框架（集成测试）
- S2-xx: DSP 链路测试

---

## 8. 单元测试要求

```csharp
[Fact]
public void MockEegSource_GeneratesCorrectSampleRate()
{
    var source = new MockEegSource();
    var samples = new List<EegSample>();
    
    source.DataReceived += (s, e) => samples.Add(e.Sample);
    source.Start();
    
    Thread.Sleep(1000);  // 等待1秒
    source.Stop();
    
    // 1秒应该产生约160个样本
    Assert.InRange(samples.Count, 155, 165);
}

[Fact]
public void MockEegSource_TimestampsAreMonotonic()
{
    var source = new MockEegSource();
    var samples = new List<EegSample>();
    
    source.DataReceived += (s, e) => samples.Add(e.Sample);
    source.Start();
    Thread.Sleep(500);
    source.Stop();
    
    // 时间戳必须单调递增
    for (int i = 1; i < samples.Count; i++)
    {
        Assert.True(samples[i].TimestampUs > samples[i-1].TimestampUs);
    }
}

[Fact]
public void GapInjector_CreatesTimestampJump()
{
    var source = new MockEegSource();
    var samples = new List<EegSample>();
    
    source.DataReceived += (s, e) => samples.Add(e.Sample);
    source.Start();
    Thread.Sleep(100);
    
    source.InjectArtifact(ArtifactType.Gap, 8);  // 8样本 = 50ms
    
    Thread.Sleep(100);
    source.Stop();
    
    // 检查时间戳跳变 > 40ms
    var gaps = samples
        .Zip(samples.Skip(1), (a, b) => b.TimestampUs - a.TimestampUs)
        .Where(diff => diff > 40000)  // 40ms = 40000μs
        .ToList();
    
    Assert.NotEmpty(gaps);
}
```

---

## 9. 启动指令（给 Claude Code）

```
请先阅读以下文件：
1. spec/00_CONSTITUTION.md（铁律2、铁律11）
2. spec/DSP_SPEC.md §1, §6
3. spec/TIME_SYNC.md §3
4. handoff/interfaces-api.md（S1-01产出）

然后执行任务 TASK-S1-05：
- 实现 MockEegSource（160Hz，4通道）
- 实现 MockNirsSource（4Hz，6通道）
- 实现伪迹注入器（Gap/Clip/Outlier）
- 编写单元测试
- 完成后生成 handoff/mock-data-api.md
```

---

**任务卡结束**
