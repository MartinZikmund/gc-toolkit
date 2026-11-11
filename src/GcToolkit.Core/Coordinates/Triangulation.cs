namespace GcToolkit.Core.Coordinates;

/// <summary>Outcome of a <see cref="Triangulation.Intersect"/> call.</summary>
public enum TriangulationStatus
{
    /// <summary>A solution was found.</summary>
    Ok,

    /// <summary>Both sights lie on the same great circle, so every point on it fits.</summary>
    Coincident,

    /// <summary>Both observers stand on the same point, so the "intersection" is meaningless.</summary>
    IdenticalPoints,

    /// <summary>A coordinate or bearing is not a finite, valid value.</summary>
    InvalidInput,
}

/// <summary>One of the two antipodal crossings of the bearing lines.</summary>
/// <param name="Point">The crossing point.</param>
/// <param name="IsAheadOfA">Whether the point lies in the direction observer A is looking.</param>
/// <param name="IsAheadOfB">Whether the point lies in the direction observer B is looking.</param>
/// <param name="DistanceFromAMeters">Geodesic distance from A.</param>
/// <param name="DistanceFromBMeters">Geodesic distance from B.</param>
/// <param name="BearingFromA">Initial bearing from A to the point, degrees.</param>
/// <param name="BearingFromB">Initial bearing from B to the point, degrees.</param>
public sealed record TriangulationSolution(
    GeoCoordinate Point,
    bool IsAheadOfA,
    bool IsAheadOfB,
    double DistanceFromAMeters,
    double DistanceFromBMeters,
    double BearingFromA,
    double BearingFromB);

/// <summary>The full result of triangulating two bearing lines.</summary>
/// <param name="Status">Whether a solution exists.</param>
/// <param name="Primary">The nearer crossing, the one a field puzzle almost always means.</param>
/// <param name="Alternate">The antipodal crossing, for the user to rule out.</param>
/// <param name="IsIllConditioned">The lines are nearly parallel, so a small bearing error moves the result a lot.</param>
/// <param name="BaselineMeters">Distance between the two observers.</param>
public sealed record TriangulationResult(
    TriangulationStatus Status,
    TriangulationSolution? Primary,
    TriangulationSolution? Alternate,
    bool IsIllConditioned,
    double BaselineMeters)
{
    internal static TriangulationResult Failure(TriangulationStatus status, double baseline = 0)
        => new(status, null, null, false, baseline);
}

/// <summary>
/// Finds the point where two great-circle bearing lines cross (spherical vector method; the
/// reported distances and bearings use the ellipsoidal <see cref="Geodesy"/>).
/// </summary>
public static class Triangulation
{
    // Below this angle between the lines the answer is dominated by bearing error.
    private const double IllConditionedAngleDegrees = 1.0;
    private const double CoincidentSine = 1e-10;
    private const double IdenticalPointsMeters = 0.5;

    /// <summary>Intersects the line from <paramref name="a"/> along <paramref name="bearingA"/> with the line from
    /// <paramref name="b"/> along <paramref name="bearingB"/> (degrees clockwise from true North).</summary>
    public static TriangulationResult Intersect(GeoCoordinate a, double bearingA, GeoCoordinate b, double bearingB)
    {
        if (!a.IsValid || !b.IsValid || !double.IsFinite(bearingA) || !double.IsFinite(bearingB))
        {
            return TriangulationResult.Failure(TriangulationStatus.InvalidInput);
        }

        var baseline = Geodesy.DistanceMeters(a, b);
        if (baseline < IdenticalPointsMeters)
        {
            return TriangulationResult.Failure(TriangulationStatus.IdenticalPoints, baseline);
        }

        var cross = Cross(GreatCircleNormal(a, bearingA), GreatCircleNormal(b, bearingB));
        var sine = Length(cross);
        if (sine < CoincidentSine)
        {
            return TriangulationResult.Failure(TriangulationStatus.Coincident, baseline);
        }

        var unit = new Vec(cross.X / sine, cross.Y / sine, cross.Z / sine);
        var first = BuildSolution(unit, a, bearingA, b, bearingB);
        var second = BuildSolution(new Vec(-unit.X, -unit.Y, -unit.Z), a, bearingA, b, bearingB);

        var firstIsNearer = first.DistanceFromAMeters + first.DistanceFromBMeters
            <= second.DistanceFromAMeters + second.DistanceFromBMeters;
        var illConditioned = Math.Asin(Math.Min(1.0, sine)) * 180.0 / Math.PI < IllConditionedAngleDegrees;

        return new TriangulationResult(
            TriangulationStatus.Ok,
            firstIsNearer ? first : second,
            firstIsNearer ? second : first,
            illConditioned,
            baseline);
    }

    private static TriangulationSolution BuildSolution(Vec v, GeoCoordinate a, double bearingA, GeoCoordinate b, double bearingB)
    {
        var lat = Math.Atan2(v.Z, Math.Sqrt(v.X * v.X + v.Y * v.Y)) * 180.0 / Math.PI;
        var lon = Math.Atan2(v.Y, v.X) * 180.0 / Math.PI;
        var point = new GeoCoordinate(lat, lon);

        var distanceA = Geodesy.DistanceMeters(a, point);
        var distanceB = Geodesy.DistanceMeters(b, point);
        var toPointA = distanceA < IdenticalPointsMeters ? bearingA : Geodesy.InitialBearingDegrees(a, point);
        var toPointB = distanceB < IdenticalPointsMeters ? bearingB : Geodesy.InitialBearingDegrees(b, point);

        return new TriangulationSolution(
            point,
            IsAhead(toPointA, bearingA),
            IsAhead(toPointB, bearingB),
            distanceA,
            distanceB,
            toPointA,
            toPointB);
    }

    // On a great circle the point is either ahead or exactly behind, so a quarter turn splits them.
    private static bool IsAhead(double bearingToPoint, double sightedBearing)
    {
        var diff = Math.Abs(((bearingToPoint - sightedBearing) % 360.0 + 540.0) % 360.0 - 180.0);
        return diff < 90.0;
    }

    // Normal of the great circle through a point along a bearing: position x direction.
    private static Vec GreatCircleNormal(GeoCoordinate p, double bearingDegrees)
    {
        var phi = p.Latitude * Math.PI / 180.0;
        var lambda = p.Longitude * Math.PI / 180.0;
        var theta = bearingDegrees * Math.PI / 180.0;

        var pos = new Vec(Math.Cos(phi) * Math.Cos(lambda), Math.Cos(phi) * Math.Sin(lambda), Math.Sin(phi));
        var north = new Vec(-Math.Sin(phi) * Math.Cos(lambda), -Math.Sin(phi) * Math.Sin(lambda), Math.Cos(phi));
        var east = new Vec(-Math.Sin(lambda), Math.Cos(lambda), 0.0);

        var dir = new Vec(
            Math.Cos(theta) * north.X + Math.Sin(theta) * east.X,
            Math.Cos(theta) * north.Y + Math.Sin(theta) * east.Y,
            Math.Cos(theta) * north.Z + Math.Sin(theta) * east.Z);

        return Cross(pos, dir);
    }

    private static Vec Cross(Vec u, Vec v)
        => new(u.Y * v.Z - u.Z * v.Y, u.Z * v.X - u.X * v.Z, u.X * v.Y - u.Y * v.X);

    private static double Length(Vec v) => Math.Sqrt(v.X * v.X + v.Y * v.Y + v.Z * v.Z);

    private readonly record struct Vec(double X, double Y, double Z);
}
