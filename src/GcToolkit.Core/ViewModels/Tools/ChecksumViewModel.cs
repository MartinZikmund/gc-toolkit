using System.Globalization;
using System.Text;
using System.Threading.Tasks;
using GcToolkit.Core.Catalog;
using GcToolkit.Core.Discovery;
using GcToolkit.Core.FavoriteTools;
using GcToolkit.Core.Infrastructure;
using GcToolkit.Core.Numbers;
using GcToolkit.Core.Recents;
using GcToolkit.Core.Services;
using Microsoft.Extensions.Localization;

namespace GcToolkit.Core.ViewModels.Tools;

/// <summary>
/// The geocaching checksum: the cross sum of the digits, the A=1…Z=26 value of the letters, and the
/// iterated digital root of each. Shows the working — "1 + 2 + 3 + 4 + 5 = 15", then "15 = 6" — because
/// a cacher checks the intermediate sum, not just the answer. Accents fold to plain letters by default
/// (Ž → Z) and everything else is skipped; an optional part-by-part list scores each group of a
/// coordinate line on its own. A second section checks whole coordinates the way a cache page lists
/// them (digit sum of the degrees-decimal-minutes form), one per line, optionally verified with
/// <c>= 62</c>. All arithmetic lives in the pure <see cref="ChecksumCalculator"/> and
/// <see cref="CoordinateChecksumCalculator"/>.
/// </summary>
[Tool("Checksum", ToolCategory.Numbers,
      Introduced = "2026-02-10", Updated = "2026-09-03",
      Keywords = ["checksum", "cross sum", "digit sum", "digital root", "letter value", "reduce",
                  "kontrolní součet", "ciferný součet", "ciferný kořen", "součet číslic",
                  "hodnota písmen", "coordinate checksum", "kontrolní součet souřadnic"])]
public sealed partial class ChecksumViewModel : ToolViewModelBase
{
    /// <summary>A coordinate line: digits, a letter and punctuation, so every section has something to show.</summary>
    private const string ExampleInput = "N 49 12.345 E 016 30.678";

    private const string CoordinateExample = "N 49° 12.345' E 016° 30.678' = 59\nN 50 05.000 E 014 25.000";

    private readonly UiDebouncer _debouncer = new(TimeSpan.FromMilliseconds(150));
    private readonly IClipboardService _clipboard;
    private readonly IShareService _share;
    private readonly IStringLocalizer _localizer;

    public ChecksumViewModel(
        ICatalogService catalog,
        IRecentsService recents,
        IFavoriteToolsService favorites,
        IStringLocalizer localizer,
        IClipboardService clipboard,
        IShareService share)
        : base("Checksum", catalog, recents, favorites, localizer)
    {
        _clipboard = clipboard;
        _share = share;
        _localizer = localizer;
        Recompute();
    }

    [ObservableProperty]
    public partial string InputText { get; set; } = string.Empty;

    /// <summary>Fold accented letters onto their base letter (Ž → Z) instead of skipping them.</summary>
    [ObservableProperty]
    public partial bool FoldDiacritics { get; set; } = true;

    /// <summary>Show the per-character sums, not only the totals.</summary>
    [ObservableProperty]
    public partial bool ShowWorking { get; set; } = true;

    /// <summary>Also score each whitespace/punctuation-delimited group on its own.</summary>
    [ObservableProperty]
    public partial bool ShowParts { get; set; }

    [ObservableProperty]
    public partial bool HasOutput { get; set; }

    public static IReadOnlyList<int> BaseOptions { get; } = [.. Enumerable.Range(2, 35)];

    /// <summary>The base the number is written in (2-36).</summary>
    [ObservableProperty]
    public partial int InputBase { get; set; } = 10;

    /// <summary>The base whose digits are summed (2-36).</summary>
    [ObservableProperty]
    public partial int DigitBase { get; set; } = 10;

    /// <summary>The n for "cross sum mod n". NaN (an empty NumberBox) leaves it off.</summary>
    [ObservableProperty]
    public partial double Modulus { get; set; } = double.NaN;

    [ObservableProperty]
    public partial IReadOnlyList<ChecksumVariantLine> VariantRows { get; set; } = [];

