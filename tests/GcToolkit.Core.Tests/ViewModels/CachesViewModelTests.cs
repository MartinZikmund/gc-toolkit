using System.Text;
using GcToolkit.Core.Coordinates;
using GcToolkit.Core.Geocaches;
using GcToolkit.Core.Geocaches.Gpx;
using GcToolkit.Core.Services.Dialogs;
using GcToolkit.Core.Tests.Fakes;
using GcToolkit.Core.ViewModels;

namespace GcToolkit.Core.Tests.ViewModels;

[TestClass]
public class CachesViewModelTests
{
    private readonly InMemoryGeocacheStore _store = new();
    private readonly FakeFilePickerService _picker = new();
    private readonly FakeConfirmationDialogService _confirm = new();
    private readonly FakeTextPromptService _prompt = new();
    private readonly FakeNavigationService _navigation = new();

    private CachesViewModel CreateViewModel(int pageSize = 100) => new(
        _store,
        _picker,
        _confirm,
        _prompt,
        new FakeStringLocalizer(),
        new GpxImporter(),
        _navigation)
    {
        PageSize = pageSize,
    };

    private static Geocache Cache(
        int number,
        GeocacheType type = GeocacheType.Traditional,
        GeocacheSize size = GeocacheSize.Regular,
        double? difficulty = 2,
        double? terrain = 2,
        GeocacheUserData? userData = null,
        string? owner = null) => new()
        {
            Code = GeocacheCode.Parse($"GC{number:D5}"),
            Name = $"Cache {number:D5}",
            Type = type,
            Size = size,
            Difficulty = difficulty,
            Terrain = terrain,
            Owner = owner,
            Coordinate = new(50.0 + number / 100000.0, 14.4),
            UserData = userData ?? new(),
        };

    private async Task<CachesViewModel> LoadedAsync(IEnumerable<Geocache> caches, int pageSize = 100)
    {
        await _store.UpsertAsync([.. caches]);
        var vm = CreateViewModel(pageSize);
        await vm.RefreshAsync();
        return vm;
    }

    private static string[] Codes(CachesViewModel vm) => [.. vm.Items.Select(i => i.Code.Value)];

    [TestMethod]
    public async Task Refresh_LoadsFirstPageAndTotal()
    {
        var vm = await LoadedAsync(Enumerable.Range(1, 250).Select(n => Cache(n)));

        Assert.AreEqual(100, vm.Items.Count);
        Assert.AreEqual(250, vm.TotalCount);
        Assert.IsTrue(vm.HasMore);
    }

    [TestMethod]
    public async Task LoadMore_AppendsPagesUntilExhausted()
    {
        var vm = await LoadedAsync(Enumerable.Range(1, 250).Select(n => Cache(n)));

        await vm.LoadMoreAsync();
        await vm.LoadMoreAsync();
        await vm.LoadMoreAsync();

        Assert.AreEqual(250, vm.Items.Count);
        Assert.IsFalse(vm.HasMore);
    }

    [TestMethod]
    public async Task LoadMore_FiveThousandCaches_PagesWithoutGapsOrDuplicates()
    {
        var vm = await LoadedAsync(Enumerable.Range(1, 5000).Select(n => Cache(n)), pageSize: 200);

        Assert.AreEqual(200, vm.Items.Count);
        Assert.AreEqual(5000, vm.TotalCount);
        var loads = 0;
        while (vm.HasMore)
        {
            await vm.LoadMoreAsync();
            loads++;
        }

        Assert.AreEqual(24, loads);
        Assert.AreEqual(5000, vm.Items.Count);
        Assert.AreEqual(5000, vm.Items.Select(i => i.Code).Distinct().Count());
        Assert.AreEqual("Cache 00001", vm.Items[0].Name);
        Assert.AreEqual("Cache 05000", vm.Items[^1].Name);
    }

    [TestMethod]
    public async Task SearchText_FiltersByNameCodeOrOwnerAndRestartsPaging()
    {
        var vm = await LoadedAsync([
            Cache(1, owner: "Pražák"),
            Cache(2),
            Cache(3),
            .. Enumerable.Range(10, 150).Select(n => Cache(n)),
        ]);
        await vm.LoadMoreAsync();

        vm.SearchText = "praž";
        await vm.WhenIdleAsync();
        CollectionAssert.AreEqual(new[] { "GC00001" }, Codes(vm));

        vm.SearchText = "GC00002";
        await vm.WhenIdleAsync();
        CollectionAssert.AreEqual(new[] { "GC00002" }, Codes(vm));
        Assert.AreEqual(1, vm.TotalCount);
    }

