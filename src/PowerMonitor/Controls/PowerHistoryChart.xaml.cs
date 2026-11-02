using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using PowerMonitor.Core.Presentation;
using Windows.Foundation;

namespace PowerMonitor.Controls;

public sealed partial class PowerHistoryChart : UserControl
{
    private static readonly (TimeSpan Span, string Label)[] Ranges =
    [
        (TimeSpan.FromMinutes(30), "30m"),
        (TimeSpan.FromHours(1), "1h"),
        (TimeSpan.FromHours(4), "4h"),
        (TimeSpan.FromHours(12), "12h"),
        (TimeSpan.FromHours(24), "24h"),
    ];

    private PowerHistory? _history;
    private int _selectedRangeIndex;
    private List<(double X, double Y, double Watts, DateTimeOffset At)>? _plotPoints;

    public PowerHistoryChart()
    {
        InitializeComponent();
        BuildRangeButtons();
        Plot.SizeChanged += (_, _) => Redraw();
    }

    public void Apply(PowerHistory history)
    {
        _history = history;
        Redraw();
    }

    private void BuildRangeButtons()
    {
        var style = (Style)Application.Current.Resources["RangePillStyle"];

        for (var index = 0; index < Ranges.Length; index++)
        {
            var button = new ToggleButton
            {
                Content = Ranges[index].Label,
                Style = style,
                IsChecked = index == _selectedRangeIndex,
                Tag = index,
            };
            button.Click += RangeButton_Click;
            RangeRow.Children.Add(button);
        }
    }

    private void RangeButton_Click(object sender, RoutedEventArgs e)
    {
        var clicked = (ToggleButton)sender;
        var index = (int)clicked.Tag;

        for (var i = 0; i < RangeRow.Children.Count; i++)
            ((ToggleButton)RangeRow.Children[i]).IsChecked = i == index;

        if (_selectedRangeIndex == index)
            return;

        _selectedRangeIndex = index;
        Redraw();
    }

    private void Redraw()
    {
        HideCrosshair();
        _plotPoints = null;

        var range = Ranges[_selectedRangeIndex].Span;
        var now = DateTimeOffset.Now;
        var windowStart = now - range;
        var samples = _history?.Window(now, range) ?? [];
        var hasLine = samples.Count >= 2;

        EmptyText.Visibility = hasLine ? Visibility.Collapsed : Visibility.Visible;
        StartTimeLabel.Text = windowStart.ToLocalTime().ToString("HH:mm");
        EndTimeLabel.Text = "Now";

        var width = Plot.ActualWidth;
        var height = Plot.ActualHeight;
        var zeroY = height / 2;
        ZeroLine.X1 = 0;
        ZeroLine.X2 = width;
        ZeroLine.Y1 = zeroY;
        ZeroLine.Y2 = zeroY;
        ChargeArea.Points = [];
        DischargeArea.Points = [];
        BatteryLine.Points = [];

        if (!hasLine || width <= 1 || height <= 1)
        {
            MaxLabel.Text = "";
            MinLabel.Text = "";
            return;
        }

        var points = Downsample(samples, windowStart, now, Math.Max(2, (int)width));
        var scale = NiceScale(Math.Max(100, points.Max(point => Math.Abs(point.Watts))));
        MaxLabel.Text = $"+{DashboardPresentation.FormatPower(scale)}";
        MinLabel.Text = $"-{DashboardPresentation.FormatPower(scale)}";

        var plotRange = zeroY - 2;
        double X(DateTimeOffset at) => width * (at - windowStart).TotalSeconds / range.TotalSeconds;
        double Y(double watts) => zeroY - Math.Clamp(watts / scale, -1, 1) * plotRange;

        var charge = new PointCollection { new Point(X(points[0].At), zeroY) };
        var discharge = new PointCollection { new Point(X(points[0].At), zeroY) };
        var line = new PointCollection();
        var plotPoints = new List<(double, double, double, DateTimeOffset)>(points.Count);
        foreach (var point in points)
        {
            var stored = -point.Watts;
            var x = X(point.At);
            var y = Y(stored);
            charge.Add(new Point(x, Y(Math.Max(0, stored))));
            discharge.Add(new Point(x, Y(Math.Min(0, stored))));
            line.Add(new Point(x, y));
            plotPoints.Add((x, y, point.Watts, point.At));
        }

        charge.Add(new Point(X(points[^1].At), zeroY));
        discharge.Add(new Point(X(points[^1].At), zeroY));
        ChargeArea.Points = charge;
        DischargeArea.Points = discharge;
        BatteryLine.Points = line;
        _plotPoints = plotPoints;
    }

