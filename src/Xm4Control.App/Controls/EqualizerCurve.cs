using System.Globalization;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using Xm4Control.Core;
using Xm4Control.Core.Protocol;

namespace Xm4Control.App.Controls;

/// <summary>
/// Égaliseur dessiné en courbe. Chaque point se règle en le faisant glisser ou à la molette ;
/// la valeur n'est envoyée (liaison <see cref="Values"/>) qu'au relâchement.
/// </summary>
internal sealed class EqualizerCurve : FrameworkElement
{
    public static readonly DependencyProperty ValuesProperty = DependencyProperty.Register(
        nameof(Values), typeof(IReadOnlyList<int>), typeof(EqualizerCurve),
        new FrameworkPropertyMetadata(null,
            FrameworkPropertyMetadataOptions.BindsTwoWayByDefault | FrameworkPropertyMetadataOptions.AffectsRender,
            (d, _) => ((EqualizerCurve)d).OnValuesChanged()));

    public static readonly DependencyProperty AccentBrushProperty = BrushProperty(nameof(AccentBrush));
    public static readonly DependencyProperty LineBrushProperty = BrushProperty(nameof(LineBrush));
    public static readonly DependencyProperty TextBrushProperty = BrushProperty(nameof(TextBrush));
    public static readonly DependencyProperty HandleBrushProperty = BrushProperty(nameof(HandleBrush));

    private const int Count = Commands.EqBandCount;
    private const int Min = Commands.EqBandMin;
    private const int Max = Commands.EqBandMax;
    private const double PadX = 36;
    private const double PadTop = 28;
    private const double PadBottom = 30;
    private static readonly Typeface Font = new("Segoe UI");

    private readonly int[] _values = new int[Count];
    private int _dragIndex = -1;

    public EqualizerCurve()
    {
        Cursor = Cursors.Hand;
        IsEnabledChanged += (_, _) => InvalidateVisual();
    }

    public IReadOnlyList<int>? Values
    {
        get => (IReadOnlyList<int>?)GetValue(ValuesProperty);
        set => SetValue(ValuesProperty, value);
    }

    public Brush AccentBrush
    {
        get => (Brush)GetValue(AccentBrushProperty);
        set => SetValue(AccentBrushProperty, value);
    }

    public Brush LineBrush
    {
        get => (Brush)GetValue(LineBrushProperty);
        set => SetValue(LineBrushProperty, value);
    }

    public Brush TextBrush
    {
        get => (Brush)GetValue(TextBrushProperty);
        set => SetValue(TextBrushProperty, value);
    }

    public Brush HandleBrush
    {
        get => (Brush)GetValue(HandleBrushProperty);
        set => SetValue(HandleBrushProperty, value);
    }

    private Rect Plot => new(PadX, PadTop, Math.Max(0, ActualWidth - 2 * PadX), Math.Max(0, ActualHeight - PadTop - PadBottom));

    private void OnValuesChanged()
    {
        if (_dragIndex >= 0) return;
        for (var i = 0; i < Count; i++)
            _values[i] = Values is { } v && i < v.Count ? Math.Clamp(v[i], Min, Max) : 0;
    }

    protected override void OnRender(DrawingContext dc)
    {
        var plot = Plot;
        if (plot.Width <= 0 || plot.Height <= 0) return;

        // Fond transparent : toute la surface réagit à la souris, pas seulement les traits.
        dc.DrawRectangle(Brushes.Transparent, null, new Rect(RenderSize));

        var guide = new Pen(LineBrush, 1);
        var zero = new Pen(LineBrush, 1) { DashStyle = DashStyles.Dash };
        var middle = plot.Y + plot.Height / 2;
        dc.DrawLine(zero, new Point(plot.Left, middle), new Point(plot.Right, middle));

        var points = Enumerable.Range(0, Count).Select(PointAt).ToArray();
        foreach (var p in points) dc.DrawLine(guide, new Point(p.X, plot.Top), new Point(p.X, plot.Bottom));

        var accent = IsEnabled ? AccentBrush : TextBrush;
        dc.PushOpacity(0.18);
        dc.DrawGeometry(accent, null, Smooth(points, plot.Bottom));
        dc.Pop();
        dc.DrawGeometry(null, new Pen(accent, 2.5), Smooth(points, null));

        var dpi = VisualTreeHelper.GetDpi(this).PixelsPerDip;
        for (var i = 0; i < Count; i++)
        {
            var radius = i == _dragIndex ? 9 : 7;
            dc.DrawEllipse(HandleBrush, new Pen(accent, 3), points[i], radius, radius);

            var value = Text(_values[i] > 0 ? $"+{_values[i]}" : _values[i].ToString(), 12, i == _dragIndex ? accent : TextBrush, dpi);
            dc.DrawText(value, new Point(points[i].X - value.Width / 2, 2));

            var label = Text(Labels.EqBands[i], 11.5, TextBrush, dpi);
            dc.DrawText(label, new Point(points[i].X - label.Width / 2, plot.Bottom + 10));
        }
    }

