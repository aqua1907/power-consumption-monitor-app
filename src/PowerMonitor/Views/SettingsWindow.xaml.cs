using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using PowerMonitor.BackupMode;
using PowerMonitor.Core.Deye;
using PowerMonitor.Core.Estimate;
using PowerMonitor.Core.Settings;

namespace PowerMonitor.Views;

internal sealed partial class SettingsWindow : Window
{
    private readonly AppWindow _appWindow;
    private readonly BackupPlayController _backupPlay;
    private bool _loading;
    private FinderWindow? _finder;

    public event Action<AppSettings>? Saved;

    public SettingsWindow(AppSettings settings, BackupPlayController backupPlay)
    {
        InitializeComponent();
        SystemBackdrop = new MicaBackdrop();
        ExtendsContentIntoTitleBar = true;

        _backupPlay = backupPlay;
        _appWindow = WindowSizing.AppWindowOf(this);
        WindowSizing.Fit(this, "settings", 520, 780);
        _ = new WindowSizeTracker(this, "settings");
        if (_appWindow.Presenter is OverlappedPresenter presenter)
        {
            presenter.IsResizable = true;
            presenter.IsMaximizable = true;
        }

        LoadSettings(settings);
    }

    private void LoadSettings(AppSettings settings)
    {
        _loading = true;
        ProtocolBox.SelectedIndex = settings.Protocol == LoggerProtocol.SolarmanV5 ? 1 : 0;
        HostBox.Text = settings.LoggerHost;
        PortBox.Value = Math.Clamp(settings.LoggerPort, 1, 65535);
        UnitIdBox.Value = Math.Clamp(settings.UnitId, 1, 247);
        SerialBox.Text = settings.LoggerSerial == 0 ? "" : settings.LoggerSerial.ToString();
        OffsetBox.Value = settings.RegisterAddressOffset == 1 ? 1 : 0;
        InvertToggle.IsChecked = settings.InvertPowerSign;
        ReserveBox.Value = Math.Clamp(settings.ReserveSocPercent, 0, 90);
        LowSocThresholdBox.Value = Math.Clamp(settings.LowSocAlertPercent, 1, 90);
        PacksBox.Value = Math.Clamp(settings.PackCount, 1, 64);
        BaselineBox.Value = Math.Clamp(settings.PcBaselineWatts, 0, 2000);
        IntervalBox.Value = Math.Clamp(
            settings.PollIntervalSeconds,
            AppSettings.MinPollIntervalSeconds,
            AppSettings.MaxPollIntervalSeconds);
        StartupToggle.IsChecked = settings.StartWithWindows;
        GridNotificationToggle.IsChecked = settings.NotifyOnGridLoss;
        SocNotificationToggle.IsChecked = settings.NotifyOnLowSoc;
        BackupPlayToggle.IsChecked = settings.BackupPlayModeEnabled;
        WowConfigPathBox.Text = settings.WowForeverConfigPath;
        BackupHzBox.Value = Math.Clamp(settings.BackupDisplayHz, 60, 240);
        GridHzBox.Value = Math.Clamp(settings.GridDisplayHz, 60, 360);
        GpuPowerToggle.IsChecked = settings.BackupGpuPowerEnabled;
        BackupGpuWattsBox.Value = Math.Clamp(settings.BackupGpuPowerWatts, 150, 600);
        GridGpuWattsBox.Value = Math.Clamp(settings.GridGpuPowerWatts, 150, 600);
        SerialBox.IsEnabled = settings.Protocol == LoggerProtocol.SolarmanV5;
        _loading = false;
    }

    private AppSettings ReadSettings()
    {
        uint serial = 0;
        if (!string.IsNullOrWhiteSpace(SerialBox.Text) &&
            !uint.TryParse(SerialBox.Text.Trim(), out serial))
        {
            throw new InvalidOperationException(
                "The logger serial should be the number printed on the Wi-Fi stick.");
        }

        var settings = new AppSettings
        {
            LoggerHost = HostBox.Text.Trim(),
            LoggerPort = Number(PortBox),
            Protocol = ProtocolBox.SelectedIndex == 1
                ? LoggerProtocol.SolarmanV5
                : LoggerProtocol.ModbusTcp,
            UnitId = Number(UnitIdBox),
            LoggerSerial = serial,
            InvertPowerSign = InvertToggle.IsChecked == true,
            RegisterAddressOffset = Number(OffsetBox),
            ReserveSocPercent = Number(ReserveBox),
            LowSocAlertPercent = Number(LowSocThresholdBox),
            PackCount = Number(PacksBox),
            PcBaselineWatts = Number(BaselineBox),
            PollIntervalSeconds = Math.Clamp(
                Number(IntervalBox),
                AppSettings.MinPollIntervalSeconds,
                AppSettings.MaxPollIntervalSeconds),
            StartWithWindows = StartupToggle.IsChecked == true,
            NotifyOnGridLoss = GridNotificationToggle.IsChecked == true,
            NotifyOnLowSoc = SocNotificationToggle.IsChecked == true,
            BackupPlayModeEnabled = BackupPlayToggle.IsChecked == true,
            WowForeverConfigPath = WowConfigPathBox.Text.Trim(),
            BackupDisplayHz = Number(BackupHzBox),
            GridDisplayHz = Number(GridHzBox),
            BackupGpuPowerEnabled = GpuPowerToggle.IsChecked == true,
            BackupGpuPowerWatts = Number(BackupGpuWattsBox),
            GridGpuPowerWatts = Number(GridGpuWattsBox)
        };

        if (settings.BackupPlayModeEnabled &&
            (string.IsNullOrWhiteSpace(settings.WowForeverConfigPath) ||
             !File.Exists(settings.WowForeverConfigPath)))
        {
            throw new InvalidOperationException(
                "Backup play mode needs a valid WoW Forever Config.wtf path.");
        }

        if (!string.IsNullOrWhiteSpace(settings.LoggerHost))
            DeyeClient.Validate(settings);
        return settings;
    }

