using System.Globalization;
using System.Text;
using System.Threading.Tasks;
using GcToolkit.Core.Catalog;
using GcToolkit.Core.Discovery;
using GcToolkit.Core.FavoriteTools;
using GcToolkit.Core.Isbn;
using GcToolkit.Core.Recents;
using GcToolkit.Core.Services;
using Microsoft.Extensions.Localization;

namespace GcToolkit.Core.ViewModels.Tools;

/// <summary>
/// Validates, completes and converts ISBN-10/13 — plus the sibling ISSN, EAN-13/ISMN and UPC-A
/// codes (issue #211). Live and bidirectional: every equivalent form shows as the user types. A
/// missing or <c>?</c> check digit is computed with the weighted-sum working shown; <c>?</c> body
/// digits are solved by enumeration; a bad check digit lists the swaps and one-digit fixes that
/// would make it valid. Book lookups are opt-in deep links. All logic lives in <see cref="CodeAnalyzer"/>.
/// </summary>
[Tool("Isbn", ToolCategory.Field,
      Introduced = "2026-10-26", Updated = "2026-10-26",
      Keywords = ["isbn", "isbn-10", "isbn-13", "book", "check digit", "issn", "ismn", "ean", "ean-13",
                  "upc", "barcode", "bookland",
                  "kniha", "kontrolní číslice", "čárový kód"])]
public sealed partial class IsbnViewModel : ToolViewModelBase
{
    private static readonly CodeStandard[] _standards =
        [CodeStandard.Isbn, CodeStandard.Issn, CodeStandard.Ean13, CodeStandard.UpcA];

    /// <summary>Per standard: a valid code, a missing check digit, and a puzzle-style unknown.</summary>
    private static readonly string[][] _examples =
    [
        ["978-0-13-468599-1", "0-8044-2957-X", "979-10-90636-07-1", "0-306-40615-?", "978-0-306-4061?-7", "9780134865991"],
        ["0317-8471", "0317-847?", "2049-36?0"],
        ["9770317847001", "979-0-2600-0043-8", "400638133393?"],
        ["036000291452", "03600029145?"],
    ];

    private readonly IClipboardService _clipboard;
    private readonly IShareService _share;
    private readonly IStringLocalizer _localizer;
    private int _exampleIndex = -1;

    public IsbnViewModel(
        ICatalogService catalog,
        IRecentsService recents,
        IFavoriteToolsService favorites,
        IStringLocalizer localizer,
        IClipboardService clipboard,
        IShareService share)
        : base("Isbn", catalog, recents, favorites, localizer)
    {
        _clipboard = clipboard;
        _share = share;
        _localizer = localizer;
    }

    [ObservableProperty]
    public partial string InputText { get; set; } = string.Empty;

    /// <summary>0 = ISBN, 1 = ISSN, 2 = EAN-13 / ISMN, 3 = UPC-A.</summary>
    [ObservableProperty]
    public partial int StandardIndex { get; set; }

    [ObservableProperty]
    public partial bool ShowWorking { get; set; } = true;

    [ObservableProperty]
    public partial bool HasOutput { get; set; }

