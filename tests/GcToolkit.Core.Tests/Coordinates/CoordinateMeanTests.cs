using GcToolkit.Core.Coordinates;

namespace GcToolkit.Core.Tests.Coordinates;

[TestClass]
public class CoordinateMeanTests
{
    private static readonly GeoCoordinate Brno = new(49.19522, 16.60796);
    private static readonly GeoCoordinate Prague = new(50.08804, 14.42076);

    // ---- Single point ----

    [TestMethod]
    public void Cartesian_SinglePoint_ReturnsThatPoint()
    {
        var mean = CoordinateMean.Cartesian([Brno]);

        Assert.IsNotNull(mean);
        Assert.AreEqual(Brno.Latitude, mean.Value.Latitude, 1e-9);
        Assert.AreEqual(Brno.Longitude, mean.Value.Longitude, 1e-9);
    }

    [TestMethod]
    public void Arithmetic_SinglePoint_ReturnsThatPoint()
    {
        var mean = CoordinateMean.Arithmetic([Brno]);

        Assert.IsNotNull(mean);
        Assert.AreEqual(Brno.Latitude, mean.Value.Latitude, 1e-12);
        Assert.AreEqual(Brno.Longitude, mean.Value.Longitude, 1e-12);
    }

    // ---- Two points: the mean IS the great-circle midpoint ----

    [TestMethod]
    public void Cartesian_TwoPoints_MatchesGeodesyMidpoint()
    {
        var expected = Geodesy.Midpoint(Brno, Prague);
        var mean = CoordinateMean.Cartesian([Brno, Prague]);

        Assert.IsNotNull(mean);
        Assert.AreEqual(expected.Latitude, mean.Value.Latitude, 1e-9);
        Assert.AreEqual(expected.Longitude, mean.Value.Longitude, 1e-9);
    }

    [TestMethod]
    public void Cartesian_OrderOfPoints_DoesNotChangeTheMean()
    {
        var forward = CoordinateMean.Cartesian([Brno, Prague, new GeoCoordinate(48.5, 17.5)]);
        var reversed = CoordinateMean.Cartesian([new GeoCoordinate(48.5, 17.5), Prague, Brno]);

        Assert.IsNotNull(forward);
        Assert.IsNotNull(reversed);
        Assert.AreEqual(forward.Value.Latitude, reversed.Value.Latitude, 1e-12);
        Assert.AreEqual(forward.Value.Longitude, reversed.Value.Longitude, 1e-12);
    }

    // ---- Antimeridian: the two means genuinely disagree ----

    [TestMethod]
    public void Cartesian_StraddlingTheAntimeridian_StaysOnTheShortArc()
    {
        // 179°E and 177°W are 4° apart across the date line; their midpoint is 181°E = 179°W.
        var mean = CoordinateMean.Cartesian([new GeoCoordinate(45.0, 179.0), new GeoCoordinate(45.0, -177.0)]);

        Assert.IsNotNull(mean);
        Assert.AreEqual(-179.0, mean.Value.Longitude, 1e-9);

        // A great circle between two points at equal latitude bulges poleward, so the mean sits
        // slightly north of 45° — the same behaviour Geodesy.Midpoint shows.
        Assert.IsTrue(mean.Value.Latitude > 45.0, $"latitude was {mean.Value.Latitude}");
    }

    [TestMethod]
    public void Arithmetic_StraddlingTheAntimeridian_LandsOnTheWrongSideOfTheWorld()
    {
        // (179 + -177) / 2 = 1: averaging the raw degrees walks the long way round the globe.
        var mean = CoordinateMean.Arithmetic([new GeoCoordinate(45.0, 179.0), new GeoCoordinate(45.0, -177.0)]);

        Assert.IsNotNull(mean);
        Assert.AreEqual(45.0, mean.Value.Latitude, 1e-12);
        Assert.AreEqual(1.0, mean.Value.Longitude, 1e-12);
    }

    [TestMethod]
    public void TryCompute_StraddlingTheAntimeridian_ReportsTheDivergenceBetweenBothMeans()
    {
        var ok = CoordinateMean.TryCompute(
            [new GeoCoordinate(45.0, 179.0), new GeoCoordinate(45.0, -177.0)],
            out var result);

        Assert.IsTrue(ok);

        // 179°W -> 1°E at 45°N runs over the pole: a quarter of the globe, ~10 000 km apart.
        Assert.IsTrue(result.DivergenceMeters > 9_000_000, $"divergence was {result.DivergenceMeters}");
    }

    [TestMethod]
    public void Cartesian_ExactlyOnTheAntimeridian_ReturnsThe180thMeridian()
    {
        var mean = CoordinateMean.Cartesian([new GeoCoordinate(0.0, 179.0), new GeoCoordinate(0.0, -179.0)]);

        Assert.IsNotNull(mean);
        Assert.AreEqual(0.0, mean.Value.Latitude, 1e-9);

        // +180 and -180 name the same meridian; only the magnitude is meaningful here.
        Assert.AreEqual(180.0, Math.Abs(mean.Value.Longitude), 1e-9);
    }

    // ---- Poles ----

    [TestMethod]
    public void Cartesian_RingAroundTheNorthPole_ReturnsThePole()
    {
        GeoCoordinate[] ring =
        [
            new(89.9, 0.0),
            new(89.9, 90.0),
            new(89.9, 180.0),
            new(89.9, -90.0),
        ];

        var mean = CoordinateMean.Cartesian(ring);

        Assert.IsNotNull(mean);

        // Longitude is undefined at the pole, so only the latitude is asserted.
        Assert.AreEqual(90.0, mean.Value.Latitude, 1e-9);
    }

