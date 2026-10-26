using GcToolkit.Core.Tests.Fakes;
using GcToolkit.Core.ViewModels.Tools;
using Microsoft.UI.Xaml.Controls;

namespace GcToolkit.Core.Tests.ViewModels;

[TestClass]
public class IsbnViewModelTests
{
    // Mirrors IsbnViewModel's documented StandardIndex: 0 = ISBN, 1 = ISSN, 2 = EAN-13, 3 = UPC-A.
    private const int Issn = 1;

    private readonly FakeClipboardService _clipboard = new();

    private IsbnViewModel CreateViewModel() => new(
        new StubCatalogService("Isbn"),
        new FakeRecentsService(),
        new FakeFavoriteToolsService(),
        new FakeStringLocalizer(),
        _clipboard,
        new FakeShareService());

    [TestMethod]
    public void InputText_ValidIsbn13_ShowsBothFormsAtOnce()
    {
        var vm = CreateViewModel();

        vm.InputText = "978-0-13-468599-1";

        Assert.AreEqual(InfoBarSeverity.Success, vm.StatusSeverity);
        CollectionAssert.AreEqual(
            new[] { "9780134685991", "0134685997" },
            vm.Forms.Select(f => f.Code).ToArray());
        Assert.IsTrue(vm.HasLookupLinks);
        Assert.IsTrue(vm.CopyOutputCommand.CanExecute(null));
    }

    [TestMethod]
    public void InputText_WrongCheckDigit_WarnsAndOffersCorrections()
    {
        var vm = CreateViewModel();

        vm.InputText = "9780134685992";

        Assert.AreEqual(InfoBarSeverity.Error, vm.StatusSeverity);
        Assert.AreEqual(0, vm.Forms.Count);
        Assert.IsTrue(vm.HasCorrections);
        Assert.IsTrue(vm.Corrections.Any(c => c.Code == "9780134685991"));
        Assert.IsTrue(vm.HasWorking);
    }

    [TestMethod]
    public void InputText_Wildcards_ListsSolutions()
    {
        var vm = CreateViewModel();

        vm.InputText = "03064061??";

        Assert.IsTrue(vm.HasSolutions);
        Assert.AreEqual(10, vm.Solutions.Count);
        Assert.IsFalse(vm.HasWorking);
    }

    [TestMethod]
    public void ShowWorking_Off_HidesTheCalculation()
    {
        var vm = CreateViewModel();
        vm.InputText = "080442957";
        Assert.IsTrue(vm.HasWorking);

        vm.ShowWorking = false;

        Assert.IsFalse(vm.HasWorking);
    }

    [TestMethod]
    public void StandardIndex_Change_ReanalysesTheSameInput()
    {
        var vm = CreateViewModel();
        vm.InputText = "0317-8471";
        Assert.AreEqual(InfoBarSeverity.Error, vm.StatusSeverity, "Eight digits is no ISBN.");

        vm.StandardIndex = Issn;

        Assert.AreEqual(InfoBarSeverity.Success, vm.StatusSeverity);
        Assert.AreEqual("9770317847001", vm.Forms[^1].Code);
    }

    [TestMethod]
    public void CopyOutput_CopiesEveryForm()
    {
        var vm = CreateViewModel();
        vm.InputText = "0-8044-2957-X";

        vm.CopyOutputCommand.Execute(null);

        StringAssert.Contains(_clipboard.LastText, "080442957X");
        StringAssert.Contains(_clipboard.LastText, "9780804429573");
    }

    [TestMethod]
    public void UseExample_CyclesThroughValidPresets()
    {
        var vm = CreateViewModel();

        vm.UseExampleCommand.Execute(null);
        var first = vm.InputText;
        vm.UseExampleCommand.Execute(null);

        Assert.AreNotEqual(first, vm.InputText);
        Assert.IsTrue(vm.HasOutput);
    }

    [TestMethod]
    public void Clear_ResetsToTheEmptyState()
    {
        var vm = CreateViewModel();
        vm.InputText = "9780134685991";

        vm.ClearCommand.Execute(null);

        Assert.IsFalse(vm.HasOutput);
        Assert.IsFalse(vm.CopyOutputCommand.CanExecute(null));
    }
}
