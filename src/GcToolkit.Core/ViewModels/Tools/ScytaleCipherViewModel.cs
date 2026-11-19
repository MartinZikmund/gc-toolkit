using System.Threading.Tasks;
using GcToolkit.Core.Catalog;
using GcToolkit.Core.Ciphers;
using GcToolkit.Core.Discovery;
using GcToolkit.Core.FavoriteTools;
using GcToolkit.Core.Infrastructure;
using GcToolkit.Core.Recents;
using GcToolkit.Core.Services;
using Microsoft.Extensions.Localization;
using Microsoft.UI.Dispatching;

namespace GcToolkit.Core.ViewModels.Tools;

/// <summary>
/// Scytale (rod) cipher (issue #272). Encodes and decodes the ancient columnar transposition live as
/// the user types: the text is written across a chosen number of columns (the rod's diameter) and read
/// down them. Mirrors cachesleuth.com parity — columns key, random columns, an extra/pad character for
/// the last row, and an ignore-spaces toggle — then goes beyond it with an auto-solve that brute-forces
/// every plausible column count (cachesleuth makes you guess), plus copy/share. All transform logic
/// lives in the pure <see cref="ScytaleCipher"/> (thin-VM convention).
/// </summary>
[Tool("ScytaleCipher", ToolCategory.Ciphers,
      Introduced = "2026-06-13", Updated = "2026-06-13",
      Keywords = ["scytale", "skytale", "rod", "rail", "transposition", "columns", "caesar box", "cipher", "skytalé", "tyč", "sloupce", "transpozice", "šifra"])]
public sealed partial class ScytaleCipherViewModel : ToolViewModelBase, IToolTextInput, IToolTextOutput
{
    private const int DefaultColumns = 4;

    private readonly ScytaleCipher _cipher = new();
    private readonly IClipboardService _clipboard;
    private readonly IShareService _share;
    private readonly Random _random = new();
    private readonly UiDebouncer _debouncer = new(TimeSpan.FromMilliseconds(200));
    private readonly DispatcherQueue? _dispatcher = UiDispatcher.TryGetForCurrentThread();

    private int _autoSolveGeneration;
    private bool _suppressRecompute;

    public ScytaleCipherViewModel(
        ICatalogService catalog,
        IRecentsService recents,
        IFavoriteToolsService favorites,
        IStringLocalizer localizer,
        IClipboardService clipboard,
        IShareService share)
        : base("ScytaleCipher", catalog, recents, favorites, localizer)
    {
        _clipboard = clipboard;
        _share = share;
    }

    /// <summary>Number of columns (rod diameter) — the key. Must be at least <see cref="ScytaleCipher.MinColumns"/>.</summary>
    [ObservableProperty]
    public partial int Columns { get; set; } = DefaultColumns;

    /// <summary>0 = Encrypt (write across, read down), 1 = Decrypt (the inverse).</summary>
    [ObservableProperty]
    public partial int DirectionIndex { get; set; }

    /// <summary>Exclude spaces from the transposition (cachesleuth's "Ignore Spaces").</summary>
    [ObservableProperty]
    public partial bool IgnoreSpaces { get; set; }

    /// <summary>Pad the final incomplete row when encrypting (cachesleuth's "Extra Character").</summary>
    [ObservableProperty]
    public partial bool UsePadCharacter { get; set; }

    /// <summary>The single character used to pad the final row when <see cref="UsePadCharacter"/> is on.</summary>
    [ObservableProperty]
    public partial string PadCharacter { get; set; } = "X";

    [ObservableProperty]
    public partial string InputText { get; set; } = string.Empty;

    /// <summary>When on, lists a decoding for every plausible column count (the beyond-parity win).</summary>
    [ObservableProperty]
    public partial bool AutoSolve { get; set; }

    [ObservableProperty]
    public partial string OutputText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial bool HasOutput { get; set; }

    /// <summary>Set when the column count is invalid (below the minimum) — surfaces the validation hint.</summary>
    [ObservableProperty]
    public partial bool HasColumnError { get; set; }

    /// <summary>The candidates shown in auto-solve mode (one per plausible column count), assigned wholesale.</summary>
    [ObservableProperty]
    public partial IReadOnlyList<ScytaleSolveItem> SolveResults { get; set; } = [];

    private bool IsDecode => DirectionIndex == 1;

    private char? PadChar
        => UsePadCharacter && !IsDecode && PadCharacter.Length > 0 ? PadCharacter[0] : null;

    public override void ViewCreated()
    {
        base.ViewCreated();
        Recompute();
    }

    partial void OnColumnsChanged(int value) => _debouncer.RunNow(Recompute);

