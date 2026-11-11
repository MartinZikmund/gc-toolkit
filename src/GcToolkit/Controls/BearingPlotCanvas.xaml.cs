using GcToolkit.Core.Coordinates;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Windows.Foundation;

namespace GcToolkit.Controls;

/// <summary>
/// Renders a <see cref="BearingPlotGeometry"/>: rays, labelled points, the fix with its RMS circle and a
/// scale bar. All layout math (projection, fitting, scale bar) lives in Core; this only draws.
/// </summary>
public sealed partial class BearingPlotCanvas : UserControl
{
    private const double PlotPadding = 28;
    private const double MarkerRadius = 4;
    private const double FixRadius = 6;

    public BearingPlotCanvas() => InitializeComponent();

    public static readonly DependencyProperty PlotProperty = DependencyProperty.Register(
        nameof(Plot), typeof(BearingPlotGeometry), typeof(BearingPlotCanvas),
        new PropertyMetadata(null, (d, _) => ((BearingPlotCanvas)d).Redraw()));

    public BearingPlotGeometry? Plot
    {
        get => (BearingPlotGeometry?)GetValue(PlotProperty);
        set => SetValue(PlotProperty, value);
    }

    private void OnRootSizeChanged(object sender, SizeChangedEventArgs e) => Redraw();

    private void Redraw()
    {
        LabelCanvas.Children.Clear();
        RaysPath.Data = null;
        MarkersPath.Data = null;
        FixPath.Data = null;
        ErrorPath.Data = null;
        ScalePath.Data = null;

        var width = Root.ActualWidth;
        var height = Root.ActualHeight;
        if (Plot is not { } plot || plot.Markers.Count == 0 || width <= 0 || height <= 0)
        {
            return;
        }

        var t = plot.Fit(width, height, PlotPadding);

        GeometryGroup rays = new();
        foreach (var ray in plot.Rays)
        {
            rays.Children.Add(new LineGeometry { StartPoint = ToPoint(t.ToScreen(ray.Start)), EndPoint = ToPoint(t.ToScreen(ray.End)) });
        }

        RaysPath.Data = rays;

        GeometryGroup markers = new();
        foreach (var marker in plot.Markers)
        {
            var p = t.ToScreen(marker.Position);
            markers.Children.Add(Circle(p, MarkerRadius));
            AddLabel(marker.Label, p.X + 6, p.Y - 20, isStrong: true);
        }

        MarkersPath.Data = markers;

        if (plot.Fix is { } fix)
        {
            var p = t.ToScreen(fix);
            var errorRadius = plot.FixRadiusMeters * t.Scale;
            if (errorRadius >= FixRadius)
            {
                ErrorPath.Data = Circle(p, errorRadius);
            }

            FixPath.Data = Circle(p, FixRadius);
        }

        var bar = t.ScaleBar(Math.Min(140, width / 3));
        var y = height - 12;
        var x = 12.0;
        GeometryGroup scale = new();
        scale.Children.Add(new LineGeometry { StartPoint = new Point(x, y), EndPoint = new Point(x + bar.Pixels, y) });
        scale.Children.Add(new LineGeometry { StartPoint = new Point(x, y - 5), EndPoint = new Point(x, y + 1) });
        scale.Children.Add(new LineGeometry { StartPoint = new Point(x + bar.Pixels, y - 5), EndPoint = new Point(x + bar.Pixels, y + 1) });
        ScalePath.Data = scale;
        AddLabel(BearingPlot.FormatScale(bar.Meters), x, y - 22, isStrong: false);
    }

    private void AddLabel(string text, double left, double top, bool isStrong)
    {
        TextBlock label = new()
        {
            Text = text,
            Style = (Style)Application.Current.Resources[isStrong ? "BodyStrongTextBlockStyle" : "CaptionTextBlockStyle"],
        };
        Canvas.SetLeft(label, left);
        Canvas.SetTop(label, top);
        LabelCanvas.Children.Add(label);
    }

    private static EllipseGeometry Circle(PlotPoint centre, double radius)
        => new() { Center = ToPoint(centre), RadiusX = radius, RadiusY = radius };

    private static Point ToPoint(PlotPoint p) => new(p.X, p.Y);
}
