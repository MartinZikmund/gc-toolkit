using System.Globalization;
using System.Threading.Tasks;
using GcToolkit.Core.Alphabets;
using GcToolkit.Core.Catalog;
using GcToolkit.Core.Discovery;
using GcToolkit.Core.FavoriteTools;
using GcToolkit.Core.Recents;
using GcToolkit.Core.Services;
using Microsoft.Extensions.Localization;

namespace GcToolkit.Core.ViewModels.Tools;

/// <summary>
/// Bidirectional ICS signal-flag converter (issue #61). Renders typed text as a live flag hoist and
/// builds text from taps on the reference chart, with per-flag NATO phonetic names and single-flag
/// meanings, adjustable flag size, and copy/share — feature parity with geocachingtoolbox.com plus
/// captions, tooltips and sizing it does not have. See <see cref="SignalFlagsAlphabet"/>.
/// </summary>
[Tool("SignalFlags", ToolCategory.Alphabets,
      Introduced = "2026-06-10", Updated = "2026-06-10",
      Keywords = ["signal", "flags", "maritime", "nautical", "ICS", "ship", "vlajky", "námořní", "signální", "vlajková abeceda"])]
public sealed partial class SignalFlagsViewModel : ToolViewModelBase, IToolTextInput
{
    private readonly SignalFlagsCodec _codec = new();
    private readonly IStringLocalizer _localizer;
    private readonly IClipboardService _clipboard;
    private readonly IShareService _share;

    public SignalFlagsViewModel(
        ICatalogService catalog,
        IRecentsService recents,
        IFavoriteToolsService favorites,
        IStringLocalizer localizer,
        IClipboardService clipboard,
        IShareService share)
        : base("SignalFlags", catalog, recents, favorites, localizer)
    {
        _localizer = localizer;
        _clipboard = clipboard;
        _share = share;

        LetterPalette = [.. SignalFlagsAlphabet.Letters.Select(CreateInteractiveItem)];
        NumeralPalette = [.. SignalFlagsAlphabet.Numerals.Select(CreateInteractiveItem)];
        SpecialPalette = [.. SignalFlagsAlphabet.Specials.Select(CreateDisplayItem)];
    }

    public IReadOnlyList<SignalFlagsPaletteItem> LetterPalette { get; }

    public IReadOnlyList<SignalFlagsPaletteItem> NumeralPalette { get; }

    public IReadOnlyList<SignalFlagsPaletteItem> SpecialPalette { get; }

    [ObservableProperty]
    public partial string InputText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial IReadOnlyList<SignalFlagsSequenceItem> Sequence { get; set; } = [];

    [ObservableProperty]
    public partial bool HasFlags { get; set; }

    /// <summary><see langword="true"/> when the input contained characters with no flag.</summary>
    [ObservableProperty]
    public partial bool HasSkipped { get; set; }

    [ObservableProperty]
    public partial string SkippedNotice { get; set; } = string.Empty;

    [ObservableProperty]
    public partial double FlagSize { get; set; } = 56;

    public double MinFlagSize => 32;

    public double MaxFlagSize => 112;

    private bool HasText => !string.IsNullOrEmpty(InputText);

    partial void OnInputTextChanged(string value)
    {
        Rebuild();
        BackspaceCommand.NotifyCanExecuteChanged();
        ClearCommand.NotifyCanExecuteChanged();
        CopyTextCommand.NotifyCanExecuteChanged();
        ShareTextCommand.NotifyCanExecuteChanged();
    }

    partial void OnFlagSizeChanged(double value) => Rebuild();

    [RelayCommand(CanExecute = nameof(HasText))]
    private void Backspace()
    {
        // Remove a whole text element so surrogate pairs and combining marks never get split.
        StringInfo info = new(InputText);
        InputText = info.LengthInTextElements <= 1
            ? string.Empty
            : info.SubstringByTextElements(0, info.LengthInTextElements - 1);
    }

    [RelayCommand]
    private void AppendSpace() => InputText += " ";

    [RelayCommand(CanExecute = nameof(HasText))]
    private void Clear() => InputText = string.Empty;

    [RelayCommand(CanExecute = nameof(HasText))]
    private void CopyText() => _clipboard.SetText(InputText);

    [RelayCommand(CanExecute = nameof(HasText))]
    private async Task ShareTextAsync()
    {
        try
        {
            await _share.ShareTextAsync(ToolName, InputText);
        }
        catch (Exception)
        {
            // Sharing is best-effort; a platform share failure must not crash the tool.
        }
    }

    private void Rebuild()
    {
        var result = _codec.Encode(InputText);

        Sequence = [.. result.Tokens.Select(CreateSequenceItem)];
        HasFlags = Sequence.Count > 0;
        HasSkipped = result.HasSkipped;
        SkippedNotice = result.HasSkipped
            ? string.Format(
                CultureInfo.CurrentCulture,
                _localizer["SignalFlagsSkippedNotice"].Value,
                string.Join(' ', result.SkippedCharacters))
            : string.Empty;
    }

    private void AppendFlag(SignalFlagDescriptor flag) => InputText += flag.Symbol;

    private SignalFlagsSequenceItem CreateSequenceItem(SignalFlagsToken token)
        => token.IsGap
            ? new(null, FlagSize, string.Empty, _localizer["SignalFlagsGapName"].Value)
            : new(token.Flag, FlagSize, Describe(token.Flag!), ShortName(token.Flag!));

    private SignalFlagsPaletteItem CreateInteractiveItem(SignalFlagDescriptor flag)
        => new(flag, flag.Symbol!.Value.ToString(), flag.PhoneticName, Describe(flag), ShortName(flag), AppendFlag);

    private SignalFlagsPaletteItem CreateDisplayItem(SignalFlagDescriptor flag)
        => new(flag, _localizer[flag.CaptionKey!].Value, string.Empty, Describe(flag), _localizer[flag.CaptionKey!].Value, append: null);

    /// <summary>"A — Alfa", or the localized special name.</summary>
    private string ShortName(SignalFlagDescriptor flag)
        => flag.Symbol is char symbol
            ? string.IsNullOrEmpty(flag.PhoneticName) ? symbol.ToString() : $"{symbol} — {flag.PhoneticName}"
            : _localizer[flag.CaptionKey!].Value;

    /// <summary>The full tooltip: short name plus the single-flag ICS meaning, when one is assigned.</summary>
    private string Describe(SignalFlagDescriptor flag)
    {
        var name = ShortName(flag);
        return flag.MeaningKey is null ? name : $"{name} — {_localizer[flag.MeaningKey].Value}";
    }
}
