using Neo.NIRS;
using Neo.UI.ViewModels;

namespace Neo.UI.Services;

public sealed class NirsPanelController : IDisposable
{
    private readonly NirsIntegrationShell _shell;
    private readonly NirsViewModel _viewModel;
    private readonly Random _random = new(20260205);
    private readonly System.Windows.Threading.Dispatcher? _dispatcher;
    private System.Threading.Timer? _simulationTimer;
    private bool _isSimulating;
    private bool _started;
    private int _simulationTick;

    public NirsPanelController(
        NirsIntegrationShell shell,
        NirsViewModel viewModel,
        System.Windows.Threading.Dispatcher? dispatcher = null)
    {
        _shell = shell;
        _viewModel = viewModel;
        _dispatcher = dispatcher ?? System.Windows.Application.Current?.Dispatcher;
    }

    public void Start()
    {
        if (_started)
        {
            return;
        }

        _started = true;

        // 订阅 NIRS 数据事件
        _shell.SampleReceived += OnNirsSampleReceived;
        _shell.CrcErrorOccurred += OnCrcError;
        _shell.SerialErrorOccurred += OnSerialError;

        _shell.Start();

        if (!_shell.IsAvailable)
        {
            _viewModel.PanelStatus = $"{_shell.BlockReason} Simulated values active for UI interaction.";
            for (int i = 1; i <= _viewModel.Channels.Count; i++)
            {
                _viewModel.SetChannelState(i, i <= 3 ? NirsChannelState.Unknown : NirsChannelState.Blocked);
            }

            StartSimulationTimer();
            return;
        }

        _viewModel.PanelStatus = "NIRS data source active (1 Hz)";
        for (int i = 1; i <= _viewModel.Channels.Count; i++)
        {
            _viewModel.SetChannelState(i, NirsChannelState.Unknown);
        }
    }

    public void Stop()
    {
        if (!_started)
        {
            return;
        }

        _started = false;
        StopSimulationTimer();

        // 取消订阅事件
        _shell.SampleReceived -= OnNirsSampleReceived;
        _shell.CrcErrorOccurred -= OnCrcError;
        _shell.SerialErrorOccurred -= OnSerialError;

        _shell.Stop();
    }

    /// <summary>
    /// NIRS 样本到达处理。
    /// </summary>
    private void OnNirsSampleReceived(Neo.Core.Models.NirsSample sample, Neo.Core.Enums.QualityFlag[] qualityFlags)
    {
        void UpdateChannels()
        {
            // 更新 6 个通道
            double[] values = {
                sample.Ch1Percent,
                sample.Ch2Percent,
                sample.Ch3Percent,
                sample.Ch4Percent,
                sample.Ch5Percent,
                sample.Ch6Percent
            };

            for (int i = 0; i < 6; i++)
            {
                int channelIndex = i + 1;

                if (!_viewModel.Channels[i].IsEnabled)
                {
                    _viewModel.SetChannelState(channelIndex, NirsChannelState.Blocked);
                    continue;
                }

                // 检查质量标志
                if (qualityFlags[i] == Neo.Core.Enums.QualityFlag.LeadOff)
                {
                    _viewModel.SetChannelState(channelIndex, NirsChannelState.Fault);
                    continue;
                }

                // 显示百分比值
                int percentValue = (int)Math.Round(values[i]);
                _viewModel.SetChannelState(channelIndex, NirsChannelState.Percentage, percentValue);
            }
        }

        if (_dispatcher is null || _dispatcher.CheckAccess())
        {
            UpdateChannels();
        }
        else
        {
            _ = _dispatcher.BeginInvoke((Action)UpdateChannels);
        }
    }

    /// <summary>
    /// CRC 错误处理。
    /// </summary>
    private void OnCrcError(long errorCount)
    {
        System.Diagnostics.Debug.WriteLine($"[NIRS] CRC errors: {errorCount}");

        void UpdateStatus()
        {
            _viewModel.PanelStatus = $"NIRS active (CRC errors: {errorCount})";
        }

        if (_dispatcher is null || _dispatcher.CheckAccess())
        {
            UpdateStatus();
        }
        else
        {
            _ = _dispatcher.BeginInvoke((Action)UpdateStatus);
        }
    }

    /// <summary>
    /// 串口错误处理。
    /// </summary>
    private void OnSerialError(Exception ex)
    {
        System.Diagnostics.Debug.WriteLine($"[NIRS] Serial error: {ex.Message}");

        void UpdateStatus()
        {
            _viewModel.PanelStatus = $"NIRS error: {ex.Message}";
        }

        if (_dispatcher is null || _dispatcher.CheckAccess())
        {
            UpdateStatus();
        }
        else
        {
            _ = _dispatcher.BeginInvoke((Action)UpdateStatus);
        }
    }

    public void TickSimulationForTest()
    {
        if (!_isSimulating)
        {
            return;
        }

        RunSimulationTick();
    }

    private void StartSimulationTimer()
    {
        if (_isSimulating)
        {
            return;
        }

        _isSimulating = true;
        _simulationTimer = new System.Threading.Timer(
            _ => RunSimulationTick(),
            null,
            TimeSpan.FromSeconds(1),
            TimeSpan.FromSeconds(1));
    }

    private void StopSimulationTimer()
    {
        _isSimulating = false;
        _simulationTimer?.Dispose();
        _simulationTimer = null;
    }

    private void RunSimulationTick()
    {
        if (!_started || !_isSimulating)
        {
            return;
        }

        void ApplyTick()
        {
            _simulationTick++;

            for (int i = 1; i <= _viewModel.Channels.Count; i++)
            {
                var channel = _viewModel.Channels[i - 1];
                if (!channel.IsEnabled)
                {
                    _viewModel.SetChannelState(i, NirsChannelState.Blocked);
                    continue;
                }

                if (i > 3)
                {
                    _viewModel.SetChannelState(i, NirsChannelState.Blocked);
                    continue;
                }

                if (_simulationTick % 9 == 0 && i == 2)
                {
                    _viewModel.SetChannelState(i, NirsChannelState.Fault);
                    continue;
                }

                if (_simulationTick % 4 == 0 && i == 3)
                {
                    _viewModel.SetChannelState(i, NirsChannelState.Unknown);
                    continue;
                }

                _viewModel.SetChannelState(i, NirsChannelState.Percentage, _random.Next(58, 87));
            }
        }

        if (_dispatcher is null || _dispatcher.CheckAccess())
        {
            ApplyTick();
        }
        else
        {
            _ = _dispatcher.BeginInvoke((Action)ApplyTick);
        }
    }

    public void Dispose()
    {
        Stop();
        _shell.Dispose();
    }
}
