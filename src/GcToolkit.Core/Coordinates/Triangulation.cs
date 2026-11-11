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

    /// <summary>Fewer than two observations were given.</summary>
    TooFewObservations,
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
    private const double MeanRadiusMeters = 6_371_008.8;
    private const int RefinementPasses = 8;

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

    /// <summary>
    /// Fixes a position from two or more bearing lines. Two lines give their exact crossing; more give the
    /// point with the least sum of squared distances to the great circles (smallest eigenvector of the sum of
    /// their normals' outer products). The spherical answer is then refined so the bearings hold as WGS84
    /// geodesic azimuths, which is what a sighted "true bearing" is.
    /// </summary>
    public static BearingFix Solve(IReadOnlyList<BearingObservation> observations)
    {
        if (observations.Count < 2)
        {
            return BearingFix.Failure(TriangulationStatus.TooFewObservations);
        }

        if (observations.Any(o => !o.Point.IsValid || !double.IsFinite(o.BearingDegrees)))
        {
            return BearingFix.Failure(TriangulationStatus.InvalidInput);
        }

        var first = observations[0].Point;
        if (observations.All(o => Geodesy.DistanceMeters(first, o.Point) < IdenticalPointsMeters))
        {
            return BearingFix.Failure(TriangulationStatus.IdenticalPoints);
        }

        var bearings = observations.Select(o => o.BearingDegrees).ToArray();
        if (SolveSpherical(observations, bearings, out var maxSine) is not { } solution)
        {
            return BearingFix.Failure(TriangulationStatus.Coincident);
        }

        for (var pass = 0; pass < RefinementPasses; pass++)
        {
            var provisional = ToGeo(solution);
            for (var i = 0; i < observations.Count; i++)
            {
                bearings[i] = SphericalBearingOfGeodesic(observations[i], provisional);
            }

            if (SolveSpherical(observations, bearings, out _) is not { } refined)
            {
                break;
            }

            var shift = Length(Sub(refined, solution));
            solution = refined;
            if (shift < 1e-12)
            {
                break;
            }
        }

        var point = ToGeo(solution);
        var residuals = observations.Select(o => Residual(o, point)).ToList();
        var rms = Math.Sqrt(residuals.Average(r => r.OffsetMeters * r.OffsetMeters));
        var illConditioned = Math.Asin(Math.Min(1.0, maxSine)) * 180.0 / Math.PI < IllConditionedAngleDegrees;

        return new BearingFix(
            TriangulationStatus.Ok,
            point,
            ToGeo(Scale(solution, -1.0)),
            residuals,
            rms,
            residuals.Max(r => r.OffsetMeters),
            illConditioned);
    }

    // The spherical bearing whose great circle runs through the point the WGS84 geodesic along the sighted
    // bearing reaches abreast of the fix, so the spherical solve follows the ellipsoidal line where it matters.
    private static double SphericalBearingOfGeodesic(BearingObservation o, GeoCoordinate fix)
    {
        var distance = Geodesy.DistanceMeters(o.Point, fix);
        var turn = (Geodesy.InitialBearingDegrees(o.Point, fix) - o.BearingDegrees) * Math.PI / 180.0;
        var along = distance * Math.Cos(turn);
        if (Math.Abs(along) < IdenticalPointsMeters)
        {
            return o.BearingDegrees;
        }

        return along > 0
            ? SphericalBearing(o.Point, Geodesy.Destination(o.Point, along, o.BearingDegrees))
            : SphericalBearing(o.Point, Geodesy.Destination(o.Point, -along, o.BearingDegrees + 180.0)) + 180.0;
    }

    private static ObservationResidual Residual(BearingObservation o, GeoCoordinate fix)
    {
        var distance = Geodesy.DistanceMeters(o.Point, fix);
        var bearingToFix = distance < IdenticalPointsMeters ? o.BearingDegrees : Geodesy.InitialBearingDegrees(o.Point, fix);
        var turn = SignedAngle(bearingToFix - o.BearingDegrees);
        var lineError = turn > 90.0 ? turn - 180.0 : turn <= -90.0 ? turn + 180.0 : turn;

        // Cross-track distance on the sphere: asin(sin(d/R) * sin(angle)).
        var offset = MeanRadiusMeters * Math.Asin(Math.Min(1.0,
            Math.Abs(Math.Sin(distance / MeanRadiusMeters) * Math.Sin(lineError * Math.PI / 180.0))));

        return new ObservationResidual(
            o.Point,
            distance,
            Normalize360(bearingToFix),
            offset,
            lineError,
            Math.Abs(turn) < 90.0);
    }

    // Returns the unit vector of the fix, or null when every line lies on one great circle.
    private static Vec? SolveSpherical(IReadOnlyList<BearingObservation> observations, double[] bearings, out double maxSine)
    {
        var normals = new Vec[observations.Count];
        for (var i = 0; i < normals.Length; i++)
        {
            normals[i] = GreatCircleNormal(observations[i].Point, bearings[i]);
        }

        maxSine = 0;
        var best = default(Vec);
        for (var i = 0; i < normals.Length; i++)
        {
            for (var j = i + 1; j < normals.Length; j++)
            {
                var cross = Cross(normals[i], normals[j]);
                var sine = Length(cross);
                if (sine > maxSine)
                {
                    maxSine = sine;
                    best = cross;
                }
            }
        }

        if (maxSine < CoincidentSine)
        {
            return null;
        }

        var v = normals.Length == 2 ? Scale(best, 1.0 / maxSine) : SmallestEigenvector(normals);

        // Of the two antipodal candidates, the one nearer the observers is the one meant.
        double SumOfAngles(Vec p) => observations.Sum(o =>
            Math.Acos(Math.Clamp(Dot(p, ToVector(o.Point)), -1.0, 1.0)));

        return SumOfAngles(v) <= SumOfAngles(Scale(v, -1.0)) ? v : Scale(v, -1.0);
    }

    // Smallest-eigenvalue eigenvector of sum(n n^T), by cyclic Jacobi rotations on the 3x3 symmetric matrix.
    private static Vec SmallestEigenvector(Vec[] normals)
    {
        var a = new double[3, 3];
        foreach (var n in normals)
        {
            double[] c = [n.X, n.Y, n.Z];
            for (var r = 0; r < 3; r++)
            {
                for (var k = 0; k < 3; k++)
                {
                    a[r, k] += c[r] * c[k];
                }
            }
        }

        var v = new double[3, 3] { { 1, 0, 0 }, { 0, 1, 0 }, { 0, 0, 1 } };
        for (var sweep = 0; sweep < 50; sweep++)
        {
            if (Math.Abs(a[0, 1]) + Math.Abs(a[0, 2]) + Math.Abs(a[1, 2]) < 1e-300)
            {
                break;
            }

            for (var p = 0; p < 2; p++)
            {
                for (var q = p + 1; q < 3; q++)
                {
                    if (a[p, q] == 0.0)
                    {
                        continue;
                    }

                    var theta = (a[q, q] - a[p, p]) / (2.0 * a[p, q]);
                    var t = theta == 0.0 ? 1.0 : Math.Sign(theta) / (Math.Abs(theta) + Math.Sqrt(theta * theta + 1.0));
                    var cos = 1.0 / Math.Sqrt(t * t + 1.0);
                    var sin = t * cos;
                    Rotate(a, v, p, q, cos, sin);
                }
            }
        }

        var min = 0;
        for (var k = 1; k < 3; k++)
        {
            if (a[k, k] < a[min, min])
            {
                min = k;
            }
        }

        var e = new Vec(v[0, min], v[1, min], v[2, min]);
        return Scale(e, 1.0 / Length(e));
    }

    // A <- J^T A J and V <- V J for the Jacobi rotation in the (p, q) plane.
    private static void Rotate(double[,] a, double[,] v, int p, int q, double cos, double sin)
    {
        for (var k = 0; k < 3; k++)
        {
            var akp = a[k, p];
            var akq = a[k, q];
            a[k, p] = cos * akp - sin * akq;
            a[k, q] = sin * akp + cos * akq;
        }

        for (var k = 0; k < 3; k++)
        {
            var apk = a[p, k];
            var aqk = a[q, k];
            a[p, k] = cos * apk - sin * aqk;
            a[q, k] = sin * apk + cos * aqk;
        }

        for (var k = 0; k < 3; k++)
        {
            var vkp = v[k, p];
            var vkq = v[k, q];
            v[k, p] = cos * vkp - sin * vkq;
            v[k, q] = sin * vkp + cos * vkq;
        }
    }

    private static double SphericalBearing(GeoCoordinate from, GeoCoordinate to)
    {
        var phi1 = from.Latitude * Math.PI / 180.0;
        var phi2 = to.Latitude * Math.PI / 180.0;
        var dLambda = (to.Longitude - from.Longitude) * Math.PI / 180.0;
        var y = Math.Sin(dLambda) * Math.Cos(phi2);
        var x = Math.Cos(phi1) * Math.Sin(phi2) - Math.Sin(phi1) * Math.Cos(phi2) * Math.Cos(dLambda);
        return Math.Atan2(y, x) * 180.0 / Math.PI;
    }

    // Wraps to (-180, 180].
    private static double SignedAngle(double degrees)
    {
        var a = ((degrees % 360.0) + 360.0) % 360.0;
        return a > 180.0 ? a - 360.0 : a;
    }

    private static double Normalize360(double degrees) => ((degrees % 360.0) + 360.0) % 360.0;

    private static GeoCoordinate ToGeo(Vec v)
        => new(
            Math.Atan2(v.Z, Math.Sqrt(v.X * v.X + v.Y * v.Y)) * 180.0 / Math.PI,
            Math.Atan2(v.Y, v.X) * 180.0 / Math.PI);

    private static Vec ToVector(GeoCoordinate p)
    {
        var phi = p.Latitude * Math.PI / 180.0;
        var lambda = p.Longitude * Math.PI / 180.0;
        return new Vec(Math.Cos(phi) * Math.Cos(lambda), Math.Cos(phi) * Math.Sin(lambda), Math.Sin(phi));
    }

    private static double Dot(Vec u, Vec v) => u.X * v.X + u.Y * v.Y + u.Z * v.Z;

    private static Vec Scale(Vec v, double k) => new(v.X * k, v.Y * k, v.Z * k);

    private static Vec Sub(Vec u, Vec v) => new(u.X - v.X, u.Y - v.Y, u.Z - v.Z);

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
