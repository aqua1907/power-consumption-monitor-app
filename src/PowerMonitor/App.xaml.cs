using Microsoft.UI.Dispatching;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using PowerMonitor.Core.Deye;
using PowerMonitor.Core.Settings;
using PowerMonitor.Monitoring;
using PowerMonitor.Pc;
using PowerMonitor.Startup;
using PowerMonitor.Tray;
using PowerMonitor.Ui;
using PowerMonitor.Views;

namespace PowerMonitor;

public sealed partial class App : Application
{
    private readonly DispatcherQueue _dispatcher;
    private SingleInstance? _instance;
    private RegisteredWaitHandle? _showWait;
    private SettingsHolder? _settings;
    private PcPowerReader? _pc;
    private StatusWindow? _status;
    private SettingsWindow? _settingsWindow;
    private MonitorLoop? _loop;
    private TrayIconService? _tray;
    private readonly AlertState _alerts = new();
    private int? _iconSoc;
    private int _iconPixels;
    private bool? _iconGrid;
    private bool _iconFailed;
    private bool _exiting;

    public App()
    {
        InitializeComponent();
        _dispatcher = DispatcherQueue.GetForCurrentThread();
    }

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        try
        {
            _instance = SingleInstance.TryAcquire();
            if (_instance is null)
            {
                Exit();
                return;
            }

            var loadedSettings = SettingsStore.Load();
            _settings = new SettingsHolder(loadedSettings);
            _pc = new PcPowerReader();
            _status = new StatusWindow();
            _status.SettingsRequested += (_, _) => ShowSettings();
            _status.NativeAppWindow.Closing += StatusClosing;

            try
            {
                WindowsStartup.Apply(loadedSettings.StartWithWindows);
            }
            catch
            {
                // The app still runs; Settings can repair startup registration.
            }

            _iconPixels = MonitorMetrics.TrayIconPixels();
            _tray = new TrayIconService(
                _dispatcher,
                BatteryIcon.Create(null, null, failed: false, _iconPixels));
            _tray.OpenRequested += ShowStatus;
            _tray.ToggleRequested += () => _status?.ToggleDashboard();
            _tray.SettingsRequested += ShowSettings;
            _tray.ExitRequested += ExitApplication;

            _showWait = ThreadPool.RegisterWaitForSingleObject(
                _instance.ShowEvent,
                (_, _) => _dispatcher.TryEnqueue(ShowStatus),
                null,
                Timeout.Infinite,
                executeOnlyOnce: false);

            _loop = new MonitorLoop(_settings, _pc, Publish);
            _loop.Start();
        }
        catch (Exception ex)
        {
            var directory = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "PowerMonitor");
            Directory.CreateDirectory(directory);
            File.WriteAllText(Path.Combine(directory, "startup-error.txt"), ex.ToString());
            ExitApplication();
        }
    }

    private void Publish(MonitorUpdate update) =>
        _dispatcher.TryEnqueue(() => ShowUpdate(update));

    private void ShowUpdate(MonitorUpdate update)
    {
        if (_status is null || _tray is null || _settings is null)
            return;

        _status.Apply(update, _settings.Current.ReserveSocPercent);
        var failed = update.Error is not null;
        var soc = failed ? null : update.Inverter?.SocPercent;
        var grid = failed ? null : update.Inverter?.GridConnected;
        var iconPixels = MonitorMetrics.TrayIconPixels();
        if (soc != _iconSoc || grid != _iconGrid || failed != _iconFailed || iconPixels != _iconPixels)
        {
            _tray.Update(BatteryIcon.Create(soc, grid, failed, iconPixels), Tooltip(update));
            _iconPixels = iconPixels;
            _iconSoc = soc;
            _iconGrid = grid;
            _iconFailed = failed;
        }
        else
        {
            _tray.UpdateTooltip(Tooltip(update));
        }

        if (update.Error is null && update.Inverter is not null)
        {
            var message = _alerts.Next(update.Inverter, _settings.Current);
            if (message is not null)
                _tray.ShowNotification("Power monitor", message);
        }
    }

    private void ShowStatus() => _status?.ShowDashboard();

    private void ShowSettings()
    {
        if (_settings is null)
            return;
        if (_settingsWindow is not null)
        {
            _settingsWindow.Activate();
            return;
        }

        _settingsWindow = new SettingsWindow(_settings.Current);
        _settingsWindow.Saved += SaveSettings;
        _settingsWindow.Closed += (_, _) => _settingsWindow = null;
        _settingsWindow.Activate();
    }

    private void SaveSettings(AppSettings edited)
    {
        if (_settings is null)
            return;

        try
        {
            WindowsStartup.Apply(edited.StartWithWindows);
        }
        catch (Exception ex)
        {
            _tray?.ShowNotification("Could not update Windows startup", ex.Message);
        }

        SettingsStore.Save(edited);
        _settings.Update(edited);
        _alerts.Reset();
        _loop?.Kick();
    }

    private void StatusClosing(AppWindow sender, AppWindowClosingEventArgs args)
    {
        if (_exiting)
            return;

        args.Cancel = true;
        _status?.HideDashboard();
    }

    private void ExitApplication()
    {
        if (_exiting)
            return;
        _exiting = true;

        _showWait?.Unregister(null);
        _showWait = null;
        _loop?.Dispose();
        _loop = null;
        _tray?.Dispose();
        _tray = null;
        _settingsWindow?.Close();
        _settingsWindow = null;
        _status?.Close();
        _status = null;
        _pc?.Dispose();
        _pc = null;
        _instance?.Dispose();
        _instance = null;
        Exit();
    }

    private static string Tooltip(MonitorUpdate update)
    {
        if (update.Error is not null)
            return update.Inverter is null
                ? update.Error
                : $"Stale since {update.Inverter.ReadAt.LocalDateTime:HH:mm}: {update.Error}";
        if (update.Inverter is null)
            return "Power monitor";

        var grid = update.Inverter.GridConnected ? "Grid on" : "Grid off";
        var backup = update.Backup?.DurationText ?? "—";
        return $"{update.Inverter.SocPercent}% | {grid} | " +
               $"{Math.Abs(update.Inverter.LoadPowerWatts)} W | {backup} | " +
               $"PC ~{update.Pc.EstimatedWatts:0} W";
    }

    private sealed class AlertState
    {
        private bool _seen;
        private int _soc;
        private bool _grid;

        public void Reset() => _seen = false;

        public string? Next(InverterSnapshot snapshot, AppSettings settings)
        {
            string? message = null;
            if (_seen)
            {
                var gridLost = settings.NotifyOnGridLoss && _grid && !snapshot.GridConnected;
                var low = settings.NotifyOnLowSoc &&
                          _soc > AppSettings.LowSocPercent &&
                          snapshot.SocPercent <= AppSettings.LowSocPercent;
                message = (gridLost, low) switch
                {
                    (true, true) => $"The grid dropped. Battery is down to {snapshot.SocPercent}%.",
                    (true, false) => $"The grid dropped. Battery is at {snapshot.SocPercent}%.",
                    (false, true) => $"Battery is down to {snapshot.SocPercent}%.",
                    _ => null
                };
            }

            _seen = true;
            _soc = snapshot.SocPercent;
            _grid = snapshot.GridConnected;
            return message;
        }
    }
}
