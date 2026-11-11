using GcToolkit.Core.Coordinates;
using GcToolkit.Core.Geocaches;
using GcToolkit.Core.Services;
using GcToolkit.Core.Services.Settings;
using GcToolkit.Core.Tests.Fakes;
using GcToolkit.Core.ViewModels;
using GcToolkit.Core.ViewModels.Tools;
using Microsoft.UI.Xaml;

namespace GcToolkit.Core.Tests.ViewModels;

[TestClass]
public class GeocacheDetailViewModelTests
{
    private static readonly GeocacheCode _code = GeocacheCode.Parse("GC8KX2Q");
    private static readonly GeoCoordinate _posted = new(49.2057, 16.5761);
    private static readonly GeoCoordinate _corrected = new(49.2100, 16.5800);

    private readonly InMemoryGeocacheStore _store = new();
    private readonly FakeClipboardService _clipboard = new();
    private readonly FakeNavigationService _navigation = new();
    private readonly FakePreferences _preferences = new();

    private static Geocache CreateCache(Func<Geocache, Geocache>? customize = null)
    {
        Geocache cache = new()
        {
            Code = _code,
            Name = "The Lost Legion",
            Type = GeocacheType.Mystery,
            Size = GeocacheSize.Small,
            Status = GeocacheStatus.Available,
            Difficulty = 3,
            Terrain = 1.5,
            Coordinate = _posted,
            Owner = "Pathfinder",
            Hint = "Look under the rock",
            ShortDescription = "Short & sweet",
            LongDescription = "<p>Hello <b>world</b></p><script>alert(1)</script>",
            LongDescriptionIsHtml = true,
        };
        return customize?.Invoke(cache) ?? cache;
    }

    private async Task<GeocacheDetailViewModel> LoadAsync(Func<Geocache, Geocache>? customize = null, Dictionary<string, string>? strings = null)
    {
        await _store.UpsertAsync([CreateCache(customize)]);
        GeocacheDetailViewModel vm = new(_store, _clipboard, _navigation, new FakeStringLocalizer(strings), _preferences);
        await vm.LoadAsync(_code);
        return vm;
    }

    [TestMethod]
    public async Task LoadAsync_KnownCode_FillsTheHeader()
    {
        var vm = await LoadAsync();

        Assert.IsTrue(vm.IsLoaded);
        Assert.IsFalse(vm.NotFound);
        Assert.AreEqual("The Lost Legion", vm.Name);
        Assert.AreEqual("The Lost Legion", vm.PageTitle);
        Assert.AreEqual("GC8KX2Q", vm.Code);
        Assert.AreEqual("GeocacheType_Mystery", vm.TypeLabel);
        Assert.AreEqual("GeocacheSize_Small", vm.SizeLabel);
        Assert.AreEqual("GeocacheStatus_Available", vm.StatusLabel);
        Assert.AreEqual("D 3 / T 1.5", vm.DifficultyTerrain);
        Assert.AreEqual("Pathfinder", vm.Owner);
    }

    [TestMethod]
    public async Task LoadAsync_UnknownCode_ReportsNotFound()
    {
        GeocacheDetailViewModel vm = new(_store, _clipboard, _navigation, new FakeStringLocalizer(), _preferences);

        await vm.LoadAsync(_code);

        Assert.IsTrue(vm.NotFound);
        Assert.IsFalse(vm.IsLoaded);
    }

    [TestMethod]
    public async Task OnNavigatedTo_CodeText_LoadsTheCache()
    {
        await _store.UpsertAsync([CreateCache()]);
        GeocacheDetailViewModel vm = new(_store, _clipboard, _navigation, new FakeStringLocalizer(), _preferences);

        vm.OnNavigatedTo("gc8kx2q");
        await Task.Delay(50);

        Assert.IsTrue(vm.IsLoaded);
    }

    [TestMethod]
    public async Task Coordinates_UseThePreferredFormat()
    {
        _preferences.CoordinateFormat = CoordinateFormat.DecimalDegrees;

        var vm = await LoadAsync(c => c with { UserData = new GeocacheUserData { CorrectedCoordinate = _corrected } });

        Assert.AreEqual(CoordinateFormatter.Format(_posted, CoordinateFormat.DecimalDegrees), vm.PostedCoordinate);
        Assert.AreEqual(CoordinateFormatter.Format(_corrected, CoordinateFormat.DecimalDegrees), vm.CorrectedCoordinate);
        Assert.IsTrue(vm.HasCorrectedCoordinate);
    }

