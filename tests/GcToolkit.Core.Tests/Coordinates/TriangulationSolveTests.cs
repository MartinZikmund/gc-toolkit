using GcToolkit.Core.Coordinates;

namespace GcToolkit.Core.Tests.Coordinates;

[TestClass]
public class TriangulationSolveTests
{
    private static readonly GeoCoordinate _prague = new(50.0875, 14.4214);

    [TestMethod]
    public void Solve_SingleObservation_ReturnsTooFewObservations()
    {
        var fix = Triangulation.Solve([new(new GeoCoordinate(0, 0), 45)]);

        Assert.AreEqual(TriangulationStatus.TooFewObservations, fix.Status);
        Assert.IsNull(fix.Point);
    }

    [TestMethod]
    public void Solve_NonFiniteBearing_ReturnsInvalidInput()
    {
        var fix = Triangulation.Solve([new(new GeoCoordinate(0, 0), 45), new(new GeoCoordinate(0, 1), double.NaN)]);

        Assert.AreEqual(TriangulationStatus.InvalidInput, fix.Status);
    }

    // The fix must satisfy the bearings as the user measures them: geodesic azimuths on WGS84
    // (checked with the Vincenty inverse in Geodesy, itself tested against published values).
    [TestMethod]
    public void Solve_TwoObservations_GeodesicBearingsToFixMatchSighted()
    {
        var a = new GeoCoordinate(50.10, 14.38);
        var b = new GeoCoordinate(50.06, 14.47);
        var fix = Triangulation.Solve([new(a, 120.0), new(b, 10.0)]);

        Assert.AreEqual(TriangulationStatus.Ok, fix.Status);
        Assert.AreEqual(120.0, Geodesy.InitialBearingDegrees(a, fix.Point!.Value), 1e-6);
        Assert.AreEqual(10.0, Geodesy.InitialBearingDegrees(b, fix.Point.Value), 1e-6);
    }

    [TestMethod]
    public void Solve_ThreeExactBearingsToKnownTarget_RecoversTargetWithZeroResiduals()
    {
        GeoCoordinate[] observers = [new(50.11, 14.40), new(50.07, 14.37), new(50.08, 14.48)];
        var fix = Triangulation.Solve(observers.Select(o => new BearingObservation(o, Geodesy.InitialBearingDegrees(o, _prague))).ToList());

        Assert.AreEqual(TriangulationStatus.Ok, fix.Status);
        Assert.AreEqual(0.0, Geodesy.DistanceMeters(_prague, fix.Point!.Value), 0.01);
        Assert.AreEqual(0.0, fix.RmsOffsetMeters, 0.01);
        Assert.AreEqual(3, fix.Residuals.Count);
        Assert.IsTrue(fix.Residuals.All(r => r.IsAhead));
    }

    // An equilateral "triangle of error" near the equator: the least-squares point is its centre,
    // and every line misses it by the inradius = side / (2 * sqrt(3)).
    [TestMethod]
    public void Solve_EquilateralTriangleOfError_FixAtCentreWithInradiusResiduals()
    {
        const double a = 0.01; // half the side, degrees (~1.1 km)
        var v1 = new GeoCoordinate(0, -a);
        var v2 = new GeoCoordinate(0, a);
        var fix = Triangulation.Solve([new(v1, 90), new(v2, 330), new(v1, 30)]);

        var sideMeters = Geodesy.DistanceMeters(v1, v2);
        var inradius = sideMeters / (2 * Math.Sqrt(3));

        Assert.AreEqual(TriangulationStatus.Ok, fix.Status);
        var centre = Geodesy.Destination(new GeoCoordinate(0, 0), inradius, 0);
        Assert.AreEqual(0.0, fix.Point!.Value.Longitude, 1e-9);
        // The fit runs on the sphere, so its metric is off from WGS84 by under 1% of the misfit size.
        Assert.AreEqual(0.0, Geodesy.DistanceMeters(centre, fix.Point.Value), inradius * 0.01);
        foreach (var r in fix.Residuals)
        {
            Assert.AreEqual(inradius, r.OffsetMeters, inradius * 0.01);
        }

        Assert.AreEqual(inradius, fix.RmsOffsetMeters, inradius * 0.01);
        Assert.AreEqual(inradius, fix.MaxOffsetMeters, inradius * 0.01);
    }

