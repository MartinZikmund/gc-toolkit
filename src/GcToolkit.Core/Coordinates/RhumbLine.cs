namespace GcToolkit.Core.Coordinates;

/// <summary>Distance and constant bearing along a rhumb line between two points.</summary>
public readonly record struct RhumbInverseResult(double DistanceMeters, double BearingDegrees);

/// <summary>Where a rhumb line ends up; <see cref="ReachedPole"/> means the distance ran past the pole and was clamped to it.</summary>
public readonly record struct RhumbDirectResult(GeoCoordinate Destination, bool ReachedPole);

/// <summary>
/// Rhumb lines (loxodromes) on the WGS84 ellipsoid. Longitude comes from the isometric latitude
/// (exact) and distance from the meridian arc (4th-order series in the third flattening, sub-micrometre
/// error), so the numbers stay accurate over thousands of kilometres.
/// </summary>
public static class RhumbLine
{
    private const double A = Wgs84.A;
    private const double E2 = Wgs84.E2;
    private const double N3 = Wgs84.F / (2.0 - Wgs84.F);
    private const double HalfPi = Math.PI / 2.0;
    private const double SmallLatitudeStep = 1e-7; // rad, ~0.6 m: treat as an east-west leg

    /// <summary>Shortest rhumb line from <paramref name="a"/> to <paramref name="b"/> (crosses the antimeridian when shorter).</summary>
    public static RhumbInverseResult Inverse(GeoCoordinate a, GeoCoordinate b)
    {
        var phi1 = Rad(a.Latitude);
        var phi2 = Rad(b.Latitude);
        var deltaLambda = Rad(NormalizeDelta(b.Longitude - a.Longitude));

        if (Math.Abs(phi1) == HalfPi || Math.Abs(phi2) == HalfPi)
        {
            // A pole has no longitude: the line runs along a meridian.
            var bearing = phi2 >= phi1 ? 0.0 : 180.0;
            return new RhumbInverseResult(Math.Abs(MeridianArc(phi2) - MeridianArc(phi1)), bearing);
        }

        var deltaPsi = IsometricLatitude(phi2) - IsometricLatitude(phi1);
        var alpha = Math.Atan2(deltaLambda, deltaPsi);
        var north = MeridianArc(phi2) - MeridianArc(phi1);

        double distance;
        if (Math.Abs(phi2 - phi1) < SmallLatitudeStep)
        {
            var phiMid = (phi1 + phi2) / 2.0;
            var east = deltaLambda * PrimeVerticalRadius(phiMid) * Math.Cos(phiMid);
            distance = Math.Sqrt(north * north + east * east);
        }
        else
        {
            distance = north / Math.Cos(alpha);
        }

        return new RhumbInverseResult(Math.Abs(distance), Normalize360(Deg(alpha)));
    }

    /// <summary>Destination after <paramref name="distanceMeters"/> on a constant <paramref name="bearingDegrees"/>.</summary>
    public static RhumbDirectResult Direct(GeoCoordinate start, double bearingDegrees, double distanceMeters)
    {
        var phi1 = Rad(start.Latitude);
        var alpha = Rad(bearingDegrees);
        var cosAlpha = Math.Cos(alpha);

        if (Math.Abs(cosAlpha) < 1e-12)
        {
            // Due east/west: stay on the parallel (degenerate at a pole, where the circle has no length).
            var radius = PrimeVerticalRadius(phi1) * Math.Cos(phi1);
            if (radius < 1e-3)
            {
                return new RhumbDirectResult(start, false);
            }

            var lon = start.Longitude + Deg(Math.Sign(Math.Sin(alpha)) * distanceMeters / radius);
            return new RhumbDirectResult(new GeoCoordinate(start.Latitude, NormalizeLongitude(lon)), false);
        }

        var target = MeridianArc(phi1) + distanceMeters * cosAlpha;
        if (Math.Abs(target) >= MeridianArc(HalfPi))
        {
            return new RhumbDirectResult(new GeoCoordinate(Math.Sign(target) * 90.0, start.Longitude), true);
        }

        var phi2 = InverseMeridianArc(target, phi1);
        var deltaLambda = Math.Tan(alpha) * (IsometricLatitude(phi2) - IsometricLatitude(phi1));
        return new RhumbDirectResult(
            new GeoCoordinate(Deg(phi2), NormalizeLongitude(start.Longitude + Deg(deltaLambda))),
            false);
    }

    private static double IsometricLatitude(double phi)
    {
        var e = Math.Sqrt(E2);
        return Math.Asinh(Math.Tan(phi)) - e * Math.Atanh(e * Math.Sin(phi));
    }

    private static double PrimeVerticalRadius(double phi)
        => A / Math.Sqrt(1.0 - E2 * Math.Sin(phi) * Math.Sin(phi));

    private static double MeridianRadius(double phi)
    {
        var w = 1.0 - E2 * Math.Sin(phi) * Math.Sin(phi);
        return A * (1.0 - E2) / (w * Math.Sqrt(w));
    }

    // Distance along the meridian from the equator to latitude phi.
    private static double MeridianArc(double phi)
    {
        var n2 = N3 * N3;
        var n3 = n2 * N3;
        var n4 = n3 * N3;
        return A / (1.0 + N3) * (
            (1.0 + n2 / 4.0 + n4 / 64.0) * phi
            - 1.5 * (N3 - n3 / 8.0) * Math.Sin(2.0 * phi)
            + 15.0 / 16.0 * (n2 - n4 / 4.0) * Math.Sin(4.0 * phi)
            - 35.0 / 48.0 * n3 * Math.Sin(6.0 * phi)
            + 315.0 / 512.0 * n4 * Math.Sin(8.0 * phi));
    }

    private static double InverseMeridianArc(double arc, double guess)
    {
        var phi = guess;
        for (var i = 0; i < 20; i++)
        {
            var step = (MeridianArc(phi) - arc) / MeridianRadius(phi);
            phi -= step;
            if (Math.Abs(step) < 1e-15)
            {
                break;
            }
        }

        return phi;
    }

    private static double Rad(double degrees) => degrees * Math.PI / 180.0;

    private static double Deg(double radians) => radians * 180.0 / Math.PI;

    private static double Normalize360(double degrees) => (degrees % 360.0 + 360.0) % 360.0;

    // Wraps a longitude difference into (-180, 180] so the short way round wins.
    private static double NormalizeDelta(double degrees)
    {
        var d = (degrees % 360.0 + 360.0) % 360.0;
        return d > 180.0 ? d - 360.0 : d;
    }

    private static double NormalizeLongitude(double lon) => (lon % 360.0 + 540.0) % 360.0 - 180.0;
}
