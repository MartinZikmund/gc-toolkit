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
/// One-time pad / Vernam cipher (issue #23). Encodes and decodes live as the user types, combining each
/// aligned plaintext/key symbol modulo the active charset. Matches geocachingtoolbox.com parity
/// (letters-only A–Z, <c>(P+K)/(C−K)</c> mod 26, non-letters ignored) and goes beyond it with the Beaufort
/// and variant-Beaufort tabula-recta variants, letters+digits (mod 36) and printable-ASCII (mod 95)
/// charsets, an opt-in case- and punctuation-preserving mode, a cryptographically-random pad generator,
/// and a key-too-short warning (a true OTP needs a key at least as long as the message, used once). All
/// transform logic lives in the pure <see cref="OneTimePad"/> (thin-VM convention).
/// </summary>
[Tool("OneTimePad", ToolCategory.Ciphers,
      Introduced = "2026-06-13", Updated = "2026-06-13",
      Keywords = ["one-time pad", "onetime pad", "otp", "vernam", "vigenere", "beaufort", "pad", "key", "cipher",
                  "jednorázová tabulka", "vernamova šifra", "klíč", "šifra"])]
public sealed partial class OneTimePadViewModel : ToolViewModelBase, IToolTextInput
{
    private readonly OneTimePad _cipher = new();
    private readonly IClipboardService _clipboard;
    private readonly IShareService _share;

    /// <summary>Guards the input/output swap on a direction toggle from re-triggering a recompute mid-swap.</summary>
    private bool _suppressRecompute;

    public OneTimePadViewModel(
        ICatalogService catalog,
        IRecentsService recents,
        IFavoriteToolsService favorites,
        IStringLocalizer localizer,
        IClipboardService clipboard,
        IShareService share)
        : base("OneTimePad", catalog, recents, favorites, localizer)
    {
        _clipboard = clipboard;
        _share = share;
    }

    /// <summary>0 = Encrypt, 1 = Decrypt.</summary>
    [ObservableProperty]
    public partial int DirectionIndex { get; set; }

    /// <summary>0 = Vigenère/Vernam, 1 = Beaufort, 2 = variant Beaufort.</summary>
    [ObservableProperty]
    public partial int VariantIndex { get; set; }

    /// <summary>0 = Letters (mod 26), 1 = Letters+digits (mod 36), 2 = printable ASCII (mod 95).</summary>
    [ObservableProperty]
    public partial int CharsetIndex { get; set; }

    /// <summary>Beyond-parity: keep original case and pass out-of-charset characters through unchanged.</summary>
    [ObservableProperty]
    public partial bool PreserveNonLetters { get; set; }

    [ObservableProperty]
    public partial string KeyText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string InputText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string OutputText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial bool HasOutput { get; set; }

    /// <summary><see langword="true"/> when the key was cycled because it is shorter than the message —
    /// surfaces the "a true one-time pad needs a key at least as long as the message" warning.</summary>
    [ObservableProperty]
    public partial bool KeyTooShort { get; set; }

    private bool IsDecode => DirectionIndex == 1;

    private OneTimePadVariant Variant => VariantIndex switch
    {
        1 => OneTimePadVariant.Beaufort,
        2 => OneTimePadVariant.VariantBeaufort,
        _ => OneTimePadVariant.Vigenere,
    };

    private OneTimePadCharset Charset => CharsetIndex switch
    {
        1 => OneTimePadCharset.LettersAndDigits,
        2 => OneTimePadCharset.PrintableAscii,
        _ => OneTimePadCharset.Letters,
    };

    partial void OnDirectionIndexChanged(int value)
    {
        if (_suppressRecompute || value is not (0 or 1))
        {
            return;
        }

        // Carry the previous result into the input for a one-tap encrypt/decrypt round-trip.
        _suppressRecompute = true;
        InputText = OutputText;
        _suppressRecompute = false;
        Recompute();
    }

    partial void OnVariantIndexChanged(int value)
    {
        if (value is 0 or 1 or 2)
        {
            Recompute();
        }
    }

    partial void OnCharsetIndexChanged(int value)
    {
        if (value is 0 or 1 or 2)
        {
            Recompute();
        }
    }

    partial void OnPreserveNonLettersChanged(bool value) => Recompute();

    partial void OnKeyTextChanged(string value) => Recompute();

    partial void OnInputTextChanged(string value)
    {
        if (_suppressRecompute)
        {
            return;
        }

        Recompute();
    }

    partial void OnHasOutputChanged(bool value)
    {
        CopyOutputCommand.NotifyCanExecuteChanged();
        ShareOutputCommand.NotifyCanExecuteChanged();
    }

    private void Recompute()
    {
        if (string.IsNullOrEmpty(InputText))
        {
            OutputText = string.Empty;
            HasOutput = false;
            KeyTooShort = false;
            return;
        }

        var result = _cipher.Transform(InputText, KeyText, IsDecode, Variant, Charset, PreserveNonLetters);
        OutputText = result.Text;
        KeyTooShort = result.KeyTooShort;
        HasOutput = result.Text.Length > 0;
    }

    /// <summary>Generates a fresh cryptographically-random pad as long as the message (a true OTP key).</summary>
    [RelayCommand]
    private void GeneratePad()
    {
        var length = InputText.Length > 0 ? InputText.Length : 1;
        KeyText = _cipher.GeneratePad(length, Charset);
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
    private void Clear()
    {
        InputText = string.Empty;
        KeyText = string.Empty;
    }
}
