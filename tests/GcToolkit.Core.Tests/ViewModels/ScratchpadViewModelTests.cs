using GcToolkit.Core.Geocaches;
using GcToolkit.Core.Tests.Fakes;
using GcToolkit.Core.ViewModels;

namespace GcToolkit.Core.Tests.ViewModels;

[TestClass]
public class ScratchpadViewModelTests
{
    private static readonly GeocacheCode _code = GeocacheCode.Parse("GC8KX2Q");
    private static readonly GeocacheCode _otherCode = GeocacheCode.Parse("GC12345");

    private readonly InMemoryScratchpadRepository _repository = new();
    private readonly FakeTimeProvider _time = new();

    private ScratchpadViewModel CreateViewModel() => new(
        _repository,
        new FakeStringLocalizer(new Dictionary<string, string>
        {
            ["Scratchpad_Saved"] = "Saved",
            ["Scratchpad_SavedJustNow"] = "Saved just now",
            ["Scratchpad_SavedMinutesAgo"] = "Saved {0} min ago",
            ["Scratchpad_Saving"] = "Saving",
            ["Scratchpad_Unsaved"] = "Unsaved",
        }),
        _time);

    private async Task<ScratchpadViewModel> CreateLoadedAsync(string? savedText = null)
    {
        if (savedText is not null)
        {
            _repository.Seed(_code, savedText);
        }

        var viewModel = CreateViewModel();
        await viewModel.InitializeAsync(_code);
        return viewModel;
    }

    [TestMethod]
    public async Task InitializeAsync_SavedText_LoadsItWithoutSaving()
    {
        var viewModel = await CreateLoadedAsync("Wall: THREE SEVEN FIVE");

        Assert.AreEqual("Wall: THREE SEVEN FIVE", viewModel.Text);
        Assert.AreEqual(ScratchpadSaveState.Saved, viewModel.SaveState);

        _time.Advance(TimeSpan.FromSeconds(5));
        Assert.AreEqual(0, _repository.Saves.Count);
    }

    [TestMethod]
    public async Task InitializeAsync_NothingSaved_StartsEmpty()
    {
        var viewModel = await CreateLoadedAsync();

        Assert.AreEqual(string.Empty, viewModel.Text);
        Assert.AreEqual(ScratchpadSaveState.Saved, viewModel.SaveState);
    }

    [TestMethod]
    public async Task InitializeAsync_AnotherCache_FlushesPendingTextToThePreviousOne()
    {
        _repository.Seed(_otherCode, "other");
        var viewModel = await CreateLoadedAsync();
        viewModel.Text = "draft";

        await viewModel.InitializeAsync(_otherCode);

        Assert.AreEqual("draft", _repository.Get(_code));
        Assert.AreEqual("other", viewModel.Text);
    }

    [TestMethod]
    public async Task Text_Typed_IsUnsavedUntilTheDelayPasses()
    {
        var viewModel = await CreateLoadedAsync();

        viewModel.Text = "a";

        Assert.AreEqual(ScratchpadSaveState.Unsaved, viewModel.SaveState);
        Assert.AreEqual(0, _repository.Saves.Count);

        _time.Advance(TimeSpan.FromMilliseconds(999));
        Assert.AreEqual(0, _repository.Saves.Count);

        _time.Advance(TimeSpan.FromMilliseconds(1));
        Assert.AreEqual("a", _repository.Get(_code));
        Assert.AreEqual(ScratchpadSaveState.Saved, viewModel.SaveState);
    }

    [TestMethod]
    public async Task Text_TypedRepeatedly_CoalescesIntoOneSave()
    {
        var viewModel = await CreateLoadedAsync();

        foreach (var text in new[] { "a", "ab", "abc" })
        {
            viewModel.Text = text;
            _time.Advance(TimeSpan.FromMilliseconds(600));
        }

        Assert.AreEqual(0, _repository.Saves.Count);

        _time.Advance(TimeSpan.FromMilliseconds(400));

        Assert.AreEqual(1, _repository.Saves.Count);
        Assert.AreEqual("abc", _repository.Saves[0].Text);
    }

