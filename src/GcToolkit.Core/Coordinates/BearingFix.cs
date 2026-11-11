namespace GcToolkit.Core.Coordinates;

/// <summary>A known point and the true bearing of a line through it, degrees clockwise from North.</summary>
public readonly record struct BearingObservation(GeoCoordinate Point, double BearingDegrees);

/// <summary>How one observation agrees with the fix.</summary>
/// <param name="Point">The observation's known point.</param>
/// <param name="DistanceMeters">Geodesic distance from the point to the fix.</param>
/// <param name="BearingToFixDegrees">Geodesic bearing from the point to the fix.</param>
/// <param name="OffsetMeters">How far the observation's line passes from the fix.</param>
/// <param name="BearingErrorDegrees">How far the line would have to turn to hit the fix, in <c>(-90, 90]</c>.</param>
/// <param name="IsAhead">Whether the fix lies in the sighted direction rather than behind it.</param>
public sealed record ObservationResidual(
    GeoCoordinate Point,
    double DistanceMeters,
    double BearingToFixDegrees,
    double OffsetMeters,
    double BearingErrorDegrees,
    bool IsAhead);

/// <summary>The position that best fits two or more bearing lines.</summary>
/// <param name="Status">Whether a fix exists.</param>
/// <param name="Point">The fix: the exact crossing for two lines, the least-squares point for more.</param>
/// <param name="Alternate">The antipodal point, the other crossing of the great circles.</param>
/// <param name="Residuals">One entry per observation, in input order.</param>
/// <param name="RmsOffsetMeters">Root-mean-square distance of the lines from the fix: the accuracy figure.</param>
/// <param name="MaxOffsetMeters">The worst line's distance from the fix.</param>
/// <param name="IsIllConditioned">No two lines cross at more than a degree, so bearing error dominates.</param>
public sealed record BearingFix(
    TriangulationStatus Status,
    GeoCoordinate? Point,
    GeoCoordinate? Alternate,
    IReadOnlyList<ObservationResidual> Residuals,
    double RmsOffsetMeters,
    double MaxOffsetMeters,
    bool IsIllConditioned)
{
    internal static BearingFix Failure(TriangulationStatus status) => new(status, null, null, [], 0, 0, false);
}
