using Microsoft.UI.Dispatching;
using PowerMonitor.Core.Schedule;
using PowerMonitor.Core.Settings;
using PowerMonitor.Monitoring;

namespace PowerMonitor.Schedule;

/// <summary>
/// Keeps the outage schedule current: fetches it on its own timer (never on the inverter loop, so a
/// slow operator site cannot delay a reading), stores each day for the comparison, and raises the
/// notifications. Everything runs on the UI thread; only the HTTP request awaits.
/// </summary>
internal sealed class ScheduleService : IDisposable
{
    public static readonly TimeSpan FetchEvery = TimeSpan.FromMinutes(15);
    public static readonly TimeSpan RetryEvery = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan TickEvery = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan SaveEvery = TimeSpan.FromMinutes(1);

    private readonly SettingsHolder _settings;
    private readonly Action<string, string> _notify;
    private readonly Action _changed;
    private readonly ScheduleAlerts _alerts = new();
    private readonly ScheduleHttp _http = new([YasnoProvider.Host]);
    private readonly DispatcherQueueTimer _timer;
    private readonly CancellationTokenSource _cts = new();
    private bool _busy;
    private bool _force = true;
    private bool _disposed;
    private DateTimeOffset _nextFetch = DateTimeOffset.MinValue;
    private DateTimeOffset _lastSaved = DateTimeOffset.MinValue;

    public ScheduleService(
        DispatcherQueue dispatcher,
        SettingsHolder settings,
        ScheduleArchive archive,
        Action<string, string> notify,
        Action changed)
    {
        _settings = settings;
        Archive = archive;
        _notify = notify;
        _changed = changed;
        _timer = dispatcher.CreateTimer();
        _timer.Interval = TickEvery;
        _timer.Tick += (_, _) => Tick();
    }

    public ScheduleArchive Archive { get; }

    /// <summary>Why the last fetch failed, or null.</summary>
    public string? Error { get; private set; }

    public DateTimeOffset? LastFetched { get; private set; }

    public void Start()
    {
        _timer.Start();
        Tick();
    }

    /// <summary>Fetch now, for example after the address changed.</summary>
    public void Kick()
    {
        _force = true;
        Tick();
    }

    /// <summary>Called for every successful inverter read, so the comparison knows when the grid was being watched.</summary>
    public void ObserveGrid(DateTimeOffset at, TimeSpan pollInterval)
    {
        if (!_settings.Current.ScheduleActive)
            return;

        Archive.Observe(at, pollInterval);
        Save(at, force: false);
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
        _timer.Stop();
        _cts.Cancel();
        if (_settings.Current.ScheduleActive || Archive.Days.Count > 0)
            ScheduleStore.Save(Archive);
        _http.Dispose();
        _cts.Dispose();
    }

    private void Tick()
    {
        if (_disposed)
            return;

        var settings = _settings.Current;
        if (!settings.ScheduleActive)
        {
            if (Error is not null)
            {
                Error = null;
                _changed();
            }

            _force = true;
            return;
        }

        var now = DateTimeOffset.Now;
        if (ScheduleOperator.Find(settings.ScheduleOperator) is not { } op)
        {
            Error = "The saved outage operator is not supported. Choose one in Settings.";
            _changed();
            return;
        }

        if (!Archive.BelongsTo(op.Id, settings.ScheduleGroup))
        {
            Archive.Rebind(op.Id, settings.ScheduleGroup);
            _alerts.Reset();
            _force = true;
        }

        if (!_busy && (_force || now >= _nextFetch))
            _ = RefreshAsync(op, settings.ScheduleGroup);

        foreach (var message in _alerts.Check(Archive.Days.Values, now, settings))
            _notify("Outage schedule", message);

        _changed();
    }

    private async Task RefreshAsync(ScheduleOperator op, string group)
    {
        _busy = true;
        _force = false;
        try
        {
            var days = await op.CreateProvider(_http).FetchAsync(group, _cts.Token);
            if (_disposed || !Archive.BelongsTo(op.Id, group))
                return;

            var now = DateTimeOffset.Now;
            var firstFetch = Archive.Days.Count == 0;
            var changes = Archive.Merge(days, KyivTime.DayOf(now));
            Archive.Prune(now);
            LastFetched = now;
            Error = null;
            _nextFetch = now + FetchEvery;
            Save(now, force: true);

            if (!firstFetch && _settings.Current.NotifyOnScheduleChange &&
                ScheduleAlerts.DescribeChanges(changes, KyivTime.DayOf(now)) is { } message)
                _notify("Outage schedule", message);
        }
        catch (OperationCanceledException) when (_cts.IsCancellationRequested)
        {
            return;
        }
        catch (Exception ex)
        {
            Error = ex.Message;
            _nextFetch = DateTimeOffset.Now + RetryEvery;
        }
        finally
        {
            _busy = false;
        }

        if (!_disposed)
            _changed();
    }

    private void Save(DateTimeOffset now, bool force)
    {
        if (!force && now - _lastSaved < SaveEvery)
            return;

        _lastSaved = now;
        ScheduleStore.Save(Archive);
    }
}
