using GcToolkit.Core.Coordinates;
using GcToolkit.Core.FavoriteTools;
using GcToolkit.Core.Recents;
using GcToolkit.Core.Tests.Fakes;
using GcToolkit.Core.ViewModels.Tools;

namespace GcToolkit.Core.Tests.Coordinates;

[TestClass]
public class ResectionViewModelTests
{
    private static ResectionViewModel CreateViewModel()
    {
        var catalog = new StubCatalogService("Resection");
        var preferences = new InMemoryPreferences();

        return new ResectionViewModel(
            catalog,
            new RecentsService(preferences, catalog),
            new FavoriteToolsService(preferences, catalog),
            new FakeStringLocalizer(new Dictionary<string, string>
            {
                ["ResectionPointHeader"] = "Landmark {0}",
                ["ResectionWarnBehind"] = "away from {0}",
            }),
            new FakeClipboardService(),
            new FakeShareService());
    }

    [TestMethod]
    public void BlankForm_LabelsRowsAsLandmarks()
    {
        var vm = CreateViewModel();

        Assert.AreEqual("Landmark A", vm.Rows[0].CoordinateHeader);
        Assert.IsFalse(vm.HasResult);
    }

    [TestMethod]
    public void Example_FixesTheObserverAtPragueCastle()
    {
        var vm = CreateViewModel();
        vm.LoadExampleCommand.Execute(null);

        Assert.AreEqual(3, vm.Rows.Count);
        Assert.IsTrue(vm.HasResult);
        Assert.IsTrue(vm.HasAccuracy);
        Assert.IsTrue(CoordinateParser.TryParse(vm.DecimalDegrees, out var fix, out _));
        Assert.IsTrue(Geodesy.DistanceMeters(fix, new GeoCoordinate(50.0909, 14.4005)) < 50);
    }

    [TestMethod]
    public void Plot_DrawsBackBearingsFromLandmarksThroughTheFix()
    {
        var vm = CreateViewModel();
        vm.LoadExampleCommand.Execute(null);

        var plot = vm.Plot!;
        var fix = plot.Fix!.Value;
        foreach (var ray in plot.Rays)
        {
            // Back bearing: the ray heads from the landmark towards the observer.
            var toFix = (X: fix.X - ray.Start.X, Y: fix.Y - ray.Start.Y);
            var along = (X: ray.End.X - ray.Start.X, Y: ray.End.Y - ray.Start.Y);
            Assert.IsTrue(toFix.X * along.X + toFix.Y * along.Y > 0);
        }
    }

    [TestMethod]
    public void SightsPointingAway_WarnPerLandmark()
    {
        var vm = CreateViewModel();
        vm.Rows[0].Coordinate = "N 50 05.020 E 014 23.700";
        vm.Rows[0].Bearing = "26.0";
        vm.Rows[1].Coordinate = "N 50 04.873 E 014 27.084";
        vm.Rows[1].Bearing = "286.5";

        Assert.IsTrue(vm.HasResult);
        StringAssert.Contains(vm.WarningMessage, "away from A");
        StringAssert.Contains(vm.WarningMessage, "away from B");
    }

    [TestMethod]
    public void ReadoutBearing_IsFromTheFixToTheLandmark()
    {
        var vm = CreateViewModel();
        vm.LoadExampleCommand.Execute(null);

        // Petřín lies south-west of the castle.
        var bearing = double.Parse(vm.Rows[0].MeasuredBearingText.TrimEnd('°').Replace(',', '.'), System.Globalization.CultureInfo.InvariantCulture);
        Assert.AreEqual(206.0, bearing, 1.0);
    }
}
