using GcToolkit.Core.Coordinates;
using GcToolkit.Core.Coordinates.Magnetic;
using GcToolkit.Core.FavoriteTools;
using GcToolkit.Core.Recents;
using GcToolkit.Core.Tests.Fakes;
using GcToolkit.Core.ViewModels.Tools;

namespace GcToolkit.Core.Tests.Coordinates;

[TestClass]
public class TriangulationViewModelTests
{
    private static readonly Dictionary<string, string> _strings = new(StringComparer.Ordinal)
    {
        ["TriangulationErrorPoint"] = "bad point {0}",
        ["TriangulationErrorBearing"] = "bad bearing {0}",
        ["TriangulationWarnBehind"] = "behind {0}",
        ["TriangulationPointHeader"] = "Point {0}",
        ["BearingFixDeclinationApplied"] = "{0} -> {1}",
        ["BearingFixAccuracyFormat"] = "rms {0} max {1}",
        ["BearingFixOffsetFormat"] = "{0} ({1:0.0})",
    };

    private static TriangulationViewModel CreateViewModel(FakeClipboardService? clipboard = null)
    {
        var catalog = new StubCatalogService("Triangulation");
        var preferences = new InMemoryPreferences();

        return new TriangulationViewModel(
            catalog,
            new RecentsService(preferences, catalog),
            new FavoriteToolsService(preferences, catalog),
            new FakeStringLocalizer(_strings),
            clipboard ?? new FakeClipboardService(),
            new FakeShareService());
    }

    // Veness' published intersection example, in DDM.
    private static void FillTwoPoints(TriangulationViewModel vm, string bearingA = "108.55", string bearingB = "32.44")
    {
        vm.Rows[0].Coordinate = "N 51 53.118 E 000 15.270";
        vm.Rows[0].Bearing = bearingA;
        vm.Rows[1].Coordinate = "N 49 00.204 E 002 34.410";
        vm.Rows[1].Bearing = bearingB;
    }

    [TestMethod]
    public void BlankForm_HasTwoLabelledRowsAndNoResultOrError()
    {
        var vm = CreateViewModel();

        Assert.AreEqual(2, vm.Rows.Count);
        Assert.AreEqual("A", vm.Rows[0].Label);
        Assert.AreEqual("Point B", vm.Rows[1].CoordinateHeader);
        Assert.IsFalse(vm.HasResult);
        Assert.IsFalse(vm.HasError);
        Assert.IsFalse(vm.HasPlot);
    }

    [TestMethod]
    public void TwoValidRows_ProduceResultWithBaselineAndNoAccuracy()
    {
        var vm = CreateViewModel();
        FillTwoPoints(vm);

        Assert.IsTrue(vm.HasResult);
        Assert.IsFalse(vm.HasError);
        StringAssert.StartsWith(vm.DegreesDecimalMinutes, "N 50");
        Assert.IsTrue(vm.HasAlternate);
        Assert.IsTrue(vm.HasBaseline);
        Assert.IsFalse(vm.HasAccuracy);
        Assert.AreNotEqual(string.Empty, vm.Rows[0].DistanceText);
    }

    [TestMethod]
    public void InvalidCoordinate_NamesTheRowAndClearsResult()
    {
        var vm = CreateViewModel();
        FillTwoPoints(vm);
        vm.Rows[0].Coordinate = "garbage";

        Assert.IsTrue(vm.HasError);
        Assert.IsFalse(vm.HasResult);
        Assert.AreEqual("bad point A", vm.ErrorMessage);
    }

    [TestMethod]
    public void NonNumericBearing_NamesTheRow()
    {
        var vm = CreateViewModel();
        FillTwoPoints(vm, bearingB: "north-ish");

        Assert.AreEqual("bad bearing B", vm.ErrorMessage);
    }

    [TestMethod]
    public void OnlyOneRowFilled_AsksForMore()
    {
        var vm = CreateViewModel();
        vm.Rows[0].Coordinate = "N 50 05.020 E 014 23.700";
        vm.Rows[0].Bearing = "26.5";

        Assert.AreEqual("TriangulationErrorTooFew", vm.ErrorMessage);
        Assert.IsTrue(vm.HasPlot, "the single parsed row is still drawn");
        Assert.IsNull(vm.Plot!.Fix);
    }

