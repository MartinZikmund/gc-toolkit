using GcToolkit.Core.Geocaches;
using GcToolkit.Core.Geocaches.Storage;
using GcToolkit.Core.Infrastructure;
using SQLite;

namespace GcToolkit.Core.Tests.Geocaches;

[TestClass]
public class SqliteGeocacheStoreTests : GeocacheStoreContractTests
{
    private readonly List<string> _folders = [];
    private readonly List<SqliteGeocacheStore> _stores = [];

    [TestCleanup]
    public async Task DeleteTempFolders()
    {
        foreach (var store in _stores)
        {
            await store.DisposeAsync();
        }

        foreach (var folder in _folders)
        {
            try
            {
                Directory.Delete(folder, recursive: true);
            }
            catch (IOException)
            {
            }
        }
    }

    protected override IGeocacheStore CreateStore() => CreateStore(NewDatabasePath(), new NoOpStorageFlusher());

    private SqliteGeocacheStore CreateStore(string path, IStorageFlusher flusher)
    {
        SqliteGeocacheStore store = new(() => Task.FromResult(path), flusher);
        _stores.Add(store);
        return store;
    }

    private string NewDatabasePath()
    {
        var folder = Path.Combine(Path.GetTempPath(), "gc-toolkit-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        _folders.Add(folder);
        return Path.Combine(folder, "geocaches.db3");
    }

    private sealed class CountingFlusher : IStorageFlusher
    {
        public int Count { get; private set; }

        public Task FlushAsync()
        {
            Count++;
            return Task.CompletedTask;
        }
    }

    [TestMethod]
    public async Task NewDatabase_IsMigratedToCurrentVersion()
    {
        var path = NewDatabasePath();
        await CreateStore(path, new NoOpStorageFlusher()).GetAsync(Code("GC1"));

        using SQLiteConnection connection = new(path, SQLiteOpenFlags.ReadOnly);

        Assert.AreEqual(SqliteGeocacheStore.CurrentSchemaVersion, connection.ExecuteScalar<int>("PRAGMA user_version"));
    }

    [TestMethod]
    public async Task ExistingDatabase_ReopensWithItsData()
    {
        var path = NewDatabasePath();
        await CreateStore(path, new NoOpStorageFlusher()).UpsertAsync([Cache()]);

        var loaded = await CreateStore(path, new NoOpStorageFlusher()).GetAsync(Code("GC8KX2Q"));

        Assert.AreEqual("The Lost Legion", loaded!.Name);
    }

    [TestMethod]
    public async Task DatabaseFromNewerApp_IsRefusedInsteadOfDamaged()
    {
        var path = NewDatabasePath();
        using (SQLiteConnection connection = new(path))
        {
            connection.Execute($"PRAGMA user_version = {SqliteGeocacheStore.CurrentSchemaVersion + 1}");
        }

        await Assert.ThrowsExactlyAsync<InvalidOperationException>(() => CreateStore(path, new NoOpStorageFlusher()).GetAsync(Code("GC1")));
    }

    [TestMethod]
    public async Task EveryCommittedWrite_FlushesStorage()
    {
        var flusher = new CountingFlusher();
        var store = CreateStore(NewDatabasePath(), flusher);

        await store.UpsertAsync([Cache()]);
        var list = await store.CreateListAsync("A");
        await store.AddToListAsync(list.Id, [Code("GC8KX2Q")]);
        await store.UpdateUserDataAsync(Code("GC8KX2Q"), new() { IsFound = true });
        await store.DeleteAsync([Code("GC8KX2Q")]);

        Assert.AreEqual(5, flusher.Count);
    }

    [TestMethod]
    public async Task Reads_DoNotFlush()
    {
        var flusher = new CountingFlusher();
        var store = CreateStore(NewDatabasePath(), flusher);

        await store.GetAsync(Code("GC1"));
        await store.QueryAsync(new());

        Assert.AreEqual(0, flusher.Count);
    }

    [TestMethod]
    public async Task Upsert_Of5000Caches_StoresAndPagesThemAll()
    {
        var store = CreateStore();
        var caches = Enumerable.Range(1, 5000).Select(i => Cache($"GC{i:D5}", $"Cache {i:D5}")).ToList();

        var result = await store.UpsertAsync(caches);
        var page = await store.QueryAsync(new() { Skip = 4990, Take = 100 });

        Assert.AreEqual(5000, result.Added);
        Assert.AreEqual(5000, page.TotalCount);
        Assert.AreEqual(10, page.Items.Count);
    }
}
