using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes;
using Windows.Foundation;
using Xm4Control.Core;
using Xm4Control.Core.Protocol;

namespace Xm4Control.App.Controls;

/// <summary>
/// Égaliseur dessiné en courbe. Chaque point se règle en le faisant glisser ou à la molette ;
/// la valeur n'est publiée (propriété <see cref="Values"/>) qu'au relâchement.
/// </summary>
public sealed partial class EqualizerCurve : UserControl
{
    public static readonly DependencyProperty ValuesProperty = DependencyProperty.Register(
        nameof(Values), typeof(object), typeof(EqualizerCurve),
        new PropertyMetadata(null, (d, _) => ((EqualizerCurve)d).OnValuesChanged()));

    private const int Count = Commands.EqBandCount;
    private const int Min = Commands.EqBandMin;
    private const int Max = Commands.EqBandMax;
    private const double PadX = 36;
    private const double PadTop = 30;
    private const double PadBottom = 32;
    private const double HandleSize = 16;

    private readonly int[] _values = new int[Count];
    private readonly Line[] _guides = new Line[Count];
    private readonly Ellipse[] _handles = new Ellipse[Count];
    private readonly TextBlock[] _valueTexts = new TextBlock[Count];
    private readonly TextBlock[] _labels = new TextBlock[Count];
    private int _dragIndex = -1;

    public EqualizerCurve()
    {
        InitializeComponent();
        for (var i = 0; i < Count; i++)
        {
            _guides[i] = new Line { StrokeThickness = 1 };
            _handles[i] = new Ellipse { Width = HandleSize, Height = HandleSize, StrokeThickness = 3 };
            _valueTexts[i] = new TextBlock { FontSize = 12 };
            _labels[i] = new TextBlock { FontSize = 12, Text = Labels.EqBands[i] };
            Surface.Children.Insert(1, _guides[i]);
            Surface.Children.Add(_handles[i]);
            Surface.Children.Add(_valueTexts[i]);
            Surface.Children.Add(_labels[i]);
        }
        ApplyBrushes();
        ActualThemeChanged += (_, _) => ApplyBrushes();
        IsEnabledChanged += (_, _) => Opacity = IsEnabled ? 1 : 0.5;
        SizeChanged += (_, _) => Relayout();
    }

    public IReadOnlyList<int>? Values
    {
        get => (IReadOnlyList<int>?)GetValue(ValuesProperty);
        set => SetValue(ValuesProperty, value);
    }

    private Rect Plot => new(PadX, PadTop, Math.Max(0, ActualWidth - 2 * PadX), Math.Max(0, ActualHeight - PadTop - PadBottom));

    private void OnValuesChanged()
    {
        if (_dragIndex >= 0) return;
        for (var i = 0; i < Count; i++)
            _values[i] = Values is { } v && i < v.Count ? Math.Clamp(v[i], Min, Max) : 0;
        Relayout();
    }

    private void ApplyBrushes()
    {
        for (var i = 0; i < Count; i++)
        {
            _guides[i].Stroke = ZeroLine.Stroke;
            _handles[i].Fill = HandleTemplate.Fill;
            _handles[i].Stroke = StrokePath.Stroke;
            _valueTexts[i].Foreground = i == _dragIndex ? StrokePath.Stroke : TextTemplate.Foreground;
            _labels[i].Foreground = TextTemplate.Foreground;
        }
    }

    private void Relayout()
    {
        var plot = Plot;
        if (plot.Width <= 0 || plot.Height <= 0) return;

        var middle = plot.Y + plot.Height / 2;
        (ZeroLine.X1, ZeroLine.Y1, ZeroLine.X2, ZeroLine.Y2) = (plot.Left, middle, plot.Right, middle);

        var points = new Point[Count];
        for (var i = 0; i < Count; i++)
        {
            points[i] = PointAt(i);
            (_guides[i].X1, _guides[i].Y1, _guides[i].X2, _guides[i].Y2) = (points[i].X, plot.Top, points[i].X, plot.Bottom);

            var size = i == _dragIndex ? HandleSize + 4 : HandleSize;
            _handles[i].Width = _handles[i].Height = size;
            Canvas.SetLeft(_handles[i], points[i].X - size / 2);
            Canvas.SetTop(_handles[i], points[i].Y - size / 2);

            _valueTexts[i].Text = _values[i] > 0 ? $"+{_values[i]}" : _values[i].ToString();
            Center(_valueTexts[i], points[i].X, 4);
            Center(_labels[i], points[i].X, plot.Bottom + 10);
        }

        StrokePath.Data = Smooth(points, null);
        FillPath.Data = Smooth(points, plot.Bottom);
    }

    private static void Center(TextBlock text, double x, double top)
    {
        text.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        Canvas.SetLeft(text, x - text.DesiredSize.Width / 2);
        Canvas.SetTop(text, top);
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
            figure.Segments.Add(new BezierSegment
            {
                Point1 = new Point(p[i].X + (p[i + 1].X - p0.X) / 6, p[i].Y + (p[i + 1].Y - p0.Y) / 6),
                Point2 = new Point(p[i + 1].X - (p3.X - p[i].X) / 6, p[i + 1].Y - (p3.Y - p[i].Y) / 6),
                Point3 = p[i + 1],
            });
        }
        if (bottom is { } y)
        {
            figure.Segments.Add(new LineSegment { Point = new Point(p[^1].X, y) });
            figure.Segments.Add(new LineSegment { Point = new Point(p[0].X, y) });
        }
        var geometry = new PathGeometry();
        geometry.Figures.Add(figure);
        return geometry;
    }

    private void OnPointerPressed(object sender, PointerRoutedEventArgs e)
    {
        if (!IsEnabled) return;
        var position = e.GetCurrentPoint(Surface).Position;
        _dragIndex = Nearest(position.X);
        Surface.CapturePointer(e.Pointer);
        ApplyBrushes();
        SetFromY(position.Y);
        e.Handled = true;
    }

    private void OnPointerMoved(object sender, PointerRoutedEventArgs e)
    {
        if (_dragIndex >= 0) SetFromY(e.GetCurrentPoint(Surface).Position.Y);
    }

    private void OnPointerReleased(object sender, PointerRoutedEventArgs e)
    {
        if (_dragIndex < 0) return;
        Surface.ReleasePointerCapture(e.Pointer);
        EndDrag();
    }

    private void OnPointerCaptureLost(object sender, PointerRoutedEventArgs e)
    {
        if (_dragIndex >= 0) EndDrag();
    }

    private void OnPointerWheelChanged(object sender, PointerRoutedEventArgs e)
    {
        if (!IsEnabled) return;
        var point = e.GetCurrentPoint(Surface);
        var index = Nearest(point.Position.X);
        _values[index] = Math.Clamp(_values[index] + Math.Sign(point.Properties.MouseWheelDelta), Min, Max);
        Relayout();
        Commit();
        e.Handled = true;
    }

    private void EndDrag()
    {
        _dragIndex = -1;
        ApplyBrushes();
        Relayout();
        Commit();
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
        Relayout();
    }

    private void Commit()
    {
        if (Values is { } current && current.SequenceEqual(_values)) return;
        Values = _values.ToArray();
    }
}
