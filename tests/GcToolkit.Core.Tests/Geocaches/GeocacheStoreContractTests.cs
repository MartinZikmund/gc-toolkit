using GcToolkit.Core.Coordinates;
using GcToolkit.Core.Geocaches;

namespace GcToolkit.Core.Tests.Geocaches;

/// <summary>Behaviour every <see cref="IGeocacheStore"/> must have. Run against the fake and the SQLite store.</summary>
public abstract class GeocacheStoreContractTests
{
    protected abstract IGeocacheStore CreateStore();

    protected static GeocacheCode Code(string value) => GeocacheCode.Parse(value);

    protected static Geocache Cache(
        string code = "GC8KX2Q",
        string name = "The Lost Legion",
        GeocacheType type = GeocacheType.Traditional,
        double? difficulty = 2,
        GeocacheUserData? userData = null) => new()
        {
            Code = Code(code),
            Name = name,
            Type = type,
            Difficulty = difficulty,
            Terrain = 1.5,
            Coordinate = new(50.06666, 14.41666),
            UserData = userData ?? new(),
        };

    [TestMethod]
    public async Task GetAsync_UnknownCode_ReturnsNull()
    {
        var store = CreateStore();

        Assert.IsNull(await store.GetAsync(Code("GC1")));
    }

    [TestMethod]
    public async Task Upsert_NewCache_RoundTripsEveryField()
    {
        var store = CreateStore();
        var cache = Cache() with
        {
            Size = GeocacheSize.Small,
            Status = GeocacheStatus.Disabled,
            Owner = "Pražák",
            PlacedOn = new DateTimeOffset(2019, 5, 4, 0, 0, 0, TimeSpan.Zero),
            IsPremiumOnly = true,
            FavoritePoints = 42,
            Country = "Czechia",
            State = "Prague",
            ShortDescription = "short",
            LongDescription = "<b>long</b>",
            LongDescriptionIsHtml = true,
            Hint = "under the bench",
            SourceId = "gpx",
            UpdatedAt = new DateTimeOffset(2024, 1, 2, 3, 4, 5, TimeSpan.Zero),
            Attributes = [new GeocacheAttribute(1, true), new GeocacheAttribute(8, false)],
            Waypoints = [new Waypoint { Prefix = "P1", Name = "Parking", Type = WaypointType.Parking, Coordinate = new(50.1, 14.2), Note = "free" }],
            Logs = [new GeocacheLog { Id = "L1", Type = GeocacheLogType.Found, Author = "alice", Date = new DateTimeOffset(2020, 1, 1, 12, 0, 0, TimeSpan.Zero), Text = "TFTC" }],
            Images = [new GeocacheImage("https://example.com/a.jpg", "A", "first")],
            UserData = new() { CorrectedCoordinate = new(50.2, 14.3), Note = "mine", IsSolved = true, IsFound = true },
        };

        var result = await store.UpsertAsync([cache]);
        var loaded = await store.GetAsync(cache.Code);

        Assert.AreEqual(new UpsertResult(1, 0), result);
        Assert.IsNotNull(loaded);
        Assert.AreEqual(cache with { Attributes = [], Waypoints = [], Logs = [], Images = [] }, loaded with { Attributes = [], Waypoints = [], Logs = [], Images = [] });
        CollectionAssert.AreEqual(cache.Attributes.ToList(), loaded.Attributes.ToList());
        CollectionAssert.AreEqual(cache.Waypoints.ToList(), loaded.Waypoints.ToList());
        CollectionAssert.AreEqual(cache.Logs.ToList(), loaded.Logs.ToList());
        CollectionAssert.AreEqual(cache.Images.ToList(), loaded.Images.ToList());
    }

    [TestMethod]
    public async Task Upsert_ExistingCache_UpdatesSourceDataAndKeepsUserData()
    {
        var store = CreateStore();
        var userData = new GeocacheUserData { CorrectedCoordinate = new(50.2, 14.3), Note = "mine", IsSolved = true };
        await store.UpsertAsync([Cache(name: "Old name", userData: userData)]);

        var result = await store.UpsertAsync([Cache(name: "New name")]);
        var loaded = await store.GetAsync(Code("GC8KX2Q"));

        Assert.AreEqual(new UpsertResult(0, 1), result);
        Assert.AreEqual("New name", loaded!.Name);
        Assert.AreEqual(userData, loaded.UserData);
    }

    [TestMethod]
    public async Task Upsert_Batch_CountsAddedAndUpdated()
    {
        var store = CreateStore();
        await store.UpsertAsync([Cache("GC1AAA")]);

        var result = await store.UpsertAsync([Cache("GC1AAA"), Cache("GC2BBB"), Cache("GC3CCC")]);

        Assert.AreEqual(new UpsertResult(2, 1), result);
    }

