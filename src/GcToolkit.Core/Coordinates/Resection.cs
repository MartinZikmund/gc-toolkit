namespace GcToolkit.Core.Coordinates;

/// <summary>
/// Resection: find your own position from bearings you sighted to two or more known landmarks. Each sight
/// gives a line of position through its landmark (the back bearing); the lines are crossed or fitted by
/// <see cref="Triangulation.Solve"/>. Because meridians converge, the back bearing at the landmark is not
/// simply the sight + 180 degrees, so each line is re-aimed until the fix sees its landmark at the sighted
/// geodesic azimuth.
/// </summary>
public static class Resection
{
    private const double MeanRadiusMeters = 6_371_008.8;
    private const double MinimumMeters = 0.5;
    private const int MaxPasses = 10;
    private const double ConvergedMeters = 1e-4;

    /// <summary>Fixes the observer from <paramref name="sightsToLandmarks"/>: each landmark with the true bearing sighted to it.</summary>
    public static BearingFix Solve(IReadOnlyList<BearingObservation> sightsToLandmarks)
    {
        var lines = sightsToLandmarks.Select(s => s with { BearingDegrees = s.BearingDegrees + 180.0 }).ToArray();
        var fix = Triangulation.Solve(lines);
        if (fix.Point is not { } point)
        {
            return fix;
        }

        for (var pass = 0; pass < MaxPasses; pass++)
        {
            for (var i = 0; i < lines.Length; i++)
            {
                lines[i] = lines[i] with { BearingDegrees = lines[i].BearingDegrees + Reaim(lines[i], sightsToLandmarks[i].BearingDegrees, point) };
            }

            var refined = Triangulation.Solve(lines);
            if (refined.Point is not { } next)
            {
                break;
            }

            var shift = Geodesy.DistanceMeters(point, next);
            fix = refined;
            point = next;
            if (shift < ConvergedMeters)
            {
                break;
            }
        }

        var residuals = sightsToLandmarks.Select(s => Residual(s, point)).ToList();
        return fix with
        {
            Residuals = residuals,
            RmsOffsetMeters = Math.Sqrt(residuals.Average(r => r.OffsetMeters * r.OffsetMeters)),
            MaxOffsetMeters = residuals.Max(r => r.OffsetMeters),
        };
    }

    // How far to turn the line at the landmark so that, abreast of the fix, it sees the landmark at the sighted azimuth.
    private static double Reaim(BearingObservation line, double sighted, GeoCoordinate fix)
    {
        var landmark = line.Point;
        var distance = Geodesy.DistanceMeters(landmark, fix);
        if (distance < MinimumMeters)
        {
            return 0;
        }

        var along = distance * Math.Cos((Geodesy.InitialBearingDegrees(landmark, fix) - line.BearingDegrees) * Math.PI / 180.0);
        if (Math.Abs(along) < MinimumMeters)
        {
            return 0;
        }

        // Behind the landmark the same line sees it the other way round.
        var foot = along > 0
            ? Geodesy.Destination(landmark, along, line.BearingDegrees)
            : Geodesy.Destination(landmark, -along, line.BearingDegrees + 180.0);
        var wanted = along > 0 ? sighted : sighted + 180.0;
        return SignedAngle(wanted - Geodesy.InitialBearingDegrees(foot, landmark));
    }

    private static ObservationResidual Residual(BearingObservation sight, GeoCoordinate fix)
    {
        var distance = Geodesy.DistanceMeters(fix, sight.Point);
        var actual = distance < MinimumMeters ? sight.BearingDegrees : Geodesy.InitialBearingDegrees(fix, sight.Point);
        var turn = SignedAngle(actual - sight.BearingDegrees);
        var lineError = turn > 90.0 ? turn - 180.0 : turn <= -90.0 ? turn + 180.0 : turn;
        var offset = MeanRadiusMeters * Math.Asin(Math.Min(1.0,
            Math.Abs(Math.Sin(distance / MeanRadiusMeters) * Math.Sin(lineError * Math.PI / 180.0))));

        return new ObservationResidual(sight.Point, distance, actual, offset, lineError, Math.Abs(turn) < 90.0);
    }

    private static double SignedAngle(double degrees)
    {
        var a = ((degrees % 360.0) + 360.0) % 360.0;
        return a > 180.0 ? a - 360.0 : a;
    }
}