    [ObservableProperty]
    public partial string StatusTitle { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string StatusMessage { get; set; } = string.Empty;

    [ObservableProperty]
    public partial InfoBarSeverity StatusSeverity { get; set; } = InfoBarSeverity.Informational;

    /// <summary>The code itself first, then every equivalent form.</summary>
    [ObservableProperty]
    public partial IReadOnlyList<IsbnFormItem> Forms { get; set; } = [];

    [ObservableProperty]
    public partial bool HasForms { get; set; }

    [ObservableProperty]
    public partial string ConversionNote { get; set; } = string.Empty;

    [ObservableProperty]
    public partial bool HasConversionNote { get; set; }

    [ObservableProperty]
    public partial bool HasWorking { get; set; }

    [ObservableProperty]
    public partial string WorkingTermsLine { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string WorkingRemainderLine { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string WorkingCheckLine { get; set; } = string.Empty;

    [ObservableProperty]
    public partial IReadOnlyList<string> Solutions { get; set; } = [];

    [ObservableProperty]
    public partial bool HasSolutions { get; set; }

    [ObservableProperty]
    public partial string SolutionsTruncatedNote { get; set; } = string.Empty;

    [ObservableProperty]
    public partial bool HasSolutionsTruncatedNote { get; set; }

    [ObservableProperty]
    public partial IReadOnlyList<IsbnCorrectionItem> Corrections { get; set; } = [];

    [ObservableProperty]
    public partial bool HasCorrections { get; set; }

    [ObservableProperty]
    public partial IReadOnlyList<BookLookupLink> LookupLinks { get; set; } = [];

    [ObservableProperty]
    public partial bool HasLookupLinks { get; set; }

    private CodeStandard Standard => _standards[Math.Clamp(StandardIndex, 0, _standards.Length - 1)];

    partial void OnInputTextChanged(string value) => Recompute();

    partial void OnStandardIndexChanged(int value)
    {
        _exampleIndex = -1;
        Recompute();
    }

    partial void OnShowWorkingChanged(bool value) => Recompute();

    partial void OnHasOutputChanged(bool value)
    {
        CopyOutputCommand.NotifyCanExecuteChanged();
        ShareOutputCommand.NotifyCanExecuteChanged();
    }

    private void Recompute()
    {
        var analysis = CodeAnalyzer.Analyze(InputText, Standard);
        HasOutput = analysis.Status != CodeStatus.Empty;

        (StatusTitle, StatusMessage, StatusSeverity) = DescribeStatus(analysis);

        Forms = analysis.Code is { } code && analysis.Format is { } format
            ? [new(FormatName(format), code), .. analysis.Conversions.Select(c => new IsbnFormItem(FormatName(c.Format), c.Code))]
            : [];
        HasForms = Forms.Count > 0;

        ConversionNote = analysis.Note switch
        {
            Isbn.ConversionNote.NoIsbn10For979 => _localizer["IsbnNote979"].Value,
            Isbn.ConversionNote.NotBookland => _localizer["IsbnNoteNotBookland"].Value,
            _ => string.Empty,
        };
        HasConversionNote = ConversionNote.Length > 0;

        HasWorking = ShowWorking && analysis.Working is not null;
        WorkingTermsLine = analysis.Working?.TermsLine ?? string.Empty;
        WorkingRemainderLine = analysis.Working?.RemainderLine ?? string.Empty;
        WorkingCheckLine = analysis.Working?.CheckLine ?? string.Empty;

        Solutions = analysis.Solutions;
        HasSolutions = Solutions.Count > 0;
        HasSolutionsTruncatedNote = analysis.SolutionsTruncated;
        SolutionsTruncatedNote = HasSolutionsTruncatedNote
            ? Localize("IsbnSolutionsTruncatedNote", analysis.Solutions.Count, analysis.SolutionCount)
            : string.Empty;

        Corrections = [.. analysis.Corrections.Select(c => new IsbnCorrectionItem(c.Code, DescribeCorrection(c)))];
        HasCorrections = Corrections.Count > 0;

        LookupLinks = analysis.LookupLinks;
        HasLookupLinks = LookupLinks.Count > 0;
    }

    private (string Title, string Message, InfoBarSeverity Severity) DescribeStatus(CodeAnalysis analysis)
    {
        var format = analysis.Format is { } f ? FormatName(f) : StandardName(Standard);
        return analysis.Status switch
        {
            CodeStatus.Valid => (Localize("IsbnStatusValidTitle", format),
                Localize("IsbnStatusValidMessage", analysis.ExpectedCheck!), InfoBarSeverity.Success),
            CodeStatus.CheckDigitComputed => (_localizer["IsbnStatusComputedTitle"].Value,
                Localize("IsbnStatusComputedMessage", format, analysis.ExpectedCheck!), InfoBarSeverity.Success),
            CodeStatus.InvalidCheckDigit => (_localizer["IsbnStatusInvalidTitle"].Value,
                Localize("IsbnStatusInvalidMessage", analysis.EnteredCheck!, format, analysis.ExpectedCheck!), InfoBarSeverity.Error),
            CodeStatus.Solved when analysis.SolutionCount == 0 => (_localizer["IsbnStatusNoSolutionTitle"].Value,
                Localize("IsbnStatusNoSolutionMessage", format), InfoBarSeverity.Warning),
            CodeStatus.Solved => (Localize("IsbnStatusSolvedTitle", analysis.SolutionCount),
                Localize("IsbnStatusSolvedMessage", analysis.WildcardCount, format), InfoBarSeverity.Informational),
            CodeStatus.TooManyWildcards => (_localizer["IsbnStatusTooManyWildcardsTitle"].Value,
                Localize("IsbnStatusTooManyWildcardsMessage", CodeAnalyzer.MaxWildcards), InfoBarSeverity.Warning),
            CodeStatus.InvalidCharacter => (_localizer["IsbnStatusInvalidCharacterTitle"].Value,
                Localize("IsbnStatusInvalidCharacterMessage", analysis.Error!.Character, analysis.Error.Position), InfoBarSeverity.Error),
            CodeStatus.MisplacedX => (_localizer["IsbnStatusMisplacedXTitle"].Value,
                _localizer["IsbnStatusMisplacedXMessage"].Value, InfoBarSeverity.Error),
            CodeStatus.WrongLength => (_localizer["IsbnStatusWrongLengthTitle"].Value,
                Localize("IsbnStatusWrongLengthMessage", analysis.Entered.Length, format,
                    string.Join(_localizer["IsbnOr"].Value, analysis.ExpectedLengths)), InfoBarSeverity.Error),
            _ => (string.Empty, string.Empty, InfoBarSeverity.Informational),
        };
    }

    private string DescribeCorrection(CodeCorrection correction) => correction.Kind switch
    {
        CorrectionKind.Transposition => Localize("IsbnCorrectionSwap", correction.Position, correction.Position + 1),
        _ => Localize("IsbnCorrectionReplace", correction.Position, correction.From, correction.To),
    };

    private string FormatName(CodeFormat format) => _localizer[$"IsbnFormat_{format}"].Value;

    private string StandardName(CodeStandard standard) => _localizer[$"IsbnStandard_{standard}"].Value;

    private string Localize(string key, params object[] args)
        => string.Format(CultureInfo.CurrentCulture, _localizer[key].Value, args);

    /// <summary>The plain-text summary Copy and Share emit.</summary>
    private string BuildResultText()
    {
        StringBuilder builder = new();
        builder.AppendLine(StatusTitle);
        foreach (var form in Forms)
        {
            builder.Append(form.Label).Append(": ").AppendLine(form.Code);
        }

        if (HasConversionNote)
        {
            builder.AppendLine(ConversionNote);
        }

        if (HasWorking)
        {
            builder.AppendLine(WorkingTermsLine).AppendLine(WorkingRemainderLine).AppendLine(WorkingCheckLine);
        }

        foreach (var solution in Solutions)
        {
            builder.AppendLine(solution);
        }

        foreach (var correction in Corrections)
        {
            builder.Append(correction.Code).Append(" — ").AppendLine(correction.Description);
        }

        return builder.ToString().TrimEnd();
    }

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
    private void CopyCode(string? code)
    {
        if (!string.IsNullOrEmpty(code))
        {
            _clipboard.SetText(code);
        }
    }

    /// <summary>Steps through the current standard's presets: valid, missing check, puzzle unknowns, a typo.</summary>
    [RelayCommand]
    private void UseExample()
    {
        var examples = _examples[Array.IndexOf(_standards, Standard)];
        _exampleIndex = (_exampleIndex + 1) % examples.Length;
        InputText = examples[_exampleIndex];
    }

    [RelayCommand]
    private void Clear() => InputText = string.Empty;
}

public sealed record IsbnFormItem(string Label, string Code);

public sealed record IsbnCorrectionItem(string Code, string Description);
