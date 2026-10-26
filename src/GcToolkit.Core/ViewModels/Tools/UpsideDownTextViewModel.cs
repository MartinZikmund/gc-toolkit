using System.Globalization;
using System.Threading.Tasks;
using GcToolkit.Core.Catalog;
using GcToolkit.Core.Coordinates;
using GcToolkit.Core.Discovery;
using GcToolkit.Core.FavoriteTools;
using GcToolkit.Core.Recents;
using GcToolkit.Core.Services;
using GcToolkit.Core.Text;
using Microsoft.Extensions.Localization;

namespace GcToolkit.Core.ViewModels.Tools;

/// <summary>
/// Upside-down text (issue #307) — flips text 180° with rotated Unicode look-alikes and reads it back.
/// Goes beyond cachesleuth.com parity with auto-detect, round-trip and coverage warnings, separate
/// reverse / rotate toggles, glyph variants, per-line batching and coordinate detection. All logic is
/// in the pure <see cref="UpsideDownCodec"/>.
/// </summary>
[Tool("UpsideDownText", ToolCategory.Text,
      Introduced = "2026-10-26", Updated = "2026-10-26",
      Keywords = ["upside down", "flip", "rotate", "mirror", "reverse", "text", "180", "unflip",
                  "vzhůru nohama", "otočit", "převrátit", "zrcadlo", "text"])]
public sealed partial class UpsideDownTextViewModel : ToolViewModelBase
{
    private static readonly int[] _groupSizes = [0, 2, 3, 4, 5];

    private readonly IStringLocalizer _localizer;
    private readonly IClipboardService _clipboard;
    private readonly IShareService _share;

    public UpsideDownTextViewModel(
        ICatalogService catalog,
        IRecentsService recents,
        IFavoriteToolsService favorites,
        IStringLocalizer localizer,
        IClipboardService clipboard,
        IShareService share)
        : base("UpsideDownText", catalog, recents, favorites, localizer)
    {
        _localizer = localizer;
        _clipboard = clipboard;
        _share = share;
        Recompute();
    }

    /// <summary>0 = auto-detect, 1 = flip, 2 = unflip (mirrors <see cref="UpsideDownDirection"/>).</summary>
    [ObservableProperty]
    public partial int DirectionIndex { get; set; }

    [ObservableProperty]
    public partial string InputText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string OutputText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial bool HasOutput { get; set; }

    [ObservableProperty]
    public partial bool ReverseEnabled { get; set; } = true;

    [ObservableProperty]
    public partial bool RotateEnabled { get; set; } = true;

    /// <summary>0 = full glyph set, 1 = compatible subset.</summary>
    [ObservableProperty]
    public partial int VariantIndex { get; set; }

    /// <summary>0 = keep, 1 = placeholder, 2 = remove.</summary>
    [ObservableProperty]
    public partial int UnmappedIndex { get; set; }

    /// <summary>0 = none, 1 = UPPER, 2 = lower, 3 = First Letter.</summary>
    [ObservableProperty]
    public partial int CaseIndex { get; set; }

    /// <summary>Index into the group sizes 0 (off), 2, 3, 4, 5.</summary>
    [ObservableProperty]
    public partial int GroupIndex { get; set; }

    [ObservableProperty]
    public partial bool PerLine { get; set; }

    [ObservableProperty]
    public partial bool RemoveSpaces { get; set; }

    [ObservableProperty]
    public partial double OutputFontSize { get; set; } = 24;

