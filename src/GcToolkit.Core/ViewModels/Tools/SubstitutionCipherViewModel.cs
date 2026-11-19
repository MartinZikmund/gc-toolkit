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
/// Monoalphabetic substitution cipher (issue #29). Each plain letter A–Z maps to a letter from a
/// user-supplied 26-letter substitution alphabet (the key); decoding applies the inverse. Encodes and
/// decodes live as the user types, with a case-sensitive toggle and a Preserve / Remove / Replace-with-*
/// choice for characters outside the key, mirroring the reference tool. Goes beyond parity with a
/// keyword→alphabet generator, partial-key auto-completion, one-tap key inversion, an aligned plain/key
/// display, and copy/share. All transform logic lives in the pure <see cref="SubstitutionCipher"/>
/// (thin-VM convention); the VM only wires it to bindings and surfaces validation.
/// </summary>
[Tool("SubstitutionCipher", ToolCategory.Ciphers,
      Introduced = "2026-06-13", Updated = "2026-06-13",
      Keywords = ["substitution", "monoalphabetic", "cipher", "cryptogram", "alphabet", "key", "keyword",
                  "substituce", "substituční", "šifra", "klíč", "abeceda", "kryptogram"])]
public sealed partial class SubstitutionCipherViewModel : ToolViewModelBase, IToolTextInput, IToolTextOutput
{
    private readonly SubstitutionCipher _cipher = new();
    private readonly IClipboardService _clipboard;
    private readonly IShareService _share;
    private readonly IStringLocalizer _localizer;

    public SubstitutionCipherViewModel(
        ICatalogService catalog,
        IRecentsService recents,
        IFavoriteToolsService favorites,
        IStringLocalizer localizer,
        IClipboardService clipboard,
        IShareService share)
        : base("SubstitutionCipher", catalog, recents, favorites, localizer)
    {
        _clipboard = clipboard;
        _share = share;
        _localizer = localizer;
        Recompute();
    }

    [ObservableProperty]
    public partial string InputText { get; set; } = string.Empty;

    /// <summary>The substitution alphabet (key). Defaults to a scrambled sample so the tool works on open.</summary>
    [ObservableProperty]
    public partial string Key { get; set; } = "BYKWASLFXOCZTDHJUMIGPVENQR";

    /// <summary>The seed for keyword/auto-complete generation.</summary>
    [ObservableProperty]
    public partial string Keyword { get; set; } = string.Empty;

    /// <summary>0 = Encode (plain → cipher), 1 = Decode (cipher → plain).</summary>
    [ObservableProperty]
    public partial int DirectionIndex { get; set; }

    [ObservableProperty]
    public partial bool CaseSensitive { get; set; }

    /// <summary>0 = Preserve, 1 = Remove, 2 = Replace with <c>*</c>.</summary>
    [ObservableProperty]
    public partial int UnknownHandlingIndex { get; set; }

    [ObservableProperty]
    public partial string OutputText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial bool HasOutput { get; set; }

    /// <summary>The plain alphabet row of the key display (<c>A B C … Z</c>).</summary>
    [ObservableProperty]
    public partial string KeyPlain { get; set; } = SubstitutionCipher.PlainAlphabet;

    /// <summary>The substitution row aligned under <see cref="KeyPlain"/> (the effective key for the direction).</summary>
    [ObservableProperty]
    public partial string KeyCipher { get; set; } = string.Empty;

    [ObservableProperty]
    public partial bool IsKeyValid { get; set; }

    /// <summary><see langword="true"/> when the key is incomplete/invalid, so the error UI can show.</summary>
    [ObservableProperty]
    public partial bool HasKeyError { get; set; }

    [ObservableProperty]
    public partial string KeyErrorMessage { get; set; } = string.Empty;

    private bool IsDecode => DirectionIndex == 1;

    private UnknownCharacterHandling UnknownHandling => UnknownHandlingIndex switch
    {
        1 => UnknownCharacterHandling.Remove,
        2 => UnknownCharacterHandling.Replace,
        _ => UnknownCharacterHandling.Preserve,
    };

    partial void OnInputTextChanged(string value) => Recompute();

    partial void OnKeyChanged(string value) => Recompute();

    partial void OnDirectionIndexChanged(int value)
    {
        if (value is 0 or 1)
        {
            Recompute();
        }
    }

    partial void OnCaseSensitiveChanged(bool value) => Recompute();

    partial void OnUnknownHandlingIndexChanged(int value)
    {
        if (value is 0 or 1 or 2)
        {
            Recompute();
        }
    }

    partial void OnHasOutputChanged(bool value)
    {
        CopyOutputCommand.NotifyCanExecuteChanged();
        ShareOutputCommand.NotifyCanExecuteChanged();
    }

    private void Recompute()
    {
        var validation = _cipher.Validate(Key);
        IsKeyValid = validation.IsValid;
        HasKeyError = !validation.IsValid;
        KeyErrorMessage = validation.IsValid ? string.Empty : DescribeError(validation);

        // The displayed substitution row reflects the active direction (decode shows the inverse alphabet).
        KeyCipher = IsDecode ? _cipher.InvertKey(Key) : Pad(Key);

        if (string.IsNullOrEmpty(InputText))
        {
            OutputText = string.Empty;
            HasOutput = false;
            return;
        }

        OutputText = _cipher.Transform(InputText, Key, IsDecode, CaseSensitive, UnknownHandling);
        HasOutput = true;
    }

    private string DescribeError(KeyValidationResult result) => result.Error switch
    {
        KeyValidationError.WrongLength => _localizer["SubstitutionCipherKeyErrorLength"].Value,
        KeyValidationError.DuplicateLetter =>
            string.Format(_localizer["SubstitutionCipherKeyErrorDuplicate"].Value, result.OffendingCharacter),
        KeyValidationError.NonLetter =>
            string.Format(_localizer["SubstitutionCipherKeyErrorNonLetter"].Value, result.OffendingCharacter),
        _ => string.Empty,
    };

    /// <summary>Pads/upper-cases a (possibly partial) key to 26 visible characters for the aligned display.</summary>
    private static string Pad(string key)
    {
        var upper = (key ?? string.Empty).ToUpperInvariant();
        return upper.Length >= SubstitutionCipher.AlphabetSize
            ? upper[..SubstitutionCipher.AlphabetSize]
            : upper.PadRight(SubstitutionCipher.AlphabetSize, '·');
    }

    [RelayCommand]
    private void GenerateFromKeyword() => Key = _cipher.GenerateFromKeyword(Keyword);

    [RelayCommand]
    private void AutoCompleteKey() => Key = _cipher.AutoComplete(Key);

    /// <summary>Swaps the key for its reciprocal — a one-tap encode↔decode of the key itself.</summary>
    [RelayCommand]
    private void InvertKey() => Key = _cipher.InvertKey(Key);

    [RelayCommand]
    private void ResetKey() => Key = SubstitutionCipher.PlainAlphabet;

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
