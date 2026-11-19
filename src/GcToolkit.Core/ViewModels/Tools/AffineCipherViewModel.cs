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
/// Affine cipher (issue #3). A monoalphabetic substitution <c>E(x) = (a·x + b) mod 26</c> over A–Z that
/// encodes and decodes live as the user types, with a multiplier <c>a</c> dropdown (the 12 values coprime
/// to 26), an offset <c>b</c> dropdown (0–26), an encode/decode toggle, the live substitution "key" rows,
/// and a beyond-parity "auto-solve" that lists every candidate decryption across all 324 valid keys for
/// brute-forcing an unknown key. The decode inverse <c>aⁱⁿᵛ</c> is surfaced too. All transform logic lives
/// in the pure <see cref="AffineCipher"/> (thin-VM convention).
/// </summary>
[Tool("AffineCipher", ToolCategory.Ciphers,
      Introduced = "2026-06-13", Updated = "2026-06-13",
      Keywords = ["affine", "cipher", "monoalphabetic", "substitution", "modular", "afinní", "afinni", "šifra", "sifra", "substituce", "modulární"])]
public sealed partial class AffineCipherViewModel : ToolViewModelBase, IToolTextInput, IToolTextOutput
{
    private readonly AffineCipher _cipher = new();
    private readonly IClipboardService _clipboard;
    private readonly IShareService _share;
    private readonly UiDebouncer _debouncer = new(TimeSpan.FromMilliseconds(200));
    private readonly DispatcherQueue? _dispatcher = UiDispatcher.TryGetForCurrentThread();

    private int _autoSolveGeneration;

    public AffineCipherViewModel(
        ICatalogService catalog,
        IRecentsService recents,
        IFavoriteToolsService favorites,
        IStringLocalizer localizer,
        IClipboardService clipboard,
        IShareService share)
        : base("AffineCipher", catalog, recents, favorites, localizer)
    {
        _clipboard = clipboard;
        _share = share;
        Recompute();
    }

    /// <summary>The 12 multipliers coprime to 26 — the only valid choices for <see cref="Multiplier"/>.</summary>
    public IReadOnlyList<int> Multipliers { get; } = AffineCipher.ValidMultipliers;

    /// <summary>The selectable offsets 0–26 (26 ≡ 0 mod 26), mirroring the reference site.</summary>
    public IReadOnlyList<int> Offsets { get; } = [.. Enumerable.Range(0, AffineCipher.MaxOffset + 1)];

    /// <summary>The multiplier <c>a</c> (default 5 — a common coprime value).</summary>
    [ObservableProperty]
    public partial int Multiplier { get; set; } = 5;

    /// <summary>The offset <c>b</c> (default 8).</summary>
    [ObservableProperty]
    public partial int Offset { get; set; } = 8;

    /// <summary>0 = Encode, 1 = Decode.</summary>
    [ObservableProperty]
    public partial int DirectionIndex { get; set; }

    [ObservableProperty]
    public partial string InputText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial bool AutoSolve { get; set; }

    [ObservableProperty]
    public partial string OutputText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial bool HasOutput { get; set; }

    /// <summary>The modular inverse <c>aⁱⁿᵛ</c> of the current multiplier, shown as the decode key hint.</summary>
    [ObservableProperty]
    public partial int ModularInverse { get; set; }

    /// <summary>The plain alphabet row of the substitution "key" (<c>A B C … Z</c>).</summary>
    [ObservableProperty]
    public partial string KeyPlain { get; set; } = string.Empty;

    /// <summary>The substituted alphabet row of the "key", aligned under <see cref="KeyPlain"/>.</summary>
    [ObservableProperty]
    public partial string KeyCipher { get; set; } = string.Empty;

    /// <summary>The candidate decryptions shown in "auto-solve" mode (one per valid key).</summary>
    [ObservableProperty]
    public partial IReadOnlyList<AffineCandidateItem> Candidates { get; set; } = [];

    private bool IsDecode => DirectionIndex == 1;

    partial void OnMultiplierChanged(int value) => _debouncer.RunNow(Recompute);

    partial void OnOffsetChanged(int value) => _debouncer.RunNow(Recompute);

    partial void OnDirectionIndexChanged(int value)
    {
        if (value is 0 or 1)
        {
            _debouncer.RunNow(Recompute);
        }
    }

    // Typing while auto-solving brute-forces all 324 keys synchronously, which freezes the UI per
    // keystroke — debounce that path so it recomputes once typing pauses. Single-key stays instant.
    partial void OnInputTextChanged(string value)
    {
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
        // A ComboBox can momentarily report no selection while re-templating; ignore invalid multipliers.
        if (!_cipher.IsCoprime(Multiplier))
        {
            return;
        }

        ModularInverse = _cipher.ModInverse(Multiplier);

        // The live substitution key always reflects the current key (encode mapping A→…).
        KeyPlain = "ABCDEFGHIJKLMNOPQRSTUVWXYZ";
        KeyCipher = _cipher.SubstitutionAlphabet(Multiplier, Offset);

        if (string.IsNullOrEmpty(InputText))
        {
            _autoSolveGeneration++; // discard any in-flight brute force
            Candidates = [];
            OutputText = string.Empty;
            HasOutput = false;
            return;
        }

        if (AutoSolve)
        {
            OutputText = string.Empty;
            StartAutoSolve(InputText);
        }
        else
        {
            _autoSolveGeneration++;
            Candidates = [];
            OutputText = _cipher.Transform(InputText, Multiplier, Offset, IsDecode);
            HasOutput = true;
        }
    }

    /// <summary>
    /// Brute-forces all 324 keys on a background thread (it also builds 324 rows), then publishes the
    /// candidates back on the UI thread. A generation guard drops results a newer keystroke/key change
    /// has already superseded. Runs synchronously when there is no dispatcher (unit tests).
    /// </summary>
    private void StartAutoSolve(string input)
    {
        var generation = ++_autoSolveGeneration;

        if (_dispatcher is null)
        {
            Candidates = BuildCandidates(input);
            HasOutput = Candidates.Count > 0;
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

                Candidates = candidates;
                HasOutput = candidates.Count > 0;
            });
        });
    }

    private IReadOnlyList<AffineCandidateItem> BuildCandidates(string input)
        => [.. _cipher.BruteForce(input).Select(c => new AffineCandidateItem(c.A, c.B, c.Text, _clipboard.SetText))];

    /// <summary>The text the Copy/Share actions emit: the single result, or every candidate line in auto-solve mode.</summary>
    private string BuildResultText()
        => AutoSolve
            ? string.Join(Environment.NewLine, Candidates.Select(c => $"{c.Key}: {c.Text}"))
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