    [TestMethod]
    public async Task TypeAndSizeFilters_NarrowTheList()
    {
        var vm = await LoadedAsync([
            Cache(1, GeocacheType.Mystery, GeocacheSize.Micro),
            Cache(2, GeocacheType.Mystery, GeocacheSize.Large),
            Cache(3, GeocacheType.Multi, GeocacheSize.Micro),
        ]);

        vm.SelectedTypeOption = vm.TypeOptions.Single(o => o.Value == GeocacheType.Mystery);
        await vm.WhenIdleAsync();
        CollectionAssert.AreEqual(new[] { "GC00001", "GC00002" }, Codes(vm));

        vm.SelectedSizeOption = vm.SizeOptions.Single(o => o.Value == GeocacheSize.Micro);
        await vm.WhenIdleAsync();
        CollectionAssert.AreEqual(new[] { "GC00001" }, Codes(vm));
    }

    [TestMethod]
    public async Task DifficultyAndTerrainRanges_NarrowTheList()
    {
        var vm = await LoadedAsync([
            Cache(1, difficulty: 1, terrain: 1),
            Cache(2, difficulty: 3, terrain: 2),
            Cache(3, difficulty: 5, terrain: 5),
        ]);

        vm.MinDifficultyOption = vm.RatingOptions.Single(o => o.Value == 2);
        vm.MaxTerrainOption = vm.RatingOptions.Single(o => o.Value == 3);
        await vm.WhenIdleAsync();

        CollectionAssert.AreEqual(new[] { "GC00002" }, Codes(vm));
    }

    [TestMethod]
    public async Task StatusSolvedFoundAndCorrectedFilters_NarrowTheList()
    {
        var vm = await LoadedAsync([
            Cache(1, userData: new() { IsSolved = true }),
            Cache(2, userData: new() { IsFound = true }),
            Cache(3, userData: new() { CorrectedCoordinate = new(50, 14) }),
            Cache(4) with { Status = GeocacheStatus.Disabled },
        ]);

        vm.SolvedOption = vm.YesNoOptions.Single(o => o.Value == true);
        await vm.WhenIdleAsync();
        CollectionAssert.AreEqual(new[] { "GC00001" }, Codes(vm));

        vm.ClearFiltersCommand.Execute(null);
        vm.FoundOption = vm.YesNoOptions.Single(o => o.Value == true);
        await vm.WhenIdleAsync();
        CollectionAssert.AreEqual(new[] { "GC00002" }, Codes(vm));

        vm.ClearFiltersCommand.Execute(null);
        vm.CorrectedOption = vm.YesNoOptions.Single(o => o.Value == true);
        await vm.WhenIdleAsync();
        CollectionAssert.AreEqual(new[] { "GC00003" }, Codes(vm));

        vm.ClearFiltersCommand.Execute(null);
        vm.SelectedStatusOption = vm.StatusOptions.Single(o => o.Value == GeocacheStatus.Disabled);
        await vm.WhenIdleAsync();
        CollectionAssert.AreEqual(new[] { "GC00004" }, Codes(vm));
    }

    [TestMethod]
    public async Task Sort_ByDifficultyDescending_PutsHardestFirst()
    {
        var vm = await LoadedAsync([Cache(1, difficulty: 1), Cache(2, difficulty: 4.5), Cache(3, difficulty: 3)]);

        vm.SelectedSortOption = vm.SortOptions.Single(o => o.Value == GeocacheSort.Difficulty);
        vm.IsDescending = true;
        await vm.WhenIdleAsync();

        CollectionAssert.AreEqual(new[] { "GC00002", "GC00003", "GC00001" }, Codes(vm));
    }

    [TestMethod]
    public async Task ClearFilters_ResetsEveryFilterAndTheSearch()
    {
        var vm = await LoadedAsync([Cache(1, GeocacheType.Mystery), Cache(2)]);
        vm.SearchText = "x";
        vm.SelectedTypeOption = vm.TypeOptions.Single(o => o.Value == GeocacheType.Mystery);
        await vm.WhenIdleAsync();
        Assert.IsTrue(vm.HasActiveFilters);
        Assert.IsTrue(vm.HasNoMatches);

        vm.ClearFiltersCommand.Execute(null);
        await vm.WhenIdleAsync();

        Assert.IsFalse(vm.HasActiveFilters);
        Assert.AreEqual(2, vm.Items.Count);
    }

    [TestMethod]
    public async Task EmptyStore_ShowsEmptyStateNotNoMatches()
    {
        var vm = await LoadedAsync([]);

        Assert.IsTrue(vm.IsEmpty);
        Assert.IsFalse(vm.HasNoMatches);
    }

