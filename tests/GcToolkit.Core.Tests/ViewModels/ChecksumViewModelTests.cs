using GcToolkit.Core.Numbers;
using GcToolkit.Core.Tests.Fakes;
using GcToolkit.Core.ViewModels.Tools;

namespace GcToolkit.Core.Tests.ViewModels;

[TestClass]
public class ChecksumViewModelTests
{
    private const string Coordinate = "N 49 12.345 E 16 30.678";

    private readonly FakeClipboardService _clipboard = new();
    private readonly FakeShareService _share = new();

    private ChecksumViewModel CreateViewModel() => new(
        new StubCatalogService("Checksum"),
        new FakeRecentsService(),
        new FakeFavoriteToolsService(),
        new FakeStringLocalizer(),
        _clipboard,
        _share);

    [TestMethod]
    public void CoordinateText_Empty_HasNoRows()
    {
        var vm = CreateViewModel();

        Assert.IsFalse(vm.HasCoordinateRows);
        Assert.AreEqual(0, vm.CoordinateRows.Count);
    }

    [TestMethod]
    public void CoordinateText_OneLinePerRow_DdmByDefault()
    {
        var vm = CreateViewModel();

        vm.CoordinateText = $"{Coordinate}\nbogus";

        Assert.IsTrue(vm.HasCoordinateRows);
        Assert.AreEqual(2, vm.CoordinateRows.Count);
        Assert.AreEqual("59", vm.CoordinateRows[0].TotalText);
        Assert.AreEqual("5", vm.CoordinateRows[0].RootText);
        Assert.AreEqual(CoordinateChecksumStatus.Calculated, vm.CoordinateRows[0].Status);
        Assert.AreEqual(CoordinateChecksumStatus.Invalid, vm.CoordinateRows[1].Status);
    }

    [TestMethod]
    public void CoordinateText_WithExpected_FlagsMatchAndMismatch()
    {
        var vm = CreateViewModel();

        vm.CoordinateText = $"{Coordinate} = 59\n{Coordinate} = 62";

        Assert.AreEqual(CoordinateChecksumStatus.Match, vm.CoordinateRows[0].Status);
        Assert.AreEqual(CoordinateChecksumStatus.Mismatch, vm.CoordinateRows[1].Status);
    }

    [TestMethod]
    public void AsEnteredMode_Switched_RecomputesTheRows()
    {
        var vm = CreateViewModel();
        vm.CoordinateText = "ABC123";
        Assert.AreEqual(CoordinateChecksumStatus.Invalid, vm.CoordinateRows[0].Status);

        vm.UseDdmMode = false;

        Assert.AreEqual("6", vm.CoordinateRows[0].TotalText);
    }

    [TestMethod]
    public void CopyOutput_WithOnlyCoordinates_CopiesTheTable()
    {
        var vm = CreateViewModel();
        vm.CoordinateText = Coordinate;

        vm.CopyOutputCommand.Execute(null);

        StringAssert.Contains(_clipboard.LastText, "59");
    }

    [TestMethod]
    public void UseCoordinateExample_FillsTheCoordinateBox()
    {
        var vm = CreateViewModel();

        vm.UseCoordinateExampleCommand.Execute(null);

        Assert.IsTrue(vm.HasCoordinateRows);
    }

    [TestMethod]
    public void ClearCoordinates_EmptiesTheRows()
    {
        var vm = CreateViewModel();
        vm.CoordinateText = Coordinate;

        vm.ClearCoordinatesCommand.Execute(null);

        Assert.IsFalse(vm.HasCoordinateRows);
    }
}