    private void InstallGpuHelper_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            GpuPowerLimit.StartElevatedInstaller();
            ShowResult(
                InfoBarSeverity.Informational,
                "Approve the Windows prompt to install the GPU power helper. After that, outages can change the 4090 power limit without UAC.");
        }
        catch (Exception ex)
        {
            ShowResult(InfoBarSeverity.Error, ex.Message);
        }
    }

    private void ApplyBackupNow_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var message = _backupPlay.ApplyBackup(ReadSettings());
            ShowResult(InfoBarSeverity.Success, message);
        }
        catch (Exception ex)
        {
            ShowResult(InfoBarSeverity.Error, ex.Message);
        }
    }

    private void ApplyGridNow_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var message = _backupPlay.ApplyGrid(ReadSettings());
            ShowResult(InfoBarSeverity.Success, message);
        }
        catch (Exception ex)
        {
            ShowResult(InfoBarSeverity.Error, ex.Message);
        }
    }

    private static int Number(NumberBox box)
    {
        if (double.IsNaN(box.Value))
            throw new InvalidOperationException($"{box.Header} needs a number.");
        return (int)box.Value;
    }

    private void ProtocolBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        var solarman = ProtocolBox.SelectedIndex == 1;
        SerialBox.IsEnabled = solarman;
        if (_loading)
            return;

        if (solarman && PortBox.Value == 502)
            PortBox.Value = 8899;
        else if (!solarman && PortBox.Value == 8899)
            PortBox.Value = 502;
    }

    private async void Test_Click(object sender, RoutedEventArgs e)
    {
        TestButton.IsEnabled = false;
        ShowResult(InfoBarSeverity.Informational, "Reading the logger…");
        try
        {
            var settings = ReadSettings();
            DeyeClient.Validate(settings);
            var snapshot = await DeyeClient.ReadAsync(settings, CancellationToken.None);
            var estimate = RuntimeEstimator.Estimate(snapshot, settings);
            ShowResult(
                InfoBarSeverity.Success,
                $"Connected: SOC {snapshot.SocPercent}% · load {snapshot.LoadPowerWatts:N0} W · " +
                $"battery {snapshot.BatteryPowerWatts:N0} W · grid {(snapshot.GridConnected ? "on" : "off")} · " +
                $"{estimate.DurationText}. Compare these values with the inverter screen.");
        }
        catch (Exception ex)
        {
            ShowResult(InfoBarSeverity.Error, ex.Message);
        }
        finally
        {
            TestButton.IsEnabled = true;
        }
    }

    private void Find_Click(object sender, RoutedEventArgs e)
    {
        if (_finder is not null)
        {
            _finder.Activate();
            return;
        }

        _finder = new FinderWindow();
        _finder.EndpointSelected += EndpointSelected;
        _finder.Closed += (_, _) => _finder = null;
        _finder.Activate();
    }

    private void EndpointSelected(LoggerEndpoint endpoint)
    {
        HostBox.Text = endpoint.IpAddress;
        ProtocolBox.SelectedIndex = endpoint.Port == 8899 ? 1 : 0;
        PortBox.Value = endpoint.Port;
        if (endpoint.Serial is uint serial)
            SerialBox.Text = serial.ToString();
        _finder?.Close();
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var settings = ReadSettings();
            Saved?.Invoke(settings);
            Close();
        }
        catch (Exception ex)
        {
            ShowResult(InfoBarSeverity.Error, ex.Message);
        }
    }

    private void Cancel_Click(object sender, RoutedEventArgs e) => Close();

    private void ShowResult(InfoBarSeverity severity, string message)
    {
        ResultBar.Severity = severity;
        ResultBar.Message = message;
        ResultBar.IsOpen = true;
    }
}
