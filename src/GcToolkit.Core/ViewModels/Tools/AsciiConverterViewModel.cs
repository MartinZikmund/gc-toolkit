using System.Globalization;
using System.Threading.Tasks;
using GcToolkit.Core.Catalog;
using GcToolkit.Core.Discovery;
using GcToolkit.Core.FavoriteTools;
using GcToolkit.Core.Infrastructure;
using GcToolkit.Core.Recents;
using GcToolkit.Core.Services;
using GcToolkit.Core.Text;
using Microsoft.Extensions.Localization;

namespace GcToolkit.Core.ViewModels.Tools;

/// <summary>
/// ASCII / Unicode code converter (issue #7). Mirrors geocachingtoolbox.com's any-to-any matrix —
/// a <b>From</b> and a <b>To</b> format across Text, Binary, Octal, Decimal, Hexadecimal, Base32,
/// Base64 and HTML entities — and goes beyond it with live conversion (no Convert button), input
/// auto-detection, full Unicode code points (not just 0–255), and a per-character all-bases table.
/// All conversion logic lives in the pure <see cref="AsciiConverter"/> (thin-VM convention).
/// </summary>
[Tool("AsciiConverter", ToolCategory.Text,
      Introduced = "2026-06-06", Updated = "2026-06-06",
      Keywords = ["ascii", "unicode", "code", "codes", "character", "decimal", "hex", "hexadecimal", "octal", "binary", "base32", "base64", "html", "entity", "kód", "kódy", "znak", "převod", "ascii kód"])]
public sealed partial class AsciiConverterViewModel : ToolViewModelBase, IToolTextInput
{
    /// <summary>The <b>To</b> dropdown's items, in order — the <b>From</b> list is this prefixed by "auto".</summary>
    private static readonly AsciiFormat[] Formats =
    [
        AsciiFormat.Text,
        AsciiFormat.Binary,
        AsciiFormat.Octal,
        AsciiFormat.Decimal,
        AsciiFormat.Hexadecimal,
        AsciiFormat.Base32,
        AsciiFormat.Base64,
        AsciiFormat.Html,
    ];

    /// <summary>Index of <see cref="AsciiFormat.Decimal"/> in <see cref="Formats"/> — the default target.</summary>
    private const int DecimalIndex = 3;

    private readonly AsciiConverter _converter = new();
    private readonly IClipboardService _clipboard;
    private readonly IShareService _share;
    private readonly IStringLocalizer _localizer;

    // The per-character table grows with the input, so coalesce keystrokes instead of rebuilding it on each.
    private readonly UiDebouncer _debouncer = new(TimeSpan.FromMilliseconds(200));

    private bool _suppressConvert;

    /// <summary>The format the last conversion actually read the input as (the detected one under auto).</summary>
    private AsciiFormat _resolvedFromFormat = AsciiFormat.Text;

    public AsciiConverterViewModel(
        ICatalogService catalog,
        IRecentsService recents,
        IFavoriteToolsService favorites,
        IStringLocalizer localizer,
        IClipboardService clipboard,
        IShareService share)
        : base("AsciiConverter", catalog, recents, favorites, localizer)
    {
        _clipboard = clipboard;
        _share = share;
        _localizer = localizer;
    }

    /// <summary>The input format: 0 = auto-detect, 1..8 = <see cref="Formats"/> shifted by one.</summary>
    [ObservableProperty]
    public partial int FromFormatIndex { get; set; }

    /// <summary>The output format — an index into <see cref="Formats"/>.</summary>
    [ObservableProperty]
    public partial int ToFormatIndex { get; set; } = DecimalIndex;

    /// <summary>The separator inserted between codes (and tolerated, with any punctuation, when reading them).</summary>
    [ObservableProperty]
    public partial string Separator { get; set; } = AsciiConverter.DefaultSeparator;

    /// <summary>Parity with the website's "Remove non-encoded spaces from the result" checkbox.</summary>
    [ObservableProperty]
    public partial bool RemoveSpaces { get; set; }

    [ObservableProperty]
    public partial string InputText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string OutputText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial bool HasOutput { get; set; }

    /// <summary><see langword="true"/> when the input can't be read in the chosen source format.</summary>
    [ObservableProperty]
    public partial bool HasError { get; set; }

    [ObservableProperty]
    public partial string ErrorMessage { get; set; } = string.Empty;

    /// <summary>The separator only affects the grouped numeric formats — hide it for the others.</summary>
    [ObservableProperty]
    public partial bool ShowSeparator { get; set; } = true;

    /// <summary>When auto-detecting, the format that was actually used — surfaced as a hint.</summary>
    [ObservableProperty]
    public partial string DetectedFormatName { get; set; } = string.Empty;

    [ObservableProperty]
    public partial bool ShowDetectedFormat { get; set; }

    /// <summary>
    /// The per-code-point breakdown of the decoded text: each character in all four bases. Assigned
    /// wholesale (not mutated) so the bound virtualizing list rebinds once per conversion.
    /// </summary>
    [ObservableProperty]
    public partial IReadOnlyList<AsciiCodeRow> Breakdown { get; set; } = [];

