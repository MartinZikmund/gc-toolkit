using GcToolkit.Core.Coordinates;

namespace GcToolkit.Core.Tests.Coordinates;

[TestClass]
public class TriangulationTests
{
    // Reference: Chris Veness, "Calculate distance, bearing and more between Latitude/Longitude points"
    // (movable-type.co.uk/scripts/latlong.html), intersection example:
    // 51.8853N 0.2545E @ 108.55 deg and 49.0034N 2.5735E @ 32.44 deg meet at 50.9078N 4.5084E.
    [TestMethod]
    public void Intersect_VenessReferenceExample_MatchesPublishedPoint()
    {
        var result = Triangulation.Intersect(
            new GeoCoordinate(51.8853, 0.2545), 108.55,
            new GeoCoordinate(49.0034, 2.5735), 32.44);

        Assert.AreEqual(TriangulationStatus.Ok, result.Status);
        Assert.AreEqual(50.9078, result.Primary!.Point.Latitude, 5e-4);
        Assert.AreEqual(4.5084, result.Primary.Point.Longitude, 5e-4);
        Assert.IsTrue(result.Primary.IsAheadOfA);
        Assert.IsTrue(result.Primary.IsAheadOfB);
    }

    // Analytic: two points on the equator 10 deg apart sighting NE / NW meet on the mid-meridian.
    // Napier's rule on the right spherical triangle: tan(lat) = sin(5 deg) * tan(45 deg).
    [TestMethod]
    public void Intersect_SymmetricFromEquator_MatchesAnalyticSolution()
    {
        var result = Triangulation.Intersect(
            new GeoCoordinate(0, 0), 45,
            new GeoCoordinate(0, 10), 315);

        var expectedLat = Math.Atan(Math.Sin(5 * Math.PI / 180)) * 180 / Math.PI;
        Assert.AreEqual(expectedLat, result.Primary!.Point.Latitude, 1e-9);
        Assert.AreEqual(5.0, result.Primary.Point.Longitude, 1e-9);
    }

    [TestMethod]
    public void Intersect_Always_ReturnsAntipodalAlternate()
    {
        var result = Triangulation.Intersect(
            new GeoCoordinate(0, 0), 45,
            new GeoCoordinate(0, 10), 315);

        Assert.AreEqual(-result.Primary!.Point.Latitude, result.Alternate!.Point.Latitude, 1e-9);
        Assert.AreEqual(-175.0, result.Alternate.Point.Longitude, 1e-9);
    }

    [TestMethod]
    public void Intersect_BearingsConverging_PrimaryIsAheadOfBoth()
    {
        // Both sights point south and converge there.
        var result = Triangulation.Intersect(
            new GeoCoordinate(0, 0), 135,
            new GeoCoordinate(0, 10), 225);

        Assert.IsTrue(result.Primary!.Point.Latitude < 0);
        Assert.IsTrue(result.Primary.IsAheadOfA);
        Assert.IsTrue(result.Primary.IsAheadOfB);
    }

    [TestMethod]
    public void Intersect_BearingsDiverging_PrimaryIsNearestAndBehindBoth()
    {
        // The sights fan out northwards; the nearby crossing is south, behind both observers.
        var result = Triangulation.Intersect(
            new GeoCoordinate(0, 0), 315,
            new GeoCoordinate(0, 10), 45);

        Assert.IsTrue(result.Primary!.Point.Latitude < 0);
        Assert.IsFalse(result.Primary.IsAheadOfA);
        Assert.IsFalse(result.Primary.IsAheadOfB);
    }

    [TestMethod]
    public void Intersect_OneBearingBehind_FlagsOnlyThatObserver()
    {
        // A looks NE toward the crossing; B looks SE, away from it.
        var result = Triangulation.Intersect(
            new GeoCoordinate(0, 0), 45,
            new GeoCoordinate(0, 10), 135);

        Assert.IsTrue(result.Primary!.Point.Latitude > 0);
        Assert.IsTrue(result.Primary.IsAheadOfA);
        Assert.IsFalse(result.Primary.IsAheadOfB);
    }

