using GcToolkit.Core.Geocaches;
using GcToolkit.Core.Tests.Fakes;
using GcToolkit.Core.Workbench;

namespace GcToolkit.Core.Tests.Workbench;

[TestClass]
public class WorkbenchProgressStoreTests
{
    private static readonly GeocacheCode A = GeocacheCode.Parse("GC1AAA");
    private static readonly GeocacheCode B = GeocacheCode.Parse("GC2BBB");

    private readonly InMemoryPreferences _preferences = InMemoryPreferences.TrimmedHead();
    private readonly FakeTimeProvider _time = new();

    private WorkbenchProgressStore Create() => new(_preferences, _time);

    private static CacheProgress Of(WorkbenchProgressStore store, GeocacheCode code)
        => store.GetProgress().Single(p => p.Code == code);

    [TestMethod]
    public void RecordWork_NewCache_IsInProgressAndStamped()
    {
        var store = Create();

        store.RecordWork(A, variablesFilled: 1, variablesTotal: 3);

        var progress = Of(store, A);
        Assert.IsTrue(progress.IsInProgress);
        Assert.AreEqual(1, progress.VariablesFilled);
        Assert.AreEqual(3, progress.VariablesTotal);
        Assert.AreEqual(_time.GetUtcNow(), progress.LastEdited);
        Assert.AreEqual(A.Value, progress.Name);
    }

    [TestMethod]
    public void RecordWork_OnlyGivenValuesChange()
    {
        var store = Create();
        store.RecordWork(A, variablesFilled: 1, variablesTotal: 3);

        store.RecordWork(A, scratchpadLines: 2);
        store.RecordWork(A, openTools: 4);

        var progress = Of(store, A);
        Assert.AreEqual(1, progress.VariablesFilled);
        Assert.AreEqual(2, progress.ScratchpadLineCount);
        Assert.AreEqual(4, progress.OpenToolCount);
    }

    [TestMethod]
    public void RecordWork_SameValues_KeepsTheEditTime()
    {
        var store = Create();
        store.RecordWork(A, openTools: 1);
        var first = Of(store, A).LastEdited;
        _time.Advance(TimeSpan.FromHours(1));

        store.RecordWork(A, openTools: 1);

        Assert.AreEqual(first, Of(store, A).LastEdited);
    }

    [TestMethod]
    public void RecordDetails_UpdatesNameAndFlagsWithoutCountingAsWork()
    {
        var store = Create();

        store.RecordDetails(A, "Alpha", isSolved: true, isFound: false);

        var progress = Of(store, A);
        Assert.AreEqual("Alpha", progress.Name);
        Assert.IsTrue(progress.IsSolved);
        Assert.IsFalse(progress.IsInProgress);
    }

    [TestMethod]
    public void RecordDetails_DoesNotMoveTheEditTime()
    {
        var store = Create();
        store.RecordWork(A, openTools: 1);
        var edited = Of(store, A).LastEdited;
        _time.Advance(TimeSpan.FromHours(1));

        store.RecordDetails(A, "Alpha", isSolved: false, isFound: false);

        Assert.AreEqual(edited, Of(store, A).LastEdited);
    }

    [TestMethod]
    public void Found_IsNoLongerInProgress()
    {
        var store = Create();
        store.RecordWork(A, openTools: 1);

        store.RecordDetails(A, "Alpha", isSolved: true, isFound: true);

        Assert.IsFalse(Of(store, A).IsInProgress);
    }

    [TestMethod]
    public void Remove_TakesItOffTheList_UntilThereIsNewWork()
    {
        var store = Create();
        store.RecordWork(A, openTools: 1);

        store.Remove(A);

        Assert.IsFalse(Of(store, A).IsInProgress);

        store.RecordWork(A, openTools: 2);

        Assert.IsTrue(Of(store, A).IsInProgress);
    }

    [TestMethod]
    public void Progress_SurvivesRelaunch()
    {
        Create().RecordWork(A, scratchpadLines: 5);
        Create().RecordDetails(B, "Beta", isSolved: false, isFound: false);

        var store = Create();

        Assert.AreEqual(5, Of(store, A).ScratchpadLineCount);
        Assert.AreEqual("Beta", Of(store, B).Name);
    }

    [TestMethod]
    public void Changes_RaiseProgressChanged()
    {
        var store = Create();
        var raised = 0;
        store.ProgressChanged += (_, _) => raised++;

        store.RecordWork(A, openTools: 1);
        store.RecordWork(A, openTools: 1);
        store.RecordDetails(A, "Alpha", isSolved: false, isFound: false);
        store.Remove(A);

        Assert.AreEqual(3, raised);
    }
}