    [ObservableProperty]
    public partial bool HasVariants { get; set; }

    [ObservableProperty]
    public partial string VariantError { get; set; } = string.Empty;

    [ObservableProperty]
    public partial bool HasVariantError { get; set; }

    /// <summary>One coordinate per line; a trailing <c>= 62</c> verifies the line.</summary>
    [ObservableProperty]
    public partial string CoordinateText { get; set; } = string.Empty;

    /// <summary>Normalise to degrees decimal minutes first (the standard); off sums the digits as typed.</summary>
    [ObservableProperty]
    public partial bool UseDdmMode { get; set; } = true;

    [ObservableProperty]
    public partial IReadOnlyList<ChecksumCoordinateLine> CoordinateRows { get; set; } = [];

    [ObservableProperty]
    public partial bool HasCoordinateRows { get; set; }

    /// <summary>Digits and letters added together — the headline number.</summary>
    [ObservableProperty]
    public partial string TotalText { get; set; } = string.Empty;

    /// <summary>The single digit the total reduces to.</summary>
    [ObservableProperty]
    public partial string RootText { get; set; } = string.Empty;

    /// <summary>The total's reduction chain, e.g. <c>42 = 6</c>. Hidden when the total is already one digit.</summary>
    [ObservableProperty]
    public partial string TotalReductionLine { get; set; } = string.Empty;

    [ObservableProperty]
    public partial bool ShowTotalReduction { get; set; }

    [ObservableProperty]
    public partial bool ShowDigits { get; set; }