    [TestMethod]
    public async Task Coordinates_WithoutCorrection_HaveNoCorrectedValue()
    {
        var vm = await LoadAsync();

        Assert.IsFalse(vm.HasCorrectedCoordinate);
        Assert.AreEqual(vm.PostedCoordinate, vm.EffectiveCoordinate);
    }

    [TestMethod]
    public async Task Attributes_UseLocalizedNames_AndMarkNegatedOnes()
    {
        var vm = await LoadAsync(
            c => c with { Attributes = [new(1, IsPositive: false), new(13, IsPositive: true), new(999, IsPositive: true)] },
            new() { ["GeocacheAttribute_1"] = "Dogs", ["GeocacheAttribute_13"] = "Available 24/7", ["GeocacheAttributeUnknownFormat"] = "Attribute {0}" });

        Assert.AreEqual("Dogs", vm.Attributes[0].Text);
        Assert.IsTrue(vm.Attributes[0].IsNegated);
        Assert.AreEqual("Available 24/7", vm.Attributes[1].Text);
        Assert.IsFalse(vm.Attributes[1].IsNegated);
        Assert.AreEqual("Attribute 999", vm.Attributes[2].Text);
    }

    [TestMethod]
    public async Task Hint_StartsEncoded_AndOneTapDecodes()
    {
        var vm = await LoadAsync();
        Assert.IsTrue(vm.HasHint);
        Assert.AreEqual("Ybbx haqre gur ebpx", vm.DisplayedHint);

        vm.ToggleHintCommand.Execute(null);

        Assert.IsTrue(vm.IsHintRevealed);
        Assert.AreEqual("Look under the rock", vm.DisplayedHint);
    }

    [TestMethod]
    public async Task Hint_ToggledTwice_IsEncodedAgain()
    {
        var vm = await LoadAsync();
        var encoded = vm.DisplayedHint;

        vm.ToggleHintCommand.Execute(null);
        vm.ToggleHintCommand.Execute(null);

        Assert.AreEqual(encoded, vm.DisplayedHint);
    }

    [TestMethod]
    public async Task Hint_Missing_HasNoHint()
    {
        var vm = await LoadAsync(c => c with { Hint = null });

        Assert.IsFalse(vm.HasHint);
        Assert.AreEqual(string.Empty, vm.DisplayedHint);
    }

    [TestMethod]
    public async Task Waypoints_MarkHiddenCoordinates()
    {
        var vm = await LoadAsync(c => c with
        {
            Waypoints =
            [
                new Waypoint { Prefix = "PK", Name = "Parking", Type = WaypointType.Parking, Coordinate = _posted },
                new Waypoint { Prefix = "FI", Name = "Final", Type = WaypointType.Final },
            ],
        });

        Assert.IsTrue(vm.HasWaypoints);
        Assert.IsFalse(vm.Waypoints[0].IsHidden);
        Assert.AreEqual(CoordinateFormatter.Format(_posted, CoordinateFormat.DegreesDecimalMinutes), vm.Waypoints[0].CoordinateText);
        Assert.IsTrue(vm.Waypoints[1].IsHidden);
    }

    [TestMethod]
    public async Task Logs_AreNewestFirst_AndFilterableByType()
    {
        var vm = await LoadAsync(c => c with
        {
            Logs =
            [
                new GeocacheLog { Type = GeocacheLogType.Found, Author = "a", Date = new(2024, 1, 1, 0, 0, 0, TimeSpan.Zero), Text = "old" },
                new GeocacheLog { Type = GeocacheLogType.DidNotFind, Author = "b", Date = new(2024, 3, 1, 0, 0, 0, TimeSpan.Zero), Text = "mid" },
                new GeocacheLog { Type = GeocacheLogType.Found, Author = "c", Date = new(2024, 5, 1, 0, 0, 0, TimeSpan.Zero), Text = "<b>new</b>", TextIsHtml = true },
            ],
        });

        CollectionAssert.AreEqual(new[] { "new", "mid", "old" }, vm.Logs.Select(l => l.Text).ToArray());
        Assert.AreEqual(3, vm.LogFilters[0].Count);

        vm.SelectedLogFilter = vm.LogFilters.Single(f => f.Type == GeocacheLogType.Found);

        CollectionAssert.AreEqual(new[] { "new", "old" }, vm.Logs.Select(l => l.Text).ToArray());

        vm.SelectedLogFilter = vm.LogFilters[0];

        Assert.AreEqual(3, vm.Logs.Count);
    }

