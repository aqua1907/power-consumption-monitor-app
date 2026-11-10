using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using PowerMonitor.Core.Deye;
using PowerMonitor.Core.Presentation;
using PowerMonitor.Core.Settings;
using PowerMonitor.Monitoring;
using Windows.Graphics;
using Windows.System;

namespace PowerMonitor.Views;

internal sealed partial class StatusWindow : Window
{
    private const double MinimumWidth = 320;
    private const double MinimumHeight = 300;
    private const double EdgeMargin = 12;
    private static readonly TimeSpan ReopenGuard = TimeSpan.FromMilliseconds(400);
    private static readonly TimeSpan HistorySaveThrottle = TimeSpan.FromSeconds(20);
    private readonly PowerHistory _history = new();
    private const string WindowKey = "status";
    private readonly AppWindow _appWindow;
    private readonly WindowSizeTracker _sizeTracker;
    private DateTimeOffset? _lastHistorySample;
    private DateTimeOffset _lastHistorySave = DateTimeOffset.MinValue;
    private DateTime _hiddenAt = DateTime.MinValue;
    private bool _placed;
    private bool _correctingSize;

    public event EventHandler? SettingsRequested;
    public event EventHandler? HistoryRequested;

    public StatusWindow()
    {
        InitializeComponent();
        SystemBackdrop = new DesktopAcrylicBackdrop();
        ExtendsContentIntoTitleBar = true;
        SetTitleBar(DragArea);

        _appWindow = WindowSizing.AppWindowOf(this);
        _sizeTracker = new WindowSizeTracker(this, WindowKey, MinimumWidth, MinimumHeight);
        _appWindow.IsShownInSwitchers = false;
        if (_appWindow.Presenter is OverlappedPresenter presenter)
        {
            presenter.IsMaximizable = false;
            presenter.IsMinimizable = false;
            presenter.IsResizable = true;
            presenter.SetBorderAndTitleBar(true, false);
        }

        _appWindow.Changed += AppWindow_Changed;
        Activated += Window_Activated;
    }

    public AppWindow NativeAppWindow => _appWindow;

    /// <summary>Seeds the chart with history saved from a previous run.</summary>
    public void LoadHistory(IReadOnlyList<PowerSample> samples)
    {
        if (samples.Count == 0)
            return;

        try
        {
            foreach (var sample in samples)
                _history.Add(sample);
            _lastHistorySample = samples[^1].At;
            HistoryChart.Apply(_history);
        }
        catch (ArgumentException)
        {
            // Corrupt or out-of-order backup; start fresh rather than fail to launch.
            _history.Clear();
            _lastHistorySample = null;
        }
    }

    /// <summary>A snapshot of the retained history, safe to use off the UI thread (e.g. for CSV export).</summary>
    public IReadOnlyList<PowerSample> HistorySnapshot() => _history.Samples.ToArray();

    /// <summary>Forces an immediate save; used before the app exits so the last few readings aren't lost.</summary>
    public void PersistHistory() => PowerHistoryStore.Save(_history.Samples.ToArray());

