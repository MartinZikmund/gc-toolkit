using System.Collections.ObjectModel;
using System.Linq;
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
/// Bidirectional Braille converter (issue #36). Translates text to and from Grade-1 (uncontracted)
/// English Braille live as the user types. The braille side is shown both as copy-pasteable Unicode
/// pattern characters and as 2×3 dot grids (filled vs. hollow circles, so empty positions read clearly),
/// and the geocachingtoolbox.com reference "alphabet" is rendered as a clickable chart you can use to
/// "type" braille. Beyond parity it adds the number and capital indicators, literary punctuation, full
/// round-tripping in both directions, and copy/share. All logic lives in the pure
/// <see cref="BrailleCodec"/> (thin-VM convention).
/// </summary>
[Tool("Braille", ToolCategory.Alphabets,
      Introduced = "2026-06-06", Updated = "2026-06-07",
      Keywords = ["braille", "dots", "tactile", "blind", "abeceda", "slepecké", "písmo", "body", "unicode"])]
public sealed partial class BrailleViewModel : ToolViewModelBase, IToolTextInput, IToolTextOutput
{
    private readonly BrailleCodec _codec = new();
    private readonly IClipboardService _clipboard;
    private readonly IShareService _share;

    private bool _suppressConvert;

    public BrailleViewModel(
        ICatalogService catalog,
        IRecentsService recents,
        IFavoriteToolsService favorites,
        IStringLocalizer localizer,
        IClipboardService clipboard,
        IShareService share)
        : base("Braille", catalog, recents, favorites, localizer)
    {
        _clipboard = clipboard;
        _share = share;

        // The chart's three word-captioned indicators carry a localization key; everything else has a
        // language-neutral literal label.
        Alphabet = BrailleCodec.GetAlphabet()
            .Select(entry => entry.LabelKey is null ? entry : entry with { Label = localizer[entry.LabelKey] })
            .ToList();
    }

    /// <summary>0 = text → Braille, 1 = Braille → text.</summary>
    [ObservableProperty]
    public partial int DirectionIndex { get; set; }

    [ObservableProperty]
    public partial string InputText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string OutputText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial bool HasOutput { get; set; }

    [ObservableProperty]
    public partial bool HasGlyphs { get; set; }

    /// <summary>The braille side as dot-grid cells: the result when encoding, the input when decoding.</summary>
    public ObservableCollection<BrailleGlyph> Glyphs { get; } = [];

    /// <summary>The clickable braille reference chart (labels already localized).</summary>
    public IReadOnlyList<BraillePaletteEntry> Alphabet { get; }

    private bool IsTextToBraille => DirectionIndex != 1;

    partial void OnInputTextChanged(string value)
    {
        if (!_suppressConvert)
        {
            Convert();
        }
    }

    partial void OnDirectionIndexChanged(int value)
    {
        // Ignore re-entrant carries and the transient -1 a RadioButtons control can emit.
        if (_suppressConvert || value is not (0 or 1))
        {
            return;
        }

        // Switching direction carries the previous result into the input, so a round-trip is one tap.
        _suppressConvert = true;
        InputText = OutputText;
        _suppressConvert = false;
        Convert();
    }

    partial void OnHasOutputChanged(bool value)
    {
        CopyOutputCommand.NotifyCanExecuteChanged();
        ShareOutputCommand.NotifyCanExecuteChanged();
    }

    private void Convert()
    {
        OutputText = IsTextToBraille ? _codec.Encode(InputText) : _codec.Decode(InputText);
        HasOutput = !string.IsNullOrEmpty(OutputText);

        // The dot grids always visualise the braille side, whichever direction we're going.
        RebuildGlyphs(IsTextToBraille ? OutputText : InputText);
    }

    private void RebuildGlyphs(string braille)
    {
        Glyphs.Clear();
        foreach (var glyph in BrailleCodec.ToGlyphs(braille))
        {
            Glyphs.Add(glyph);
        }

        HasGlyphs = Glyphs.Count > 0;
    }

    /// <summary>"Types" a chart cell into the input: the plain character when encoding text → braille, or
    /// the braille cell when decoding braille → text. Capital/Number have no plain-text form, so they are
    /// a no-op in the text → braille direction.</summary>
    [RelayCommand]
    private void Insert(BraillePaletteEntry? entry)
    {
        if (entry is null)
        {
            return;
        }

        var fragment = IsTextToBraille ? entry.Text : entry.Cell;
        if (fragment.Length != 0)
        {
            InputText += fragment;
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
            // Sharing is best-effort; a platform share failure must not crash the tool.
        }
    }

    [RelayCommand]
    private void Clear() => InputText = string.Empty;
}
