using GcToolkit.Core.Coordinates;
using GcToolkit.Core.Geocaches;
using GcToolkit.Core.Tests.Fakes;
using GcToolkit.Core.ViewModels;
using GcToolkit.Core.ViewModels.Tools;

namespace GcToolkit.Core.Tests.Workbench;

[TestClass]
public class CorrectedCoordinatesViewModelTests
{
    private static readonly GeocacheCode A = GeocacheCode.Parse("GC1AAA");
    private const string Template = "N 50° 0A.000 E 014° 25.000";

    private readonly InMemoryGeocacheStore _store = new();
    private readonly InMemoryPreferences _preferences = InMemoryPreferences.TrimmedHead();

    private VariableCoordinateCalculatorViewModel CreateSheet()
    {
        VariableCoordinateCalculatorViewModel sheet = new(
            new StubCatalogService("VariableCoordinateCalculator"),
            new FakeRecentsService(),
            new FakeFavoriteToolsService(),
            new FakeStringLocalizer(),
            new FakeClipboardService(),
            new FakeShareService(),
            _preferences);
        sheet.ShowSheetOf(A);
        return sheet;
    }

    private async Task<(CorrectedCoordinatesViewModel Vm, VariableCoordinateCalculatorViewModel Sheet)> CreateAsync(
        GeocacheUserData? userData = null,
        bool stored = true,
        string? a = "1")
    {
        if (stored)
        {
            await _store.UpsertAsync([new Geocache { Code = A, Name = "Alpha", Coordinate = new(50, 14), UserData = userData ?? new() { Note = "mine" } }]);
        }

        var sheet = CreateSheet();
        sheet.Stages[0].Template = Template;
        if (a is not null)
        {
            sheet.SetVariable("A", a);
        }

        CorrectedCoordinatesViewModel vm = new(_store, new FakeStringLocalizer());
        await vm.LoadAsync(A, sheet);
        return (vm, sheet);
    }

    private static void AssertNear(GeoCoordinate expected, GeoCoordinate? actual)
    {
        Assert.IsNotNull(actual);
        Assert.AreEqual(expected.Latitude, actual.Value.Latitude, 1e-9);
        Assert.AreEqual(expected.Longitude, actual.Value.Longitude, 1e-9);
    }

    private async Task<GeocacheUserData> StoredUserDataAsync() => (await _store.GetAsync(A))!.UserData;

    private static GeoCoordinate Final(string minutes)
    {
        CoordinateParser.TryParse($"N 50° {minutes} E 014° 25.000", out var coordinate, out _);
        return coordinate;
    }

    [TestMethod]
    public async Task Save_StoresTheFinalAndMarksTheCacheSolved()
    {
        var (vm, _) = await CreateAsync();

        await vm.SaveCommand.ExecuteAsync(null);

        var userData = await StoredUserDataAsync();
        AssertNear(Final("01.000"), userData.CorrectedCoordinate);
        Assert.IsTrue(userData.IsSolved);
        Assert.AreEqual("mine", userData.Note);
        Assert.IsTrue(vm.IsSolved);
        Assert.IsFalse(vm.SaveCommand.CanExecute(null));
    }

    [TestMethod]
    public async Task Save_NoCompleteStage_IsUnavailable()
    {
        var (vm, _) = await CreateAsync(a: null);

        Assert.IsFalse(vm.SaveCommand.CanExecute(null));
    }

    [TestMethod]
    public async Task Save_CacheNotStored_IsUnavailable()
    {
        var (vm, _) = await CreateAsync(stored: false);

        Assert.IsFalse(vm.IsStored);
        Assert.IsFalse(vm.SaveCommand.CanExecute(null));
    }

    [TestMethod]
    public async Task Load_SolvedCache_ShowsItsCorrectedCoordinates()
    {
        var (vm, _) = await CreateAsync(new() { CorrectedCoordinate = Final("01.000"), IsSolved = true });

        Assert.IsTrue(vm.IsSolved);
        Assert.IsTrue(vm.HasSaved);
        Assert.IsFalse(vm.NeedsUpdate);
    }

    [TestMethod]
    public async Task EditingASolvedCache_AsksInsteadOfOverwriting()
    {
        var (vm, sheet) = await CreateAsync(new() { CorrectedCoordinate = Final("01.000"), IsSolved = true });

        sheet.SetVariable("A", "2");

        Assert.IsTrue(vm.NeedsUpdate);
        AssertNear(Final("01.000"), (await StoredUserDataAsync()).CorrectedCoordinate);
    }

    [TestMethod]
    public async Task Update_OverwritesWithTheNewFinal()
    {
        var (vm, sheet) = await CreateAsync(new() { CorrectedCoordinate = Final("01.000"), IsSolved = true });
        sheet.SetVariable("A", "2");

        await vm.SaveCommand.ExecuteAsync(null);

        AssertNear(Final("02.000"), (await StoredUserDataAsync()).CorrectedCoordinate);
        Assert.IsFalse(vm.NeedsUpdate);
    }

    [TestMethod]
    public async Task Keep_LeavesTheSavedCoordinatesUntilTheFinalChangesAgain()
    {
        var (vm, sheet) = await CreateAsync(new() { CorrectedCoordinate = Final("01.000"), IsSolved = true });
        sheet.SetVariable("A", "2");

        vm.KeepCommand.Execute(null);

        Assert.IsFalse(vm.NeedsUpdate);
        AssertNear(Final("01.000"), (await StoredUserDataAsync()).CorrectedCoordinate);

        sheet.SetVariable("A", "3");

        Assert.IsTrue(vm.NeedsUpdate);
    }

    [TestMethod]
    public async Task EditingBackToTheSavedFinal_StopsAsking()
    {
        var (vm, sheet) = await CreateAsync(new() { CorrectedCoordinate = Final("01.000"), IsSolved = true });
        sheet.SetVariable("A", "2");

        sheet.SetVariable("A", "1");

        Assert.IsFalse(vm.NeedsUpdate);
    }

    [TestMethod]
    public async Task Clear_RemovesTheCoordinatesAndTheSolvedState()
    {
        var (vm, _) = await CreateAsync(new() { CorrectedCoordinate = Final("01.000"), IsSolved = true, Note = "mine" });

        await vm.ClearCommand.ExecuteAsync(null);

        var userData = await StoredUserDataAsync();
        Assert.IsNull(userData.CorrectedCoordinate);
        Assert.IsFalse(userData.IsSolved);
        Assert.AreEqual("mine", userData.Note);
        Assert.IsFalse(vm.IsSolved);
        Assert.IsFalse(vm.HasSaved);
    }

    [TestMethod]
    public async Task SolvedChanged_IsRaisedOnSaveAndClear()
    {
        var (vm, _) = await CreateAsync();
        List<bool> raised = [];
        vm.SolvedChanged += (_, _) => raised.Add(vm.IsSolved);

        await vm.SaveCommand.ExecuteAsync(null);
        await vm.ClearCommand.ExecuteAsync(null);

        CollectionAssert.AreEqual(new[] { true, false }, raised);
    }

    [TestMethod]
    public async Task Final_IsTheLastCompleteStage()
    {
        var (vm, sheet) = await CreateAsync();
        sheet.AddStageCommand.Execute(null);
        sheet.Stages[1].Template = "N 50° 05.000 E 014° 25.000";

        await vm.SaveCommand.ExecuteAsync(null);

        AssertNear(Final("05.000"), (await StoredUserDataAsync()).CorrectedCoordinate);
    }
}
