using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using PowerMonitor.Core.Presentation;
using Windows.Foundation;

namespace PowerMonitor.Controls;

public sealed partial class PowerHistoryChart : UserControl
{
    private PowerHistory? _history;

    public PowerHistoryChart()
    {
        InitializeComponent();
        SizeChanged += (_, _) => Redraw();
    }

    public string ScaleLabel { get; private set; } = "";

    public void Apply(PowerHistory history)
    {
        _history = history;
        Redraw();
    }

    private void Redraw()
    {
        var samples = _history?.Samples ?? [];
        var hasLine = samples.Count >= 2;
        EmptyText.Visibility = hasLine ? Visibility.Collapsed : Visibility.Visible;
        ScaleLabel = "";

        var width = ActualWidth;
        var height = ActualHeight;
        var zeroY = height / 2;
        ZeroLine.X1 = 0;
        ZeroLine.X2 = width;
        ZeroLine.Y1 = zeroY;
        ZeroLine.Y2 = zeroY;
        ChargeArea.Points = [];
        DischargeArea.Points = [];
        BatteryLine.Points = [];
        if (!hasLine || _history is null || width <= 1 || height <= 1)
            return;

        var scale = NiceScale(Math.Max(100, samples.Max(sample => Math.Abs(sample.BatteryWatts))));
        ScaleLabel = $"±{DashboardPresentation.FormatPower(scale)}";
        var range = zeroY - 2;
        var offset = _history.Capacity - samples.Count;
        double X(int index) => width * (offset + index) / (_history.Capacity - 1);
        double Y(double watts) => zeroY - Math.Clamp(watts / scale, -1, 1) * range;

        var charge = new PointCollection { new Point(X(0), zeroY) };
        var discharge = new PointCollection { new Point(X(0), zeroY) };
        var line = new PointCollection();
        for (var index = 0; index < samples.Count; index++)
        {
            var stored = -samples[index].BatteryWatts;
            var x = X(index);
            charge.Add(new Point(x, Y(Math.Max(0, stored))));
            discharge.Add(new Point(x, Y(Math.Min(0, stored))));
            line.Add(new Point(x, Y(stored)));
        }

        charge.Add(new Point(X(samples.Count - 1), zeroY));
        discharge.Add(new Point(X(samples.Count - 1), zeroY));
        ChargeArea.Points = charge;
        DischargeArea.Points = discharge;
        BatteryLine.Points = line;
    }

    private static int NiceScale(int watts)
    {
        var power = Math.Pow(10, Math.Floor(Math.Log10(watts)));
        var normalized = watts / power;
        var nice = normalized <= 1 ? 1 : normalized <= 2 ? 2 : normalized <= 5 ? 5 : 10;
        return (int)(nice * power);
    }
}
