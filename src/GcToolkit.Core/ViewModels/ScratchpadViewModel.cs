using System.Globalization;
using GcToolkit.Core.Geocaches;

namespace GcToolkit.Core.ViewModels;

/// <summary>
/// The per-cache scratchpad: free text that autosaves shortly after typing stops and is flushed
/// when the user leaves the cache or the app suspends. Lives apart from the geocaching.com note.
/// </summary>
public partial class ScratchpadViewModel : ViewModelBase
{
    private static readonly TimeSpan _statusRefreshInterval = TimeSpan.FromSeconds(30);

    private readonly IScratchpadRepository _repository;
    private readonly IStringLocalizer _localizer;
    private readonly TimeProvider _time;
    private readonly ITimer _debounceTimer;
    private readonly ITimer _statusTimer;
    private readonly SemaphoreSlim _saveLock = new(1, 1);
    private readonly SynchronizationContext? _uiContext = SynchronizationContext.Current;
    private GeocacheCode? _code;
    private string _savedText = string.Empty;
    private DateTimeOffset? _savedAt;
    private bool _isLoading;

    public ScratchpadViewModel(IScratchpadRepository repository, IStringLocalizer localizer, TimeProvider? timeProvider = null)
    {
        _repository = repository;
        _localizer = localizer;
        _time = timeProvider ?? TimeProvider.System;
        _debounceTimer = _time.CreateTimer(_ => RunOnUi(() => _ = FlushAsync()), null, Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
        _statusTimer = _time.CreateTimer(_ => RunOnUi(UpdateStatus), null, Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
        UpdateStatus();
    }

    /// <summary>Quiet time after the last keystroke before the text is saved.</summary>
    public TimeSpan AutosaveDelay { get; init; } = TimeSpan.FromSeconds(1);

    [ObservableProperty]
    public partial string Text { get; set; } = string.Empty;

    [ObservableProperty]
    public partial ScratchpadSaveState SaveState { get; set; }

    /// <summary>"Saved just now", "Saved 3 min ago", "Unsaved", …</summary>
    [ObservableProperty]
    public partial string StatusText { get; set; } = string.Empty;

    /// <summary>Loads the scratchpad of <paramref name="code"/>, first saving any unsaved text of the previous cache.</summary>
    public async Task InitializeAsync(GeocacheCode code)
    {
        await FlushAsync();

        var loaded = await _repository.LoadAsync(code) ?? string.Empty;

        _code = code;
        _isLoading = true;
        try
        {
            Text = loaded;
        }
        finally
        {
            _isLoading = false;
        }

        _savedText = loaded;
        _savedAt = null;
        _debounceTimer.Change(Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
        SaveState = ScratchpadSaveState.Saved;
        UpdateStatus();
    }

    /// <summary>Adds <paramref name="line"/> on a new line at the end; never replaces existing text.</summary>
    public void Append(string line)
    {
        if (string.IsNullOrEmpty(line))
        {
            return;
        }

        Text = Text.Length == 0 || Text.EndsWith('\n') || Text.EndsWith('\r') ? Text + line : Text + LineBreakOf(Text) + line;
    }

    /// <summary>Saves any unsaved text now. Never throws; a failed save leaves the text <see cref="ScratchpadSaveState.Unsaved"/>.</summary>
    public async Task FlushAsync()
    {
        _debounceTimer.Change(Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);

        await _saveLock.WaitAsync();
        try
        {
            if (_code is not { } code || SameIgnoringLineBreaks(Text, _savedText))
            {
                return;
            }

            var snapshot = Text;
            SaveState = ScratchpadSaveState.Saving;
            UpdateStatus();

            try
            {
                await _repository.SaveAsync(code, snapshot);
            }
            catch (Exception)
            {
                SaveState = ScratchpadSaveState.Unsaved;
                UpdateStatus();
                return;
            }

            _savedText = snapshot;
            _savedAt = _time.GetUtcNow();
            SaveState = SameIgnoringLineBreaks(Text, snapshot) ?ScratchpadSaveState.Saved : ScratchpadSaveState.Unsaved;
            _statusTimer.Change(_statusRefreshInterval, _statusRefreshInterval);
            UpdateStatus();
        }
        finally
        {
            _saveLock.Release();
        }
    }

    public override async void OnNavigatedTo(object? parameter)
    {
        if (parameter is not GeocacheCode code)
        {
            return;
        }

        try
        {
            await InitializeAsync(code);
        }
        catch (Exception)
        {
            // Nothing to show yet: the scratchpad just stays empty.
        }
    }

    public override void OnNavigatedFrom()
    {
        _statusTimer.Change(Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
        _ = FlushAsync();
    }

    public override void ViewUnloaded() => _statusTimer.Change(Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);

    partial void OnTextChanged(string value)
    {
        if (_isLoading || _code is null)
        {
            return;
        }

        if (SameIgnoringLineBreaks(value, _savedText))
        {
            _debounceTimer.Change(Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
            SaveState = ScratchpadSaveState.Saved;
        }
        else
        {
            SaveState = ScratchpadSaveState.Unsaved;
            _debounceTimer.Change(AutosaveDelay, Timeout.InfiniteTimeSpan);
        }

        UpdateStatus();
    }

    private void UpdateStatus() => StatusText = SaveState switch
    {
        ScratchpadSaveState.Saving => _localizer["Scratchpad_Saving"],
        ScratchpadSaveState.Unsaved => _localizer["Scratchpad_Unsaved"],
        _ => SavedStatus(),
    };

    private string SavedStatus()
    {
        if (_savedAt is not { } savedAt)
        {
            return _localizer["Scratchpad_Saved"];
        }

        var age = _time.GetUtcNow() - savedAt;
        return age switch
        {
            { TotalMinutes: < 1 } => _localizer["Scratchpad_SavedJustNow"],
            { TotalHours: < 1 } => string.Format(CultureInfo.CurrentCulture, _localizer["Scratchpad_SavedMinutesAgo"].Value, (int)age.TotalMinutes),
            _ => string.Format(CultureInfo.CurrentCulture, _localizer["Scratchpad_SavedHoursAgo"].Value, (int)age.TotalHours),
        };
    }

    // WinUI's TextBox turns every line break into '\r', so stored and typed text can differ only in those.
    private static bool SameIgnoringLineBreaks(string a, string b) =>
        a == b || NormalizeLineBreaks(a) == NormalizeLineBreaks(b);

    private static string NormalizeLineBreaks(string text) => text.Replace("\r\n", "\n").Replace('\r', '\n');

    private static string LineBreakOf(string text)
    {
        var index = text.IndexOfAny(['\r', '\n']);
        if (index < 0 || text[index] == '\n')
        {
            return "\n";
        }

        return index + 1 < text.Length && text[index + 1] == '\n' ? "\r\n" : "\r";
    }

    // Timers fire on the thread pool; bound properties must change on the thread that created the VM.
    private void RunOnUi(Action action)
    {
        if (_uiContext is null || SynchronizationContext.Current == _uiContext)
        {
            action();
            return;
        }

        _uiContext.Post(_ => action(), null);
    }
}