    private void PlotArea_PointerMoved(object sender, PointerRoutedEventArgs e)
    {
        if (_plotPoints is not { Count: > 0 } points)
            return;

        var x = e.GetCurrentPoint(Plot).Position.X;
        var nearest = points[0];
        var bestDistance = double.MaxValue;
        foreach (var point in points)
        {
            var distance = Math.Abs(point.X - x);
            if (distance >= bestDistance)
                continue;
            bestDistance = distance;
            nearest = point;
        }

        ShowCrosshair(nearest);
    }

    private void PlotArea_PointerExited(object sender, PointerRoutedEventArgs e) => HideCrosshair();

    private void ShowCrosshair((double X, double Y, double Watts, DateTimeOffset At) point)
    {
        var height = Plot.ActualHeight;
        CrosshairLine.X1 = point.X;
        CrosshairLine.X2 = point.X;
        CrosshairLine.Y1 = 0;
        CrosshairLine.Y2 = height;
        CrosshairLine.Visibility = Visibility.Visible;

        Canvas.SetLeft(CrosshairDot, point.X - CrosshairDot.Width / 2);
        Canvas.SetTop(CrosshairDot, point.Y - CrosshairDot.Height / 2);
        CrosshairDot.Visibility = Visibility.Visible;

        TooltipText.Text = CrosshairLabel(point.At, point.Watts);
        TooltipBorder.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        var tooltipWidth = TooltipBorder.DesiredSize.Width;
        var left = Math.Clamp(point.X + 8, 0, Math.Max(0, Plot.ActualWidth - tooltipWidth));
        Canvas.SetLeft(TooltipBorder, left);
        Canvas.SetTop(TooltipBorder, 2);
        TooltipBorder.Visibility = Visibility.Visible;
    }

    private void HideCrosshair()
    {
        CrosshairLine.Visibility = Visibility.Collapsed;
        CrosshairDot.Visibility = Visibility.Collapsed;
        TooltipBorder.Visibility = Visibility.Collapsed;
    }

    private static string CrosshairLabel(DateTimeOffset at, double watts)
    {
        var flow = watts switch
        {
            <= -50 => $"Charging · {DashboardPresentation.FormatPower(watts)}",
            >= 50 => $"Discharging · {DashboardPresentation.FormatPower(watts)}",
            _ => "Idle"
        };
        return $"{at.ToLocalTime():HH:mm:ss} · {flow}";
    }

    /// <summary>
    /// Averages battery watts into at most <paramref name="buckets"/> evenly time-spaced points, so
    /// wide ranges (e.g. 24 h) don't push thousands of samples through XAML rendering every tick.
    /// </summary>
    private static List<(DateTimeOffset At, double Watts)> Downsample(
        IReadOnlyList<PowerSample> samples,
        DateTimeOffset start,
        DateTimeOffset end,
        int buckets)
    {
        if (samples.Count <= buckets)
            return samples.Select(sample => (sample.At, (double)sample.BatteryWatts)).ToList();

        var result = new List<(DateTimeOffset, double)>(buckets);
        var totalSeconds = (end - start).TotalSeconds;
        var sampleIndex = 0;
        for (var bucket = 0; bucket < buckets; bucket++)
        {
            var bucketStart = start.AddSeconds(totalSeconds * bucket / buckets);
            var bucketEnd = start.AddSeconds(totalSeconds * (bucket + 1) / buckets);
            double sum = 0;
            var count = 0;
            while (sampleIndex < samples.Count && samples[sampleIndex].At < bucketEnd)
            {
                sum += samples[sampleIndex].BatteryWatts;
                count++;
                sampleIndex++;
            }

            if (count > 0)
                result.Add((bucketStart, sum / count));
        }

        return result;
    }

    private static int NiceScale(double watts)
    {
        var power = Math.Pow(10, Math.Floor(Math.Log10(watts)));
        var normalized = watts / power;
        var nice = normalized <= 1 ? 1 : normalized <= 2 ? 2 : normalized <= 5 ? 5 : 10;
        return (int)(nice * power);
    }
}
