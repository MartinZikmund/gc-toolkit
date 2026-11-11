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
/// Vigenère cipher (issue #34). Encodes and decodes a keyword Vigenère live as the user types — case
/// preserved, non-letters passed through, the key advancing only on letters — and adds an "assisted
/// decode" mode that recovers an <em>unknown</em> key from a long ciphertext via the Index of Coincidence
/// (key-length estimate) plus chi-squared scoring against the chosen language's letter frequencies (the
/// geocachingtoolbox.com solver parity feature, including its language selector and key-length range).
/// Goes beyond parity by also showing the normalized key, the running substitution "key" rows, and the
/// ranked key-length candidates, with copy/share/clear. All cipher logic lives in the pure
/// <see cref="VigenereCipher"/> (thin-VM convention).
/// </summary>
[Tool("VigenereCipher", ToolCategory.Ciphers,
      Introduced = "2026-02-10", Updated = "2026-06-06",
      Keywords = ["vigenère", "vigenere", "key", "keyword", "polyalphabetic", "cipher", "solve", "break",
                  "klíč", "klic", "šifra", "sifra", "heslo"])]
public sealed partial class VigenereCipherViewModel : ToolViewModelBase, IToolStateful
{
    private const int DefaultMaxKeyLength = 12;
    private const int TopCandidateCount = 8;

    private readonly VigenereCipher _cipher = new();
    private readonly IClipboardService _clipboard;
    private readonly IShareService _share;
    private readonly IStringLocalizer _localizer;
    private readonly UiDebouncer _debouncer = new(TimeSpan.FromMilliseconds(200));
    private readonly DispatcherQueue? _dispatcher = UiDispatcher.TryGetForCurrentThread();

    private int _solveGeneration;
    private bool _suppressRecompute;

    public VigenereCipherViewModel(
        ICatalogService catalog,
        IRecentsService recents,
        IFavoriteToolsService favorites,
        IStringLocalizer localizer,
        IClipboardService clipboard,
        IShareService share)
        : base("VigenereCipher", catalog, recents, favorites, localizer)
    {
        _clipboard = clipboard;
        _share = share;
        _localizer = localizer;
        Recompute();
    }

    /// <summary>0 = Encode, 1 = Decode (with a known key), 2 = Assisted decode (recover an unknown key).</summary>
    [ObservableProperty]
    public partial int DirectionIndex { get; set; }

    [ObservableProperty]
    public partial string KeyText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string InputText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string OutputText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial bool HasOutput { get; set; }

    /// <summary>The key actually used, normalized to A–Z (e.g. <c>"Le-mon" → "LEMON"</c>).</summary>
    [ObservableProperty]
    public partial string NormalizedKey { get; set; } = string.Empty;

    /// <summary><see langword="true"/> in the two keyed modes (Encode/Decode), where the key box and the
    /// running-key display apply.</summary>
    [ObservableProperty]
    public partial bool IsKeyedMode { get; set; } = true;

    /// <summary><see langword="true"/> in assisted-decode mode, where the derived key + candidates show.</summary>
    [ObservableProperty]
    public partial bool IsAssistedMode { get; set; }

    /// <summary><see langword="true"/> when the key is missing or holds no A–Z letter, so the transform
    /// would silently pass the text through — surfaces the inline validation warning.</summary>
    [ObservableProperty]
    public partial bool HasKeyError { get; set; }

    /// <summary><see langword="true"/> when assisted decode has a derived key to display.</summary>
    [ObservableProperty]
    public partial bool HasDerivedKey { get; set; }

    /// <summary>The key the assisted solver recovered from the ciphertext.</summary>
    [ObservableProperty]
    public partial string DerivedKey { get; set; } = string.Empty;

    /// <summary><see langword="true"/> when the assisted solver had too little text to find a key.</summary>
    [ObservableProperty]
    public partial bool ShowAssistedHint { get; set; }

    /// <summary><see langword="true"/> when the running-key substitution card has rows to show.</summary>
    [ObservableProperty]
    public partial bool ShowKeyRows { get; set; }

    /// <summary>The running "key" display: one row per key letter, each showing its alphabet mapping.</summary>
    [ObservableProperty]
    public partial IReadOnlyList<VigenereKeyRow> KeyRows { get; set; } = [];

    /// <summary>The ranked key-length candidates shown in assisted mode, assigned wholesale.</summary>
    [ObservableProperty]
    public partial IReadOnlyList<VigenereKeyLengthItem> KeyLengthCandidates { get; set; } = [];

