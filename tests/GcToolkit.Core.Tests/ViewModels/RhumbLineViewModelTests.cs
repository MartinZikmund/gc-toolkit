using GcToolkit.Core.Tests.Fakes;
using GcToolkit.Core.ViewModels.Tools;

namespace GcToolkit.Core.Tests.ViewModels;

[TestClass]
public sealed class RhumbLineViewModelTests
{
    private readonly FakeClipboardService _clipboard = new();

    private RhumbLineViewModel CreateViewModel(InMemoryPreferences? preferences = null)
        => new(
            new StubCatalogService("RhumbLine"),
            new FakeRecentsService(),
            new FakeFavoriteToolsService(),
            new FakeStringLocalizer(),
            _clipboard,
            new FakeShareService(),
            preferences ?? InMemoryPreferences.TrimmedHead());

    [TestMethod]
    public void Inverse_TwoValidPoints_ShowsRhumbAndGreatCircleFigures()
    {
        var vm = CreateViewModel();
        vm.UnitIndex = 1;

        vm.StartText = "40.6 -73.8";
        vm.EndText = "51.6 -0.5";

        Assert.IsTrue(vm.HasResult);
        StringAssert.Contains(vm.RhumbDistanceResult, "km");
        StringAssert.Contains(vm.RhumbBearingResult, "77");
        StringAssert.Contains(vm.RhumbBearingResult, "Compass_EastNorthEast");
        StringAssert.Contains(vm.GreatCircleInitialBearingResult, "51");
        Assert.IsTrue(vm.ExtraDistanceResult.StartsWith('+'));
    }

    [TestMethod]
    public void Inverse_UnparsableStart_ReportsError()
    {
        var vm = CreateViewModel();

        vm.StartText = "nonsense";
        vm.EndText = "51.6 -0.5";

        Assert.IsTrue(vm.HasError);
        Assert.AreEqual("RhumbLineErrorStart", vm.ErrorMessage);
        Assert.IsFalse(vm.HasResult);
    }

    [TestMethod]
    public void Inverse_SamePoint_ReportsError()
    {
        var vm = CreateViewModel();

        vm.StartText = "10 10";
        vm.EndText = "10 10";

        Assert.AreEqual("RhumbLineErrorSamePoint", vm.ErrorMessage);
        Assert.IsFalse(vm.HasResult);
    }

    [TestMethod]
    public void Inverse_OnePointOnly_IsQuietlyIncomplete()
    {
        var vm = CreateViewModel();

        vm.StartText = "10 10";

        Assert.IsFalse(vm.HasError);
        Assert.IsFalse(vm.HasResult);
    }

    [TestMethod]
    public void Direct_ValidInput_ShowsDestination()
    {
        var vm = CreateViewModel();
        vm.ModeIndex = 1;
        vm.UnitIndex = 1;

        vm.StartText = "49.2 16.6";
        vm.BearingText = "300";
        vm.DistanceText = "500";

        Assert.IsTrue(vm.HasResult);
        StringAssert.Contains(vm.DestinationResult, "51°");
        StringAssert.Contains(vm.DestinationResult, "010°");
    }

    [TestMethod]
    public void Direct_NegativeDistance_ReportsError()
    {
        var vm = CreateViewModel();
        vm.ModeIndex = 1;

        vm.StartText = "49.2 16.6";
        vm.BearingText = "300";
        vm.DistanceText = "-5";

        Assert.AreEqual("RhumbLineErrorDistance", vm.ErrorMessage);
    }

    [TestMethod]
    public void Direct_PastThePole_AddsNote()
    {
        var vm = CreateViewModel();
        vm.ModeIndex = 1;
        vm.UnitIndex = 1;

        vm.StartText = "80 10";
        vm.BearingText = "0";
        vm.DistanceText = "5000";

        Assert.IsTrue(vm.HasResult);
        Assert.IsTrue(vm.HasNote);
    }

    [TestMethod]
    public void UnitSwitch_ConvertsTheShownDistance()
    {
        var vm = CreateViewModel();
        vm.StartText = "0 0";
        vm.EndText = "0 1";

        vm.UnitIndex = 5;
        var nautical = vm.RhumbDistanceResult;
        vm.UnitIndex = 0;

        StringAssert.Contains(nautical, "nmi");
        StringAssert.StartsWith(nautical, "60"); // one degree of longitude on the equator is ~60.1 nmi
        StringAssert.Contains(vm.RhumbDistanceResult, " m");
    }

    [TestMethod]
    public void Swap_ExchangesPointsAndReversesBearing()
    {
        var vm = CreateViewModel();
        vm.StartText = "10 10";
        vm.EndText = "20 20";

        vm.SwapCommand.Execute(null);

        Assert.AreEqual("20 20", vm.StartText);
        Assert.AreEqual("10 10", vm.EndText);
        StringAssert.Contains(vm.RhumbBearingResult, "Compass_SouthWest");
    }

    [TestMethod]
    public void CopyReport_WithResult_PutsFiguresOnTheClipboard()
    {
        var vm = CreateViewModel();
        vm.StartText = "10 10";
        vm.EndText = "20 20";

        vm.CopyReportCommand.Execute(null);

        StringAssert.Contains(_clipboard.LastText, vm.RhumbDistanceResult);
    }

    [TestMethod]
    public void State_IsRestoredByANewInstance_OnATrimmedHead()
    {
        var preferences = InMemoryPreferences.TrimmedHead();
        var first = CreateViewModel(preferences);
        first.ModeIndex = 1;
        first.StartText = "49.2 16.6";
        first.BearingText = "123";
        first.DistanceText = "7";
        first.UnitIndex = 4;

        var second = CreateViewModel(preferences);

        Assert.AreEqual(1, second.ModeIndex);
        Assert.AreEqual("49.2 16.6", second.StartText);
        Assert.AreEqual("123", second.BearingText);
        Assert.AreEqual("7", second.DistanceText);
        Assert.AreEqual(4, second.UnitIndex);
    }
}