    [TestMethod]
    public async Task Text_ChangedAfterASave_SavesAgain()
    {
        var viewModel = await CreateLoadedAsync();
        viewModel.Text = "a";
        _time.Advance(TimeSpan.FromSeconds(1));

        viewModel.Text = "ab";
        _time.Advance(TimeSpan.FromSeconds(1));

        Assert.AreEqual(2, _repository.Saves.Count);
        Assert.AreEqual("ab", _repository.Get(_code));
    }

    [TestMethod]
    public async Task FlushAsync_DuringPendingDebounce_SavesImmediatelyAndOnlyOnce()
    {
        var viewModel = await CreateLoadedAsync();
        viewModel.Text = "important";

        await viewModel.FlushAsync();

        Assert.AreEqual("important", _repository.Get(_code));
        Assert.AreEqual(ScratchpadSaveState.Saved, viewModel.SaveState);

        _time.Advance(TimeSpan.FromSeconds(5));
        Assert.AreEqual(1, _repository.Saves.Count);
    }

    [TestMethod]
    public async Task FlushAsync_NothingChanged_DoesNotSave()
    {
        var viewModel = await CreateLoadedAsync("same");

        await viewModel.FlushAsync();

        Assert.AreEqual(0, _repository.Saves.Count);
    }

    [TestMethod]
    public async Task OnNavigatedFrom_PendingText_IsSaved()
    {
        var viewModel = await CreateLoadedAsync();
        viewModel.Text = "before leaving";

        viewModel.OnNavigatedFrom();

        Assert.AreEqual("before leaving", _repository.Get(_code));
    }

    [TestMethod]
    public async Task OnNavigatedTo_GeocacheCode_LoadsThatScratchpad()
    {
        _repository.Seed(_code, "from nav");
        var viewModel = CreateViewModel();

        viewModel.OnNavigatedTo(_code);

        Assert.AreEqual("from nav", viewModel.Text);
        await Task.CompletedTask;
    }

    [TestMethod]
    public async Task Save_Fails_StaysUnsavedAndNextFlushRetries()
    {
        var viewModel = await CreateLoadedAsync();
        _repository.FailNextSaves = 1;
        viewModel.Text = "precious";

        _time.Advance(TimeSpan.FromSeconds(1));

        Assert.AreEqual(ScratchpadSaveState.Unsaved, viewModel.SaveState);
        Assert.IsNull(_repository.Get(_code));
        Assert.AreEqual("precious", viewModel.Text);

        await viewModel.FlushAsync();

        Assert.AreEqual("precious", _repository.Get(_code));
        Assert.AreEqual(ScratchpadSaveState.Saved, viewModel.SaveState);
    }

    [TestMethod]
    public async Task Save_TextTypedWhileSaving_EndsUpUnsavedThenSavesTheNewText()
    {
        var viewModel = await CreateLoadedAsync();
        _repository.SaveGate = new(TaskCreationOptions.RunContinuationsAsynchronously);
        viewModel.Text = "one";

        var flush = viewModel.FlushAsync();
        Assert.AreEqual(ScratchpadSaveState.Saving, viewModel.SaveState);

        viewModel.Text = "one two";
        _repository.SaveGate.SetResult();
        await flush;

        Assert.AreEqual(ScratchpadSaveState.Unsaved, viewModel.SaveState);

        _time.Advance(TimeSpan.FromSeconds(1));
        Assert.AreEqual("one two", _repository.Get(_code));
        Assert.AreEqual(ScratchpadSaveState.Saved, viewModel.SaveState);
    }

    [TestMethod]
    public async Task Append_OnExistingText_AddsANewLine()
    {
        var viewModel = await CreateLoadedAsync("first");

        viewModel.Append("second");

        Assert.AreEqual("first\nsecond", viewModel.Text);
    }

    [TestMethod]
    public async Task Append_TextAlreadyEndsWithNewLine_DoesNotAddABlankLine()
    {
        var viewModel = await CreateLoadedAsync("first\n");

        viewModel.Append("second");

        Assert.AreEqual("first\nsecond", viewModel.Text);
    }

    [TestMethod]
    public async Task Append_EmptyScratchpad_JustSetsTheLine()
    {
        var viewModel = await CreateLoadedAsync();

        viewModel.Append("Caesar +3 -> THREE");

        Assert.AreEqual("Caesar +3 -> THREE", viewModel.Text);
    }