    [ObservableProperty]
    public partial string DigitWorkingLine { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string DigitReductionLine { get; set; } = string.Empty;

    [ObservableProperty]
    public partial bool ShowLetters { get; set; }

    [ObservableProperty]
    public partial string LetterWorkingLine { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string LetterReductionLine { get; set; } = string.Empty;

    /// <summary>Assigned wholesale so a long paste replaces the list in one notification.</summary>
    [ObservableProperty]
    public partial IReadOnlyList<ChecksumPart> Parts { get; set; } = [];

    [ObservableProperty]
    public partial bool ShowPartList { get; set; }

    /// <summary>"Skipped N characters" — reassures the user that the spaces and symbols were meant to be dropped.</summary>
    [ObservableProperty]
    public partial string SkippedNote { get; set; } = string.Empty;

    [ObservableProperty]
    public partial bool HasSkippedNote { get; set; }

    /// <summary>Only raised for input long enough to hit the working/part caps.</summary>
    [ObservableProperty]
    public partial string TruncationNote { get; set; } = string.Empty;

    [ObservableProperty]
    public partial bool HasTruncationNote { get; set; }

    partial void OnFoldDiacriticsChanged(bool value) => _debouncer.RunNow(Recompute);

    partial void OnShowWorkingChanged(bool value) => _debouncer.RunNow(Recompute);

    partial void OnShowPartsChanged(bool value) => _debouncer.RunNow(Recompute);

    partial void OnInputBaseChanged(int value) => _debouncer.RunNow(Recompute);

    partial void OnDigitBaseChanged(int value) => _debouncer.RunNow(Recompute);

    partial void OnModulusChanged(double value) => _debouncer.RunNow(Recompute);

    partial void OnUseDdmModeChanged(bool value) => RecomputeCoordinates();

    partial void OnCoordinateTextChanged(string value) => RecomputeCoordinates();

    // The working rebuilds a term per character, so a pasted cache description is expensive —
    // coalesce keystrokes then, but keep ordinary short input instant.
    partial void OnInputTextChanged(string value)
    {
        if (value.Length > 200)
        {
            _debouncer.Debounce(Recompute);
        }
        else
        {
            _debouncer.RunNow(Recompute);
        }
    }

    partial void OnHasOutputChanged(bool value) => NotifyOutputChanged();

    partial void OnHasCoordinateRowsChanged(bool value) => NotifyOutputChanged();

    partial void OnHasVariantsChanged(bool value) => NotifyOutputChanged();

    private bool CanCopy => HasOutput || HasCoordinateRows || HasVariants;

    private void NotifyOutputChanged()
    {
        CopyOutputCommand.NotifyCanExecuteChanged();
        ShareOutputCommand.NotifyCanExecuteChanged();
    }

    private void RecomputeCoordinates()
    {
        var mode = UseDdmMode ? CoordinateChecksumMode.DegreesDecimalMinutes : CoordinateChecksumMode.AsEntered;
        var rows = CoordinateChecksumCalculator.Calculate(CoordinateText, mode);

        CoordinateRows = rows.Select(ToLine).ToList();
        HasCoordinateRows = rows.Count > 0;
    }

    private ChecksumCoordinateLine ToLine(CoordinateChecksumRow row)
    {
        var detail = row.Latitude is { } lat && row.Longitude is { } lon
            ? $"{Localize("ChecksumLatLabel")} {Format(lat)} · {Localize("ChecksumLonLabel")} {Format(lon)}"
            : string.Empty;

        var status = row.Status switch
        {
            CoordinateChecksumStatus.Match => Localize("ChecksumMatch", Format(row.Expected ?? 0)),
            CoordinateChecksumStatus.Mismatch => Localize("ChecksumMismatch", Format(row.Expected ?? 0), Format(row.Total)),
            CoordinateChecksumStatus.Invalid => Localize(UseDdmMode ? "ChecksumErrorCoordinate" : "ChecksumErrorNoDigits"),
            _ => string.Empty,
        };

        return new ChecksumCoordinateLine(
            row.Input,
            row.IsValid && UseDdmMode ? row.Normalized : string.Empty,
            detail,
            row.IsValid ? Format(row.Total) : string.Empty,
            row.IsValid ? Format(row.Root) : string.Empty,
            status,
            row.Status);
    }

    private void Recompute()
    {
        var analysis = ChecksumCalculator.Analyze(InputText, FoldDiacritics);

        HasOutput = analysis.HasContent;

        TotalText = Format(analysis.Total);
        RootText = Format(analysis.TotalRoot);
        TotalReductionLine = FormatChain(analysis.TotalReduction);
        ShowTotalReduction = analysis.HasContent && analysis.TotalReduction.Count > 1;

        ShowDigits = analysis.Digits.HasContent;
        DigitWorkingLine = ShowWorking ? FormatWorking(analysis.Digits, withCharacter: false) : string.Empty;
        DigitReductionLine = FormatChain(analysis.Digits.Reduction);

        ShowLetters = analysis.Letters.HasContent;
        LetterWorkingLine = ShowWorking ? FormatWorking(analysis.Letters, withCharacter: true) : string.Empty;
        LetterReductionLine = FormatChain(analysis.Letters.Reduction);

        // One part carries exactly the headline numbers, so listing it would just repeat them.
        ShowPartList = ShowParts && analysis.Parts.Count > 1;
        Parts = ShowPartList ? analysis.Parts : [];

        HasSkippedNote = analysis.HasContent && analysis.IgnoredCount > 0;
        SkippedNote = HasSkippedNote ? Localize("ChecksumSkippedNote", analysis.IgnoredCount) : string.Empty;

        TruncationNote = BuildTruncationNote(analysis);
        HasTruncationNote = TruncationNote.Length > 0;

        RecomputeVariants();
    }

    private bool HasCustomVariantOptions => InputBase != 10 || DigitBase != 10 || !double.IsNaN(Modulus);

    private void RecomputeVariants()
    {
        var result = CrossSumCalculator.Calculate(InputText, InputBase, DigitBase, ToModulus(Modulus));

        // Free text ("N 49 12.345") is not a number; stay quiet unless the user asked for something specific.
        var error = result.Error switch
        {
            CrossSumError.None or CrossSumError.NoDigits => string.Empty,
            CrossSumError.InvalidDigit when !HasCustomVariantOptions => string.Empty,
            CrossSumError.InvalidDigit => Localize("ChecksumErrorDigit", result.InvalidCharacter, InputBase),
            CrossSumError.InvalidBase => Localize("ChecksumErrorBase"),
            CrossSumError.InvalidModulus => Localize("ChecksumErrorModulus"),
            _ => Localize("ChecksumErrorTooLong", CrossSumCalculator.MaxConversionDigits),
        };

        VariantError = error;
        HasVariantError = error.Length > 0;
        VariantRows = result.IsValid ? BuildVariantRows(result) : [];
        HasVariants = result.IsValid;
    }

    /// <summary>NaN (empty box) means off; anything that is not a whole number maps to 0 so the calculator rejects it.</summary>
    private static long? ToModulus(double value)
        => double.IsNaN(value)
            ? null
            : value is >= 1 and <= 1e15 && value == Math.Floor(value) ? (long)value : 0;

    private List<ChecksumVariantLine> BuildVariantRows(CrossSumResult result)
    {
        List<ChecksumVariantLine> rows = [];

        if (InputBase != 10 || DigitBase != 10)
        {
            rows.Add(new ChecksumVariantLine(
                Localize("ChecksumBaseSumLabel", result.DigitBase),
                Format(result.CrossSum),
                ShowWorking ? FormatDigitWorking(result, alternating: false) : string.Empty));
        }

        rows.Add(new ChecksumVariantLine(
            Localize("ChecksumAlternatingLabel"),
            Format(result.AlternatingSum),
            ShowWorking ? FormatDigitWorking(result, alternating: true) : string.Empty));

        if (result is { Modulus: { } n, CrossSumModulo: { } remainder })
        {
            rows.Add(new ChecksumVariantLine(
                Localize("ChecksumModuloLabel", Format(n)),
                Format(remainder),
                ShowWorking ? $"{Format(result.CrossSum)} mod {Format(n)} = {Format(remainder)}" : string.Empty));
        }

        return rows;
    }

    /// <summary>"1 - 2 + 3 = 2"; signs run from the rightmost digit (+). Digits above 9 read "F(15)".</summary>
    private static string FormatDigitWorking(CrossSumResult result, bool alternating)
    {
        var digits = result.Digits;
        var shown = Math.Min(digits.Count, ChecksumCalculator.MaxDetailTerms);
        StringBuilder builder = new();

        for (var i = 0; i < shown; i++)
        {
            var negative = alternating && (digits.Count - 1 - i) % 2 == 1;
            var digit = digits[i];
            var term = result.DigitBase > 10 ? $"{DigitSymbol(digit)}({digit})" : digit.ToString(CultureInfo.InvariantCulture);

            if (i == 0)
            {
                builder.Append(negative ? "-" : string.Empty).Append(term);
            }
            else
            {
                builder.Append(negative ? " - " : " + ").Append(term);
            }
        }

        if (shown < digits.Count)
        {
            builder.Append(" + …");
        }

        return builder.Append(" = ").Append(Format(alternating ? result.AlternatingSum : result.CrossSum)).ToString();
    }

    private static char DigitSymbol(int digit) => (char)(digit < 10 ? '0' + digit : 'A' + digit - 10);

    private string BuildTruncationNote(ChecksumAnalysis analysis)
    {
        if (ShowWorking && (analysis.Digits.TermsTruncated || analysis.Letters.TermsTruncated))
        {
            return Localize("ChecksumWorkingTruncatedNote", ChecksumCalculator.MaxDetailTerms);
        }

        return ShowPartList && analysis.PartsTruncated
            ? Localize("ChecksumPartsTruncatedNote", ChecksumCalculator.MaxParts)
            : string.Empty;
    }

    /// <summary>"1 + 2 + 3 = 6" for digits, "G(7) + C(3) = 10" for letters.</summary>
    private static string FormatWorking(ChecksumSum sum, bool withCharacter)
    {
        if (sum.Terms.Count == 0)
        {
            return string.Empty;
        }

        var terms = sum.Terms.Select(term => withCharacter
            ? $"{term.Character}({Format(term.Value)})"
            : Format(term.Value));

        var body = string.Join(" + ", terms);
        if (sum.TermsTruncated)
        {
            body += " + …";
        }

        return $"{body} = {Format(sum.Value)}";
    }

    /// <summary>Renders a reduction as <c>"782 = 17 = 8"</c>; a single step shows just itself.</summary>
    private static string FormatChain(IReadOnlyList<long> steps)
        => string.Join(" = ", steps.Select(Format));

    private static string Format(long value) => value.ToString(CultureInfo.CurrentCulture);

    private string Localize(string key, params object[] args)
        => string.Format(CultureInfo.CurrentCulture, _localizer[key].Value, args);

    /// <summary>The plain-text summary Copy and Share emit.</summary>
    private string BuildResultText()
    {
        StringBuilder builder = new();
        AppendNumberResult(builder);
        AppendVariantResult(builder);
        AppendCoordinateResult(builder);
        return builder.ToString().TrimEnd();
    }

    private void AppendCoordinateResult(StringBuilder builder)
    {
        if (!HasCoordinateRows)
        {
            return;
        }

        if (builder.Length > 0)
        {
            builder.AppendLine();
        }

        foreach (var row in CoordinateRows)
        {
            builder.Append(row.Input).Append(": ");
            builder.AppendLine(row.Status == CoordinateChecksumStatus.Invalid
                ? row.StatusText
                : $"{row.TotalText} = {row.RootText}" + (row.HasStatus ? $" ({row.StatusText})" : string.Empty));
        }
    }

    private void AppendVariantResult(StringBuilder builder)
    {
        if (!HasVariants)
        {
            return;
        }

        if (builder.Length > 0)
        {
            builder.AppendLine();
        }

        foreach (var row in VariantRows)
        {
            builder.Append(row.Label).Append(": ").AppendLine(row.Value);
        }
    }

    private void AppendNumberResult(StringBuilder builder)
    {
        if (!HasOutput)
        {
            return;
        }

        builder.Append(_localizer["ChecksumTotalLabel"].Value).Append(": ").AppendLine(TotalReductionLine);
        builder.Append(_localizer["ChecksumRootLabel"].Value).Append(": ").AppendLine(RootText);

        if (ShowDigits)
        {
            builder.Append(_localizer["ChecksumDigitsHeader"].Value).Append(": ").AppendLine(DigitReductionLine);
        }

        if (ShowLetters)
        {
            builder.Append(_localizer["ChecksumLettersHeader"].Value).Append(": ").AppendLine(LetterReductionLine);
        }

        if (ShowPartList)
        {
            builder.AppendLine().AppendLine(_localizer["ChecksumPartsHeader"].Value);
            foreach (var part in Parts)
            {
                builder.Append(part.Text).Append(": ").Append(Format(part.Total))
                    .Append(" = ").AppendLine(Format(part.Root));
            }
        }
    }

    [RelayCommand(CanExecute = nameof(CanCopy))]
    private void CopyOutput() => _clipboard.SetText(BuildResultText());

    [RelayCommand(CanExecute = nameof(CanCopy))]
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

    /// <summary>Drops a coordinate line in so the tool explains itself on first open.</summary>
    [RelayCommand]
    private void UseExample() => InputText = ExampleInput;

    [RelayCommand]
    private void Clear() => InputText = string.Empty;

    [RelayCommand]
    private void UseCoordinateExample() => CoordinateText = CoordinateExample;

    [RelayCommand]
    private void ClearCoordinates() => CoordinateText = string.Empty;
}

/// <summary>One row of the cross-sum variants card: what it is, its value, and the sum written out.</summary>
public sealed record ChecksumVariantLine(string Label, string Value, string Working)
{
    public bool HasWorking => Working.Length > 0;
}

/// <summary>One coordinate line, ready to display. <see cref="Normalized"/> is the DDM form, empty in as-entered mode.</summary>
public sealed record ChecksumCoordinateLine(
    string Input,
    string Normalized,
    string Detail,
    string TotalText,
    string RootText,
    string StatusText,
    CoordinateChecksumStatus Status)
{
    public bool IsValid => Status != CoordinateChecksumStatus.Invalid;

    public bool HasNormalized => Normalized.Length > 0;

    public bool HasDetail => Detail.Length > 0;

    public bool HasStatus => StatusText.Length > 0;

    public bool IsMatch => Status == CoordinateChecksumStatus.Match;

    public bool IsProblem => Status is CoordinateChecksumStatus.Mismatch or CoordinateChecksumStatus.Invalid;
}