    [TestMethod]
    public async Task Open_SingleSelection_NavigatesToTheCacheDetail()
    {
        var vm = await LoadedAsync([Cache(1), Cache(2)]);
        Assert.IsFalse(vm.OpenCommand.CanExecute(null));

        vm.SetSelection([vm.Items[1]]);
        vm.OpenCommand.Execute(null);

        Assert.AreEqual(typeof(GeocacheDetailViewModel), _navigation.LastViewModelType);
        Assert.AreEqual(vm.Items[1].Code, _navigation.LastParameter);
    }

    [TestMethod]
    public async Task Selection_OneItemIsTheDetail_SeveralAreNot()
    {
        var vm = await LoadedAsync([Cache(1), Cache(2)]);

        vm.SetSelection([vm.Items[0]]);
        Assert.AreEqual(vm.Items[0], vm.Detail);
        Assert.IsTrue(vm.DeleteSelectedCommand.CanExecute(null));

        vm.SetSelection([.. vm.Items]);
        Assert.IsNull(vm.Detail);
        Assert.AreEqual(2, vm.SelectedCount);

        vm.SetSelection([]);
        Assert.IsFalse(vm.DeleteSelectedCommand.CanExecute(null));
    }

    [TestMethod]
    public async Task DeleteSelected_Confirmed_RemovesFromStoreAndList()
    {
        var vm = await LoadedAsync([Cache(1), Cache(2), Cache(3)]);
        vm.SetSelection([vm.Items[0], vm.Items[2]]);

        await vm.DeleteSelectedCommand.ExecuteAsync(null);

        CollectionAssert.AreEqual(new[] { "GC00002" }, Codes(vm));
        Assert.AreEqual(1, (await _store.QueryAsync(new())).TotalCount);
    }

    [TestMethod]
    public async Task DeleteSelected_Denied_KeepsEverything()
    {
        var vm = await LoadedAsync([Cache(1), Cache(2)]);
        _confirm.Answer = ConfirmationResult.Denied;
        vm.SetSelection([vm.Items[0]]);

        await vm.DeleteSelectedCommand.ExecuteAsync(null);

        Assert.AreEqual(2, (await _store.QueryAsync(new())).TotalCount);
    }

    [TestMethod]
    public async Task NewList_CreatesItAndOffersItAsFilter()
    {
        var vm = await LoadedAsync([Cache(1)]);
        _prompt.Answer = "Prague mysteries";

        await vm.NewListCommand.ExecuteAsync(null);

        Assert.AreEqual("Prague mysteries", vm.Lists.Single().Name);
        Assert.IsTrue(vm.ListOptions.Any(o => o.Value?.Name == "Prague mysteries"));
    }

    [TestMethod]
    public async Task NewList_DuplicateName_ShowsErrorInsteadOfThrowing()
    {
        var vm = await LoadedAsync([Cache(1)]);
        await _store.CreateListAsync("Trip");
        _prompt.Answer = "trip";

        await vm.NewListCommand.ExecuteAsync(null);

        Assert.AreEqual(CachesNoticeSeverity.Error, vm.Notice?.Severity);
    }

    [TestMethod]
    public async Task NewList_Cancelled_DoesNothing()
    {
        var vm = await LoadedAsync([Cache(1)]);
        _prompt.Answer = null;

        await vm.NewListCommand.ExecuteAsync(null);

        Assert.AreEqual(0, vm.Lists.Count);
    }

    [TestMethod]
    public async Task MoveSelectedToList_AddsAndLeavesTheListBeingViewed()
    {
        var vm = await LoadedAsync([Cache(1), Cache(2)]);
        var from = await _store.CreateListAsync("From");
        var to = await _store.CreateListAsync("To");
        await _store.AddToListAsync(from.Id, [GeocacheCode.Parse("GC00001"), GeocacheCode.Parse("GC00002")]);
        await vm.RefreshAsync();
        vm.SelectedListOption = vm.ListOptions.Single(o => o.Value?.Id == from.Id);
        await vm.WhenIdleAsync();
        vm.SetSelection([vm.Items[0]]);

        await vm.MoveSelectedToListCommand.ExecuteAsync(vm.Lists.Single(l => l.Id == to.Id));
        await vm.WhenIdleAsync();

        CollectionAssert.AreEqual(new[] { "GC00002" }, Codes(vm));
        Assert.AreEqual(1, (await _store.QueryAsync(new() { ListId = to.Id })).TotalCount);
    }