    [TestMethod]
    public async Task UpdateUserData_StoredCache_ReplacesUserDataOnly()
    {
        var store = CreateStore();
        await store.UpsertAsync([Cache(name: "Keep me")]);

        var updated = await store.UpdateUserDataAsync(Code("GC8KX2Q"), new() { Note = "n", IsFound = true });
        var loaded = await store.GetAsync(Code("GC8KX2Q"));

        Assert.IsTrue(updated);
        Assert.AreEqual("Keep me", loaded!.Name);
        Assert.AreEqual("n", loaded.UserData.Note);
        Assert.IsTrue(loaded.UserData.IsFound);
    }

    [TestMethod]
    public async Task UpdateUserData_UnknownCache_ReturnsFalse()
        => Assert.IsFalse(await CreateStore().UpdateUserDataAsync(Code("GC1"), new()));

    [TestMethod]
    public async Task Delete_StoredAndUnknown_ReturnsHowManyWereStored()
    {
        var store = CreateStore();
        await store.UpsertAsync([Cache("GC1AAA"), Cache("GC2BBB")]);

        var deleted = await store.DeleteAsync([Code("GC1AAA"), Code("GC9ZZZ")]);

        Assert.AreEqual(1, deleted);
        Assert.IsNull(await store.GetAsync(Code("GC1AAA")));
        Assert.IsNotNull(await store.GetAsync(Code("GC2BBB")));
    }

    [TestMethod]
    public async Task Query_Empty_ReturnsEverythingSortedByName()
    {
        var store = CreateStore();
        await store.UpsertAsync([Cache("GC1", "beta"), Cache("GC2", "Alpha"), Cache("GC3", "gamma")]);

        var page = await store.QueryAsync(new());

        Assert.AreEqual(3, page.TotalCount);
        CollectionAssert.AreEqual(new[] { "Alpha", "beta", "gamma" }, page.Items.Select(i => i.Name).ToArray());
    }

    [TestMethod]
    public async Task Query_Text_MatchesNameOrCodeIgnoringCase()
    {
        var store = CreateStore();
        await store.UpsertAsync([Cache("GC1AAA", "Old Bridge"), Cache("GC2BBB", "Forest"), Cache("GC3BRI", "Other")]);

        var page = await store.QueryAsync(new() { Text = "bri" });

        CollectionAssert.AreEquivalent(new[] { "GC1AAA", "GC3BRI" }, page.Items.Select(i => i.Code.Value).ToArray());
    }

    [TestMethod]
    public async Task Query_Text_AlsoMatchesOwner()
    {
        var store = CreateStore();
        await store.UpsertAsync([Cache("GC1AAA", "One") with { Owner = "Pražák" }, Cache("GC2BBB", "Two") with { Owner = "someone" }]);

        var page = await store.QueryAsync(new() { Text = "praž" });

        CollectionAssert.AreEqual(new[] { "GC1AAA" }, page.Items.Select(i => i.Code.Value).ToArray());
    }

    [TestMethod]
    public async Task Query_SizeFilter_KeepsOnlyListedSizes()
    {
        var store = CreateStore();
        await store.UpsertAsync([
            Cache("GC1AAA") with { Size = GeocacheSize.Micro },
            Cache("GC2BBB") with { Size = GeocacheSize.Regular },
            Cache("GC3CCC") with { Size = GeocacheSize.Large }]);

        var page = await store.QueryAsync(new() { Sizes = [GeocacheSize.Micro, GeocacheSize.Large] });

        CollectionAssert.AreEquivalent(new[] { "GC1AAA", "GC3CCC" }, page.Items.Select(i => i.Code.Value).ToArray());
    }

    [TestMethod]
    public async Task Query_DifficultyAndTerrainRanges_AreInclusiveAndSkipUnrated()
    {
        var store = CreateStore();
        await store.UpsertAsync([
            Cache("GC1AAA", difficulty: 1) with { Terrain = 1 },
            Cache("GC2BBB", difficulty: 3) with { Terrain = 2.5 },
            Cache("GC3CCC", difficulty: 5) with { Terrain = 5 },
            Cache("GC4DDD", difficulty: null) with { Terrain = null }]);

        var difficulty = await store.QueryAsync(new() { MinDifficulty = 3, MaxDifficulty = 5 });
        var terrain = await store.QueryAsync(new() { MaxTerrain = 2.5 });

        CollectionAssert.AreEquivalent(new[] { "GC2BBB", "GC3CCC" }, difficulty.Items.Select(i => i.Code.Value).ToArray());
        CollectionAssert.AreEquivalent(new[] { "GC1AAA", "GC2BBB" }, terrain.Items.Select(i => i.Code.Value).ToArray());
    }