    [TestMethod]
    public async Task Append_Twice_KeepsBothLines()
    {
        var viewModel = await CreateLoadedAsync("start");

        viewModel.Append("a");
        viewModel.Append("b");

        Assert.AreEqual("start\na\nb", viewModel.Text);
    }

    [TestMethod]
    public async Task Append_TriggersAutosave()
    {
        var viewModel = await CreateLoadedAsync("start");

        viewModel.Append("more");
        _time.Advance(TimeSpan.FromSeconds(1));

        Assert.AreEqual("start\nmore", _repository.Get(_code));
    }

    [TestMethod]
    [DataRow(null)]
    [DataRow("")]
    public async Task Append_NothingToAdd_ChangesNothing(string? line)
    {
        var viewModel = await CreateLoadedAsync("keep");

        viewModel.Append(line!);

        Assert.AreEqual("keep", viewModel.Text);
        Assert.AreEqual(ScratchpadSaveState.Saved, viewModel.SaveState);
    }

    [TestMethod]
    public async Task StatusText_ReflectsStateAndAge()
    {
        var viewModel = await CreateLoadedAsync();
        Assert.AreEqual("Saved", viewModel.StatusText);

        viewModel.Text = "x";
        Assert.AreEqual("Unsaved", viewModel.StatusText);

        _time.Advance(TimeSpan.FromSeconds(1));
        Assert.AreEqual("Saved just now", viewModel.StatusText);

        _time.Advance(TimeSpan.FromMinutes(3));
        Assert.AreEqual("Saved 3 min ago", viewModel.StatusText);
    }

    [TestMethod]
    public async Task StatusText_WhileSaving_ShowsSaving()
    {
        var viewModel = await CreateLoadedAsync();
        _repository.SaveGate = new(TaskCreationOptions.RunContinuationsAsynchronously);
        viewModel.Text = "x";

        var flush = viewModel.FlushAsync();

        Assert.AreEqual("Saving", viewModel.StatusText);
        _repository.SaveGate.SetResult();
        await flush;
    }

    [TestMethod]
    public async Task Autosave_TimerFiresOffTheUiThread_RunsOnTheUiContext()
    {
        RecordingSynchronizationContext ui = new();
        ScratchpadViewModel viewModel;
        SynchronizationContext.SetSynchronizationContext(ui);
        try
        {
            viewModel = CreateViewModel();
        }
        finally
        {
            SynchronizationContext.SetSynchronizationContext(null);
        }

        await viewModel.InitializeAsync(_code);
        viewModel.Text = "a";

        _time.Advance(TimeSpan.FromSeconds(2));
        Assert.AreEqual(0, _repository.Saves.Count);

        ui.RunPosted();
        Assert.AreEqual("a", _repository.Get(_code));
    }

    [TestMethod]
    [DataRow("first\r", "first\rsecond")]
    [DataRow("first\r\n", "first\r\nsecond")]
    [DataRow("first\rmiddle", "first\rmiddle\rsecond")]
    [DataRow("first\r\nmiddle", "first\r\nmiddle\r\nsecond")]
    public async Task Append_UsesTheTextsLineBreaks(string saved, string expected)
    {
        var viewModel = await CreateLoadedAsync(saved);

        viewModel.Append("second");

        Assert.AreEqual(expected, viewModel.Text);
    }

    [TestMethod]
    public async Task Text_OnlyLineBreaksDiffer_StaysSaved()
    {
        var viewModel = await CreateLoadedAsync("a\nb");

        viewModel.Text = "a\rb";
        _time.Advance(TimeSpan.FromSeconds(5));

        Assert.AreEqual(ScratchpadSaveState.Saved, viewModel.SaveState);
        Assert.AreEqual(0, _repository.Saves.Count);
    }

    private sealed class RecordingSynchronizationContext : SynchronizationContext
    {
        private readonly Queue<(SendOrPostCallback Callback, object? State)> _posted = new();

        public override void Post(SendOrPostCallback d, object? state) => _posted.Enqueue((d, state));

        public void RunPosted()
        {
            while (_posted.TryDequeue(out var item))
            {
                item.Callback(item.State);
            }
        }
    }
}
