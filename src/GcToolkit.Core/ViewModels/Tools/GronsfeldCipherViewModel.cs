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
/// Gronsfeld cipher (issue #17) — a Vigenère variant with a purely numeric key. Each successive letter
/// is Caesar-shifted by the next digit of the key (0–9), the key repeating cyclically; the key advances
/// only on letters, so spaces/digits/punctuation pass through unchanged. Encodes and decodes live as the
/// user types, with an encode/decode toggle, a one-tap direction swap, copy/share, and a 0⇒A…9⇒J digit
/// reference. All transform logic lives in the pure <see cref="GronsfeldCipher"/> (thin-VM convention).
/// </summary>
[Tool("GronsfeldCipher", ToolCategory.Ciphers,
      Introduced = "2026-06-13", Updated = "2026-06-13",
      Keywords = ["gronsfeld", "vigenère", "vigenere", "numeric key", "cipher", "shift", "číselný klíč", "posun", "šifra"])]
public sealed partial class GronsfeldCipherViewModel : ToolViewModelBase, IToolTextInput, IToolTextOutput
{
    private readonly GronsfeldCipher _cipher = new();
    private readonly IClipboardService _clipboard;
    private readonly IShareService _share;

    private bool _suppressRecompute;

    public GronsfeldCipherViewModel(
        ICatalogService catalog,
        IRecentsService recents,
        IFavoriteToolsService favorites,
        IStringLocalizer localizer,
        IClipboardService clipboard,
        IShareService share)
        : base("GronsfeldCipher", catalog, recents, favorites, localizer)
    {
        _clipboard = clipboard;
        _share = share;
        Recompute();
    }

    /// <summary>The numeric key (digits 0–9). Each digit shifts the next letter; the key repeats cyclically.</summary>
    [ObservableProperty]
    public partial string Key { get; set; } = "1234";

    /// <summary>0 = Encode (shift forward), 1 = Decode (shift back).</summary>
    [ObservableProperty]
    public partial int DirectionIndex { get; set; }

    [ObservableProperty]
    public partial string InputText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string OutputText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial bool HasOutput { get; set; }

    /// <summary><see langword="true"/> when the key is empty or contains a non-digit — surfaces the inline error.</summary>
    [ObservableProperty]
    public partial bool HasKeyError { get; set; }

    /// <summary>The digit→letter reference rows (0⇒A … 9⇒J) shown to explain the shifts.</summary>
    public IReadOnlyList<GronsfeldDigitReference> DigitReference { get; } = GronsfeldCipher.DigitReference;

    private bool IsDecode => DirectionIndex == 1;

    partial void OnKeyChanged(string value) => Recompute();

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
    }

    partial void OnInputTextChanged(string value)
    {
        if (!_suppressRecompute)
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
        HasKeyError = !GronsfeldCipher.IsValidKey(Key);

        if (HasKeyError || string.IsNullOrEmpty(InputText))
        {
            OutputText = string.Empty;
            HasOutput = false;
            return;
        }

        OutputText = _cipher.Transform(InputText, Key, IsDecode);
        HasOutput = true;
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
