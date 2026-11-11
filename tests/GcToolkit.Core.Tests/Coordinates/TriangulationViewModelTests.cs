using GcToolkit.Core.FavoriteTools;
using GcToolkit.Core.Recents;
using GcToolkit.Core.Tests.Fakes;
using GcToolkit.Core.ViewModels.Tools;

namespace GcToolkit.Core.Tests.Coordinates;

[TestClass]
public class TriangulationViewModelTests
{
    private static TriangulationViewModel CreateViewModel(FakeClipboardService? clipboard = null)
    {
        var catalog = new StubCatalogService("Triangulation");
        var preferences = new InMemoryPreferences();

        return new TriangulationViewModel(
            catalog,
            new RecentsService(preferences, catalog),
            new FavoriteToolsService(preferences, catalog),
            new FakeStringLocalizer(),
            clipboard ?? new FakeClipboardService(),
            new FakeShareService());
    }

    private static void FillExample(TriangulationViewModel vm)
    {
        vm.CoordinateA = "N 51 53.118 E 000 15.270";
        vm.BearingA = "108.55";
        vm.CoordinateB = "N 49 00.204 E 002 34.410";
        vm.BearingB = "32.44";
    }

    [TestMethod]
    public void BlankForm_HasNoResultAndNoError()
    {
        var vm = CreateViewModel();

        Assert.IsFalse(vm.HasResult);
        Assert.IsFalse(vm.HasError);
    }

    [TestMethod]
    public void ValidInput_ProducesResult()
    {
        var vm = CreateViewModel();
        FillExample(vm);

        Assert.IsTrue(vm.HasResult);
        Assert.IsFalse(vm.HasError);
        Assert.IsTrue(vm.DegreesDecimalMinutes.Contains("50", StringComparison.Ordinal));
        Assert.IsFalse(vm.HasWarning);
        Assert.IsTrue(vm.HasAlternate);
    }

    [TestMethod]
    public void InvalidCoordinate_SetsErrorKeyAndClearsResult()
    {
        var vm = CreateViewModel();
        FillExample(vm);
        vm.CoordinateA = "garbage";

        Assert.IsTrue(vm.HasError);
        Assert.IsFalse(vm.HasResult);
        Assert.AreEqual("TriangulationErrorPointA", vm.ErrorMessage);
    }

    [TestMethod]
    public void NonNumericBearing_SetsError()
    {
        var vm = CreateViewModel();
        FillExample(vm);
        vm.BearingB = "north-ish";

        Assert.AreEqual("TriangulationErrorBearingB", vm.ErrorMessage);
    }

    [TestMethod]
    public void SameLine_SetsCoincidentError()
    {
        var vm = CreateViewModel();
        vm.CoordinateA = "N 0 0.000 E 0 0.000";
        vm.BearingA = "90";
        vm.CoordinateB = "N 0 0.000 E 30 0.000";
        vm.BearingB = "90";

        Assert.AreEqual("TriangulationErrorCoincident", vm.ErrorMessage);
    }

    [TestMethod]
    public void DivergingBearings_WarnAboutBehind()
    {
        var vm = CreateViewModel();
        vm.CoordinateA = "N 0 0.000 E 0 0.000";
        vm.BearingA = "315";
        vm.CoordinateB = "N 0 0.000 E 10 0.000";
        vm.BearingB = "45";

        Assert.IsTrue(vm.HasResult);
        Assert.IsTrue(vm.HasWarning);
        StringAssert.Contains(vm.WarningMessage, "TriangulationWarnBehindA");
        StringAssert.Contains(vm.WarningMessage, "TriangulationWarnBehindB");
    }

    [TestMethod]
    public void LoadExample_FillsFormWithResult()
    {
        var vm = CreateViewModel();
        vm.LoadExampleCommand.Execute(null);

        Assert.IsTrue(vm.HasResult);
    }

    [TestMethod]
    public void Clear_ResetsEverything()
    {
        var vm = CreateViewModel();
        FillExample(vm);
        vm.ClearCommand.Execute(null);

        Assert.AreEqual(string.Empty, vm.CoordinateA);
        Assert.IsFalse(vm.HasResult);
        Assert.IsFalse(vm.HasError);
    }

    [TestMethod]
    public void CopyText_SendsValueToClipboard()
    {
        var clipboard = new FakeClipboardService();
        var vm = CreateViewModel(clipboard);
        vm.CopyTextCommand.Execute("N 1 2.3");

        Assert.AreEqual("N 1 2.3", clipboard.LastText);
    }
}