    [ObservableProperty]
    public partial bool HasBreakdown { get; set; }

    private AsciiFormat? SelectedFromFormat
        => FromFormatIndex <= 0 ? null : Formats[Math.Min(FromFormatIndex - 1, Formats.Length - 1)];

    private AsciiFormat SelectedToFormat
        => Formats[Math.Clamp(ToFormatIndex, 0, Formats.Length - 1)];

    // Ignore the transient -1 a selector can emit while re-templating.
    partial void OnFromFormatIndexChanged(int value) => ConvertUnlessSuppressed(value >= 0);

    partial void OnToFormatIndexChanged(int value) => ConvertUnlessSuppressed(value >= 0);

    partial void OnRemoveSpacesChanged(bool value) => ConvertUnlessSuppressed(true);

    partial void OnSeparatorChanged(string value)
    {
        if (!_suppressConvert)
        {
            _debouncer.Debounce(Convert);
        }
    }

    private void ConvertUnlessSuppressed(bool valid)
    {
        if (valid && !_suppressConvert)
        {
            _debouncer.RunNow(Convert);
        }
    }

    partial void OnInputTextChanged(string value)
    {
        if (!_suppressConvert)
        {
            _debouncer.Debounce(Convert);
        }
    }

    partial void OnHasOutputChanged(bool value)
    {
        CopyOutputCommand.NotifyCanExecuteChanged();
        ShareOutputCommand.NotifyCanExecuteChanged();
        SwapCommand.NotifyCanExecuteChanged();
    }

    private void Convert()
    {
        var to = SelectedToFormat;
        ShowSeparator = IsGrouped(to);
        ShowDetectedFormat = false;
        DetectedFormatName = string.Empty;

        if (string.IsNullOrEmpty(InputText))
        {
            OutputText = string.Empty;
            HasOutput = false;
            HasError = false;
            ErrorMessage = string.Empty;
            Breakdown = [];
            HasBreakdown = false;
            return;
        }

        var from = SelectedFromFormat;
        var separator = Separator ?? string.Empty;

        if (!_converter.TryConvert(InputText, from, to, separator, RemoveSpaces, out var result, out var resolvedFrom))
        {
            OutputText = string.Empty;
            HasOutput = false;
            Breakdown = [];
            HasBreakdown = false;
            HasError = true;
            ErrorMessage = string.Format(
                CultureInfo.CurrentCulture,
                _localizer["AsciiInvalidInput"].Value,
                FormatDisplayName(from ?? resolvedFrom));
            return;
        }

        _resolvedFromFormat = resolvedFrom;
        if (from is null)
        {
            DetectedFormatName = FormatDisplayName(resolvedFrom);
            ShowDetectedFormat = true;
        }

        OutputText = result;
        HasOutput = result.Length > 0;
        HasError = false;
        ErrorMessage = string.Empty;

        // The table describes the decoded text, so it is meaningful whichever direction we're going.
        _converter.TryToText(InputText, resolvedFrom, out var pivot);
        Breakdown = _converter.Describe(pivot);
        HasBreakdown = Breakdown.Count > 0;
    }

    /// <summary>Only the numeric bases emit one group per character, so only they use the separator.</summary>
    private static bool IsGrouped(AsciiFormat format)
        => format is AsciiFormat.Binary or AsciiFormat.Octal or AsciiFormat.Decimal or AsciiFormat.Hexadecimal;

    private string FormatDisplayName(AsciiFormat format) => format switch
    {
        AsciiFormat.Text => _localizer["AsciiFormatText"].Value,
        AsciiFormat.Binary => _localizer["AsciiBaseBinary"].Value,
        AsciiFormat.Octal => _localizer["AsciiBaseOctal"].Value,
        AsciiFormat.Hexadecimal => _localizer["AsciiBaseHex"].Value,
        AsciiFormat.Base32 => _localizer["AsciiFormatBase32"].Value,
        AsciiFormat.Base64 => _localizer["AsciiFormatBase64"].Value,
        AsciiFormat.Html => _localizer["AsciiFormatHtml"].Value,
        _ => _localizer["AsciiBaseDecimal"].Value,
    };

    /// <summary>Swaps the From/To formats and feeds the result back in, so a round-trip is one tap.</summary>
    [RelayCommand(CanExecute = nameof(HasOutput))]
    private void Swap()
    {
        // Under auto-detect there is no explicit source format — fall back to what was actually detected.
        var previousFrom = SelectedFromFormat ?? _resolvedFromFormat;
        var previousTo = Math.Clamp(ToFormatIndex, 0, Formats.Length - 1);

        _suppressConvert = true;
        FromFormatIndex = previousTo + 1;
        ToFormatIndex = Math.Max(Array.IndexOf(Formats, previousFrom), 0);
        InputText = OutputText;
        _suppressConvert = false;

        _debouncer.RunNow(Convert);
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
