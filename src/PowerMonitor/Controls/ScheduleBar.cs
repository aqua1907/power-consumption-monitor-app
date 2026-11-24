using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using PowerMonitor.Core.Schedule;

namespace PowerMonitor.Controls;

/// <summary>A day as a 24-hour bar: outages as blocks, a marker for now, and hour ticks underneath.</summary>
internal sealed class ScheduleBar : Grid
{
    private const double BarHeight = 14;
    private static readonly string[] Ticks = ["0", "6", "12", "18"];
    private readonly Grid _track = new() { Height = BarHeight, CornerRadius = new CornerRadius(4) };
    private ScheduleDayView? _day;

    public ScheduleBar()
    {
        RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        _track.Background = Theme("SubtleFillColorSecondaryBrush");
        Children.Add(_track);

        var labels = new Grid { Margin = new Thickness(0, 2, 0, 0) };
        SetRow(labels, 1);
        for (var i = 0; i < Ticks.Length; i++)
        {
            labels.ColumnDefinitions.Add(new ColumnDefinition());
            var text = Label(Ticks[i], HorizontalAlignment.Left);
            SetColumn(text, i);
            labels.Children.Add(text);
        }

        var end = Label("24", HorizontalAlignment.Right);
        SetColumn(end, Ticks.Length - 1);
        labels.Children.Add(end);
        Children.Add(labels);

        SizeChanged += (_, _) => Draw();
    }

    public void Apply(ScheduleDayView day)
    {
        _day = day;
        Draw();
    }

    private void Draw()
    {
        _track.Children.Clear();
        var width = ActualWidth;
        if (_day is not { } day || width <= 0)
            return;

        var caution = Theme("SystemFillColorCautionBrush");
        var critical = Theme("SystemFillColorCriticalBrush");
        foreach (var segment in day.Segments)
        {
            var left = width * segment.StartFraction;
            var block = new Border
            {
                HorizontalAlignment = HorizontalAlignment.Left,
                Margin = new Thickness(left, 0, 0, 0),
                Width = Math.Max(2, width * (segment.EndFraction - segment.StartFraction)),
                Background = segment.IsCurrent ? critical : caution,
                Opacity = segment.Possible ? 0.35 : 1
            };
            _track.Children.Add(block);
        }

        if (day.NowFraction is { } now)
        {
            _track.Children.Add(new Border
            {
                HorizontalAlignment = HorizontalAlignment.Left,
                Margin = new Thickness(Math.Clamp(width * now - 1, 0, Math.Max(0, width - 2)), 0, 0, 0),
                Width = 2,
                Background = Theme("TextFillColorPrimaryBrush")
            });
        }
    }

    private static TextBlock Label(string text, HorizontalAlignment alignment) => new()
    {
        Text = text,
        HorizontalAlignment = alignment,
        Style = (Style)Application.Current.Resources["TertiaryCaptionStyle"]
    };

    private static Brush Theme(string key) => (Brush)Application.Current.Resources[key];
}
