using GcToolkit.Core.Coordinates;

namespace GcToolkit.Core.Tests.Coordinates;

// Reference values: 40-digit numerical quadrature (mpmath) of the WGS84 rhumb integrals
// (isometric latitude + meridian arc); meridian/equator cases cross-checked against GeographicLib geodesics.
[TestClass]
public sealed class RhumbLineTests
{
    [TestMethod]
    [DataRow(40.6, -73.8, 51.6, -0.5, 5771083.38332803, 77.7683897103)]
    [DataRow(10.0, 170.0, 20.0, -170.0, 2416158.75277148, 62.7442555335)]   // antimeridian
    [DataRow(-30.0, 150.0, 45.0, -120.0, 12372060.737136, 47.8344367632)]
    [DataRow(80.0, 10.0, 85.0, 100.0, 1380038.05923296, 66.1336245931)]     // high latitude
    [DataRow(49.2, 16.6, 50.1, 14.4, 187772.782396643, 302.214575065)]
    [DataRow(0.0, 0.0, 0.0, 90.0, 10018754.1713946, 90.0)]                  // equator
    [DataRow(50.0, 10.0, 50.0, 20.0, 716957.53616003, 90.0)]                // due east
    [DataRow(10.0, 5.0, -20.0, 5.0, 3318221.08740601, 180.0)]               // due south
    public void Inverse_ReferencePairs_MatchHighPrecisionValues(double lat1, double lon1, double lat2, double lon2, double meters, double bearing)
    {
        var result = RhumbLine.Inverse(new GeoCoordinate(lat1, lon1), new GeoCoordinate(lat2, lon2));

        Assert.AreEqual(meters, result.DistanceMeters, 0.001);
        Assert.AreEqual(bearing, result.BearingDegrees, 1e-7);
    }

    [TestMethod]
    public void Inverse_DueWest_ReportsBearing270()
    {
        var result = RhumbLine.Inverse(new GeoCoordinate(50, 20), new GeoCoordinate(50, 10));

        Assert.AreEqual(716957.53616003, result.DistanceMeters, 0.001);
        Assert.AreEqual(270.0, result.BearingDegrees, 1e-9);
    }

    [TestMethod]
    public void Inverse_IdenticalPoints_IsZero()
    {
        var p = new GeoCoordinate(12, 34);

        var result = RhumbLine.Inverse(p, p);

        Assert.AreEqual(0.0, result.DistanceMeters, 1e-9);
    }

    [TestMethod]
    public void Inverse_PoleToPoint_RunsAlongMeridian()
    {
        var result = RhumbLine.Inverse(new GeoCoordinate(90, 0), new GeoCoordinate(0, 45));

        Assert.AreEqual(180.0, result.BearingDegrees);
        Assert.AreEqual(10001965.729, result.DistanceMeters, 0.01); // WGS84 meridian quadrant
    }

    [TestMethod]
    public void Inverse_NearPoleShortLeg_IsFiniteAndSensible()
    {
        var result = RhumbLine.Inverse(new GeoCoordinate(89.9999, 0), new GeoCoordinate(89.9999, 90));

        Assert.IsTrue(double.IsFinite(result.DistanceMeters));
        Assert.AreEqual(90.0, result.BearingDegrees, 1e-9);
        Assert.IsTrue(result.DistanceMeters is > 0 and < 20);
    }

    [TestMethod]
    public void Inverse_IsSymmetricInDistance()
    {
        var a = new GeoCoordinate(40.6, -73.8);
        var b = new GeoCoordinate(51.6, -0.5);

        var there = RhumbLine.Inverse(a, b);
        var back = RhumbLine.Inverse(b, a);

        Assert.AreEqual(there.DistanceMeters, back.DistanceMeters, 1e-6);
        Assert.AreEqual((there.BearingDegrees + 180.0) % 360.0, back.BearingDegrees, 1e-7);
    }

    [TestMethod]
    [DataRow(40.0, -70.0, 45.0, 1000000.0, 46.364805256836, -61.289993078311)]
    [DataRow(10.0, 170.0, 90.0, 2000000.0, 10.0, -171.758376501009)]   // east, wraps the antimeridian
    [DataRow(10.0, -170.0, 270.0, 2000000.0, 10.0, 171.758376501009)]    // west, wraps back
    [DataRow(49.2, 16.6, 300.0, 500000.0, 51.447488544753, 10.517953709701)]
    [DataRow(85.0, 0.0, 60.0, 500000.0, 87.238365008464, 58.951903913206)]
    [DataRow(-33.0, 151.0, 200.0, 3000000.0, -58.36400542173, 137.47781552602)]
    public void Direct_ReferencePoints_MatchHighPrecisionValues(double lat, double lon, double bearing, double meters, double expectedLat, double expectedLon)
    {
        var result = RhumbLine.Direct(new GeoCoordinate(lat, lon), bearing, meters);

        Assert.IsFalse(result.ReachedPole);
        Assert.AreEqual(expectedLat, result.Destination.Latitude, 1e-9);
        Assert.AreEqual(expectedLon, result.Destination.Longitude, 1e-9);
    }

    [TestMethod]
    public void Direct_PastThePole_ClampsToThePole()
    {
        var result = RhumbLine.Direct(new GeoCoordinate(80, 10), 0, 5_000_000);

        Assert.IsTrue(result.ReachedPole);
        Assert.AreEqual(90.0, result.Destination.Latitude);
    }

    [TestMethod]
    public void Direct_EastFromThePole_StaysPut()
    {
        var start = new GeoCoordinate(90, 0);

        var result = RhumbLine.Direct(start, 90, 1000);

        Assert.AreEqual(start, result.Destination);
    }

    [TestMethod]
    public void Direct_ThenInverse_RoundTrips()
    {
        var start = new GeoCoordinate(-12.5, 130.25);

        var end = RhumbLine.Direct(start, 123.4, 4_567_890).Destination;
        var back = RhumbLine.Inverse(start, end);

        Assert.AreEqual(4_567_890, back.DistanceMeters, 1e-3);
        Assert.AreEqual(123.4, back.BearingDegrees, 1e-8);
    }
}