    [TestMethod]
    public void Arithmetic_RingAroundTheNorthPole_MissesThePoleByKilometres()
    {
        GeoCoordinate[] ring =
        [
            new(89.9, 0.0),
            new(89.9, 90.0),
            new(89.9, 180.0),
            new(89.9, -90.0),
        ];

        var mean = CoordinateMean.Arithmetic(ring);

        Assert.IsNotNull(mean);
        Assert.AreEqual(89.9, mean.Value.Latitude, 1e-12);
        Assert.AreEqual(45.0, mean.Value.Longitude, 1e-12);

        // 0.1° of latitude short of the pole: about 11 km of error the 3D mean does not make.
        var offBy = Geodesy.DistanceMeters(new GeoCoordinate(90.0, 0.0), mean.Value);
        Assert.IsTrue(offBy > 10_000, $"arithmetic mean was only {offBy} m from the pole");
    }

    // ---- Degenerate and empty input ----

    [TestMethod]
    public void Cartesian_EmptyInput_ReturnsNull()
        => Assert.IsNull(CoordinateMean.Cartesian([]));

    [TestMethod]
    public void Arithmetic_EmptyInput_ReturnsNull()
        => Assert.IsNull(CoordinateMean.Arithmetic([]));

    [TestMethod]
    public void TryCompute_EmptyInput_ReturnsFalse()
        => Assert.IsFalse(CoordinateMean.TryCompute([], out _));

    [TestMethod]
    public void Cartesian_AntipodalPair_HasNoMeanDirection()
    {
        // The two unit vectors cancel exactly; every direction is equally "central".
        Assert.IsNull(CoordinateMean.Cartesian([new GeoCoordinate(0.0, 0.0), new GeoCoordinate(0.0, 180.0)]));
    }

    // ---- Dispersion ----

    [TestMethod]
    public void Dispersion_ReadingsAroundAPoint_ReportsFarthestAndAverageDistance()
    {
        var centre = new GeoCoordinate(49.2, 16.6);
        GeoCoordinate[] readings =
        [
            Geodesy.Destination(centre, 20.0, 0.0),
            Geodesy.Destination(centre, 20.0, 180.0),
            Geodesy.Destination(centre, 5.0, 90.0),
            Geodesy.Destination(centre, 5.0, 270.0),
        ];

        var (max, mean) = CoordinateMean.Dispersion(centre, readings);

        Assert.AreEqual(20.0, max, 0.01);
        Assert.AreEqual(12.5, mean, 0.01);
    }

    [TestMethod]
    public void Dispersion_NoPoints_IsZero()
    {
        var (max, mean) = CoordinateMean.Dispersion(Brno, []);

        Assert.AreEqual(0.0, max, 1e-12);
        Assert.AreEqual(0.0, mean, 1e-12);
    }

    [TestMethod]
    public void TryCompute_SymmetricReadings_MeanSitsOnTheOriginalPoint()
    {
        var centre = new GeoCoordinate(49.2, 16.6);
        GeoCoordinate[] readings =
        [
            Geodesy.Destination(centre, 20.0, 0.0),
            Geodesy.Destination(centre, 20.0, 180.0),
            Geodesy.Destination(centre, 5.0, 90.0),
            Geodesy.Destination(centre, 5.0, 270.0),
        ];

        var ok = CoordinateMean.TryCompute(readings, out var result);

        Assert.IsTrue(ok);
        Assert.AreEqual(4, result.Count);
        Assert.IsTrue(Geodesy.DistanceMeters(centre, result.CartesianMean) < 0.5);
        Assert.AreEqual(20.0, result.MaxDistanceMeters, 0.5);
        Assert.AreEqual(12.5, result.MeanDistanceMeters, 0.5);

        // Far from the date line the two means agree to well under a centimetre.
        Assert.IsTrue(result.DivergenceMeters < 0.01, $"divergence was {result.DivergenceMeters}");
    }

    // ---- Multi-line parsing ----

    [TestMethod]
    public void ParseLines_MixedNotations_ParsesEveryValidLine()
    {
        var result = CoordinateMean.ParseLines("N 49 12.345 E 016 34.567\r\n49.2 16.6\n33U 614803 5451524");

        Assert.AreEqual(3, result.Points.Count);
        Assert.AreEqual(0, result.Errors.Count);
    }

    [TestMethod]
    public void ParseLines_BareCarriageReturns_SeparateLines()
    {
        var result = CoordinateMean.ParseLines("49.2 16.6\r50.0 15.0\rnope");

        Assert.AreEqual(2, result.Points.Count);
        Assert.AreEqual(3, result.Errors.Single().LineNumber);
    }

    [TestMethod]
    public void ParseLines_UnreadableLine_IsReportedWithItsLineNumber()
    {
        var result = CoordinateMean.ParseLines("49.2 16.6\n\nnot a coordinate\n50.0 15.0");

        Assert.AreEqual(2, result.Points.Count);
        Assert.AreEqual(1, result.Errors.Count);
        Assert.AreEqual(3, result.Errors[0].LineNumber);
        Assert.AreEqual("not a coordinate", result.Errors[0].Text);
    }

    [TestMethod]
    public void ParseLines_BlankInput_YieldsNothingAndNoErrors()
    {
        var result = CoordinateMean.ParseLines("   \n\n  ");

        Assert.AreEqual(0, result.Points.Count);
        Assert.AreEqual(0, result.Errors.Count);
    }

    [TestMethod]
    public void ParseLines_Null_YieldsNothingAndNoErrors()
    {
        var result = CoordinateMean.ParseLines(null);

        Assert.AreEqual(0, result.Points.Count);
        Assert.AreEqual(0, result.Errors.Count);
    }
}
