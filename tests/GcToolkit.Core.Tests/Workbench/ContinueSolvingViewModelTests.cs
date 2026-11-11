using GcToolkit.Core.Geocaches;
using GcToolkit.Core.Navigation;
using GcToolkit.Core.Services;
using GcToolkit.Core.Tests.Fakes;
using GcToolkit.Core.ViewModels;
using GcToolkit.Core.Workbench;

namespace GcToolkit.Core.Tests.Workbench;

[TestClass]
public class ContinueSolvingViewModelTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 11, 12, 0, 0, TimeSpan.Zero);

    private readonly FakeProgressProvider _progress = new();
    private readonly FakeWorkbenchSession _session = new();
    private readonly RecordingNavigation _navigation = new();

    private static CacheProgress Progress(string code, DateTimeOffset edited, int vars = 1, bool solved = false, bool found = false, int lines = 12)
        => new(GeocacheCode.Parse(code), "Name " + code, vars, 4, lines, 0, edited, solved, found, false);

    private ContinueSolvingViewModel Create()
    {
        FakeStringLocalizer localizer = new(new Dictionary<string, string>
        {
            ["Workbench_VariablesProgress"] = "{0} of {1} variables",
            ["Workbench_ScratchpadLinesOne"] = "{0} line",
            ["Workbench_ScratchpadLinesFew"] = "{0} lines (few)",
            ["Workbench_ScratchpadLinesMany"] = "{0} lines",
            ["Workbench_EditedJustNow"] = "just now",
            ["Workbench_EditedMinutesAgo"] = "{0} min ago",
            ["Workbench_EditedHoursAgo"] = "{0} h ago",
            ["Workbench_EditedYesterday"] = "yesterday",
            ["Workbench_EditedDaysAgo"] = "{0} days ago",
        });
        return new ContinueSolvingViewModel(_progress, _session, _navigation, localizer, new FixedTime(Now));
    }

    [TestMethod]
    public void Refresh_ListsOnlyInProgressCachesNewestFirst()
    {
        _progress.Items.Add(Progress("GC1OLD", Now.AddDays(-3)));
        _progress.Items.Add(Progress("GC2NEW", Now.AddMinutes(-5)));
        _progress.Items.Add(Progress("GC3FND", Now.AddMinutes(-1), found: true));
        _progress.Items.Add(new CacheProgress(GeocacheCode.Parse("GC4IDL"), "Idle", 0, 4, 0, 0, Now.AddMinutes(-2), false, false, false));
        var sut = Create();

        sut.ViewCreated();

        CollectionAssert.AreEqual(new[] { "GC2NEW", "GC1OLD" }, sut.Items.Select(i => i.Code.Value).ToArray());
        Assert.IsTrue(sut.HasItems);
    }

    [TestMethod]
    public void Refresh_FormatsProgressAndRelativeTime()
    {
        _progress.Items.Add(Progress("GC1AAA", Now.AddHours(-2), vars: 3, solved: true));
        var sut = Create();

        sut.ViewCreated();

        var item = sut.Items.Single();
        Assert.AreEqual("3 of 4 variables", item.VariablesText);
        Assert.AreEqual("12 lines", item.ScratchpadText);
        Assert.AreEqual("2 h ago", item.EditedText);
        Assert.IsTrue(item.IsSolved);
    }

    [TestMethod]
    [DataRow(0, "0 lines")]
    [DataRow(1, "1 line")]
    [DataRow(2, "2 lines (few)")]
    [DataRow(4, "4 lines (few)")]
    [DataRow(5, "5 lines")]
    public void Refresh_ScratchpadLines_PicksPluralForm(int lines, string expected)
    {
        _progress.Items.Add(Progress("GC1AAA", Now, lines: lines));
        var sut = Create();

        sut.ViewCreated();

        Assert.AreEqual(expected, sut.Items.Single().ScratchpadText);
    }

    [TestMethod]
    [DataRow(10, "just now")]
    [DataRow(300, "5 min ago")]
    [DataRow(86400 + 100, "yesterday")]
    [DataRow(86400 * 4, "4 days ago")]
    public void Refresh_RelativeTimeBuckets(int secondsAgo, string expected)
    {
        _progress.Items.Add(Progress("GC1AAA", Now.AddSeconds(-secondsAgo)));
        var sut = Create();

        sut.ViewCreated();

        Assert.AreEqual(expected, sut.Items.Single().EditedText);
    }

    [TestMethod]
    public void ProgressChanged_WhileLoaded_RefreshesList()
    {
        var sut = Create();
        sut.ViewCreated();
        sut.ViewLoaded();
        Assert.IsFalse(sut.HasItems);

        _progress.Items.Add(Progress("GC1AAA", Now));
        _progress.Raise();

        Assert.IsTrue(sut.HasItems);
    }

    [TestMethod]
    public void ViewUnloaded_UnsubscribesFromProgress()
    {
        var sut = Create();
        sut.ViewCreated();
        sut.ViewLoaded();

        sut.ViewUnloaded();

        Assert.AreEqual(0, _progress.HandlerCount);
    }

    [TestMethod]
    public void ViewLoaded_AgainAfterUnload_SubscribesOnce()
    {
        var sut = Create();
        sut.ViewCreated();
        sut.ViewLoaded();
        sut.ViewUnloaded();

        sut.ViewLoaded();
        sut.ViewLoaded();

        Assert.AreEqual(1, _progress.HandlerCount);
    }

    [TestMethod]
    public void ProgressChanged_FromBackgroundThread_RefreshesOnUiContext()
    {
        RecordingSynchronizationContext ui = new();
        var previous = SynchronizationContext.Current;
        SynchronizationContext.SetSynchronizationContext(ui);
        try
        {
            var sut = Create();
            sut.ViewCreated();
            sut.ViewLoaded();
            _progress.Items.Add(Progress("GC1AAA", Now));

            Thread worker = new(_progress.Raise);
            worker.Start();
            worker.Join();

            Assert.IsFalse(sut.HasItems);
            ui.RunPosted();
            Assert.IsTrue(sut.HasItems);
        }
        finally
        {
            SynchronizationContext.SetSynchronizationContext(previous);
        }
    }

    [TestMethod]
    public void Resume_OpensCacheInSessionAndNavigatesToWorkbench()
    {
        _progress.Items.Add(Progress("GC1AAA", Now));
        var sut = Create();
        sut.ViewCreated();

        sut.ResumeCommand.Execute(sut.Items.Single());

        Assert.AreEqual(GeocacheCode.Parse("GC1AAA"), _session.ActiveCache);
        Assert.AreEqual(typeof(WorkbenchViewModel), _navigation.LastType);
    }

    [TestMethod]
    public void RestoredNotice_ShownWhenSessionWasRestored_AndDismissible()
    {
        _session.RestoredCount = 3;
        var sut = Create();

        sut.ViewCreated();
        Assert.IsTrue(sut.IsRestoredNoticeVisible);
        Assert.AreEqual(3, sut.RestoredCount);

        sut.DismissRestoredNoticeCommand.Execute(null);
        Assert.IsFalse(sut.IsRestoredNoticeVisible);
    }

    private sealed class FixedTime(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }

    private sealed class RecordingNavigation : INavigationService
    {
        public Type? LastType { get; private set; }

        public bool CanGoBack => false;

        public NavigationSection? CurrentSection => null;

        public void Initialize()
        {
        }

        public void Navigate<TViewModel>() => LastType = typeof(TViewModel);

        public void Navigate<TViewModel>(object? parameter) => LastType = typeof(TViewModel);

        public void Navigate(Type viewModelType, object? parameter = null) => LastType = viewModelType;

        public bool GoBack() => false;

        public void ClearBackStack()
        {
        }
    }
}
