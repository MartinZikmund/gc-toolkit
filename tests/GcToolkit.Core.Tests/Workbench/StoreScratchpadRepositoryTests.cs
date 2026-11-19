using GcToolkit.Core.Geocaches;
using GcToolkit.Core.Tests.Fakes;
using GcToolkit.Core.Workbench;

namespace GcToolkit.Core.Tests.Workbench;

[TestClass]
public class StoreScratchpadRepositoryTests
{
    private static readonly GeocacheCode Stored = GeocacheCode.Parse("GC1AAA");
    private static readonly GeocacheCode Typed = GeocacheCode.Parse("GC2BBB");

    private readonly InMemoryGeocacheStore _store = new();
    private readonly InMemoryPreferences _preferences = InMemoryPreferences.TrimmedHead();

    private StoreScratchpadRepository Create() => new(_store, _preferences);

    private Task SeedAsync(GeocacheUserData? userData = null)
        => _store.UpsertAsync([new Geocache { Code = Stored, Name = "Alpha", Coordinate = new(50, 14), UserData = userData ?? new() }]);

    [TestMethod]
    public async Task Save_StoredCache_GoesIntoItsUserData()
    {
        await SeedAsync(new() { Note = "mine", IsSolved = true });

        await Create().SaveAsync(Stored, "Wall: Caesar +3");

        var cache = await _store.GetAsync(Stored);
        Assert.AreEqual("Wall: Caesar +3", cache!.UserData.Scratchpad);
        Assert.AreEqual("mine", cache.UserData.Note);
        Assert.IsTrue(cache.UserData.IsSolved);
    }

    [TestMethod]
    public async Task Load_StoredCache_ReadsItsUserData()
    {
        await SeedAsync(new() { Scratchpad = "count the stones" });

        Assert.AreEqual("count the stones", await Create().LoadAsync(Stored));
    }

    [TestMethod]
    public async Task SaveAndLoad_CacheNotInTheStore_UsePreferences()
    {
        await Create().SaveAsync(Typed, "typed code");

        Assert.AreEqual("typed code", await Create().LoadAsync(Typed));
    }

    [TestMethod]
    public async Task Load_NothingSaved_IsNull()
        => Assert.IsNull(await Create().LoadAsync(Typed));

    [TestMethod]
    public async Task Load_CacheImportedAfterTheScratchpadWasWritten_StillFindsIt()
    {
        await Create().SaveAsync(Stored, "before import");
        await SeedAsync();

        Assert.AreEqual("before import", await Create().LoadAsync(Stored));
    }

    [TestMethod]
    public async Task Save_CacheImportedAfterTheScratchpadWasWritten_MovesItIntoTheStore()
    {
        await Create().SaveAsync(Stored, "before import");
        await SeedAsync();

        await Create().SaveAsync(Stored, "after import");

        Assert.AreEqual("after import", (await _store.GetAsync(Stored))!.UserData.Scratchpad);
        Assert.AreEqual("after import", await Create().LoadAsync(Stored));
    }
}
