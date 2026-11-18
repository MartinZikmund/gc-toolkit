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
/// Rail Fence (zig-zag) transposition cipher (issue #27). Encodes and decodes live as the user types,
/// with a configurable number of rails (≥ 2) and a starting offset (≥ 0) — the geocachingtoolbox.com
/// parity feature. As a pure transposition it preserves every character (case, spaces, digits and
/// punctuation), so coordinate strings round-trip exactly. Beyond parity it draws the rail-fence
/// diagram with a configurable delimiter and brute-forces an unknown key via auto-solve. All transform
/// logic lives in the pure <see cref="RailFenceCipher"/> (thin-VM convention).
/// </summary>
[Tool("RailFenceCipher", ToolCategory.Ciphers,
      Introduced = "2026-06-13", Updated = "2026-06-13",
      Keywords = ["rail", "fence", "zigzag", "zig-zag", "transposition", "cipher", "rails", "plot", "klikatá", "šifra", "transpozice", "kolová"])]
public sealed partial class RailFenceCipherViewModel : ToolViewModelBase, IToolTextInput
{
    private readonly RailFenceCipher _cipher = new();
    private readonly IClipboardService _clipboard;
    private readonly IShareService _share;
    private readonly IStringLocalizer _localizer;
    private readonly UiDebouncer _debouncer = new(TimeSpan.FromMilliseconds(200));
    private readonly DispatcherQueue? _dispatcher = UiDispatcher.TryGetForCurrentThread();

    private int _solveGeneration;
    private bool _suppressRecompute;

    public RailFenceCipherViewModel(
        ICatalogService catalog,
        IRecentsService recents,
        IFavoriteToolsService favorites,
        IStringLocalizer localizer,
        IClipboardService clipboard,
        IShareService share)
        : base("RailFenceCipher", catalog, recents, favorites, localizer)
    {
        _clipboard = clipboard;
        _share = share;
        _localizer = localizer;
        Recompute();
    }

    /// <summary>Number of rails (rows) the zig-zag spans. Must be at least 2.</summary>
    [ObservableProperty]
    public partial int Rails { get; set; } = 3;

    /// <summary>Starting position within the zig-zag cycle. Must be ≥ 0.</summary>
    [ObservableProperty]
    public partial int Offset { get; set; }

    /// <summary>0 = Encode, 1 = Decode.</summary>
    [ObservableProperty]
    public partial int DirectionIndex { get; set; }

    [ObservableProperty]
    public partial string InputText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string OutputText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial bool HasOutput { get; set; }

    /// <summary>Whether the monospace rail-fence diagram is shown.</summary>
    [ObservableProperty]
    public partial bool ShowFence { get; set; }

    /// <summary>Placeholder character used for empty cells in the diagram (first char of the field).</summary>
    [ObservableProperty]
    public partial string Delimiter { get; set; } = ".";

    /// <summary>The rendered rail-fence diagram (one line per rail), or empty when hidden.</summary>
    [ObservableProperty]
    public partial string FenceDiagram { get; set; } = string.Empty;

    /// <summary>A non-blocking validation message (empty when the inputs are valid).</summary>
    [ObservableProperty]
    public partial string ValidationMessage { get; set; } = string.Empty;

    [ObservableProperty]
    public partial bool HasValidationMessage { get; set; }

    /// <summary>Whether the auto-solve brute-force candidate list is shown.</summary>
    [ObservableProperty]
    public partial bool ShowAutoSolve { get; set; }

    /// <summary>The auto-solve candidates (each a distinct rails/offset decode of the input).</summary>
    [ObservableProperty]
    public partial IReadOnlyList<RailFenceSolveItem> SolveResults { get; set; } = [];

    private bool IsDecode => DirectionIndex == 1;

    private char PlaceholderChar => string.IsNullOrEmpty(Delimiter) ? RailFenceCipher.EmptyCell : Delimiter[0];

    partial void OnRailsChanged(int value) => Recompute();

    partial void OnOffsetChanged(int value)
    {
        // Auto-solve now decodes at the selected offset, so it must refresh when the offset changes.
        Recompute();
        RefreshAutoSolve(immediate: true);
    }

    partial void OnDirectionIndexChanged(int value)
    {
        // Ignore re-entrant carries and the transient -1 a RadioButtons control can emit.
        if (_suppressRecompute || value is not (0 or 1))
        {
            return;
        }

        // Switching direction carries the previous result into the input, so a round-trip is one tap.
        _suppressRecompute = true;
        InputText = OutputText;
        _suppressRecompute = false;
        Recompute();
        RefreshAutoSolve(immediate: true);
    }

