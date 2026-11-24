using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using PowerMonitor.Core.Schedule;

namespace PowerMonitor.Controls;

/// <summary>Today's and tomorrow's outages for the saved group, as day cards with a 24-hour bar and a list of times.</summary>
public sealed partial class ScheduleView : UserControl
{
    public ScheduleView()
    {
        InitializeComponent();
    }

    public event EventHandler? RefreshRequested;

    public void Apply(SchedulePresentation presentation)
    {
        HeaderText.Text = presentation.Header;
        UpdatedText.Text = presentation.Updated;
        UpdatedText.Visibility = presentation.Updated.Length == 0 ? Visibility.Collapsed : Visibility.Visible;
        NoticeBar.Message = presentation.Notice ?? "";
        NoticeBar.IsOpen = presentation.Notice is not null;

        DaysPanel.Children.Clear();
        foreach (var day in presentation.Days)
            DaysPanel.Children.Add(Card(day));
    }

    private void Refresh_Click(object sender, RoutedEventArgs e) =>
        RefreshRequested?.Invoke(this, EventArgs.Empty);

    private static Border Card(ScheduleDayView day)
    {
        var content = new StackPanel { Spacing = 6 };
        var title = new Grid { ColumnSpacing = 8 };
        title.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        title.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        title.Children.Add(Text(day.Title, "BodyStrongTextBlockStyle"));
        var status = Text(day.Status, "SecondaryCaptionStyle");
        status.HorizontalAlignment = HorizontalAlignment.Right;
        status.VerticalAlignment = VerticalAlignment.Center;
        status.TextWrapping = TextWrapping.Wrap;
        Grid.SetColumn(status, 1);
        title.Children.Add(status);
        content.Children.Add(title);

        var bar = new ScheduleBar();
        bar.Apply(day);
        content.Children.Add(bar);

        foreach (var segment in day.Segments)
            content.Children.Add(Row(segment));

        if (day.Total.Length > 0)
            content.Children.Add(Text(day.Total, "SecondaryCaptionStyle"));

        return new Border
        {
            Style = (Style)Application.Current.Resources["CardStyle"],
            Child = content
        };
    }

    private static Border Row(ScheduleSegment segment)
    {
        var grid = new Grid { ColumnSpacing = 8 };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        grid.Children.Add(Text(segment.Range, segment.IsCurrent ? "BodyStrongTextBlockStyle" : "BodyTextBlockStyle"));
        var duration = Text(segment.Duration, "SecondaryCaptionStyle");
        duration.VerticalAlignment = VerticalAlignment.Center;
        Grid.SetColumn(duration, 1);
        grid.Children.Add(duration);
        if (segment.Note is { } note)
        {
            var text = Text(note, segment.IsCurrent ? "BodyStrongTextBlockStyle" : "SecondaryCaptionStyle");
            text.HorizontalAlignment = HorizontalAlignment.Right;
            text.VerticalAlignment = VerticalAlignment.Center;
            Grid.SetColumn(text, 2);
            grid.Children.Add(text);
        }

        return new Border
        {
            Padding = new Thickness(6, 2, 6, 2),
            CornerRadius = new CornerRadius(4),
            Opacity = segment.Possible ? 0.7 : 1,
            Background = segment.IsCurrent
                ? (Brush)Application.Current.Resources["SubtleFillColorSecondaryBrush"]
                : null,
            Child = grid
        };
    }

    private static TextBlock Text(string text, string styleKey) => new()
    {
        Text = text,
        Style = (Style)Application.Current.Resources[styleKey]
    };
}