    [TestMethod]
    public void Solve_OneNoisyBearing_ReportsItsMisfitInDegrees()
    {
        GeoCoordinate[] observers = [new(50.11, 14.40), new(50.07, 14.37), new(50.08, 14.48)];
        var observations = observers.Select(o => new BearingObservation(o, Geodesy.InitialBearingDegrees(o, _prague))).ToList();
        observations[2] = observations[2] with { BearingDegrees = observations[2].BearingDegrees + 2.0 };

        var fix = Triangulation.Solve(observations);

        Assert.AreEqual(TriangulationStatus.Ok, fix.Status);
        Assert.IsTrue(fix.RmsOffsetMeters > 1.0);
        var worst = fix.Residuals.OrderByDescending(r => Math.Abs(r.BearingErrorDegrees)).First();
        Assert.AreSame(fix.Residuals[2], worst);
        Assert.IsTrue(Math.Abs(worst.BearingErrorDegrees) is > 0.5 and < 2.0);
    }

    [TestMethod]
    public void Solve_AllOnOneGreatCircle_ReturnsCoincident()
    {
        var fix = Triangulation.Solve([
            new(new GeoCoordinate(0, 0), 90),
            new(new GeoCoordinate(0, 10), 270),
            new(new GeoCoordinate(0, 20), 90)]);

        Assert.AreEqual(TriangulationStatus.Coincident, fix.Status);
    }

    [TestMethod]
    public void Solve_TwoOfThreeLinesCoincident_StillSolvesOnTheThird()
    {
        var fix = Triangulation.Solve([
            new(new GeoCoordinate(0, 0), 90),
            new(new GeoCoordinate(0, 1), 90),
            new(new GeoCoordinate(-1, 0.5), 0)]);

        Assert.AreEqual(TriangulationStatus.Ok, fix.Status);
        Assert.AreEqual(0.0, fix.Point!.Value.Latitude, 1e-6);
        Assert.AreEqual(0.5, fix.Point.Value.Longitude, 1e-6);
    }

    [TestMethod]
    public void Solve_AllObserversInOnePlace_ReturnsIdenticalPoints()
    {
        var fix = Triangulation.Solve([new(_prague, 10), new(_prague, 80), new(_prague, 200)]);

        Assert.AreEqual(TriangulationStatus.IdenticalPoints, fix.Status);
    }

    [TestMethod]
    public void Solve_NearlyParallelLines_FlagsIllConditioned()
    {
        var fix = Triangulation.Solve([
            new(new GeoCoordinate(0, 0), 90),
            new(new GeoCoordinate(0.05, 0), 90.3),
            new(new GeoCoordinate(0.1, 0), 90.5)]);

        Assert.AreEqual(TriangulationStatus.Ok, fix.Status);
        Assert.IsTrue(fix.IsIllConditioned);
    }

    [TestMethod]
    public void Solve_WellSeparated_IsNotIllConditioned()
    {
        var fix = Triangulation.Solve([new(new GeoCoordinate(0, 0), 45), new(new GeoCoordinate(0, 10), 315)]);

        Assert.IsFalse(fix.IsIllConditioned);
    }

    [TestMethod]
    public void Solve_DivergingSights_FlagsObserversBehind()
    {
        var fix = Triangulation.Solve([new(new GeoCoordinate(0, 0), 315), new(new GeoCoordinate(0, 10), 45)]);

        Assert.IsTrue(fix.Point!.Value.Latitude < 0);
        Assert.IsFalse(fix.Residuals[0].IsAhead);
        Assert.IsFalse(fix.Residuals[1].IsAhead);
    }

    [TestMethod]
    public void Solve_Always_ReturnsAntipodalAlternate()
    {
        var fix = Triangulation.Solve([new(new GeoCoordinate(0, 0), 45), new(new GeoCoordinate(0, 10), 315)]);

        Assert.AreEqual(-fix.Point!.Value.Latitude, fix.Alternate!.Value.Latitude, 1e-6);
        Assert.AreEqual(fix.Point.Value.Longitude - 180.0, fix.Alternate.Value.Longitude, 1e-6);
    }

    [TestMethod]
    public void Solve_ReportsDistanceAndBearingFromEachObserver()
    {
        var a = new GeoCoordinate(0, 0);
        var fix = Triangulation.Solve([new(a, 45), new(new GeoCoordinate(0, 10), 315)]);

        Assert.AreEqual(Geodesy.DistanceMeters(a, fix.Point!.Value), fix.Residuals[0].DistanceMeters, 1e-6);
        Assert.AreEqual(45.0, fix.Residuals[0].MeasuredBearingDegrees, 1e-6);
    }
}
