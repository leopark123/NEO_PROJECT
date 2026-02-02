// ZeroPhaseFilterTests.cs
// AT-19 验收测试: Zero-Phase 滤波（filtfilt）

using Xunit;
using Neo.DSP.Filters;

namespace Neo.DSP.Tests.Filters;

/// <summary>
/// AT-19 Zero-Phase 滤波验收测试。
/// </summary>
public class ZeroPhaseFilterTests
{
    private const int SampleRate = 160;

    /// <summary>
    /// Zero-phase 滤波应产生零相位延迟。
    /// 10Hz 正弦波经 LPF(35Hz) zero-phase 后，互相关峰应在 lag=0。
    /// </summary>
    [Fact]
    public void ZeroPhase_PhaseDelayIsZero()
    {
        const int N = 1600; // 10 seconds
        const double freq = 10.0;
        var input = new double[N];
        var output = new double[N];

        for (int i = 0; i < N; i++)
            input[i] = Math.Sin(2 * Math.PI * freq * i / SampleRate);

        var lpf = LowPassFilter.Create(LowPassCutoff.Hz35);
        lpf.ProcessZeroPhase(input, output);

        // Find cross-correlation peak
        int bestLag = 0;
        double bestCorr = double.MinValue;

        // Skip first/last 200 samples to avoid edge effects
        int trim = 200;
        for (int lag = -10; lag <= 10; lag++)
        {
            double corr = 0;
            int count = 0;
            for (int i = trim; i < N - trim; i++)
            {
                int j = i + lag;
                if (j >= 0 && j < N)
                {
                    corr += input[i] * output[j];
                    count++;
                }
            }
            corr /= count;
            if (corr > bestCorr)
            {
                bestCorr = corr;
                bestLag = lag;
            }
        }

        Assert.Equal(0, bestLag);
    }

    /// <summary>
    /// Zero-phase 增益为 |H(f)|²: 即 gain_zp(dB) ≈ 2 × gain_single(dB)。
    /// 在通带外的频率，zero-phase 衰减应大于单次滤波。
    /// </summary>
    [Fact]
    public void ZeroPhase_AmplitudeSquared()
    {
        const int N = 3200;
        const double freq = 50.0; // Above cutoff (35Hz LPF)
        var input = new double[N];
        var singleOutput = new double[N];
        var zeroPhaseOutput = new double[N];

        for (int i = 0; i < N; i++)
            input[i] = Math.Sin(2 * Math.PI * freq * i / SampleRate);

        // Single-pass filtering
        var lpf1 = LowPassFilter.Create(LowPassCutoff.Hz35);
        for (int i = 0; i < N; i++)
            singleOutput[i] = lpf1.Process(input[i]);

        // Zero-phase filtering
        var lpf2 = LowPassFilter.Create(LowPassCutoff.Hz35);
        lpf2.ProcessZeroPhase(input, zeroPhaseOutput);

        // Measure RMS in steady-state region (skip edges)
        int trim = 400;
        double inputRms = 0, singleRms = 0, zeroPhaseRms = 0;
        int count = N - 2 * trim;
        for (int i = trim; i < N - trim; i++)
        {
            inputRms += input[i] * input[i];
            singleRms += singleOutput[i] * singleOutput[i];
            zeroPhaseRms += zeroPhaseOutput[i] * zeroPhaseOutput[i];
        }
        inputRms = Math.Sqrt(inputRms / count);
        singleRms = Math.Sqrt(singleRms / count);
        zeroPhaseRms = Math.Sqrt(zeroPhaseRms / count);

        double singleGainDb = 20 * Math.Log10(singleRms / inputRms);
        double zeroPhaseGainDb = 20 * Math.Log10(zeroPhaseRms / inputRms);

        // Zero-phase gain should be approximately double the single-pass gain (in dB)
        // i.e., zeroPhaseGainDb ≈ 2 × singleGainDb
        double expectedZpGainDb = 2.0 * singleGainDb;
        Assert.InRange(zeroPhaseGainDb, expectedZpGainDb - 2.0, expectedZpGainDb + 2.0);

        // Zero-phase should attenuate more than single pass
        Assert.True(zeroPhaseGainDb < singleGainDb,
            $"Zero-phase ({zeroPhaseGainDb:F2}dB) should attenuate more than single-pass ({singleGainDb:F2}dB)");
    }

