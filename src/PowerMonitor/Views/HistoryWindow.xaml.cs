using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using PowerMonitor.Core.Presentation;
using Windows.Storage.Pickers;

namespace PowerMonitor.Views;

internal sealed partial class HistoryWindow : Window
{
    private readonly Func<IReadOnlyList<PowerSample>> _historyProvider;

    public HistoryWindow(IReadOnlyList<OutageEvent> outages, Func<IReadOnlyList<PowerSample>> historyProvider)
    {
        InitializeComponent();
        SystemBackdrop = new MicaBackdrop();
        ExtendsContentIntoTitleBar = true;
        _historyProvider = historyProvider;

        WindowSizing.Fit(this, "history", 560, 520);
        _ = new WindowSizeTracker(this, "history");
        if (WindowSizing.AppWindowOf(this).Presenter is OverlappedPresenter presenter)
            presenter.IsResizable = true;

        var rows = outages.OrderByDescending(outage => outage.Start).Select(ToRow).ToList();
        OutagesList.ItemsSource = rows;
        OutagesList.Visibility = rows.Count == 0 ? Visibility.Collapsed : Visibility.Visible;
        OutagesEmptyText.Visibility = rows.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    private static OutageRow ToRow(OutageEvent outage)
    {
        var now = DateTimeOffset.Now;
        var started = outage.Start.ToLocalTime().ToString("MMM d, HH:mm");
        var ended = outage.End is { } end ? end.ToLocalTime().ToString("MMM d, HH:mm") : "Ongoing";
        return new OutageRow(started, ended, FormatDuration(outage.Duration(now)));
    }

    private static string FormatDuration(TimeSpan span) =>
        span.TotalHours >= 1 ? $"{(int)span.TotalHours} h {span.Minutes} m" : $"{Math.Max(1, (int)span.TotalMinutes)} m";

    private async void Export_Click(object sender, RoutedEventArgs e)
    {
        ExportButton.IsEnabled = false;
        try
        {
            var picker = new FileSavePicker
            {
                SuggestedStartLocation = PickerLocationId.DocumentsLibrary,
                SuggestedFileName = PowerHistoryCsv.SuggestedFileName()
            };
            picker.FileTypeChoices.Add("CSV", new List<string> { ".csv" });

            var windowHandle = WinRT.Interop.WindowNative.GetWindowHandle(this);
            WinRT.Interop.InitializeWithWindow.Initialize(picker, windowHandle);

            var file = await picker.PickSaveFileAsync();
            if (file is null)
                return;

            var samples = _historyProvider();
            PowerHistoryCsv.Write(samples, file.Path);
            ExportResultText.Text = samples.Count == 0
                ? $"No history yet. Wrote an empty file to {file.Path}"
                : $"Saved {samples.Count} readings to {file.Path}";
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
}