    /// <summary>Which direction auto-detect (or the user) resolved to, shown under the result.</summary>
    [ObservableProperty]
    public partial string DetectedDirectionName { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string CoverageText { get; set; } = string.Empty;

    /// <summary>Warning about characters without a rotated form (lossy round trip); empty when fine.</summary>
    [ObservableProperty]
    public partial string WarningMessage { get; set; } = string.Empty;

    /// <summary>A coordinate found in the readable text, formatted for display and copying.</summary>
    [ObservableProperty]
    public partial string CoordinateText { get; set; } = string.Empty;

    public bool HasWarning => !string.IsNullOrEmpty(WarningMessage);

    public bool HasCoordinate => !string.IsNullOrEmpty(CoordinateText);

    partial void OnDirectionIndexChanged(int value) => Recompute();

    partial void OnInputTextChanged(string value) => Recompute();

    partial void OnReverseEnabledChanged(bool value) => Recompute();

    partial void OnRotateEnabledChanged(bool value) => Recompute();

    partial void OnVariantIndexChanged(int value) => Recompute();

    partial void OnUnmappedIndexChanged(int value) => Recompute();

    partial void OnCaseIndexChanged(int value) => Recompute();

    partial void OnGroupIndexChanged(int value) => Recompute();

    partial void OnPerLineChanged(bool value) => Recompute();

    partial void OnRemoveSpacesChanged(bool value) => Recompute();

    partial void OnWarningMessageChanged(string value) => OnPropertyChanged(nameof(HasWarning));

    partial void OnCoordinateTextChanged(string value)
    {
        OnPropertyChanged(nameof(HasCoordinate));
        CopyCoordinateCommand.NotifyCanExecuteChanged();
    }

    partial void OnHasOutputChanged(bool value)
    {
        CopyOutputCommand.NotifyCanExecuteChanged();
        ShareOutputCommand.NotifyCanExecuteChanged();
        SwapCommand.NotifyCanExecuteChanged();
    }

    private UpsideDownOptions BuildOptions() => new(
        Direction: (UpsideDownDirection)Math.Clamp(DirectionIndex, 0, 2),
        Reverse: ReverseEnabled,
        Rotate: RotateEnabled,
        Variant: (UpsideDownVariant)Math.Clamp(VariantIndex, 0, 1),
        Unmapped: (UpsideDownUnmapped)Math.Clamp(UnmappedIndex, 0, 2),
        PerLine: PerLine,
        Case: (UpsideDownCase)Math.Clamp(CaseIndex, 0, 3),
        RemoveSpaces: RemoveSpaces,
        GroupSize: _groupSizes[Math.Clamp(GroupIndex, 0, _groupSizes.Length - 1)]);

    private void Recompute()
    {
        if (string.IsNullOrEmpty(InputText))
        {
            OutputText = string.Empty;
            HasOutput = false;
            DetectedDirectionName = string.Empty;
            CoverageText = string.Empty;
            WarningMessage = string.Empty;
            CoordinateText = string.Empty;
            return;
        }

        var result = UpsideDownCodec.Transform(InputText, BuildOptions());
        OutputText = result.Text;
        HasOutput = result.Text.Length > 0;

        var flipping = result.ResolvedDirection == UpsideDownDirection.Flip;
        DetectedDirectionName = _localizer[flipping ? "UpsideDownDirectionFlip" : "UpsideDownDirectionUnflip"].Value;

        if (result.TotalCount == 0)
        {
            CoverageText = string.Empty;
            WarningMessage = _localizer["UpsideDownNoFlippable"].Value;
        }
        else
        {
            CoverageText = string.Format(CultureInfo.CurrentCulture, _localizer["UpsideDownCoverage"].Value, result.MappedCount, result.TotalCount);
            WarningMessage = result.RoundTrips
                ? string.Empty
                : result.UnmappedCharacters.Count > 0
                    ? string.Format(CultureInfo.CurrentCulture, _localizer["UpsideDownUnmappedWarning"].Value, string.Concat(result.UnmappedCharacters.Select(c => c + " ")).TrimEnd())
                    : _localizer["UpsideDownLossyWarning"].Value;
        }

        // The readable side is the output when decoding and the input when encoding.
        CoordinateText = FindCoordinate(flipping ? InputText : result.Text);
    }

    private static string FindCoordinate(string readable)
    {
        var text = readable.Trim();
        return text.Length > 0 && CoordinateParser.TryParse(text, out var coordinate, out _) && coordinate.IsValid
            ? CoordinateFormatter.Format(coordinate, CoordinateFormat.DegreesDecimalMinutes)
            : string.Empty;
    }

    [RelayCommand(CanExecute = nameof(HasOutput))]
    private void CopyOutput() => _clipboard.SetText(OutputText);

    [RelayCommand(CanExecute = nameof(HasCoordinate))]
    private void CopyCoordinate() => _clipboard.SetText(CoordinateText);

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

    /// <summary>Feeds the result back as input, so the user can verify or keep transforming.</summary>
    [RelayCommand(CanExecute = nameof(HasOutput))]
    private void Swap() => InputText = OutputText;

    [RelayCommand]
    private void Example() => InputText = "Geocache";

    [RelayCommand]
    private void Clear() => InputText = string.Empty;
}
