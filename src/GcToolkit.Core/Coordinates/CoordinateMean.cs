namespace GcToolkit.Core.Coordinates;

/// <summary>An input line that is not a coordinate in any notation the parser knows.</summary>
public readonly record struct CoordinateLineError(int LineNumber, string Text);

/// <summary>The points read out of a multi-line block, plus the lines that could not be read.</summary>
public sealed record CoordinateLineParseResult(
    IReadOnlyList<GeoCoordinate> Points,
    IReadOnlyList<CoordinateLineError> Errors);

/// <summary>
/// Both means of a set of coordinates and how tightly that set clusters.
/// <see cref="DivergenceMeters"/> is the distance between the two means — small everywhere except
/// across the antimeridian, where the arithmetic mean walks the long way round the globe.
/// </summary>
public readonly record struct CoordinateMeanResult(
    int Count,
    GeoCoordinate CartesianMean,
    GeoCoordinate ArithmeticMean,
    double MaxDistanceMeters,
    double MeanDistanceMeters,
    double DivergenceMeters);

/// <summary>
/// The average position of a set of coordinates, computed two ways because puzzles disagree on
/// which one they mean:
/// <list type="number">
///   <item><description><b>Cartesian</b> — each point becomes a 3D unit vector, the vectors are
///   averaged and the result re-projected to lat/lon. Correct for widely separated points and
///   across the antimeridian; for two points it is exactly <see cref="Geodesy.Midpoint"/>.</description></item>
///   <item><description><b>Arithmetic</b> — the plain mean of the decimal degrees. Simpler, and
///   what many puzzle authors assume, but wrong across the antimeridian and near the poles.</description></item>
/// </list>
/// Shared by the centroid tool (distinct points) and the averaging tool (repeated readings of one
/// point), which differ only in how they present the same numbers.
/// </summary>
public static class CoordinateMean
{
    /// <summary>Below this, the averaged unit vector has no usable direction (points that cancel out).</summary>
    private const double DegenerateVectorLength = 1e-10;

    /// <summary>The geodesic (3D unit-vector) mean. <see langword="null"/> for no points, or for a
    /// set whose vectors cancel — an antipodal pair has no "middle".</summary>
    public static GeoCoordinate? Cartesian(IEnumerable<GeoCoordinate> points)
    {
        ArgumentNullException.ThrowIfNull(points);

        var x = 0.0;
        var y = 0.0;
        var z = 0.0;
        var count = 0;

        foreach (var point in points)
        {
            var lat = Rad(point.Latitude);
            var lon = Rad(point.Longitude);
            var cosLat = Math.Cos(lat);

            x += cosLat * Math.Cos(lon);
            y += cosLat * Math.Sin(lon);
            z += Math.Sin(lat);
            count++;
        }

        if (count == 0)
        {
            return null;
        }

        x /= count;
        y /= count;
        z /= count;

        if (Math.Sqrt(x * x + y * y + z * z) < DegenerateVectorLength)
        {
            return null;
        }

        // Atan2 already yields (-180, 180] for the longitude and [-90, 90] for the latitude.
        return new GeoCoordinate(Deg(Math.Atan2(z, Math.Sqrt(x * x + y * y))), Deg(Math.Atan2(y, x)));
    }

    /// <summary>The arithmetic mean of the decimal degrees. <see langword="null"/> for no points.</summary>
    public static GeoCoordinate? Arithmetic(IEnumerable<GeoCoordinate> points)
    {
        ArgumentNullException.ThrowIfNull(points);

        var latitude = 0.0;
        var longitude = 0.0;
        var count = 0;

        foreach (var point in points)
        {
            latitude += point.Latitude;
            longitude += point.Longitude;
            count++;
        }

        return count == 0 ? null : new GeoCoordinate(latitude / count, longitude / count);
    }

    /// <summary>How far the points sit from <paramref name="centre"/>: the farthest one, and the average.</summary>
    public static (double MaxMeters, double MeanMeters) Dispersion(GeoCoordinate centre, IEnumerable<GeoCoordinate> points)
    {
        ArgumentNullException.ThrowIfNull(points);

        var max = 0.0;
        var total = 0.0;
        var count = 0;

        foreach (var point in points)
        {
            var distance = Geodesy.DistanceMeters(centre, point);
            if (distance > max)
            {
                max = distance;
            }

            total += distance;
            count++;
        }

        return count == 0 ? (0.0, 0.0) : (max, total / count);
    }

    /// <summary>Computes both means and the dispersion for one set, materialising it once so a
    /// lazy sequence is not enumerated repeatedly. Returns <see langword="false"/> for an empty set
    /// or one with no usable mean direction.</summary>
    public static bool TryCompute(IEnumerable<GeoCoordinate> points, out CoordinateMeanResult result)
    {
        ArgumentNullException.ThrowIfNull(points);

        result = default;

        var list = points as IReadOnlyList<GeoCoordinate> ?? [.. points];
        if (Cartesian(list) is not { } cartesian || Arithmetic(list) is not { } arithmetic)
        {
            return false;
        }

        // Dispersion is measured from the Cartesian mean: it is the geodesically correct centre.
        var (max, mean) = Dispersion(cartesian, list);

        result = new CoordinateMeanResult(
            Count: list.Count,
            CartesianMean: cartesian,
            ArithmeticMean: arithmetic,
            MaxDistanceMeters: max,
            MeanDistanceMeters: mean,
            DivergenceMeters: Geodesy.DistanceMeters(cartesian, arithmetic));
        return true;
    }

    /// <summary>Reads one coordinate per line, in any notation, blank lines skipped. Lines that fail
    /// are returned rather than dropped, so the UI can point at them.</summary>
    public static CoordinateLineParseResult ParseLines(string? text)
    {
        List<GeoCoordinate> points = [];
        List<CoordinateLineError> errors = [];

        if (string.IsNullOrWhiteSpace(text))
        {
            return new CoordinateLineParseResult(points, errors);
        }

        // WinUI's TextBox hands back bare \r for Enter, so \n alone would see one giant line.
        var lines = text.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
        for (var i = 0; i < lines.Length; i++)
        {
            var line = lines[i].Trim();
            if (line.Length == 0)
            {
                continue;
            }

            if (CoordinateParser.TryParse(line, out var coordinate, out _))
            {
                points.Add(coordinate);
            }
            else
            {
                errors.Add(new CoordinateLineError(i + 1, line));
            }
        }

        return new CoordinateLineParseResult(points, errors);
    }

    private static double Rad(double degrees) => degrees * Math.PI / 180.0;

    private static double Deg(double radians) => radians * 180.0 / Math.PI;
}
