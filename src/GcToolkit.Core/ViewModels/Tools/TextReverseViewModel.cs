using System.Threading.Tasks;
using GcToolkit.Core.Catalog;
using GcToolkit.Core.Discovery;
using GcToolkit.Core.FavoriteTools;
using GcToolkit.Core.Recents;
using GcToolkit.Core.Services;
using GcToolkit.Core.Text;
using Microsoft.Extensions.Localization;

namespace GcToolkit.Core.ViewModels.Tools;

/// <summary>
/// Text reverse (issue #24) — reorders text seven ways (reverse whole / per line, reverse word order
/// whole / per line, reverse or randomize characters per word, and upside-down flip) with composable
/// case and typoglycemia modifiers, matching the geocachingtoolbox.com Reverse-text tool. Goes beyond
/// parity with full grapheme awareness (emoji and combining diacritics stay intact), a seeded re-roll
/// for the random mode, and a clean upside-down de-flip. All logic lives in the pure
/// <see cref="TextReverseCodec"/> (thin-VM convention).
/// </summary>
[Tool("TextReverse", ToolCategory.Text,
      Introduced = "2026-06-13", Updated = "2026-06-13",
      Keywords = ["reverse", "text", "mirror", "flip", "upside down", "backwards", "shuffle", "anagram", "typoglycemia",
                  "obrátit", "obrácený", "text", "zrcadlo", "vzhůru nohama", "pozpátku", "zamíchat"])]
public sealed partial class TextReverseViewModel : ToolViewModelBase, IToolTextInput
{
    private readonly TextReverseCodec _codec = new();
    private readonly IClipboardService _clipboard;
    private readonly IShareService _share;
    private int _rerollSeed = Environment.TickCount;

    public TextReverseViewModel(
        ICatalogService catalog,
        IRecentsService recents,
        IFavoriteToolsService favorites,
        IStringLocalizer localizer,
        IClipboardService clipboard,
        IShareService share)
        : base("TextReverse", catalog, recents, favorites, localizer)
    {
        _clipboard = clipboard;
        _share = share;
        Recompute();
    }

    /// <summary>Active mode 0..6, mapping to <see cref="TextReverseMode"/> in declaration order.</summary>
    [ObservableProperty]
    public partial int ModeIndex { get; set; }

    [ObservableProperty]
    public partial string InputText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string OutputText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial bool HasOutput { get; set; }

    // ---- Modifiers ----

    [ObservableProperty]
    public partial bool KeepUpperCaseLocation { get; set; }

    [ObservableProperty]
    public partial bool KeepFirstAndLastCharacter { get; set; }

    [ObservableProperty]
    public partial bool SwapCase { get; set; }

    [ObservableProperty]
    public partial bool ToUpperCase { get; set; }

    [ObservableProperty]
    public partial bool ToLowerCase { get; set; }

    [ObservableProperty]
    public partial bool RemoveUnknownCharacters { get; set; }

    /// <summary>The upside-down "reverse text" sub-option (read correctly when the device is rotated 180°).</summary>
    [ObservableProperty]
    public partial bool ReverseUpsideDown { get; set; }

    /// <summary><see langword="true"/> when the random-per-word mode is active (shows the re-roll button).</summary>
    public bool IsRandomMode => ModeIndex == (int)TextReverseMode.RandomCharactersPerWord;

    /// <summary><see langword="true"/> when the upside-down mode is active (shows its sub-option + remove-unknown).</summary>
    public bool IsUpsideDownMode => ModeIndex == (int)TextReverseMode.UpsideDown;

    private TextReverseMode Mode => (TextReverseMode)ModeIndex;

    private TextReverseModifiers Modifiers
    {
        get
        {
            var flags = TextReverseModifiers.None;
            if (KeepUpperCaseLocation)
            {
                flags |= TextReverseModifiers.KeepUpperCaseLocation;
            }

            if (KeepFirstAndLastCharacter)
            {
                flags |= TextReverseModifiers.KeepFirstAndLastCharacter;
            }

            if (SwapCase)
            {
                flags |= TextReverseModifiers.SwapCase;
            }

            if (ToUpperCase)
            {
                flags |= TextReverseModifiers.ToUpperCase;
            }

            if (ToLowerCase)
            {
                flags |= TextReverseModifiers.ToLowerCase;
            }

            if (RemoveUnknownCharacters)
            {
                flags |= TextReverseModifiers.RemoveUnknownCharacters;
            }

            return flags;
        }
    }

    partial void OnModeIndexChanged(int value)
    {
        if (value is < 0 or > 6)
        {
            return;
        }

        OnPropertyChanged(nameof(IsRandomMode));
        OnPropertyChanged(nameof(IsUpsideDownMode));
        RerollCommand.NotifyCanExecuteChanged();
        Recompute();
    }

    partial void OnInputTextChanged(string value) => Recompute();

    partial void OnKeepUpperCaseLocationChanged(bool value) => Recompute();

    partial void OnKeepFirstAndLastCharacterChanged(bool value) => Recompute();

    partial void OnSwapCaseChanged(bool value) => Recompute();

    partial void OnToUpperCaseChanged(bool value)
    {
        // Upper and lower are mutually exclusive — turning one on clears the other.
        if (value && ToLowerCase)
        {
            ToLowerCase = false;
        }

        Recompute();
    }

    partial void OnToLowerCaseChanged(bool value)
    {
        if (value && ToUpperCase)
        {
            ToUpperCase = false;
        }

        Recompute();
    }

    partial void OnRemoveUnknownCharactersChanged(bool value) => Recompute();

    partial void OnReverseUpsideDownChanged(bool value) => Recompute();

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
            return;
        }

        var options = new TextReverseOptions(Mode, Modifiers, ReverseUpsideDown, _rerollSeed);
        OutputText = _codec.Transform(InputText, options);
        HasOutput = true;
    }

    /// <summary>Draws a fresh seed and re-runs, giving the random mode a new shuffle on demand.</summary>
    [RelayCommand(CanExecute = nameof(IsRandomMode))]
    private void Reroll()
    {
        _rerollSeed = Environment.TickCount + Random.Shared.Next();
        Recompute();
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