    partial void OnInputTextChanged(string value)
    {
        if (_suppressRecompute)
        {
            return;
        }

        Recompute();
        RefreshAutoSolve(immediate: false); // debounced while typing — auto-solve is the expensive path
    }

    partial void OnShowFenceChanged(bool value) => Recompute();

    partial void OnDelimiterChanged(string value) => Recompute();

    partial void OnShowAutoSolveChanged(bool value) => RefreshAutoSolve(immediate: true);

    partial void OnHasOutputChanged(bool value)
    {
        CopyOutputCommand.NotifyCanExecuteChanged();
        ShareOutputCommand.NotifyCanExecuteChanged();
    }

    private void Recompute()
    {
        FenceDiagram = string.Empty;

        // Guard the invariants the codec asserts before calling it.
        if (Rails < 2)
        {
            SetValidation(_localizer["RailFenceRailsTooFew"]);
            ClearOutput();
            return;
        }

        if (Offset < 0)
        {
            SetValidation(_localizer["RailFenceOffsetNegative"]);
            ClearOutput();
            return;
        }

        if (string.IsNullOrEmpty(InputText))
        {
            ClearValidation();
            ClearOutput();
            return;
        }

        // Decoding with rails ≥ length is degenerate (every char on its own rail = identity); warn but proceed.
        if (IsDecode && Rails >= InputText.Length)
        {
            SetValidation(_localizer["RailFenceRailsTooMany"]);
        }
        else
        {
            ClearValidation();
        }

        OutputText = IsDecode
            ? _cipher.Decrypt(InputText, Rails, Offset)
            : _cipher.Encrypt(InputText, Rails, Offset);
        HasOutput = true;

        if (ShowFence)
        {
            // The diagram reflects how the *current* text is laid out on the rails: for decoding that
            // is the ciphertext being read off, for encoding the plaintext being written.
            var rows = _cipher.BuildFence(InputText, Rails, Offset, PlaceholderChar);
            FenceDiagram = string.Join(Environment.NewLine, rows);
        }
    }

    /// <summary>
    /// Refreshes the auto-solve candidate list. When shown, the brute force runs (debounced while
    /// typing, else immediately) on a background thread and publishes on the UI thread — a generation
    /// guard drops superseded results. When hidden, the list is just cleared.
    /// </summary>
    private void RefreshAutoSolve(bool immediate)
    {
        if (!ShowAutoSolve)
        {
            _solveGeneration++;
            SolveResults = [];
            return;
        }

        if (immediate)
        {
            _debouncer.RunNow(RunAutoSolve);
        }
        else
        {
            _debouncer.Debounce(RunAutoSolve);
        }
    }

    private void RunAutoSolve()
    {
        var input = InputText;
        var offset = Offset;
        var generation = ++_solveGeneration;

        if (string.IsNullOrEmpty(input))
        {
            SolveResults = [];
            return;
        }

        if (_dispatcher is null)
        {
            SolveResults = BuildSolveResults(input, offset);
            return;
        }

        _ = Task.Run(() =>
        {
            var results = BuildSolveResults(input, offset);
            _dispatcher.TryEnqueue(() =>
            {
                if (generation == _solveGeneration)
                {
                    SolveResults = results;
                }
            });
        });
    }

    private IReadOnlyList<RailFenceSolveItem> BuildSolveResults(string input, int offset)
        => [.. _cipher.AutoSolve(input, offset).Select(c => new RailFenceSolveItem(c.Rails, c.Offset, c.Text, _clipboard.SetText))];

    private void ClearOutput()
    {
        OutputText = string.Empty;
        HasOutput = false;
    }

    private void SetValidation(string message)
    {
        ValidationMessage = message;
        HasValidationMessage = true;
    }

    private void ClearValidation()
    {
        ValidationMessage = string.Empty;
        HasValidationMessage = false;
    }

    [RelayCommand(CanExecute = nameof(HasOutput))]
    private void CopyOutput() => _clipboard.SetText(OutputText);

    [RelayCommand(CanExecute = nameof(HasOutput))]
    private async Task ShareOutputAsync()
    {
        try
        {
            await _share.ShareTextAsync(ToolName, OutputText);
        }
        catch (Exception)
        {
            // Sharing is best-effort; a platform share failure must not crash the tool.
        }
    }

    [RelayCommand]
    private void Clear() => InputText = string.Empty;
}
