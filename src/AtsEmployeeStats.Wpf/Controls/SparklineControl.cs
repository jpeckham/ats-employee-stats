using System.Collections;
using System.Windows;
using System.Windows.Media;

namespace AtsEmployeeStats.Wpf.Controls;

public sealed class SparklineControl : FrameworkElement
{
    public static readonly DependencyProperty ValuesProperty =
        DependencyProperty.Register(
            nameof(Values),
            typeof(IEnumerable),
            typeof(SparklineControl),
            new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));

    public IEnumerable? Values
    {
        get => (IEnumerable?)GetValue(ValuesProperty);
        set => SetValue(ValuesProperty, value);
    }

    protected override void OnRender(DrawingContext drawingContext)
    {
        base.OnRender(drawingContext);
        var points = Values?.Cast<object>()
            .Select(Convert.ToDouble)
            .ToArray() ?? [];
        if (points.Length < 2 || ActualWidth <= 1 || ActualHeight <= 1)
            return;

        var min = points.Min();
        var max = points.Max();
        var range = Math.Max(1, max - min);
        var geometry = new StreamGeometry();
        using (var context = geometry.Open())
        {
            for (var i = 0; i < points.Length; i++)
            {
                var x = points.Length == 1 ? 0 : i * ActualWidth / (points.Length - 1);
                var y = ActualHeight - ((points[i] - min) / range * ActualHeight);
                var point = new Point(x, y);
                if (i == 0)
                    context.BeginFigure(point, false, false);
                else
                    context.LineTo(point, true, false);
            }
        }

        geometry.Freeze();
        drawingContext.DrawGeometry(null, new Pen(new SolidColorBrush(Color.FromRgb(37, 99, 235)), 1.4), geometry);
    }
}
