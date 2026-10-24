using PowerMonitor.Core.Deye;
using PowerMonitor.Core.Estimate;
using PowerMonitor.Core.Settings;
using PowerMonitor.Pc;

namespace PowerMonitor.Monitoring;

internal sealed class MonitorLoop : IDisposable
{
    private readonly SettingsHolder _settings;
    private readonly PcPowerReader _pc;
    private readonly Action<MonitorUpdate> _publish;
    private readonly CancellationTokenSource _cts = new();
    private readonly SemaphoreSlim _kick = new(0);
    private Task? _task;

    public MonitorLoop(SettingsHolder settings, PcPowerReader pc, Action<MonitorUpdate> publish)
    {
        _settings = settings;
        _pc = pc;
        _publish = publish;
    }

    public void Start() => _task = Task.Run(() => RunAsync(_cts.Token));

    public void Kick()
    {
        try
        {
            _kick.Release();
        }
        catch (ObjectDisposedException)
        {
        }
    }

    public void Dispose()
    {
        _cts.Cancel();
        try
        {
            _kick.Release();
        }
        catch (ObjectDisposedException)
        {
        }

        try
        {
            _task?.Wait(TimeSpan.FromSeconds(3));
        }
        catch
        {
        }

        _kick.Dispose();
        _cts.Dispose();
    }

    private async Task RunAsync(CancellationToken cancellationToken)
    {
        InverterSnapshot? last = null;
        string? connectionKey = null;
        var guard = new SocJumpGuard();

        while (!cancellationToken.IsCancellationRequested)
        {
            var settings = _settings.Current;
            var key = ConnectionKey(settings);
            if (key != connectionKey)
            {
                last = null;
                guard.Reset();
                connectionKey = key;
            }

            var pc = _pc.Read(settings.PcBaselineWatts);
            string? error = null;
            try
            {
                var reading = await DeyeClient.ReadAsync(settings, cancellationToken);
                if (guard.Accept(reading))
                    last = reading;
                else
                    error = $"Battery jumped from {last?.SocPercent}% to {reading.SocPercent}%. Waiting for the next reading to confirm it.";
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                error = ex.Message;
            }

            BackupEstimate? backup = null;
            if (last is not null)
            {
                try
                {
                    backup = RuntimeEstimator.Estimate(last, settings);
                }
                catch (Exception ex)
                {
                    error ??= ex.Message;
                }
            }

            _publish(new MonitorUpdate(last, backup, pc, error, DateTimeOffset.Now));

            if (!await WaitAsync(settings.PollIntervalSeconds, cancellationToken))
                break;
        }
    }

    private async Task<bool> WaitAsync(int intervalSeconds, CancellationToken cancellationToken)
    {
        using var wake = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var seconds = Math.Clamp(intervalSeconds, AppSettings.MinPollIntervalSeconds, AppSettings.MaxPollIntervalSeconds);
        var delay = Task.Delay(TimeSpan.FromSeconds(seconds), wake.Token);
        var kick = _kick.WaitAsync(wake.Token);
        try
        {
            await Task.WhenAny(delay, kick);
            wake.Cancel();
            try
            {
                await delay;
            }
            catch (OperationCanceledException)
            {
            }

            try
            {
                await kick;
            }
            catch (OperationCanceledException)
            {
            }

            while (_kick.CurrentCount > 0)
                _kick.Wait(0);
            return !cancellationToken.IsCancellationRequested;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return false;
        }
    }

    private static string ConnectionKey(AppSettings settings) =>
        $"{settings.LoggerHost}|{settings.LoggerPort}|{settings.Protocol}|{settings.UnitId}|{settings.LoggerSerial}|{settings.RegisterAddressOffset}|{settings.InvertPowerSign}";
}