    /// <summary>Shortest key length the solver searches (site parity: 1–50).</summary>
    [ObservableProperty]
    public partial int MinKeyLength { get; set; } = VigenereCipher.MinSupportedKeyLength;

    /// <summary>Longest key length the solver searches (site parity: 1–50).</summary>
    [ObservableProperty]
    public partial int MaxKeyLength { get; set; } = DefaultMaxKeyLength;

    /// <summary>The plaintext language profiles the solver can score against (site parity: a language
    /// dropdown); we ship the app's own EN + CS.</summary>
    public IReadOnlyList<VigenereLanguageProfile> Languages { get; } = VigenereLanguageProfile.All;

    /// <summary>Index into <see cref="Languages"/> of the expected plaintext language.</summary>
    [ObservableProperty]
    public partial int LanguageIndex { get; set; }

    /// <summary>The localized display names of <see cref="Languages"/>, in the same order.</summary>
    public IReadOnlyList<string> LanguageNames
        => [.. Languages.Select(l => _localizer[$"VigenereLanguage_{l.Id}"].Value)];

    private bool IsDecode => DirectionIndex == 1;

    private bool IsAssisted => DirectionIndex == 2;

    private VigenereLanguageProfile SelectedLanguage
        => Languages[Math.Clamp(LanguageIndex, 0, Languages.Count - 1)];

    partial void OnDirectionIndexChanged(int value)
    {
        // Ignore re-entrant carries and the transient -1 a RadioButtons control can emit.
        if (_suppressRecompute || value is not (0 or 1 or 2))
        {
            return;
        }

        // Switching between the two keyed directions carries the previous result into the input, so a
        // round-trip is one tap. Assisted mode has no key to round-trip with, so it keeps the input.
        if (value is 0 or 1 && OutputText.Length > 0)
        {
            _suppressRecompute = true;
            InputText = OutputText;
            _suppressRecompute = false;
        }

        _debouncer.RunNow(Recompute);
    }

    partial void OnKeyTextChanged(string value)
    {
        if (!_suppressRecompute)
        {
            _debouncer.RunNow(Recompute);
        }
    }

    // Assisted decode runs a full IC + chi-squared cryptanalysis over the whole text, which freezes the
    // UI if it reruns on every keystroke — debounce that path. The keyed transform stays instant.
    partial void OnInputTextChanged(string value)
    {
        if (_suppressRecompute)
        {
            return;
        }

        if (IsAssisted)
        {
            _debouncer.Debounce(Recompute);
        }
        else
        {
            _debouncer.RunNow(Recompute);
        }
    }

    partial void OnLanguageIndexChanged(int value) => _debouncer.RunNow(Recompute);

    partial void OnMinKeyLengthChanged(int value) => _debouncer.RunNow(Recompute);

    partial void OnMaxKeyLengthChanged(int value) => _debouncer.RunNow(Recompute);

    partial void OnHasOutputChanged(bool value)
    {
        CopyOutputCommand.NotifyCanExecuteChanged();
        ShareOutputCommand.NotifyCanExecuteChanged();
    }

    private void Recompute()
    {
        IsAssistedMode = IsAssisted;
        IsKeyedMode = !IsAssisted;

        if (IsAssisted)
        {
            RecomputeAssisted();
        }
        else
        {
            RecomputeKeyed();
        }
    }

    private void RecomputeKeyed()
    {
        // Reset assisted-only state so a leftover derived key never lingers, and drop any in-flight solve.
        _solveGeneration++;
        DerivedKey = string.Empty;
        HasDerivedKey = false;
        ShowAssistedHint = false;
        KeyLengthCandidates = [];

        NormalizedKey = _cipher.NormalizeKey(KeyText);
        BuildKeyRows(NormalizedKey);

        // Only nag about a keyless key once there is text it would silently pass through unchanged.
        var hasKey = NormalizedKey.Length > 0;
        HasKeyError = !hasKey && InputText.Length > 0;

        if (!hasKey || string.IsNullOrEmpty(InputText))
        {
            OutputText = string.Empty;
            HasOutput = false;
            return;
        }

        OutputText = IsDecode
            ? _cipher.Decode(InputText, NormalizedKey)
            : _cipher.Encode(InputText, NormalizedKey);
        HasOutput = true;
    }

    private void RecomputeAssisted()
    {
        HasKeyError = false;

        if (string.IsNullOrEmpty(InputText))
        {
            _solveGeneration++;
            ResetAssistedOutput();
            return;
        }

        StartSolve(InputText, EffectiveMinKeyLength, EffectiveMaxKeyLength, SelectedLanguage);
    }