    /// <summary>
    /// ProcessZeroPhase 不应污染实时滤波器的持久状态。
    /// </summary>
    [Fact]
    public void ZeroPhase_DoesNotPollutePersistentState()
    {
        var lpf = LowPassFilter.Create(LowPassCutoff.Hz35);

        // Process some samples in real-time mode
        for (int i = 0; i < 100; i++)
            lpf.Process(Math.Sin(2 * Math.PI * 10 * i / SampleRate));

        double stateCheckBefore = lpf.Process(1.0);

        // Reset and repeat to get reference state
        lpf.Reset();
        for (int i = 0; i < 100; i++)
            lpf.Process(Math.Sin(2 * Math.PI * 10 * i / SampleRate));

        // Now do a zero-phase pass (should not affect state)
        var zeroInput = new double[500];
        var zeroOutput = new double[500];
        for (int i = 0; i < 500; i++)
            zeroInput[i] = Math.Sin(2 * Math.PI * 5 * i / SampleRate);
        lpf.ProcessZeroPhase(zeroInput, zeroOutput);

        double stateCheckAfter = lpf.Process(1.0);

        Assert.Equal(stateCheckBefore, stateCheckAfter, 10);
    }

    /// <summary>
    /// 通过 EegFilterChain.ProcessBlockZeroPhase 验证完整链路。
    /// </summary>
    [Fact]
    public void ZeroPhase_FullChain()
    {
        var config = new EegFilterChainConfig
        {
            NotchFrequency = NotchFrequency.Hz50,
            HighPassCutoff = HighPassCutoff.Hz0_5,
            LowPassCutoff = LowPassCutoff.Hz35,
            ChannelCount = 1
        };

        using var chain = new EegFilterChain(config);

        const int N = 800;
        var input = new double[N];
        var output = new double[N];

        // 10Hz signal should pass through mostly intact
        for (int i = 0; i < N; i++)
            input[i] = 100.0 * Math.Sin(2 * Math.PI * 10 * i / SampleRate);

        chain.ProcessBlockZeroPhase(0, input, output);

        // Verify output is not all zeros (signal should pass)
        double rms = 0;
        int trim = 200;
        for (int i = trim; i < N - trim; i++)
            rms += output[i] * output[i];
        rms = Math.Sqrt(rms / (N - 2 * trim));

        Assert.True(rms > 50.0, $"10Hz signal should pass through filter chain, got RMS={rms:F2}");
    }

    /// <summary>
    /// 短块（10 样本）不应崩溃。
    /// </summary>
    [Fact]
    public void ZeroPhase_ShortBlock()
    {
        var lpf = LowPassFilter.Create(LowPassCutoff.Hz35);
        var input = new double[10];
        var output = new double[10];

        for (int i = 0; i < 10; i++)
            input[i] = i * 0.1;

        lpf.ProcessZeroPhase(input, output);

        // Just verify no exception and output is finite
        for (int i = 0; i < 10; i++)
            Assert.False(double.IsNaN(output[i]) || double.IsInfinity(output[i]),
                $"Output[{i}] should be finite, got {output[i]}");
    }

    /// <summary>
    /// 10000 随机样本无 NaN/Inf。
    /// </summary>
    [Fact]
    public void ZeroPhase_NumericalStability()
    {
        const int N = 10000;
        var rng = new Random(42);
        var input = new double[N];
        var output = new double[N];

        for (int i = 0; i < N; i++)
            input[i] = (rng.NextDouble() - 0.5) * 200.0; // ±100 μV range

        var lpf = LowPassFilter.Create(LowPassCutoff.Hz35);
        lpf.ProcessZeroPhase(input, output);

        for (int i = 0; i < N; i++)
        {
            Assert.False(double.IsNaN(output[i]), $"Output[{i}] is NaN");
            Assert.False(double.IsInfinity(output[i]), $"Output[{i}] is Infinity");
        }

        // Also test with HPF
        var hpf = HighPassFilter.Create(HighPassCutoff.Hz0_5);
        hpf.ProcessZeroPhase(input, output);

        for (int i = 0; i < N; i++)
        {
            Assert.False(double.IsNaN(output[i]), $"HPF Output[{i}] is NaN");
            Assert.False(double.IsInfinity(output[i]), $"HPF Output[{i}] is Infinity");
        }
    }
}
