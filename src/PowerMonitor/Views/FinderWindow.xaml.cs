using System.Collections.ObjectModel;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using PowerMonitor.Core.Deye;

namespace PowerMonitor.Views;

internal sealed partial class FinderWindow : Window
{
    private readonly CancellationTokenSource _scan = new();
    private readonly ObservableCollection<LoggerEndpoint> _results = [];
    private bool _started;

    public event Action<LoggerEndpoint>? EndpointSelected;

    public FinderWindow()
    {
        InitializeComponent();
        SystemBackdrop = new MicaBackdrop();
        ExtendsContentIntoTitleBar = true;
        ResultsList.ItemsSource = _results;
        ResultsList.Loaded += async (_, _) => await ScanAsync();
        Closed += (_, _) =>
        {
            _scan.Cancel();
            _scan.Dispose();
        };

        WindowSizing.Fit(this, 540, 420);
    }

    private async Task ScanAsync()
    {
        if (_started)
            return;
        _started = true;

        try
        {
            var found = await LoggerFinder.FindAsync(_scan.Token);
            foreach (var endpoint in found)
                _results.Add(endpoint);

            StatusText.Text = found.Count == 0
                ? "Nothing answered. Enter the address from your router. If ports 502 and 8899 are closed, ask Deye to enable Modbus TCP on Link 2."
                : $"Found {found.Count} possible logger{(found.Count == 1 ? "" : "s")}.";
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            StatusText.Text = ex.Message;
        }
        finally
        {
            Progress.IsIndeterminate = false;
            Progress.Visibility = Visibility.Collapsed;
        }
    }

    private void ResultsList_SelectionChanged(object sender, SelectionChangedEventArgs e) =>
        UseButton.IsEnabled = ResultsList.SelectedItem is LoggerEndpoint;

    private void ResultsList_ItemClick(object sender, ItemClickEventArgs e)
    {
        if (e.ClickedItem is LoggerEndpoint endpoint)
            EndpointSelected?.Invoke(endpoint);
    }

    private void Use_Click(object sender, RoutedEventArgs e)
    {
        if (ResultsList.SelectedItem is LoggerEndpoint endpoint)
            EndpointSelected?.Invoke(endpoint);
    }

    private void Close_Click(object sender, RoutedEventArgs e) => Close();
}
