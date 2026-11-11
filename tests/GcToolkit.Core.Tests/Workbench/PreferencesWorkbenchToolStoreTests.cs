using GcToolkit.Core.Geocaches;
using GcToolkit.Core.Tests.Fakes;
using GcToolkit.Core.Workbench;

namespace GcToolkit.Core.Tests.Workbench;

[TestClass]
public class PreferencesWorkbenchToolStoreTests
{
    private static readonly GeocacheCode A = GeocacheCode.Parse("GC1AAA");
    private static readonly GeocacheCode B = GeocacheCode.Parse("GC2BBB");

    // TrimmedHead: proves every stored type is in PreferencesJsonContext (WASM release).
    private readonly InMemoryPreferences _preferences = InMemoryPreferences.TrimmedHead();

    private static WorkbenchToolsState Sample() => new(
        [
            new WorkbenchToolState("CaesarCipher", new() { ["InputText"] = "Uryyb", ["Shift"] = "13" }),
            new WorkbenchToolState("MorseCode", null),
        ],
        SelectedIndex: 1);

    [TestMethod]
    public void Load_NothingSaved_ReturnsNull()
    {
        PreferencesWorkbenchToolStore store = new(_preferences);

        Assert.IsNull(store.Load(A));
    }

    [TestMethod]
    public void Save_ThenLoadInANewStore_RoundTrips()
    {
        new PreferencesWorkbenchToolStore(_preferences).Save(A, Sample());

        var loaded = new PreferencesWorkbenchToolStore(_preferences).Load(A);

        Assert.IsNotNull(loaded);
        Assert.AreEqual(1, loaded.SelectedIndex);
        CollectionAssert.AreEqual(new[] { "CaesarCipher", "MorseCode" }, loaded.Tools.Select(t => t.ToolId).ToArray());
        Assert.AreEqual("Uryyb", loaded.Tools[0].Inputs!["InputText"]);
        Assert.AreEqual("13", loaded.Tools[0].Inputs!["Shift"]);
        Assert.IsNull(loaded.Tools[1].Inputs);
    }

    [TestMethod]
    public void Save_IsPerCache()
    {
        PreferencesWorkbenchToolStore store = new(_preferences);

        store.Save(A, Sample());

        Assert.IsNull(store.Load(B));
    }

    [TestMethod]
    public void Save_NoTools_ForgetsTheCache()
    {
        PreferencesWorkbenchToolStore store = new(_preferences);
        store.Save(A, Sample());

        store.Save(A, new WorkbenchToolsState([], -1));

        Assert.IsNull(store.Load(A));
        Assert.IsFalse(_preferences.ContainsKey(PreferencesWorkbenchToolStore.KeyFor(A)));
    }

    [TestMethod]
    public void Load_CorruptData_ReturnsNull()
    {
        _preferences.SetRawComplex(PreferencesWorkbenchToolStore.KeyFor(A), "{not json");

        Assert.IsNull(new PreferencesWorkbenchToolStore(_preferences).Load(A));
    }
}
