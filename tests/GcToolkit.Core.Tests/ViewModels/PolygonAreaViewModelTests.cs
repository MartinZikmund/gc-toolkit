using GcToolkit.Core.Catalog;
using GcToolkit.Core.FavoriteTools;
using GcToolkit.Core.Recents;
using GcToolkit.Core.Services;
using GcToolkit.Core.Tests.Fakes;
using GcToolkit.Core.ViewModels.Tools;
using MZikmund.Toolkit.WinUI.Services;

namespace GcToolkit.Core.Tests.ViewModels;

[TestClass]
public class PolygonAreaViewModelTests
{
    private const string DecimalBox = "0 0\n0 1\n1 1\n1 0";

    private static PolygonAreaViewModel Create(out CapturingClipboard clipboard, IPreferences? preferences = null)
    {
        clipboard = new CapturingClipboard();
        return new PolygonAreaViewModel(
            new StubCatalogService("PolygonArea"),
            new NoopRecents(),
            new NoopFavorites(),
            new FakeStringLocalizer(),
            clipboard,
            new NoopShare(),
            preferences ?? new InMemoryPreferences());
    }

    [TestMethod]
    public void NoInput_HasNoResultAndNoHint()
    {
        var vm = Create(out _);

        Assert.IsFalse(vm.HasResult);
        Assert.IsFalse(vm.NeedsMorePoints);
    }

    [TestMethod]
    public void TwoVertices_ShowsTheNeedMorePointsHint()
    {
        var vm = Create(out _);

        vm.InputText = "0 0\n1 1";

        Assert.IsFalse(vm.HasResult);
        Assert.IsTrue(vm.NeedsMorePoints);
    }

    [TestMethod]
    public void Box_ProducesAreaPerimeterAndEdges()
    {
        var vm = Create(out _);

        vm.InputText = DecimalBox;

        Assert.IsTrue(vm.HasResult);
        Assert.AreEqual(4, vm.Edges.Count);
        Assert.IsFalse(vm.HasSelfIntersection);
        Assert.IsTrue(vm.AreaText.EndsWith("m²", StringComparison.Ordinal), vm.AreaText);
        Assert.IsTrue(vm.PerimeterText.EndsWith(" m", StringComparison.Ordinal), vm.PerimeterText);
    }

    [TestMethod]
    public void MixedNotations_AreAllAccepted()
    {
        var vm = Create(out _);

        vm.InputText = "N 50° 0.000' E 14° 0.000'\n50.0 14.01\n50°00'18\"N 14°00'36\"E";

        Assert.IsFalse(vm.HasParseErrors);
        Assert.IsTrue(vm.HasResult);
    }

    [TestMethod]
    public void UnreadableLine_IsReportedByNumberAndRestStillComputes()
    {
        var vm = Create(out _);

        vm.InputText = "0 0\n0 1\nnot a coordinate\n1 1\n1 0";

        Assert.IsTrue(vm.HasParseErrors);
        Assert.IsTrue(vm.HasResult);
        Assert.AreEqual(4, vm.Edges.Count);
    }

    [TestMethod]
    public void ChangingTheAreaUnit_ReformatsWithoutChangingTheInput()
    {
        var vm = Create(out _);
        vm.InputText = DecimalBox;
        var squareMeters = vm.AreaText;

        vm.AreaUnitIndex = 2; // hectares

        Assert.AreNotEqual(squareMeters, vm.AreaText);
        Assert.IsTrue(vm.AreaText.EndsWith("ha", StringComparison.Ordinal), vm.AreaText);
    }

    [TestMethod]
    public void ChangingTheLengthUnit_ReformatsPerimeterAndEdges()
    {
        var vm = Create(out _);
        vm.InputText = DecimalBox;

        vm.LengthUnitIndex = 1; // kilometres

        Assert.IsTrue(vm.PerimeterText.EndsWith("km", StringComparison.Ordinal), vm.PerimeterText);
        Assert.IsTrue(vm.Edges[0].Length.EndsWith("km", StringComparison.Ordinal), vm.Edges[0].Length);
    }

    [TestMethod]
    public void BowTie_ShowsTheSelfIntersectionWarning()
    {
        var vm = Create(out _);

        vm.InputText = "0 0\n1 1\n1 0\n0 1";

        Assert.IsTrue(vm.HasSelfIntersection);
    }

