using System.Collections.ObjectModel;
using System.Threading.Tasks;
using GcToolkit.Core.Catalog;
using GcToolkit.Core.Ciphers;
using GcToolkit.Core.Discovery;
using GcToolkit.Core.FavoriteTools;
using GcToolkit.Core.Recents;
using GcToolkit.Core.Services;
using Microsoft.Extensions.Localization;

namespace GcToolkit.Core.ViewModels.Tools;

/// <summary>
/// Kamasutra (Vatsyayana) cipher (issue #19). A reciprocal substitution: the alphabet is split into 13
/// letter pairs and each letter swaps with its partner, so the same key both encodes and decodes (an
/// involution — one Convert direction). Offers four key modes — the classic A–Z split (A/N…M/Z), a
/// keyword-seeded alphabet, a manually entered pair list, and a re-rollable random key — plus a live
/// two-row pairing grid, inline validation, and copy/share. All transform logic lives in the pure
/// <see cref="KamasutraCipher"/> (thin-VM convention).
/// </summary>
[Tool("KamasutraCipher", ToolCategory.Ciphers,
      Introduced = "2026-06-13", Updated = "2026-06-13",
      Keywords = ["kamasutra", "vatsyayana", "reciprocal", "substitution", "pairs", "involution", "cipher",
                  "kámasútra", "vátsjájana", "reciproční", "substituce", "páry", "šifra"])]
public sealed partial class KamasutraCipherViewModel : ToolViewModelBase, IToolTextInput
{
    private readonly KamasutraCipher _cipher = new();
    private readonly IClipboardService _clipboard;
    private readonly IShareService _share;
    private readonly IStringLocalizer _localizer;

    private IReadOnlyList<KamasutraPair> _pairs;
    private bool _suppressRecompute;

    public KamasutraCipherViewModel(
        ICatalogService catalog,
        IRecentsService recents,
        IFavoriteToolsService favorites,
        IStringLocalizer localizer,
        IClipboardService clipboard,
        IShareService share)
        : base("KamasutraCipher", catalog, recents, favorites, localizer)
    {
        _clipboard = clipboard;
        _share = share;
        _localizer = localizer;
        _pairs = _cipher.BuildDefaultPairs();
        ManualPairs = _cipher.FormatPairs(_pairs);
        Recompute();
    }

    /// <summary>Key source: 0 = Default A–Z split, 1 = Keyword-seeded, 2 = Manual pairs, 3 = Random.</summary>
    [ObservableProperty]
    public partial int KeyModeIndex { get; set; }

    [ObservableProperty]
    public partial string Keyword { get; set; } = string.Empty;

    /// <summary>The user-entered pair list (Manual mode), kept in sync with the active key otherwise.</summary>
    [ObservableProperty]
    public partial string ManualPairs { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string InputText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string OutputText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial bool HasOutput { get; set; }

    /// <summary><see langword="true"/> when the active key is well-formed.</summary>
    [ObservableProperty]
    public partial bool IsKeyValid { get; set; } = true;

    /// <summary>Localized inline validation message, shown when <see cref="IsKeyValid"/> is <see langword="false"/>.</summary>
    [ObservableProperty]
    public partial string KeyError { get; set; } = string.Empty;

    /// <summary>The two-row visual pairing grid (top letter over its swapped partner).</summary>
    public ObservableCollection<KamasutraPairItem> PairGrid { get; } = [];

    private bool IsKeyword => KeyModeIndex == 1;

    private bool IsManual => KeyModeIndex == 2;

    /// <summary>Manual-pairs editing is only relevant in Manual mode; the View shows it read-only otherwise.</summary>
    public bool IsManualMode => IsManual;

    /// <summary>The keyword box is only relevant in Keyword mode.</summary>
    public bool IsKeywordMode => IsKeyword;

    /// <summary>Re-roll is only available in Random mode.</summary>
    public bool IsRandomMode => KeyModeIndex == 3;

    partial void OnKeyModeIndexChanged(int value)
    {
        // RadioButtons can emit a transient -1 while re-templating.
        if (value is not (0 or 1 or 2 or 3))
        {
            return;
        }

        OnPropertyChanged(nameof(IsManualMode));
        OnPropertyChanged(nameof(IsKeywordMode));
        OnPropertyChanged(nameof(IsRandomMode));
        RebuildKey();
    }

    partial void OnKeywordChanged(string value)
    {
        if (IsKeyword)
        {
            RebuildKey();
        }
    }

    partial void OnManualPairsChanged(string value)
    {
        if (IsManual && !_suppressRecompute)
        {
            RebuildKey();
        }
    }

    partial void OnInputTextChanged(string value) => Recompute();

    partial void OnHasOutputChanged(bool value)
    {
        CopyOutputCommand.NotifyCanExecuteChanged();
        ShareOutputCommand.NotifyCanExecuteChanged();
    }

    /// <summary>Builds the active key from the current mode, validates it, and recomputes the output.</summary>
    private void RebuildKey()
    {
        switch (KeyModeIndex)
        {
            case 1:
                _pairs = _cipher.BuildFromKeyword(Keyword);
                SyncManualPairsFromKey();
                break;
            case 2:
                _pairs = _cipher.ParsePairs(ManualPairs);
                break;
            case 3:
                _pairs = _cipher.BuildRandomPairs();
                SyncManualPairsFromKey();
                break;
            default:
                _pairs = _cipher.BuildDefaultPairs();
                SyncManualPairsFromKey();
                break;
        }

        Recompute();
    }

    /// <summary>Mirrors a generated key into the Manual box (without re-triggering a rebuild).</summary>
    private void SyncManualPairsFromKey()
    {
        _suppressRecompute = true;
        ManualPairs = _cipher.FormatPairs(_pairs);
        _suppressRecompute = false;
    }

    private void Recompute()
    {
        var validation = _cipher.Validate(_pairs);
        IsKeyValid = validation.IsValid;
        KeyError = validation.IsValid ? string.Empty : DescribeError(validation);

        RebuildPairGrid();

        if (!validation.IsValid || string.IsNullOrEmpty(InputText))
        {
            OutputText = string.Empty;
            HasOutput = false;
            return;
        }

        OutputText = _cipher.Transform(InputText, _pairs);
        HasOutput = OutputText.Length > 0;
    }

    private string DescribeError(KamasutraValidationResult validation)
    {
        var key = validation.Error switch
        {
            KamasutraValidationError.InvalidCharacter => "KamasutraErrorInvalidChar",
            KamasutraValidationError.OddLetterCount => "KamasutraErrorOddCount",
            KamasutraValidationError.DuplicateLetter => "KamasutraErrorDuplicate",
            _ => "KamasutraErrorDuplicate",
        };

        var message = _localizer[key].Value;
        return string.IsNullOrEmpty(validation.OffendingPair)
            ? message
            : $"{message} ({validation.OffendingPair})";
    }

    private void RebuildPairGrid()
    {
        PairGrid.Clear();
        if (!IsKeyValid)
        {
            return;
        }

        foreach (var pair in _pairs)
        {
            PairGrid.Add(new KamasutraPairItem(
                char.ToUpperInvariant(pair.First).ToString(),
                char.ToUpperInvariant(pair.Second).ToString()));
        }
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
            // Sharing is best-effort; a platform failure must not crash the tool.
        }
    }

    [RelayCommand]
    private void Reroll()
    {
        KeyModeIndex = 3;
        RebuildKey();
    }

    [RelayCommand]
    private void Clear() => InputText = string.Empty;
}