    [TestMethod]
    public void Intersect_SameGreatCircle_ReturnsCoincident()
    {
        var result = Triangulation.Intersect(
            new GeoCoordinate(0, 0), 90,
            new GeoCoordinate(0, 30), 90);

        Assert.AreEqual(TriangulationStatus.Coincident, result.Status);
        Assert.IsNull(result.Primary);
    }

    [TestMethod]
    public void Intersect_ReciprocalBearingsOnSameLine_ReturnsCoincident()
    {
        var result = Triangulation.Intersect(
            new GeoCoordinate(0, 0), 90,
            new GeoCoordinate(0, 30), 270);

        Assert.AreEqual(TriangulationStatus.Coincident, result.Status);
    }

    [TestMethod]
    public void Intersect_IdenticalPoints_ReturnsIdenticalPoints()
    {
        var result = Triangulation.Intersect(
            new GeoCoordinate(50, 14), 10,
            new GeoCoordinate(50, 14), 80);

        Assert.AreEqual(TriangulationStatus.IdenticalPoints, result.Status);
    }

    [TestMethod]
    public void Intersect_NearlyParallelBearings_FlagsIllConditioned()
    {
        var result = Triangulation.Intersect(
            new GeoCoordinate(0, 0), 90,
            new GeoCoordinate(0.05, 20), 90.3);

        Assert.AreEqual(TriangulationStatus.Ok, result.Status);
        Assert.IsTrue(result.IsIllConditioned);
    }

    [TestMethod]
    public void Intersect_WellSeparatedBearings_IsNotIllConditioned()
    {
        var result = Triangulation.Intersect(
            new GeoCoordinate(0, 0), 45,
            new GeoCoordinate(0, 10), 315);

        Assert.IsFalse(result.IsIllConditioned);
    }

    [TestMethod]
    public void Intersect_Meridians_MeetAtThePole()
    {
        var result = Triangulation.Intersect(
            new GeoCoordinate(10, 0), 0,
            new GeoCoordinate(10, 90), 0);

        Assert.AreEqual(90.0, result.Primary!.Point.Latitude, 1e-9);
    }

    [TestMethod]
    public void Intersect_AcrossAntimeridian_NormalizesLongitude()
    {
        var result = Triangulation.Intersect(
            new GeoCoordinate(0, 175), 45,
            new GeoCoordinate(0, -175), 315);

        Assert.AreEqual(180.0, Math.Abs(result.Primary!.Point.Longitude), 1e-9);
    }

    [TestMethod]
    public void Intersect_BearingsOutside0To360_AreNormalized()
    {
        var a = Triangulation.Intersect(new GeoCoordinate(0, 0), 45, new GeoCoordinate(0, 10), 315);
        var b = Triangulation.Intersect(new GeoCoordinate(0, 0), 405, new GeoCoordinate(0, 10), -45);

        Assert.AreEqual(a.Primary!.Point.Latitude, b.Primary!.Point.Latitude, 1e-9);
        Assert.AreEqual(a.Primary.Point.Longitude, b.Primary.Point.Longitude, 1e-9);
    }

    [TestMethod]
    public void Intersect_ReportsDistanceAndBearingFromEachObserver()
    {
        var a = new GeoCoordinate(0, 0);
        var b = new GeoCoordinate(0, 10);
        var result = Triangulation.Intersect(a, 45, b, 315);

        Assert.AreEqual(Geodesy.DistanceMeters(a, result.Primary!.Point), result.Primary.DistanceFromAMeters, 1e-6);
        Assert.AreEqual(Geodesy.DistanceMeters(b, result.Primary.Point), result.Primary.DistanceFromBMeters, 1e-6);
        Assert.AreEqual(Geodesy.DistanceMeters(a, b), result.BaselineMeters, 1e-6);
        Assert.AreEqual(45.0, result.Primary.BearingFromA, 0.2);
    }

    [TestMethod]
    public void Intersect_NonFiniteBearing_ReturnsInvalidInput()
    {
        var result = Triangulation.Intersect(new GeoCoordinate(0, 0), double.NaN, new GeoCoordinate(0, 10), 315);

        Assert.AreEqual(TriangulationStatus.InvalidInput, result.Status);
    }
}