    /// <summary>
    /// Runs the cryptanalysis on a background thread and publishes the result on the UI thread, dropping
    /// anything a newer keystroke/option change has already superseded. Runs synchronously when there is
    /// no dispatcher (unit tests).
    /// </summary>
    private void StartSolve(string input, int min, int max, VigenereLanguageProfile language)
    {
        var generation = ++_solveGeneration;

        if (_dispatcher is null)
        {
            PublishSolve(_cipher.Solve(input, max, min, language));
            return;
        }

        _ = Task.Run(() =>
        {
            var result = _cipher.Solve(input, max, min, language);
            _dispatcher.TryEnqueue(() =>
            {
                if (generation != _solveGeneration)
                {
                    return; // a newer input or option already superseded this result
                }

                PublishSolve(result);
            });
        });
    }

    private void PublishSolve(VigenereSolveResult result)
    {
        DerivedKey = result.Key;
        NormalizedKey = result.Key;
        HasDerivedKey = result.Key.Length > 0;

        // Surface the top key-length candidates so the estimate stays transparent.
        KeyLengthCandidates =
        [
            .. result.KeyLengthCandidates
                .Take(TopCandidateCount)
                .Select(c => new VigenereKeyLengthItem(c.Length, c.IndexOfCoincidence)),
        ];

        if (HasDerivedKey)
        {
            BuildKeyRows(result.Key);
            OutputText = result.PlainText;
            HasOutput = true;
            ShowAssistedHint = false;
        }
        else
        {
            BuildKeyRows(string.Empty);
            OutputText = string.Empty;
            HasOutput = false;
            ShowAssistedHint = true;
        }
    }

    private void ResetAssistedOutput()
    {
        DerivedKey = string.Empty;
        NormalizedKey = string.Empty;
        OutputText = string.Empty;
        HasOutput = false;
        HasDerivedKey = false;
        ShowAssistedHint = false;
        KeyLengthCandidates = [];
        BuildKeyRows(string.Empty);
    }

    // A NumberBox can report NaN-backed zeros while being edited, and the user can invert the range;
    // clamp both ends into the supported window and keep min <= max.
    private int EffectiveMinKeyLength
        => Math.Clamp(MinKeyLength, VigenereCipher.MinSupportedKeyLength, VigenereCipher.MaxSupportedKeyLength);

    private int EffectiveMaxKeyLength
        => Math.Max(
            EffectiveMinKeyLength,
            Math.Clamp(MaxKeyLength, VigenereCipher.MinSupportedKeyLength, VigenereCipher.MaxSupportedKeyLength));

    /// <summary>Builds the running-key display: one row per key letter, each showing how the plain
    /// alphabet maps under that letter's Caesar shift.</summary>
    private void BuildKeyRows(string key)
    {
        KeyRows = [.. key.Select(letter =>
            new VigenereKeyRow(letter, _cipher.PlainAlphabet, _cipher.CipherAlphabetForKeyLetter(letter)))];
        ShowKeyRows = KeyRows.Count > 0;
    }

    /// <summary>The text the Copy/Share actions emit — the assisted solve leads with the recovered key.</summary>
    private string BuildResultText()
        => IsAssisted && HasDerivedKey
            ? $"{DerivedKey}{Environment.NewLine}{OutputText}"
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
    private void Clear()
    {
        _suppressRecompute = true;
        InputText = string.Empty;
        KeyText = string.Empty;
        _suppressRecompute = false;
        _debouncer.RunNow(Recompute);
    }

    public ToolInputState CaptureState()
    {
        ToolInputState state = new();
        state.Set(nameof(DirectionIndex), DirectionIndex);
        state.Set(nameof(KeyText), KeyText);
        state.Set(nameof(LanguageIndex), LanguageIndex);
        state.Set(nameof(MinKeyLength), MinKeyLength);
        state.Set(nameof(MaxKeyLength), MaxKeyLength);
        state.Set(nameof(InputText), InputText);
        return state;
    }

    // Direction first: switching between the keyed directions carries the current output into the input.
    public void RestoreState(ToolInputState state)
    {
        DirectionIndex = Math.Clamp(state.GetInt(nameof(DirectionIndex), DirectionIndex), 0, 2);
        KeyText = state.GetString(nameof(KeyText), KeyText);
        LanguageIndex = Math.Clamp(state.GetInt(nameof(LanguageIndex), LanguageIndex), 0, Languages.Count - 1);
        MinKeyLength = state.GetInt(nameof(MinKeyLength), MinKeyLength);
        MaxKeyLength = state.GetInt(nameof(MaxKeyLength), MaxKeyLength);
        InputText = state.GetString(nameof(InputText), InputText);
    }
}