    partial void OnDirectionIndexChanged(int value)
    {
        // Ignore re-entrant carries and the transient -1 a RadioButtons control can emit.
        if (_suppressRecompute || value is not (0 or 1))
        {
            return;
        }

        // Switching direction carries the previous result into the input, so a round-trip is one tap.
        // Auto-solve has no single output to carry, so the swap only applies to the encode/decode view.
        if (!AutoSolve && OutputText.Length > 0)
        {
            _suppressRecompute = true;
            InputText = OutputText;
            _suppressRecompute = false;
        }

        _debouncer.RunNow(Recompute);
    }

    partial void OnIgnoreSpacesChanged(bool value) => _debouncer.RunNow(Recompute);

    partial void OnUsePadCharacterChanged(bool value) => _debouncer.RunNow(Recompute);

    partial void OnPadCharacterChanged(string value)
    {
        // Keep only the first character — the pad is a single symbol.
        if (value.Length > 1)
        {
            PadCharacter = value[..1];
            return;
        }

        _debouncer.RunNow(Recompute);
    }

    // Typing while auto-solving brute-forces every column count, which freezes the UI per keystroke —
    // debounce that path so it recomputes once typing pauses. The single encode/decode stays instant.
    partial void OnInputTextChanged(string value)
    {
        if (_suppressRecompute)
        {
            return;
        }

        if (AutoSolve)
        {
            _debouncer.Debounce(Recompute);
        }
        else
        {
            _debouncer.RunNow(Recompute);
        }
    }

    partial void OnAutoSolveChanged(bool value) => _debouncer.RunNow(Recompute);

    partial void OnHasOutputChanged(bool value)
    {
        CopyOutputCommand.NotifyCanExecuteChanged();
        ShareOutputCommand.NotifyCanExecuteChanged();
    }

    private void Recompute()
    {
        HasColumnError = Columns < ScytaleCipher.MinColumns;

        if (HasColumnError || string.IsNullOrEmpty(InputText))
        {
            _autoSolveGeneration++; // discard any in-flight brute force
            SolveResults = [];
            OutputText = string.Empty;
            HasOutput = false;
            return;
        }

        if (AutoSolve)
        {
            OutputText = string.Empty;
            StartAutoSolve(InputText);
            return;
        }

        _autoSolveGeneration++;
        SolveResults = [];
        OutputText = IsDecode
            ? _cipher.Decrypt(InputText, Columns, IgnoreSpaces)
            : _cipher.Encrypt(InputText, Columns, IgnoreSpaces, PadChar);
        HasOutput = OutputText.Length > 0;
    }

    /// <summary>
    /// Brute-forces every plausible column count on a background thread, then publishes the candidate
    /// rows back on the UI thread. A generation guard drops results a newer keystroke has superseded.
    /// Runs synchronously when there is no dispatcher (unit tests).
    /// </summary>
    private void StartAutoSolve(string input)
    {
        var generation = ++_autoSolveGeneration;

        if (_dispatcher is null)
        {
            SolveResults = BuildCandidates(input);
            HasOutput = SolveResults.Count > 0;
            return;
        }

        _ = Task.Run(() =>
        {
            var candidates = BuildCandidates(input);
            _dispatcher.TryEnqueue(() =>
            {
                if (generation != _autoSolveGeneration)
                {
                    return; // a newer input already superseded this result
                }

                SolveResults = candidates;
                HasOutput = candidates.Count > 0;
            });
        });
    }

    private IReadOnlyList<ScytaleSolveItem> BuildCandidates(string input)
        => [.. _cipher.AutoSolve(input).Select(c => new ScytaleSolveItem(c.Columns, c.Text, _clipboard.SetText))];

    /// <summary>Picks a random column count in a useful range (2 … 12) and re-runs the transform.</summary>
    [RelayCommand]
    private void RandomizeColumns() => Columns = _random.Next(ScytaleCipher.MinColumns, 13);

    /// <summary>The text the Copy/Share actions emit: the single result, or every candidate line in auto-solve mode.</summary>
    private string BuildResultText()
        => AutoSolve
            ? string.Join(Environment.NewLine, SolveResults.Select(r => $"{r.Columns}: {r.Text}"))
            : OutputText;

    [RelayCommand(CanExecute = nameof(HasOutput))]
    private void CopyOutput() => _clipboard.SetText(BuildResultText());

    [RelayCommand(CanExecute = nameof(HasOutput))]
    private async Task ShareOutputAsync()
    {
        try
        {
            await _share.ShareTextAsync(ToolName, BuildResultText());
        }
        catch (Exception)
        {
            // Sharing is best-effort; a platform share failure must not crash the tool.
        }
    }

    [RelayCommand]
    private void Clear() => InputText = string.Empty;
}
