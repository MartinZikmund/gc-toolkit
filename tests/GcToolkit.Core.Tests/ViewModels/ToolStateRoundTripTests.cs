using GcToolkit.Core.Coordinates;
using GcToolkit.Core.Tests.Fakes;
using GcToolkit.Core.Text;
using GcToolkit.Core.ViewModels;
using GcToolkit.Core.ViewModels.Tools;

namespace GcToolkit.Core.Tests.ViewModels;

/// <summary>Tools that opt into <see cref="IToolStateful"/> reopen with the same inputs and result.</summary>
[TestClass]
public class ToolStateRoundTripTests
{
    private static T Create<T>(string toolId, Func<StubCatalogService, T> factory) => factory(new StubCatalogService(toolId));

    private static CaesarCipherViewModel Caesar() => Create("CaesarCipher", c => new CaesarCipherViewModel(
        c, new FakeRecentsService(), new FakeFavoriteToolsService(), new FakeStringLocalizer(), new FakeClipboardService(), new FakeShareService()));

    private static RomanNumeralsViewModel Roman() => Create("RomanNumerals", c => new RomanNumeralsViewModel(
        c, new FakeRecentsService(), new FakeFavoriteToolsService(), new FakeStringLocalizer(), new FakeClipboardService(), new FakeShareService()));

    private static Base64ViewModel Base64() => Create("Base64", c => new Base64ViewModel(
        c, new FakeRecentsService(), new FakeFavoriteToolsService(), new FakeStringLocalizer(), new FakeClipboardService(), new FakeShareService()));

    private static VigenereCipherViewModel Vigenere() => Create("VigenereCipher", c => new VigenereCipherViewModel(
        c, new FakeRecentsService(), new FakeFavoriteToolsService(), new FakeStringLocalizer(), new FakeClipboardService(), new FakeShareService()));

    private static WordValueViewModel WordValue() => Create("WordValue", c => new WordValueViewModel(
        c, new FakeRecentsService(), new FakeFavoriteToolsService(), new FakeStringLocalizer(), new FakeClipboardService(), new FakeShareService()));

    private static CoordinateConversionViewModel Coordinates() => Create("CoordinateConversion", c => new CoordinateConversionViewModel(
        c, new FakeRecentsService(), new FakeFavoriteToolsService(), new FakeStringLocalizer(), new FakeClipboardService(), new FakeShareService()));

    [TestMethod]
    public void Caesar_RoundTrip_RestoresAlphabetShiftAndText()
    {
        var source = Caesar();
        source.ModeIndex = 1;
        source.DirectionIndex = 1;
        source.Shift = 3;
        source.InputText = "4567";
        source.ShowAllShifts = false;

        var target = Caesar();
        target.RestoreState(source.CaptureState());

        Assert.AreEqual(1, target.ModeIndex);
        Assert.AreEqual(1, target.DirectionIndex);
        Assert.AreEqual(3, target.Shift);
        Assert.AreEqual("4567", target.InputText);
        Assert.AreEqual(source.OutputText, target.OutputText);
    }

    [TestMethod]
    public void Roman_RoundTrip_RestoresDirectionAndText()
    {
        var source = Roman();
        source.DirectionIndex = 1;
        source.InputText = "MMXXVI";

        var target = Roman();
        target.RestoreState(source.CaptureState());

        Assert.AreEqual(1, target.DirectionIndex);
        Assert.AreEqual("MMXXVI", target.InputText);
        Assert.AreEqual("2026", target.OutputText);
    }

    [TestMethod]
    public void Base64_RoundTrip_RestoresDirectionPaddingAndText()
    {
        var source = Base64();
        source.DirectionIndex = 1;
        source.IncludePadding = false;
        source.InputText = "N 50";

        var target = Base64();
        target.RestoreState(source.CaptureState());

        Assert.AreEqual(1, target.DirectionIndex);
        Assert.IsFalse(target.IncludePadding);
        Assert.AreEqual("N 50", target.InputText);
        Assert.AreEqual(source.OutputText, target.OutputText);
    }

    [TestMethod]
    public void Vigenere_RoundTrip_RestoresKeyAndText()
    {
        var source = Vigenere();
        source.DirectionIndex = 1;
        source.KeyText = "LEMON";
        source.InputText = "LXFOPVEFRNHR";

        var target = Vigenere();
        target.RestoreState(source.CaptureState());

        Assert.AreEqual(1, target.DirectionIndex);
        Assert.AreEqual("LEMON", target.KeyText);
        Assert.AreEqual("LXFOPVEFRNHR", target.InputText);
        Assert.AreEqual(source.OutputText, target.OutputText);
    }

    [TestMethod]
    public void WordValue_RoundTrip_RestoresMethodOptionsAndText()
    {
        var source = WordValue();
        source.SelectedMethod = WordValueSchemes.All[^1];
        source.CountNumbers = false;
        source.ShowCalculation = false;
        source.InputText = "geocache";

        var target = WordValue();
        target.RestoreState(source.CaptureState());

        Assert.AreEqual(source.SelectedMethod, target.SelectedMethod);
        Assert.IsFalse(target.CountNumbers);
        Assert.IsFalse(target.ShowCalculation);
        Assert.AreEqual("geocache", target.InputText);
        Assert.AreEqual(source.WordValueLine, target.WordValueLine);
    }

    [TestMethod]
    public void Coordinates_RoundTrip_RestoresTextAndDatums()
    {
        var source = Coordinates();
        source.InputText = "N 50° 05.123 E 014° 25.456";
        source.InputDatum = DatumRegistry.All[^1];

        var target = Coordinates();
        target.RestoreState(source.CaptureState());

        Assert.AreEqual(source.InputText, target.InputText);
        Assert.AreEqual(source.InputDatum, target.InputDatum);
        Assert.AreEqual(source.OutputDatum, target.OutputDatum);
        Assert.IsTrue(target.HasResult);
    }

    [TestMethod]
    public void RestoreState_Empty_KeepsTheDefaults()
    {
        var vm = Caesar();
        var shift = vm.Shift;

        vm.RestoreState(new ToolInputState());

        Assert.AreEqual(shift, vm.Shift);
        Assert.AreEqual(string.Empty, vm.InputText);
    }
}