    [TestMethod]
    public async Task ListingHtml_DropsListingScripts()
    {
        var vm = await LoadAsync();

        Assert.IsFalse(vm.ListingHtml.Contains("alert(1)", StringComparison.Ordinal));
        Assert.IsTrue(vm.ListingHtml.Contains("<b>world</b>", StringComparison.Ordinal));
    }

    [TestMethod]
    public async Task IsDarkTheme_Changed_RebuildsTheListing()
    {
        var vm = await LoadAsync();
        var light = vm.ListingHtml;

        vm.IsDarkTheme = true;

        Assert.AreNotEqual(light, vm.ListingHtml);
        Assert.IsTrue(vm.ListingHtml.Contains("color-scheme:dark", StringComparison.Ordinal));
    }

    [TestMethod]
    public async Task UserNote_ShowsInTheListing()
    {
        var vm = await LoadAsync(c => c with { UserData = new GeocacheUserData { Note = "Check the bridge" } });

        Assert.IsTrue(vm.HasUserNote);
        Assert.IsTrue(vm.ListingHtml.Contains("Check the bridge", StringComparison.Ordinal));
    }

    [TestMethod]
    public async Task CopyCoordinates_CopiesTheCorrectedOneWhenThereIsOne()
    {
        var vm = await LoadAsync(c => c with { UserData = new GeocacheUserData { CorrectedCoordinate = _corrected } });

        vm.CopyCoordinatesCommand.Execute(null);

        Assert.AreEqual(CoordinateFormatter.Format(_corrected, CoordinateFormat.DegreesDecimalMinutes), _clipboard.LastText);
    }

    [TestMethod]
    public async Task OpenInConversionTool_PassesTheCoordinateAlong()
    {
        var vm = await LoadAsync();

        vm.OpenInConversionToolCommand.Execute(null);

        Assert.AreEqual(typeof(CoordinateConversionViewModel), _navigation.LastViewModelType);
        Assert.AreEqual(vm.PostedCoordinate, _navigation.LastParameter);
    }

    [TestMethod]
    public async Task Solve_IsDisabledUntilTheWorkbenchExists()
    {
        var vm = await LoadAsync();

        Assert.IsFalse(vm.SolveCommand.CanExecute(null));
    }

    [TestMethod]
    public async Task SelectedSection_Changed_UpdatesTheVisibilityFlags()
    {
        var vm = await LoadAsync();
        Assert.IsTrue(vm.IsListingSelected);

        vm.SelectedSection = GeocacheDetailSection.Logs;

        Assert.IsTrue(vm.IsLogsSelected);
        Assert.IsFalse(vm.IsListingSelected);
    }

    [TestMethod]
    public void ConversionTool_StringParameter_PrefillsTheInput()
    {
        CoordinateConversionViewModel vm = new(
            new StubCatalogService("CoordinateConversion"),
            new FakeRecentsService(),
            new FakeFavoriteToolsService(),
            new FakeStringLocalizer(),
            _clipboard,
            new FakeShareService());

        vm.OnNavigatedTo("N 49° 12.342' E 016° 34.566'");

        Assert.AreEqual("N 49° 12.342' E 016° 34.566'", vm.InputText);
        Assert.IsTrue(vm.HasResult);
    }

    private sealed class FakePreferences : IAppPreferences
    {
        public int DataVersion { get; set; }

        public bool FirstStart { get; set; }

        public int LaunchCount { get; set; }

        public bool OfferUserRating { get; set; }

        public ElementTheme Theme { get; set; } = ElementTheme.Default;

        public CoordinateFormat CoordinateFormat { get; set; } = CoordinateFormat.DegreesDecimalMinutes;

        public void Clear()
        {
        }
    }
}