    [TestMethod]
    public async Task Query_CorrectedCoordinateFilter_SeparatesCorrectedFromPosted()
    {
        var store = CreateStore();
        await store.UpsertAsync([Cache("GC1AAA", userData: new() { CorrectedCoordinate = new(50.2, 14.3) }), Cache("GC2BBB")]);

        var corrected = await store.QueryAsync(new() { HasCorrectedCoordinate = true });
        var posted = await store.QueryAsync(new() { HasCorrectedCoordinate = false });

        CollectionAssert.AreEqual(new[] { "GC1AAA" }, corrected.Items.Select(i => i.Code.Value).ToArray());
        CollectionAssert.AreEqual(new[] { "GC2BBB" }, posted.Items.Select(i => i.Code.Value).ToArray());
    }

    [TestMethod]
    public async Task Query_TypeAndStatusFilters_AreCombined()
    {
        var store = CreateStore();
        await store.UpsertAsync([
            Cache("GC1AAA", type: GeocacheType.Mystery),
            Cache("GC2BBB", type: GeocacheType.Multi),
            Cache("GC3CCC", type: GeocacheType.Mystery) with { Status = GeocacheStatus.Archived },
        ]);

        var page = await store.QueryAsync(new() { Types = [GeocacheType.Mystery], Statuses = [GeocacheStatus.Available] });

        CollectionAssert.AreEqual(new[] { "GC1AAA" }, page.Items.Select(i => i.Code.Value).ToArray());
    }

    [TestMethod]
    public async Task Query_FoundFilter_SeparatesFoundFromUnfound()
    {
        var store = CreateStore();
        await store.UpsertAsync([Cache("GC1AAA", userData: new() { IsFound = true }), Cache("GC2BBB")]);

        var found = await store.QueryAsync(new() { IsFound = true });
        var unfound = await store.QueryAsync(new() { IsFound = false });

        CollectionAssert.AreEqual(new[] { "GC1AAA" }, found.Items.Select(i => i.Code.Value).ToArray());
        CollectionAssert.AreEqual(new[] { "GC2BBB" }, unfound.Items.Select(i => i.Code.Value).ToArray());
    }

    [TestMethod]
    public async Task Query_SortByDifficultyDescending_PutsHardestFirst()
    {
        var store = CreateStore();
        await store.UpsertAsync([Cache("GC1", difficulty: 1), Cache("GC2", difficulty: 5), Cache("GC3", difficulty: 3)]);

        var page = await store.QueryAsync(new() { Sort = GeocacheSort.Difficulty, Descending = true });

        CollectionAssert.AreEqual(new[] { "GC2", "GC3", "GC1" }, page.Items.Select(i => i.Code.Value).ToArray());
    }

    [TestMethod]
    public async Task Query_Paging_ReturnsSliceAndFullTotal()
    {
        var store = CreateStore();
        await store.UpsertAsync([.. Enumerable.Range(1, 25).Select(i => Cache($"GC{i:D3}", $"Cache {i:D3}"))]);

        var page = await store.QueryAsync(new() { Skip = 10, Take = 5 });

        Assert.AreEqual(25, page.TotalCount);
        CollectionAssert.AreEqual(
            new[] { "Cache 011", "Cache 012", "Cache 013", "Cache 014", "Cache 015" },
            page.Items.Select(i => i.Name).ToArray());
    }

    [TestMethod]
    public async Task Query_Summary_UsesCorrectedCoordinateWhenSet()
    {
        var store = CreateStore();
        var corrected = new GeoCoordinate(50.2, 14.3);
        await store.UpsertAsync([Cache(userData: new() { CorrectedCoordinate = corrected })]);

        var summary = (await store.QueryAsync(new())).Items.Single();

        Assert.AreEqual(corrected, summary.Coordinate);
        Assert.IsTrue(summary.HasCorrectedCoordinate);
    }

    [TestMethod]
    public async Task Lists_CreateAndGet_ReturnsThemWithCounts()
    {
        var store = CreateStore();
        var list = await store.CreateListAsync("Weekend trip");

        var lists = await store.GetListsAsync();

        Assert.AreEqual(1, lists.Count);
        Assert.AreEqual(new GeocacheList(list.Id, "Weekend trip", 0), lists[0]);
    }

    [TestMethod]
    public async Task Lists_DuplicateName_Throws()
    {
        var store = CreateStore();
        await store.CreateListAsync("Trip");

        await Assert.ThrowsExactlyAsync<InvalidOperationException>(() => store.CreateListAsync("trip"));
    }

    [TestMethod]
    public async Task Lists_Rename_ChangesNameAndRejectsTakenOne()
    {
        var store = CreateStore();
        var a = await store.CreateListAsync("A");
        await store.CreateListAsync("B");

        Assert.IsTrue(await store.RenameListAsync(a.Id, "C"));
        await Assert.ThrowsExactlyAsync<InvalidOperationException>(() => store.RenameListAsync(a.Id, "b"));
        Assert.IsFalse(await store.RenameListAsync(9999, "Z"));
        CollectionAssert.AreEqual(new[] { "B", "C" }, (await store.GetListsAsync()).Select(l => l.Name).ToArray());
    }

