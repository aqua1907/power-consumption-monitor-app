using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Animation;
using Microsoft.UI.Xaml.Shapes;
using Windows.UI;
using Windows.UI.ViewManagement;

namespace PowerMonitor.Controls;

internal sealed class FlowLine : Grid
{
    private const double DotSize = 6;
    private static readonly UISettings System = new();
    private static readonly Brush IdleBrush = new SolidColorBrush(Color.FromArgb(0x40, 0x80, 0x80, 0x80));
    private readonly Rectangle _track = new()
    {
        Height = 2,
        RadiusX = 1,
        RadiusY = 1,
        VerticalAlignment = VerticalAlignment.Center,
        Fill = IdleBrush
    };
    private readonly Ellipse[] _dots = new Ellipse[3];
    private readonly TranslateTransform[] _transforms = new TranslateTransform[3];
    private Storyboard? _storyboard;
    private int _direction;
    private double _watts;
    private bool _animationsEnabled;

    public FlowLine()
    {
        Height = DotSize;
        Children.Add(_track);
        var canvas = new Canvas();
        for (var index = 0; index < _dots.Length; index++)
        {
            _transforms[index] = new TranslateTransform();
            _dots[index] = new Ellipse
            {
                Width = DotSize,
                Height = DotSize,
                RenderTransform = _transforms[index],
                Visibility = Visibility.Collapsed
            };
            canvas.Children.Add(_dots[index]);
        }

        Children.Add(canvas);
        SizeChanged += (_, _) => Restart();
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

    /// <param name="direction">1 flows left to right, -1 right to left, 0 is idle.</param>
    public void SetFlow(int direction, double watts, Brush brush)
    {
        var changed = direction != _direction || Math.Abs(watts - _watts) > Math.Max(50, _watts * 0.2);
        _direction = Math.Sign(direction);
        _watts = watts;
        foreach (var dot in _dots)
            dot.Fill = brush;
        _track.Fill = _direction == 0 ? IdleBrush : brush;
        _track.Opacity = _direction == 0 ? 1 : 0.3;
        if (changed)
            Restart();
    }

    private void Restart()
    {
        Stop();
        var length = ActualWidth - DotSize;
        var active = _direction != 0 && length > 4;
        foreach (var dot in _dots)
            dot.Visibility = active ? Visibility.Visible : Visibility.Collapsed;
        if (!active)
            return;

        if (!_animationsEnabled || !System.AnimationsEnabled)
        {
            for (var index = 0; index < _transforms.Length; index++)
                _transforms[index].X = length * (index + 1) / (_transforms.Length + 1);
            return;
        }

        var seconds = Math.Clamp(2.4 - Math.Log10(Math.Max(10, _watts)) * 0.5, 0.6, 2.2);
        _storyboard = new Storyboard();
        for (var index = 0; index < _transforms.Length; index++)
        {
            var animation = new DoubleAnimation
            {
                From = _direction > 0 ? 0 : length,
                To = _direction > 0 ? length : 0,
                Duration = TimeSpan.FromSeconds(seconds),
                BeginTime = TimeSpan.FromSeconds(seconds * index / _transforms.Length),
                RepeatBehavior = RepeatBehavior.Forever
            };
            Storyboard.SetTarget(animation, _transforms[index]);
            Storyboard.SetTargetProperty(animation, "X");
            _storyboard.Children.Add(animation);
        }

        _storyboard.Begin();
    }

    private void Stop()
    {
        _storyboard?.Stop();
        _storyboard = null;
    }
}
