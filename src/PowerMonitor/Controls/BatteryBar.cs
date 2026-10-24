using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Animation;
using Microsoft.UI.Xaml.Shapes;
using PowerMonitor.Core.Deye;
using Windows.Foundation;
using Windows.UI;
using Windows.UI.ViewManagement;

namespace PowerMonitor.Controls;

internal sealed class BatteryBar : Grid
{
    private const double ShimmerWidth = 56;
    private static readonly UISettings System = new();
    private readonly Border _fill = new() { HorizontalAlignment = HorizontalAlignment.Left, CornerRadius = new CornerRadius(5) };
    private readonly Grid _fillContent = new();
    private readonly TranslateTransform _shimmerTransform = new();
    private readonly Rectangle _shimmer;
    private readonly Rectangle _reserve = new()
    {
        Width = 2,
        HorizontalAlignment = HorizontalAlignment.Left,
        Fill = new SolidColorBrush(Color.FromArgb(0xCC, 0xFF, 0xFF, 0xFF))
    };
    private Storyboard? _storyboard;
    private double _soc;
    private double _reservePercent;
    private BatteryFlow _flow = BatteryFlow.Idle;
    private bool _animationsEnabled;

    public BatteryBar()
    {
        Height = 10;
        CornerRadius = new CornerRadius(5);
        Background = new SolidColorBrush(Color.FromArgb(0x33, 0x80, 0x80, 0x80));

        var gradient = new LinearGradientBrush { StartPoint = new Point(0, 0.5), EndPoint = new Point(1, 0.5) };
        gradient.GradientStops.Add(new GradientStop { Color = Color.FromArgb(0, 255, 255, 255), Offset = 0 });
        gradient.GradientStops.Add(new GradientStop { Color = Color.FromArgb(0x70, 255, 255, 255), Offset = 0.5 });
        gradient.GradientStops.Add(new GradientStop { Color = Color.FromArgb(0, 255, 255, 255), Offset = 1 });
        _shimmer = new Rectangle
        {
            Width = ShimmerWidth,
            HorizontalAlignment = HorizontalAlignment.Left,
            Fill = gradient,
            RenderTransform = _shimmerTransform,
            Visibility = Visibility.Collapsed
        };

        _fillContent.Children.Add(_shimmer);
        _fill.Child = _fillContent;
        Children.Add(_fill);
        Children.Add(_reserve);
        SizeChanged += (_, _) => Layout();
        Unloaded += (_, _) => Stop();
    }

    public bool AnimationsEnabled
    {
        set
        {
            _animationsEnabled = value;
            Restart();
        }
    }

    public void Apply(int socPercent, int reservePercent, BatteryFlow flow, Brush fill)
    {
        _soc = Math.Clamp(socPercent, 0, 100);
        _reservePercent = Math.Clamp(reservePercent, 0, 100);
        _fill.Background = fill;
        var flowChanged = flow != _flow;
        _flow = flow;
        Layout(forceRestart: flowChanged);
    }

    private double FillWidth => ActualWidth * _soc / 100;

    private void Layout(bool forceRestart = false)
    {
        var widthChanged = Math.Abs(_fill.Width - FillWidth) > 0.5 || double.IsNaN(_fill.Width);
        _fill.Width = FillWidth;
        _fillContent.Clip = new RectangleGeometry { Rect = new Rect(0, 0, FillWidth, Height) };
        _reserve.Margin = new Thickness(Math.Max(0, ActualWidth * _reservePercent / 100 - 1), 0, 0, 0);
        _reserve.Visibility = _reservePercent > 0 ? Visibility.Visible : Visibility.Collapsed;
        if (widthChanged || forceRestart)
            Restart();
    }

    private void Restart()
    {
        Stop();
        var moving = _flow != BatteryFlow.Idle && FillWidth > ShimmerWidth / 2;
        _shimmer.Visibility = moving ? Visibility.Visible : Visibility.Collapsed;
        if (!moving || !_animationsEnabled || !System.AnimationsEnabled)
        {
            _shimmerTransform.X = -ShimmerWidth;
            return;
        }

        var start = -ShimmerWidth;
        var end = FillWidth;
        var animation = new DoubleAnimation
        {
            From = _flow == BatteryFlow.Charging ? start : end,
            To = _flow == BatteryFlow.Charging ? end : start,
            Duration = TimeSpan.FromSeconds(2.4),
            RepeatBehavior = RepeatBehavior.Forever
        };
        Storyboard.SetTarget(animation, _shimmerTransform);
        Storyboard.SetTargetProperty(animation, "X");
        _storyboard = new Storyboard();
        _storyboard.Children.Add(animation);
        _storyboard.Begin();
    }

    private void Stop()
    {
        _storyboard?.Stop();
        _storyboard = null;
    }
}