    [TestMethod]
    public async Task RenameAndDeleteList_UpdateTheListAndKeepItsCaches()
    {
        var vm = await LoadedAsync([Cache(1)]);
        var list = await _store.CreateListAsync("Old");
        await _store.AddToListAsync(list.Id, [GeocacheCode.Parse("GC00001")]);
        await vm.RefreshAsync();
        vm.SelectedListOption = vm.ListOptions.Single(o => o.Value?.Id == list.Id);
        await vm.WhenIdleAsync();

        _prompt.Answer = "New";
        await vm.RenameListCommand.ExecuteAsync(null);
        Assert.AreEqual("New", vm.Lists.Single().Name);

        await vm.DeleteListCommand.ExecuteAsync(null);
        await vm.WhenIdleAsync();
        Assert.AreEqual(0, vm.Lists.Count);
        Assert.IsNull(vm.SelectedListOption.Value);
        Assert.AreEqual(1, vm.Items.Count);
    }

    [TestMethod]
    public async Task Import_PocketQueryFiles_AddsCachesToAListNamedAfterTheFile()
    {
        var vm = await LoadedAsync([]);
        _picker.AddFixture("groundspeak-1.0.1-pq.gpx", "Prague.gpx");
        _picker.AddFixture("groundspeak-1.0.1-pq-wpts.gpx", "Prague-wpts.gpx");

        await vm.ImportGpxCommand.ExecuteAsync(null);

        var list = vm.Lists.Single();
        Assert.AreEqual("Prague", list.Name);
        Assert.AreEqual(3, list.Count);
        Assert.AreEqual(3, (await _store.QueryAsync(new())).TotalCount);
        Assert.AreEqual(3, vm.Items.Count);
        Assert.AreEqual(3, vm.Notice?.Summary?.Added);
        Assert.AreEqual(0, vm.Notice?.Summary?.Updated);
        Assert.AreEqual(CachesNoticeSeverity.Warning, vm.Notice?.Severity);
        Assert.IsTrue(vm.Notice?.Summary?.Issues.Any(i => i.Subject == "GCBADBAD"));
    }

    [TestMethod]
    public async Task ImportGpx_StoreFails_ReportsErrorInsteadOfThrowing()
    {
        var vm = CreateViewModel();
        _store.UpsertFailure = new InvalidOperationException("table Geocaches has no column named SearchName");
        _picker.AddFixture("groundspeak-1.0.1-pq.gpx", "Prague.gpx");

        await vm.ImportGpxCommand.ExecuteAsync(null);

        Assert.AreEqual(CachesNoticeSeverity.Error, vm.Notice?.Severity);
        var error = vm.Notice!.Summary!.Errors.Single();
        Assert.AreEqual(GpxImportIssueKind.SaveFailed, error.Kind);
        Assert.AreEqual("Prague.gpx", error.Subject);
        Assert.IsFalse(vm.IsImporting);
    }

    [TestMethod]
    public async Task Import_SameFileTwice_UpdatesInsteadOfAddingAndReusesTheList()
    {
        var vm = await LoadedAsync([]);
        _picker.AddFixture("groundspeak-1.0.1-pq.gpx", "Prague.gpx");
        await vm.ImportGpxCommand.ExecuteAsync(null);

        await vm.ImportGpxCommand.ExecuteAsync(null);

        Assert.AreEqual(0, vm.Notice?.Summary?.Added);
        Assert.AreEqual(3, vm.Notice?.Summary?.Updated);
        Assert.AreEqual(1, vm.Lists.Count);
    }

    [TestMethod]
    public async Task Import_PlainWaypoints_AreOnlyCounted()
    {
        var vm = await LoadedAsync([]);
        _picker.AddFixture("plain.gpx");

        await vm.ImportGpxCommand.ExecuteAsync(null);

        Assert.AreEqual(2, vm.Notice?.Summary?.PlainWaypoints);
        Assert.AreEqual(0, vm.Items.Count);
        Assert.AreEqual(0, vm.Lists.Count);
    }

    [TestMethod]
    public async Task Import_BrokenFile_ReportsAnErrorAndStillImportsTheRest()
    {
        var vm = await LoadedAsync([]);
        _picker.AddBytes("broken.gpx", Encoding.UTF8.GetBytes("<gpx><wpt"));
        _picker.AddFixture("groundspeak-1.0.1-pq.gpx", "Good.gpx");

        await vm.ImportGpxCommand.ExecuteAsync(null);

        Assert.AreEqual(3, vm.Notice?.Summary?.Added);
        Assert.IsTrue(vm.Notice?.Summary?.Errors.Any(e => e.Subject.StartsWith("broken", StringComparison.Ordinal)));
    }

    [TestMethod]
    public async Task Import_PickerCancelled_ChangesNothing()
    {
        var vm = await LoadedAsync([Cache(1)]);

        await vm.ImportGpxCommand.ExecuteAsync(null);

        Assert.IsNull(vm.Notice);
        Assert.AreEqual(1, vm.Items.Count);
    }
}