    public void Apply(MonitorUpdate update, AppSettings settings)
    {
        var state = DashboardPresentation.Create(update.Inverter, update.Backup, update.Error, update.Charge);
        SummaryText.Text = state.Summary;
        StatusDot.Fill = Theme(StatusKey(state, settings.LowSocAlertPercent));
        SocText.Text = state.HasData ? $"{state.SocPercent}%" : "--%";
        var charging = state.ChargeTime.Length > 0;
        RuntimeLabel.Text = charging ? "Full in" : "Backup time";
        RuntimeText.Text = charging ? state.ChargeTime : state.Runtime;
        ToolTipService.SetToolTip(RuntimePanel, charging ? state.ChargeHint : state.RuntimeHint);
        GridVoltageText.Text = state.GridVoltage;
        GridCurrentText.Text = state.GridCurrent;
        BatteryVoltageText.Text = state.BatteryVoltage;
        BatteryCurrentText.Text = state.BatteryCurrent;
        FlowText.Text = state.FlowSummary;
        EnergyText.Text = state.UsableEnergy;

        var batteryBrush = Theme(state.Flow == BatteryFlow.Discharging
            ? "SystemFillColorCautionBrush"
            : state.SocPercent <= settings.LowSocAlertPercent && state.HasData
                ? "SystemFillColorCriticalBrush"
                : "SystemFillColorSuccessBrush");
        Battery.Apply(state.HasData ? state.SocPercent : 0, settings.ReserveSocPercent, state.Flow, batteryBrush);

        GridValue.Text = !state.HasData ? "—"
            : state.GridConnected ? DashboardPresentation.FormatPower(state.GridPowerWatts)
            : "Off";
        GridNode.Opacity = state.HasData && !state.GridConnected ? 0.5 : 1;
        HomeValue.Text = state.HasData ? DashboardPresentation.FormatPower(state.LoadPowerWatts) : "—";
        BatteryValue.Text = state.HasData ? DashboardPresentation.FormatPower(state.BatteryPowerWatts) : "—";

        var gridDirection = !state.HasData || !state.GridConnected ? 0
            : state.GridPowerWatts > 20 ? 1
            : state.GridPowerWatts < -20 ? -1
            : 0;
        GridLine.SetFlow(gridDirection, Math.Abs(state.GridPowerWatts), Theme("SystemFillColorAttentionBrush"));
        var batteryDirection = !state.HasData ? 0 : state.Flow switch
        {
            BatteryFlow.Charging => 1,
            BatteryFlow.Discharging => -1,
            _ => 0
        };
        BatteryLine.SetFlow(batteryDirection, Math.Abs(state.BatteryPowerWatts), batteryBrush);

        PcText.Text = $"This PC ~{update.Pc.EstimatedWatts:0} W";
        ToolTipService.SetToolTip(PcText, update.Pc.Detail);
        UpdatedText.Text = state.HasData ? $"Updated {state.Updated}" : "";

        if (update.Inverter is { } inverter && inverter.ReadAt != _lastHistorySample)
        {
            if (_lastHistorySample is { } previous && inverter.ReadAt < previous)
                _history.Clear();
            _history.Add(new PowerSample(inverter.ReadAt, inverter.BatteryPowerWatts, inverter.LoadPowerWatts));
            _lastHistorySample = inverter.ReadAt;
            HistoryChart.Apply(_history);

            var now = DateTimeOffset.UtcNow;
            if (now - _lastHistorySave >= HistorySaveThrottle)
            {
                _lastHistorySave = now;
                var snapshot = _history.Samples.ToArray();
                _ = Task.Run(() => PowerHistoryStore.Save(snapshot));
            }
        }

        ErrorBar.Message = state.Error ?? "";
        ErrorBar.IsOpen = state.Error is not null;
    }

    public void ShowDashboard()
    {
        if (!_placed || PinButton.IsChecked != true)
        {
            PlaceNearTray();
            _placed = true;
        }

        _appWindow.Show();
        Activate();
        SetAnimations(true);
    }

    public void ToggleDashboard()
    {
        if (_appWindow.IsVisible)
        {
            HideDashboard();
            return;
        }

        if (DateTime.UtcNow - _hiddenAt > ReopenGuard)
            ShowDashboard();
    }

    public void HideDashboard()
    {
        if (!_appWindow.IsVisible)
            return;

        SetAnimations(false);
        _appWindow.Hide();
        _hiddenAt = DateTime.UtcNow;
    }

    private void SetAnimations(bool enabled)
    {
        Battery.AnimationsEnabled = enabled;
        GridLine.AnimationsEnabled = enabled;
        BatteryLine.AnimationsEnabled = enabled;
    }

    private void PlaceNearTray()
    {
        _correctingSize = true;
        _sizeTracker.Suppressed = true;
        WindowSizing.PlaceFlyout(this, WindowKey, EdgeMargin);
        _sizeTracker.Suppressed = false;
        _correctingSize = false;
    }

    private static Brush Theme(string key) => (Brush)Application.Current.Resources[key];

    private static string StatusKey(DashboardState state, int lowSocAlertPercent)
    {
        if (!state.HasData)
            return state.Error is null ? "SystemFillColorNeutralBrush" : "SystemFillColorCriticalBrush";
        if (state.SocPercent <= lowSocAlertPercent)
            return "SystemFillColorCriticalBrush";
        return state.GridConnected ? "SystemFillColorSuccessBrush" : "SystemFillColorCautionBrush";
    }

    private void Window_Activated(object sender, WindowActivatedEventArgs args)
    {
        if (args.WindowActivationState == WindowActivationState.Deactivated && PinButton.IsChecked != true)
            HideDashboard();
    }

    private void Root_KeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key != VirtualKey.Escape)
            return;

        HideDashboard();
        e.Handled = true;
    }

    private void Settings_Click(object sender, RoutedEventArgs e) =>
        SettingsRequested?.Invoke(this, EventArgs.Empty);

    private void History_Click(object sender, RoutedEventArgs e) =>
        HistoryRequested?.Invoke(this, EventArgs.Empty);

    private void AppWindow_Changed(AppWindow sender, AppWindowChangedEventArgs args)
    {
        if (!args.DidSizeChange || _correctingSize)
            return;

        var size = sender.Size;
        var width = Math.Max(WindowSizing.ToPixels(this, MinimumWidth), size.Width);
        var height = Math.Max(WindowSizing.ToPixels(this, MinimumHeight), size.Height);
        if (width == size.Width && height == size.Height)
            return;

        _correctingSize = true;
        sender.Resize(new SizeInt32(width, height));
        _correctingSize = false;
    }
}
