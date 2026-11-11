using GcToolkit.Core.Coordinates;

namespace GcToolkit.Core.Tests.Coordinates;

[TestClass]
public class ResectionTests
{
    private static readonly GeoCoordinate _observer = new(50.0909, 14.4005);

    private static List<BearingObservation> SightsFrom(GeoCoordinate observer, params GeoCoordinate[] landmarks)
        => [.. landmarks.Select(l => new BearingObservation(l, Geodesy.InitialBearingDegrees(observer, l)))];

    // The fix must reproduce what the user measured: the geodesic azimuth from the fix to each landmark.
    [TestMethod]
    public void Solve_TwoLandmarks_FixSeesEachAtTheSightedBearing()
    {
        var sights = SightsFrom(_observer, new(50.08367, 14.395), new(50.08122, 14.4514));

        var fix = Resection.Solve(sights);

        Assert.AreEqual(TriangulationStatus.Ok, fix.Status);
        Assert.AreEqual(0.0, Geodesy.DistanceMeters(_observer, fix.Point!.Value), 0.01);
        foreach (var s in sights)
        {
            Assert.AreEqual(s.BearingDegrees, Geodesy.InitialBearingDegrees(fix.Point.Value, s.Point), 1e-6);
        }
    }

    [TestMethod]
    public void Solve_ThreeExactSights_RecoverObserverWithZeroResiduals()
    {
        var fix = Resection.Solve(SightsFrom(_observer, new(50.08367, 14.395), new(50.08122, 14.4514), new(50.06458, 14.42083)));

        Assert.AreEqual(0.0, Geodesy.DistanceMeters(_observer, fix.Point!.Value), 0.01);
        Assert.AreEqual(0.0, fix.RmsOffsetMeters, 0.01);
        Assert.IsTrue(fix.Residuals.All(r => r.IsAhead));
    }

    // Long sights at high latitude: meridians converge, so the back bearing at the landmark is not the
    // sighted bearing + 180. Here that naive shortcut would be off by hundreds of metres.
    [TestMethod]
    public void Solve_LongSightsAtHighLatitude_AccountsForMeridianConvergence()
    {
        var observer = new GeoCoordinate(65.0, 25.0);
        var landmarks = new[] { new GeoCoordinate(65.15, 25.6), new GeoCoordinate(64.85, 25.5), new GeoCoordinate(65.05, 24.3) };
        var sights = SightsFrom(observer, landmarks);

        var fix = Resection.Solve(sights);
        var naive = Triangulation.Solve([.. sights.Select(s => s with { BearingDegrees = s.BearingDegrees + 180 })]);

        Assert.AreEqual(0.0, Geodesy.DistanceMeters(observer, fix.Point!.Value), 0.05);
        Assert.IsTrue(Geodesy.DistanceMeters(observer, naive.Point!.Value) > 100);
    }

    [TestMethod]
    public void Solve_OneNoisySight_ReportsMisfitsMeasuredFromTheFix()
    {
        var sights = SightsFrom(_observer, new(50.08367, 14.395), new(50.08122, 14.4514), new(50.06458, 14.42083));
        sights[1] = sights[1] with { BearingDegrees = sights[1].BearingDegrees + 1.5 };

        var fix = Resection.Solve(sights);

        // The fit spreads the error over all lines; each misfit is measured at the fix:
        // actual bearing to the landmark minus the sighted one.
        Assert.IsTrue(fix.RmsOffsetMeters > 1.0);
        for (var i = 0; i < sights.Count; i++)
        {
            var actual = Geodesy.InitialBearingDegrees(fix.Point!.Value, sights[i].Point);
            Assert.AreEqual(actual - sights[i].BearingDegrees, fix.Residuals[i].BearingErrorDegrees, 1e-6);
        }

        Assert.IsTrue(fix.Residuals[1].BearingErrorDegrees < 0, "the over-turned sight reads high");
    }

    [TestMethod]
    public void Solve_ReportsDistanceAndBearingFromTheFixToEachLandmark()
    {
        var landmark = new GeoCoordinate(50.08367, 14.395);
        var fix = Resection.Solve(SightsFrom(_observer, landmark, new(50.08122, 14.4514)));

        Assert.AreEqual(Geodesy.DistanceMeters(_observer, landmark), fix.Residuals[0].DistanceMeters, 0.05);
        Assert.AreEqual(Geodesy.InitialBearingDegrees(fix.Point!.Value, landmark), fix.Residuals[0].MeasuredBearingDegrees, 1e-9);
    }

    [TestMethod]
    public void Solve_SightsAwayFromLandmarks_FlagsThemBehind()
    {
        // Facing away from both landmarks: the lines still cross, but behind the observer's sights.
        var sights = SightsFrom(_observer, new(50.08367, 14.395), new(50.08122, 14.4514))
            .Select(s => s with { BearingDegrees = s.BearingDegrees + 180 })
            .ToList();

        var fix = Resection.Solve(sights);

        Assert.AreEqual(TriangulationStatus.Ok, fix.Status);
        Assert.IsFalse(fix.Residuals[0].IsAhead);
        Assert.IsFalse(fix.Residuals[1].IsAhead);
    }

    [TestMethod]
    public void Solve_AllLandmarksOnOneSightLine_ReturnsCoincident()
    {
        var fix = Resection.Solve([new(new GeoCoordinate(0, 1), 90), new(new GeoCoordinate(0, 2), 90)]);

        Assert.AreEqual(TriangulationStatus.Coincident, fix.Status);
    }

    [TestMethod]
    public void Solve_SingleLandmark_ReturnsTooFewObservations()
    {
        var fix = Resection.Solve([new(new GeoCoordinate(0, 1), 90)]);

        Assert.AreEqual(TriangulationStatus.TooFewObservations, fix.Status);
    }
}
