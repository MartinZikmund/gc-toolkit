using System.Globalization;

namespace GcToolkit.Core.Coordinates;

/// <summary>A position in the plot's local plane: metres east (X) and north (Y) of its centre.</summary>
public readonly record struct PlotPoint(double X, double Y);

/// <summary>A labelled known point.</summary>
public sealed record PlotMarker(string Label, PlotPoint Position);

/// <summary>A bearing line drawn from its known point.</summary>
public sealed record PlotRay(string Label, PlotPoint Start, PlotPoint End);

/// <summary>A nice round scale-bar length and how many screen pixels it spans.</summary>
public readonly record struct PlotScaleBar(double Meters, double Pixels);

/// <summary>Maps plane metres to screen pixels: uniform scale, North up, content centred.</summary>
public sealed record PlotTransform(double Scale, double CentreX, double CentreY, double ScreenCentreX, double ScreenCentreY)
{
    public PlotPoint ToScreen(PlotPoint p)
        => new(ScreenCentreX + (p.X - CentreX) * Scale, ScreenCentreY - (p.Y - CentreY) * Scale);

    /// <summary>The longest 1/2/5 x 10^n metre length that fits in <paramref name="maxPixels"/>.</summary>
    public PlotScaleBar ScaleBar(double maxPixels)
    {
        var maxMeters = maxPixels / Scale;
        var magnitude = Math.Pow(10, Math.Floor(Math.Log10(maxMeters) + 1e-9));
        var meters = magnitude;
        foreach (var step in new[] { 5.0, 2.0 })
        {
            if (step * magnitude <= maxMeters * (1 + 1e-9))
            {
                meters = step * magnitude;
                break;
            }
        }

        return new PlotScaleBar(meters, meters * Scale);
    }
}

/// <summary>Everything the schematic plot draws, in local plane metres, with its bounding box.</summary>
public sealed record BearingPlotGeometry(
    IReadOnlyList<PlotMarker> Markers,
    IReadOnlyList<PlotRay> Rays,
    PlotPoint? Fix,
    double FixRadiusMeters,
    double MinX,
    double MaxX,
    double MinY,
    double MaxY)
{
    /// <summary>Fits the bounds into a <paramref name="width"/> x <paramref name="height"/> surface.</summary>
    public PlotTransform Fit(double width, double height, double padding)
    {
        var spanX = Math.Max(MaxX - MinX, 1.0);
        var spanY = Math.Max(MaxY - MinY, 1.0);
        var scale = Math.Min(Math.Max(width - 2 * padding, 1.0) / spanX, Math.Max(height - 2 * padding, 1.0) / spanY);

        return new PlotTransform(scale, (MinX + MaxX) / 2, (MinY + MaxY) / 2, width / 2, height / 2);
    }
}

/// <summary>
/// Lays out an offline schematic of bearing lines: points, rays and the fix, projected onto a local
/// equirectangular plane around their centre (accurate to well under a percent over a few tens of km).
/// </summary>
public static class BearingPlot
{
    private const double MeanRadiusMeters = 6_371_008.8;
    private const double MinRayMeters = 50.0;

    /// <summary>Builds the plot; markers and rays are labelled A, B, C... in input order.</summary>
    public static BearingPlotGeometry Build(IReadOnlyList<BearingObservation> lines, GeoCoordinate? fix, double fixRadiusMeters = 0)
    {
        if (lines.Count == 0)
        {
            return new BearingPlotGeometry([], [], null, 0, 0, 0, 0, 0);
        }

        // Unwrap longitudes around the first point so the antimeridian does not split the plot.
        var referenceLon = lines[0].Point.Longitude;
        var all = lines.Select(l => l.Point).ToList();
        if (fix is { } f)
        {
            all.Add(f);
        }

        var originLat = all.Average(p => p.Latitude);
        var originLon = all.Average(p => Unwrap(p.Longitude, referenceLon));
        var cosLat = Math.Cos(originLat * Math.PI / 180.0);

        PlotPoint Project(GeoCoordinate p) => new(
            (Unwrap(p.Longitude, referenceLon) - originLon) * Math.PI / 180.0 * MeanRadiusMeters * cosLat,
            (p.Latitude - originLat) * Math.PI / 180.0 * MeanRadiusMeters);

        var span = 0.0;
        foreach (var a in lines)
        {
            foreach (var b in lines)
            {
                span = Math.Max(span, Geodesy.DistanceMeters(a.Point, b.Point));
            }
        }

        List<PlotMarker> markers = new();
        List<PlotRay> rays = new();
        for (var i = 0; i < lines.Count; i++)
        {
            var line = lines[i];
            var start = Project(line.Point);
            var reach = Math.Max(span * 0.5, MinRayMeters);
            if (fix is { } target)
            {
                reach = Math.Max(reach, Geodesy.DistanceMeters(line.Point, target) * 1.2);
            }

            var label = Label(i);
            markers.Add(new PlotMarker(label, start));
            rays.Add(new PlotRay(label, start, Project(Geodesy.Destination(line.Point, reach, line.BearingDegrees))));
        }

        PlotPoint? fixPosition = fix is { } g ? Project(g) : null;
        IEnumerable<PlotPoint> extent = rays.SelectMany(r => new[] { r.Start, r.End });
        if (fixPosition is { } fp)
        {
            extent = extent.Append(fp);
        }

        var points = extent.ToList();
        return new BearingPlotGeometry(
            markers,
            rays,
            fixPosition,
            fixRadiusMeters,
            points.Min(p => p.X),
            points.Max(p => p.X),
            points.Min(p => p.Y),
            points.Max(p => p.Y));
    }

    /// <summary>A, B, ... Z, AA, AB, ... for the zero-based <paramref name="index"/>.</summary>
    public static string Label(int index)
    {
        var label = string.Empty;
        for (var n = index + 1; n > 0; n = (n - 1) / 26)
        {
            label = (char)('A' + (n - 1) % 26) + label;
        }

        return label;
    }

    /// <summary>A scale-bar caption: metres below a kilometre, kilometres above.</summary>
    public static string FormatScale(double meters)
        => meters >= 1000
            ? string.Format(CultureInfo.CurrentCulture, "{0:0.###} km", meters / 1000)
            : string.Format(CultureInfo.CurrentCulture, "{0:0.###} m", meters);

    private static double Unwrap(double longitude, double reference)
    {
        var d = longitude - reference;
        return reference + (d > 180 ? d - 360 : d < -180 ? d + 360 : d);
    }
}