    [TestMethod]
    public void SameLine_SetsCoincidentError()
    {
        var vm = CreateViewModel();
        vm.Rows[0].Coordinate = "N 0 0.000 E 0 0.000";
        vm.Rows[0].Bearing = "90";
        vm.Rows[1].Coordinate = "N 0 0.000 E 30 0.000";
        vm.Rows[1].Bearing = "90";

        Assert.AreEqual("TriangulationErrorCoincident", vm.ErrorMessage);
    }

    [TestMethod]
    public void DivergingBearings_WarnAboutBehindPerRow()
    {
        var vm = CreateViewModel();
        vm.Rows[0].Coordinate = "N 0 0.000 E 0 0.000";
        vm.Rows[0].Bearing = "315";
        vm.Rows[1].Coordinate = "N 0 0.000 E 10 0.000";
        vm.Rows[1].Bearing = "45";

        Assert.IsTrue(vm.HasResult);
        StringAssert.Contains(vm.WarningMessage, "behind A");
        StringAssert.Contains(vm.WarningMessage, "behind B");
    }

    [TestMethod]
    public void AddRow_AppendsNextLetterAndEnablesRemove()
    {
        var vm = CreateViewModel();
        Assert.IsFalse(vm.Rows[0].RemoveCommand.CanExecute(null));

        vm.AddRowCommand.Execute(null);

        Assert.AreEqual(3, vm.Rows.Count);
        Assert.AreEqual("C", vm.Rows[2].Label);
        Assert.IsTrue(vm.Rows.All(r => r.RemoveCommand.CanExecute(null)));
    }

    [TestMethod]
    public void RemoveRow_RelabelsAndKeepsAtLeastTwo()
    {
        var vm = CreateViewModel();
        vm.AddRowCommand.Execute(null);
        vm.Rows[2].Coordinate = "N 1 0.000 E 1 0.000";

        vm.Rows[0].RemoveCommand.Execute(null);

        Assert.AreEqual(2, vm.Rows.Count);
        Assert.AreEqual("B", vm.Rows[1].Label);
        Assert.AreEqual("N 1 0.000 E 1 0.000", vm.Rows[1].Coordinate);
        Assert.IsFalse(vm.Rows[0].RemoveCommand.CanExecute(null));
    }

    [TestMethod]
    public void BlankExtraRow_IsIgnored()
    {
        var vm = CreateViewModel();
        FillTwoPoints(vm);
        vm.AddRowCommand.Execute(null);

        Assert.IsTrue(vm.HasResult);
        Assert.IsFalse(vm.HasError);
    }

    [TestMethod]
    public void ThreeRows_ReportAccuracyAndPerRowOffsets()
    {
        var vm = CreateViewModel();
        vm.LoadExampleCommand.Execute(null);

        Assert.AreEqual(3, vm.Rows.Count);
        Assert.IsTrue(vm.HasResult);
        Assert.IsTrue(vm.HasAccuracy);
        Assert.IsFalse(vm.HasBaseline);
        StringAssert.StartsWith(vm.AccuracyText, "rms ");
        Assert.IsTrue(vm.Rows.All(r => r.OffsetText.Length > 0));
    }

    [TestMethod]
    public void Example_FixLandsNearPragueCastle()
    {
        var vm = CreateViewModel();
        vm.LoadExampleCommand.Execute(null);

        Assert.IsTrue(CoordinateParser.TryParse(vm.DecimalDegrees, out var fix, out _));
        Assert.IsTrue(Geodesy.DistanceMeters(fix, new GeoCoordinate(50.0909, 14.4005)) < 50);
    }

    [TestMethod]
    public void Result_IsPlottedWithFixAndOneMarkerPerRow()
    {
        var vm = CreateViewModel();
        vm.LoadExampleCommand.Execute(null);

        Assert.IsTrue(vm.HasPlot);
        Assert.AreEqual(3, vm.Plot!.Markers.Count);
        Assert.IsNotNull(vm.Plot.Fix);
        Assert.IsTrue(vm.Plot.FixRadiusMeters > 0);
    }

