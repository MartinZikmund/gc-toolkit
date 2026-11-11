using CommunityToolkit.Mvvm.Messaging;
using GcToolkit.Core.Geocaches;
using GcToolkit.Core.Messages;
using GcToolkit.Core.Tests.Fakes;
using GcToolkit.Core.Workbench;

namespace GcToolkit.Core.Tests.Workbench;

[TestClass]
public class WorkbenchSessionServiceTests
{
    private static readonly GeocacheCode A = GeocacheCode.Parse("GC1AAA");
    private static readonly GeocacheCode B = GeocacheCode.Parse("GC2BBB");
    private static readonly GeocacheCode C = GeocacheCode.Parse("GC3CCC");

    private static WorkbenchSessionService Create(InMemoryPreferences? prefs = null, IGeocacheLookup? lookup = null)
        => new(prefs ?? InMemoryPreferences.TrimmedHead(), lookup ?? new FakeGeocacheLookup("GC1AAA", "GC2BBB", "GC3CCC"));

    [TestMethod]
    public void Open_NewCache_AddsItAndMakesItActive()
    {
        var sut = Create();

        sut.Open(A);
        sut.Open(B);

        CollectionAssert.AreEqual(new[] { A, B }, sut.OpenCaches.ToArray());
        Assert.AreEqual(B, sut.ActiveCache);
    }

    [TestMethod]
    public void Open_AlreadyOpenCache_ActivatesWithoutDuplicating()
    {
        var sut = Create();
        sut.Open(A);
        sut.Open(B);

        sut.Open(A);

        Assert.AreEqual(2, sut.OpenCaches.Count);
        Assert.AreEqual(A, sut.ActiveCache);
    }

    [TestMethod]
    public void Activate_NotOpenCache_Ignored()
    {
        var sut = Create();
        sut.Open(A);

        sut.Activate(B);

        Assert.AreEqual(A, sut.ActiveCache);
    }

    [TestMethod]
    public void Close_ActiveMiddleTab_ActivatesTheTabThatTakesItsPlace()
    {
        var sut = Create();
        sut.Open(A);
        sut.Open(B);
        sut.Open(C);
        sut.Activate(B);

        sut.Close(B);

        Assert.AreEqual(C, sut.ActiveCache);
    }

    [TestMethod]
    public void Close_ActiveLastTab_ActivatesPreviousOne()
    {
        var sut = Create();
        sut.Open(A);
        sut.Open(B);

        sut.Close(B);

        Assert.AreEqual(A, sut.ActiveCache);
    }

    [TestMethod]
    public void Close_InactiveTab_KeepsActiveCache()
    {
        var sut = Create();
        sut.Open(A);
        sut.Open(B);

        sut.Close(A);

        Assert.AreEqual(B, sut.ActiveCache);
    }

    [TestMethod]
    public void Close_LastTab_LeavesNoActiveCache()
    {
        var sut = Create();
        sut.Open(A);

        sut.Close(A);

        Assert.AreEqual(0, sut.OpenCaches.Count);
        Assert.IsNull(sut.ActiveCache);
    }

    [TestMethod]
    public void Move_ReordersTabsAndKeepsActive()
    {
        var sut = Create();
        sut.Open(A);
        sut.Open(B);
        sut.Open(C);

        sut.Move(C, 0);

        CollectionAssert.AreEqual(new[] { C, A, B }, sut.OpenCaches.ToArray());
        Assert.AreEqual(C, sut.ActiveCache);
    }

    [TestMethod]
    public void Move_IndexOutOfRange_IsClamped()
    {
        var sut = Create();
        sut.Open(A);
        sut.Open(B);

        sut.Move(A, 99);

        CollectionAssert.AreEqual(new[] { B, A }, sut.OpenCaches.ToArray());
    }

    [TestMethod]
    public void NewInstance_RestoresSessionThroughTrimmedPreferences()
    {
        var prefs = InMemoryPreferences.TrimmedHead();
        var first = Create(prefs);
        first.Open(A);
        first.Open(B);
        first.Open(C);
        first.Activate(B);

        var second = Create(prefs);

        CollectionAssert.AreEqual(new[] { A, B, C }, second.OpenCaches.ToArray());
        Assert.AreEqual(B, second.ActiveCache);
        Assert.AreEqual(3, second.RestoredCount);
    }

    [TestMethod]
    public void Restore_UnknownCaches_AreDroppedSilently()
    {
        var prefs = InMemoryPreferences.TrimmedHead();
        var first = Create(prefs);
        first.Open(A);
        first.Open(B);
        first.Open(C);
        first.Activate(B);

        var second = Create(prefs, new FakeGeocacheLookup("GC1AAA", "GC3CCC"));

        CollectionAssert.AreEqual(new[] { A, C }, second.OpenCaches.ToArray());
        Assert.AreEqual(A, second.ActiveCache);
    }

    [TestMethod]
    public void Restore_CorruptStoredValue_StartsEmpty()
    {
        var prefs = InMemoryPreferences.TrimmedHead();
        prefs.SetRawComplex("workbench.session", "{ not json");

        var sut = Create(prefs);

        Assert.AreEqual(0, sut.OpenCaches.Count);
        Assert.IsNull(sut.ActiveCache);
    }

    [TestMethod]
    public void Restore_InvalidStoredCodes_AreSkipped()
    {
        var prefs = InMemoryPreferences.TrimmedHead();
        prefs.SetRawComplex("workbench.session", "{\"OpenCaches\":[\"!!\",\"GC1AAA\"],\"ActiveCache\":\"!!\"}");

        var sut = Create(prefs);

        CollectionAssert.AreEqual(new[] { A }, sut.OpenCaches.ToArray());
        Assert.AreEqual(A, sut.ActiveCache);
    }

    [TestMethod]
    public void Changes_SendMessage()
    {
        WeakReferenceMessenger messenger = new();
        var sut = new WorkbenchSessionService(InMemoryPreferences.TrimmedHead(), new FakeGeocacheLookup("GC1AAA", "GC2BBB"), messenger);
        List<WorkbenchSessionChangedMessage> received = [];
        object recipient = new();
        messenger.Register<WorkbenchSessionChangedMessage>(recipient, (_, m) => received.Add(m));

        sut.Open(A);
        sut.Open(B);
        sut.Close(B);

        Assert.AreEqual(3, received.Count);
        CollectionAssert.AreEqual(new[] { A }, received[^1].OpenCaches.ToArray());
        Assert.AreEqual(A, received[^1].ActiveCache);
    }
}
