using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using PowerMonitor.Core.Deye;
using PowerMonitor.Core.Presentation;
using PowerMonitor.Core.Schedule;
using Windows.Storage.Pickers;

namespace PowerMonitor.Views;

internal sealed partial class HistoryWindow : Window
{
    private InverterEnergy? _energy;
    private DateTimeOffset? _readAt;

    public HistoryWindow(IReadOnlyList<OutageEvent> outages)
    {
        InitializeComponent();
        SystemBackdrop = new MicaBackdrop();
        ExtendsContentIntoTitleBar = true;

        WindowSizing.Fit(this, "history", 560, 520);
        _ = new WindowSizeTracker(this, "history");
        if (WindowSizing.AppWindowOf(this).Presenter is OverlappedPresenter presenter)
            presenter.IsResizable = true;

        ShowOutages(outages);
    }

    public void ShowOutages(IReadOnlyList<OutageEvent> outages)
    {
        var rows = outages.OrderByDescending(outage => outage.Start).Select(ToRow).ToList();
        OutagesList.ItemsSource = rows;
        OutagesList.Visibility = rows.Count == 0 ? Visibility.Collapsed : Visibility.Visible;
        OutagesEmptyText.Visibility = rows.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    /// <summary>Fills the schedule section, or hides it when <paramref name="comparison"/> is null (schedule off).</summary>
    public void ShowComparison(ScheduleComparison? comparison, string group, string addressLabel)
    {
        ScheduleSection.Visibility = comparison is null ? Visibility.Collapsed : Visibility.Visible;
        if (comparison is null)
            return;

        var where = string.IsNullOrWhiteSpace(addressLabel) ? "" : $"{addressLabel}, ";
        ScheduleHeaderText.Text =
            $"{where}group {group}. The operator publishes only today and tomorrow, so this compares the schedules " +
            "this app saved with the outages it recorded. Not watched means this PC was off for part of the scheduled time.";
        ScheduleSummaryText.Text = ComparisonText.Summary(comparison.Summary);
        var rows = comparison.Rows
            .Select(row => new ScheduleRow(
                row.Day.ToString("ddd, MMM d", System.Globalization.CultureInfo.CurrentCulture),
                ComparisonText.Scheduled(row),
                ComparisonText.Actual(row),
                ComparisonText.Result(row)))
            .ToList();
        ScheduleList.ItemsSource = rows;
        ScheduleList.Visibility = rows.Count == 0 ? Visibility.Collapsed : Visibility.Visible;
        ScheduleEmptyText.Visibility = rows.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    public void ShowEnergy(InverterEnergy? energy, DateTimeOffset? readAt)
    {
        _energy = energy;
        _readAt = readAt;
        var missing = energy is null;
        EnergyEmptyText.Visibility = missing ? Visibility.Visible : Visibility.Collapsed;
        EnergyGrid.Visibility = missing ? Visibility.Collapsed : Visibility.Visible;
        if (energy is null)
            return;

        ChargeTodayText.Text = Format(energy.TodayBatteryChargeKwh);
        ChargeTotalText.Text = Format(energy.TotalBatteryChargeKwh);
        DischargeTodayText.Text = Format(energy.TodayBatteryDischargeKwh);
        DischargeTotalText.Text = Format(energy.TotalBatteryDischargeKwh);
        LoadTodayText.Text = Format(energy.TodayLoadKwh);
        LoadTotalText.Text = Format(energy.TotalLoadKwh);
        GridBoughtText.Text = Format(energy.TodayGridImportKwh);
        GridSoldText.Text = Format(energy.TodayGridExportKwh);
        EnergyReadText.Text = readAt is { } at ? $"Read {at.LocalDateTime:HH:mm:ss}" : "";
    }

    private static string Format(decimal kwh) => $"{kwh:0.0} kWh";

    private static OutageRow ToRow(OutageEvent outage)
    {
        var now = DateTimeOffset.Now;
        var started = When(outage.Start, outage.StartKnown);
        var ended = outage.End is { } end ? When(end, outage.EndKnown) : "Ongoing";
        return new OutageRow(started, ended, outage.DurationLabel(now));
    }

    private static string When(DateTimeOffset at, bool known)
    {
        var text = at.ToLocalTime().ToString("MMM d, HH:mm");
        return known ? text : $"Before {text}";
    }

    private async void Export_Click(object sender, RoutedEventArgs e)
    {
        ExportButton.IsEnabled = false;
        try
        {
            var picker = new FileSavePicker
            {
                SuggestedStartLocation = PickerLocationId.DocumentsLibrary,
                SuggestedFileName = InverterEnergyCsv.SuggestedFileName()
            };
            picker.FileTypeChoices.Add("CSV", new List<string> { ".csv" });

            var windowHandle = WinRT.Interop.WindowNative.GetWindowHandle(this);
            WinRT.Interop.InitializeWithWindow.Initialize(picker, windowHandle);

            var file = await picker.PickSaveFileAsync();
            if (file is null)
                return;

            InverterEnergyCsv.Write(_energy, _readAt, file.Path);
            ExportResultText.Text = _energy is null
                ? $"No inverter history yet. Wrote an empty file to {file.Path}"
                : $"Saved the inverter counters to {file.Path}";
        }
        catch (Exception ex)
        {
            ExportResultText.Text = ex.Message;
        }
        finally
        {
            ExportButton.IsEnabled = true;
        }
    }

    private void Close_Click(object sender, RoutedEventArgs e) => Close();

    private sealed record OutageRow(string Started, string Ended, string Duration);

    private sealed record ScheduleRow(string Day, string Scheduled, string Actual, string Result);
}