    [TestMethod]
    public async Task Lists_CacheCanBeInSeveralLists()
    {
        var store = CreateStore();
        await store.UpsertAsync([Cache("GC1AAA"), Cache("GC2BBB")]);
        var a = await store.CreateListAsync("A");
        var b = await store.CreateListAsync("B");

        var addedToA = await store.AddToListAsync(a.Id, [Code("GC1AAA"), Code("GC2BBB")]);
        var addedToB = await store.AddToListAsync(b.Id, [Code("GC1AAA")]);

        Assert.AreEqual(2, addedToA);
        Assert.AreEqual(1, addedToB);
        CollectionAssert.AreEqual(new[] { "A", "B" }, (await store.GetListsForAsync(Code("GC1AAA"))).Select(l => l.Name).ToArray());
        CollectionAssert.AreEqual(new[] { "A" }, (await store.GetListsForAsync(Code("GC2BBB"))).Select(l => l.Name).ToArray());
    }

    [TestMethod]
    public async Task Lists_AddTwiceOrUnknownCode_IsSkipped()
    {
        var store = CreateStore();
        await store.UpsertAsync([Cache("GC1AAA")]);
        var list = await store.CreateListAsync("A");
        await store.AddToListAsync(list.Id, [Code("GC1AAA")]);

        var added = await store.AddToListAsync(list.Id, [Code("GC1AAA"), Code("GC9ZZZ")]);

        Assert.AreEqual(0, added);
        Assert.AreEqual(1, (await store.GetListsAsync()).Single().Count);
    }

    [TestMethod]
    public async Task Query_ByList_ReturnsOnlyItsMembers()
    {
        var store = CreateStore();
        await store.UpsertAsync([Cache("GC1AAA"), Cache("GC2BBB")]);
        var list = await store.CreateListAsync("A");
        await store.AddToListAsync(list.Id, [Code("GC2BBB")]);

        var page = await store.QueryAsync(new() { ListId = list.Id });

        CollectionAssert.AreEqual(new[] { "GC2BBB" }, page.Items.Select(i => i.Code.Value).ToArray());
        Assert.AreEqual(1, page.TotalCount);
    }

    [TestMethod]
    public async Task Lists_RemoveFrom_DropsMembershipButKeepsCache()
    {
        var store = CreateStore();
        await store.UpsertAsync([Cache("GC1AAA")]);
        var list = await store.CreateListAsync("A");
        await store.AddToListAsync(list.Id, [Code("GC1AAA")]);

        var removed = await store.RemoveFromListAsync(list.Id, [Code("GC1AAA")]);

        Assert.AreEqual(1, removed);
        Assert.AreEqual(0, (await store.GetListsAsync()).Single().Count);
        Assert.IsNotNull(await store.GetAsync(Code("GC1AAA")));
    }

    [TestMethod]
    public async Task Lists_DeleteList_KeepsItsCaches()
    {
        var store = CreateStore();
        await store.UpsertAsync([Cache("GC1AAA")]);
        var list = await store.CreateListAsync("A");
        await store.AddToListAsync(list.Id, [Code("GC1AAA")]);

        Assert.IsTrue(await store.DeleteListAsync(list.Id));

        Assert.AreEqual(0, (await store.GetListsAsync()).Count);
        Assert.IsNotNull(await store.GetAsync(Code("GC1AAA")));
    }

    [TestMethod]
    public async Task Delete_CacheInList_RemovesItFromTheList()
    {
        var store = CreateStore();
        await store.UpsertAsync([Cache("GC1AAA"), Cache("GC2BBB")]);
        var list = await store.CreateListAsync("A");
        await store.AddToListAsync(list.Id, [Code("GC1AAA"), Code("GC2BBB")]);

        await store.DeleteAsync([Code("GC1AAA")]);

        Assert.AreEqual(1, (await store.GetListsAsync()).Single().Count);
    }

    [TestMethod]
    public async Task Upsert_MergedCache_KeepsItsListMemberships()
    {
        var store = CreateStore();
        await store.UpsertAsync([Cache("GC1AAA")]);
        var list = await store.CreateListAsync("A");
        await store.AddToListAsync(list.Id, [Code("GC1AAA")]);

        await store.UpsertAsync([Cache("GC1AAA", name: "Renamed")]);

        Assert.AreEqual(1, (await store.GetListsAsync()).Single().Count);
    }
}

[TestClass]
public class InMemoryGeocacheStoreTests : GeocacheStoreContractTests
{
    protected override IGeocacheStore CreateStore() => new Fakes.InMemoryGeocacheStore();
}