    [TestMethod]
    public void ManualDeclination_AddsToEveryBearing()
    {
        var magnetic = CreateViewModel();
        FillTwoPoints(magnetic, "106.55", "30.44");
        magnetic.BearingsAreMagnetic = true;
        magnetic.UseManualDeclination = true;
        magnetic.ManualDeclination = "2E";

        var truth = CreateViewModel();
        FillTwoPoints(truth);

        Assert.AreEqual(truth.DegreesDecimalMinutes, magnetic.DegreesDecimalMinutes);
        Assert.IsTrue(magnetic.Rows[0].HasDeclination);
        Assert.AreEqual("2.0° E -> 108.6°", magnetic.Rows[0].DeclinationText.Replace(',', '.'));
    }

    [TestMethod]
    [DataRow("3W", -3.0)]
    [DataRow("-3", -3.0)]
    [DataRow("4.5 E", 4.5)]
    [DataRow("4,5°", 4.5)]
    public void ManualDeclination_ParsesSignsAndHemispheres(string text, double expected)
    {
        Assert.IsTrue(BearingFixViewModelBase.TryParseDeclination(text, out var value));
        Assert.AreEqual(expected, value, 1e-12);
    }

    [TestMethod]
    public void ManualDeclination_Invalid_SetsError()
    {
        var vm = CreateViewModel();
        FillTwoPoints(vm);
        vm.BearingsAreMagnetic = true;
        vm.UseManualDeclination = true;
        vm.ManualDeclination = "lots";

        Assert.AreEqual("BearingFixErrorDeclination", vm.ErrorMessage);
    }

    [TestMethod]
    public void AutomaticDeclination_UsesWmmAtEachPointOnTheGivenDate()
    {
        var date = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var magnetic = CreateViewModel();
        magnetic.DeclinationDate = date;
        FillTwoPoints(magnetic);
        magnetic.BearingsAreMagnetic = true;

        // Same result as converting each bearing by hand with the model.
        var model = WorldMagneticModel.Wmm2025;
        var dA = model.DeclinationDegrees(51 + 53.118 / 60, 15.270 / 60, 2026.0);
        var dB = model.DeclinationDegrees(49 + 0.204 / 60, 2 + 34.410 / 60, 2026.0);
        var truth = CreateViewModel();
        FillTwoPoints(
            truth,
            (108.55 + dA).ToString(System.Globalization.CultureInfo.InvariantCulture),
            (32.44 + dB).ToString(System.Globalization.CultureInfo.InvariantCulture));

        Assert.AreEqual(truth.DegreesDecimalMinutes, magnetic.DegreesDecimalMinutes);
        Assert.IsFalse(magnetic.HasWarning);
    }

    [TestMethod]
    public void AutomaticDeclination_OutsideModelYears_Warns()
    {
        var vm = CreateViewModel();
        vm.DeclinationDate = new DateTimeOffset(2031, 6, 1, 0, 0, 0, TimeSpan.Zero);
        FillTwoPoints(vm);
        vm.BearingsAreMagnetic = true;

        Assert.IsTrue(vm.HasResult);
        StringAssert.Contains(vm.WarningMessage, "BearingFixWarnModelRange");
    }

    [TestMethod]
    public void Clear_ResetsToTwoBlankRows()
    {
        var vm = CreateViewModel();
        vm.LoadExampleCommand.Execute(null);
        vm.BearingsAreMagnetic = true;
        vm.ClearCommand.Execute(null);

        Assert.AreEqual(2, vm.Rows.Count);
        Assert.AreEqual(string.Empty, vm.Rows[0].Coordinate);
        Assert.IsFalse(vm.BearingsAreMagnetic);
        Assert.IsFalse(vm.HasResult);
        Assert.IsFalse(vm.HasError);
        Assert.IsFalse(vm.HasPlot);
    }

    [TestMethod]
    public void CopyOutput_SendsDdmToClipboard()
    {
        var clipboard = new FakeClipboardService();
        var vm = CreateViewModel(clipboard);
        FillTwoPoints(vm);

        vm.CopyOutputCommand.Execute(null);

        Assert.AreEqual(vm.DegreesDecimalMinutes, clipboard.LastText);
    }
}