    [TestMethod]
    public void RepeatedFirstVertex_IsNotedAndStillComputes()
    {
        var vm = Create(out _);

        vm.InputText = DecimalBox + "\n0 0";

        Assert.IsTrue(vm.WasClosed);
        Assert.AreEqual(4, vm.Edges.Count);
    }

    [TestMethod]
    public void Centroid_AndVertexMean_AreBothReported()
    {
        var vm = Create(out _);

        vm.InputText = DecimalBox;

        Assert.IsFalse(string.IsNullOrEmpty(vm.CentroidText));
        Assert.IsFalse(string.IsNullOrEmpty(vm.CentroidDecimalText));
        Assert.IsFalse(string.IsNullOrEmpty(vm.VertexMeanText));
    }

    [TestMethod]
    public void CopyOutput_PutsAllResultsOnTheClipboard()
    {
        var vm = Create(out var clipboard);
        vm.InputText = DecimalBox;

        vm.CopyOutputCommand.Execute(null);

        StringAssert.Contains(clipboard.LastText, vm.AreaText);
        StringAssert.Contains(clipboard.LastText, vm.PerimeterText);
        StringAssert.Contains(clipboard.LastText, vm.CentroidText);
    }

    [TestMethod]
    public void CopyOutput_WithoutResult_IsDisabled()
    {
        var vm = Create(out _);

        Assert.IsFalse(vm.CopyOutputCommand.CanExecute(null));
    }

    [TestMethod]
    public void Clear_EmptiesTheInput()
    {
        var vm = Create(out _);
        vm.InputText = DecimalBox;

        vm.ClearCommand.Execute(null);

        Assert.AreEqual(string.Empty, vm.InputText);
        Assert.IsFalse(vm.HasResult);
    }

    [TestMethod]
    public void LoadExample_FillsInAWorkingPolygon()
    {
        var vm = Create(out _);

        vm.LoadExampleCommand.Execute(null);

        Assert.IsFalse(vm.HasParseErrors);
        Assert.AreEqual(4, vm.Edges.Count);
        Assert.IsTrue(vm.HasResult);
    }

    [TestMethod]
    public void InputAndUnits_AreRemembered_OnATrimmedHead()
    {
        var preferences = InMemoryPreferences.TrimmedHead();
        var first = Create(out _, preferences);
        first.InputText = DecimalBox;
        first.AreaUnitIndex = 3;
        first.LengthUnitIndex = 2;

        var second = Create(out _, preferences);

        Assert.AreEqual(DecimalBox, second.InputText);
        Assert.AreEqual(3, second.AreaUnitIndex);
        Assert.AreEqual(2, second.LengthUnitIndex);
        Assert.IsTrue(second.HasResult);
    }

    [TestMethod]
    public void OutOfRangeStoredUnit_FallsBackToTheDefault()
    {
        InMemoryPreferences preferences = new();
        preferences.Set("PolygonArea.AreaUnit", 99);

        var vm = Create(out _, preferences);

        Assert.AreEqual(0, vm.AreaUnitIndex);
    }

    private sealed class CapturingClipboard : IClipboardService
    {
        public string? LastText { get; private set; }

        public void SetText(string text) => LastText = text;
    }

    private sealed class NoopShare : IShareService
    {
        public Task ShareAsync(string title, string uri) => Task.CompletedTask;

        public Task ShareTextAsync(string title, string text) => Task.CompletedTask;
    }

    private sealed class NoopRecents : IRecentsService
    {
        public event EventHandler? RecentsChanged;

        public Task RecordOpenedAsync(string toolId) => Task.CompletedTask;

        public IReadOnlyList<string> GetRecentToolIds() => [];

        public Task ClearAsync()
        {
            RecentsChanged?.Invoke(this, EventArgs.Empty);
            return Task.CompletedTask;
        }
    }

    private sealed class NoopFavorites : IFavoriteToolsService
    {
        public event EventHandler? FavoriteToolsChanged;

        public bool IsFavorite(string toolId) => false;

        public IReadOnlyList<string> GetFavoriteToolIds() => [];

        public Task ToggleAsync(string toolId)
        {
            FavoriteToolsChanged?.Invoke(this, EventArgs.Empty);
            return Task.CompletedTask;
        }
    }
}
