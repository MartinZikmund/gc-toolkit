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

    // ---- Cross-sum variants: alternating, modulo, bases ----

    [TestMethod]
    public void Variants_Number_ShowsTheAlternatingSumWithItsWorking()
    {
        var vm = CreateViewModel();

        vm.InputText = "12345";

        Assert.IsTrue(vm.HasVariants);
        Assert.IsFalse(vm.HasVariantError);
        Assert.AreEqual(1, vm.VariantRows.Count);
        Assert.AreEqual("3", vm.VariantRows[0].Value);
        Assert.AreEqual("1 - 2 + 3 - 4 + 5 = 3", vm.VariantRows[0].Working);
    }

    [TestMethod]
    public void Variants_AlternatingWorking_SignsCountFromTheRightmostDigit()
    {
        var vm = CreateViewModel();

        vm.InputText = "12";

        Assert.AreEqual("-1 + 2 = 1", vm.VariantRows[0].Working);
    }

    [TestMethod]
    public void Variants_WorkingOff_LeavesTheWorkingOut()
    {
        var vm = CreateViewModel();
        vm.ShowWorking = false;

        vm.InputText = "12345";

        Assert.AreEqual(string.Empty, vm.VariantRows[0].Working);
    }

    [TestMethod]
    public void Variants_Modulus_AddsARow()
    {
        var vm = CreateViewModel();
        vm.InputText = "12345";

        vm.Modulus = 4;

        Assert.AreEqual(2, vm.VariantRows.Count);
        Assert.AreEqual("3", vm.VariantRows[1].Value);   // 15 mod 4
    }

    [TestMethod]
    [DataRow(0.0)]
    [DataRow(-3.0)]
    [DataRow(2.5)]
    public void Variants_BadModulus_ShowsAnErrorAndNoRows(double modulus)
    {
        var vm = CreateViewModel();
        vm.InputText = "12345";

        vm.Modulus = modulus;

        Assert.IsTrue(vm.HasVariantError);
        Assert.IsFalse(vm.HasVariants);
    }

    [TestMethod]
    public void Variants_DigitBase_AddsABaseRow()
    {
        var vm = CreateViewModel();
        vm.InputText = "255";

        vm.DigitBase = 16;

        Assert.AreEqual(2, vm.VariantRows.Count);
        Assert.AreEqual("30", vm.VariantRows[0].Value);
        Assert.AreEqual("F(15) + F(15) = 30", vm.VariantRows[0].Working);
    }

    [TestMethod]
    public void Variants_InputBase_ReadsLettersAsDigits()
    {
        var vm = CreateViewModel();

        vm.InputBase = 16;
        vm.DigitBase = 16;
        vm.InputText = "FF";

        Assert.IsTrue(vm.HasVariants);
        Assert.AreEqual("30", vm.VariantRows[0].Value);
    }

    [TestMethod]
    public void Variants_DigitInvalidForTheChosenBase_ShowsAnError()
    {
        var vm = CreateViewModel();
        vm.InputText = "129";

        vm.InputBase = 8;

        Assert.IsTrue(vm.HasVariantError);
        Assert.IsFalse(vm.HasVariants);
    }

    [TestMethod]
    public void Variants_PlainTextWithDefaultBases_StaysQuiet()
    {
        var vm = CreateViewModel();

        vm.InputText = "N 49 12.345";

        Assert.IsFalse(vm.HasVariants);
        Assert.IsFalse(vm.HasVariantError);
    }

    [TestMethod]
    public void Variants_EmptyInput_HasNothing()
    {
        var vm = CreateViewModel();
        vm.InputText = "123";

        vm.InputText = string.Empty;

        Assert.IsFalse(vm.HasVariants);
    }

    [TestMethod]
    public void CopyOutput_WithVariants_IncludesThem()
    {
        var vm = CreateViewModel();
        vm.InputText = "12345";
        vm.Modulus = 4;

        vm.CopyOutputCommand.Execute(null);

        StringAssert.Contains(_clipboard.LastText, "ChecksumAlternatingLabel");
        StringAssert.Contains(_clipboard.LastText, "ChecksumModuloLabel");
    }
}