    private Point PointAt(int index)
    {
        var plot = Plot;
        return new Point(
            plot.X + plot.Width * index / (Count - 1),
            plot.Y + plot.Height * (Max - _values[index]) / (Max - Min));
    }

    /// <summary>Courbe de Catmull-Rom passant par les points ; fermée vers le bas si <paramref name="bottom"/> est donné.</summary>
    private static PathGeometry Smooth(Point[] p, double? bottom)
    {
        var figure = new PathFigure { StartPoint = p[0], IsClosed = bottom.HasValue };
        for (var i = 0; i < p.Length - 1; i++)
        {
            var p0 = p[Math.Max(i - 1, 0)];
            var p3 = p[Math.Min(i + 2, p.Length - 1)];
            var c1 = p[i] + (p[i + 1] - p0) / 6;
            var c2 = p[i + 1] - (p3 - p[i]) / 6;
            figure.Segments.Add(new BezierSegment(c1, c2, p[i + 1], true));
        }
        if (bottom is { } y)
        {
            figure.Segments.Add(new LineSegment(new Point(p[^1].X, y), false));
            figure.Segments.Add(new LineSegment(new Point(p[0].X, y), false));
        }
        return new PathGeometry([figure]);
    }

    private static FormattedText Text(string text, double size, Brush brush, double dpi) =>
        new(text, CultureInfo.CurrentCulture, FlowDirection.LeftToRight, Font, size, brush, dpi);

    protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
    {
        var position = e.GetPosition(this);
        _dragIndex = Nearest(position.X);
        CaptureMouse();
        SetFromY(position.Y);
        e.Handled = true;
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        if (_dragIndex >= 0) SetFromY(e.GetPosition(this).Y);
    }

    protected override void OnMouseLeftButtonUp(MouseButtonEventArgs e)
    {
        if (_dragIndex < 0) return;
        _dragIndex = -1;
        ReleaseMouseCapture();
        Commit();
    }

    protected override void OnLostMouseCapture(MouseEventArgs e)
    {
        if (_dragIndex < 0) return;
        _dragIndex = -1;
        Commit();
    }

    protected override void OnMouseWheel(MouseWheelEventArgs e)
    {
        var index = Nearest(e.GetPosition(this).X);
        _values[index] = Math.Clamp(_values[index] + Math.Sign(e.Delta), Min, Max);
        InvalidateVisual();
        Commit();
        e.Handled = true;
    }

    private int Nearest(double x)
    {
        var plot = Plot;
        return plot.Width <= 0 ? 0 : Math.Clamp((int)Math.Round((x - plot.X) / plot.Width * (Count - 1)), 0, Count - 1);
    }

    private void SetFromY(double y)
    {
        var plot = Plot;
        if (plot.Height <= 0) return;
        var value = Math.Clamp((int)Math.Round(Max - (y - plot.Y) / plot.Height * (Max - Min)), Min, Max);
        if (value == _values[_dragIndex]) return;
        _values[_dragIndex] = value;
        InvalidateVisual();
    }

    private void Commit()
    {
        InvalidateVisual();
        if (Values is { } current && current.SequenceEqual(_values)) return;
        SetCurrentValue(ValuesProperty, _values.ToArray());
    }

    private static DependencyProperty BrushProperty(string name) => DependencyProperty.Register(
        name, typeof(Brush), typeof(EqualizerCurve),
        new FrameworkPropertyMetadata(Brushes.Gray, FrameworkPropertyMetadataOptions.AffectsRender));
}
