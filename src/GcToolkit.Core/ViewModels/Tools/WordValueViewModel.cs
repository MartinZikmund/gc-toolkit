using System.Globalization;
using System.Text;
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
/// Word value / digital root (issue #30). Computes the letter-sum word value live as the user types and
/// reduces it to its digital root — the geocaching "turn words into numbers" method. Matches
/// geocachingtoolbox.com feature-for-feature: 15 value schemes (A=1…Z=26 and its variants, the phone
/// keypad, Scrabble NL/UK/DE, three cyclic tables, and German/Swedish accent-extended schemes), an
/// optional digit count, diacritic folding, a toggleable step-by-step calculation, a per-word
/// breakdown, and a conversion table. Goes beyond parity with a choice of number handling (digits inline
/// vs. multi-digit numbers summed separately), copy/share, and a favorite toggle. All arithmetic lives
/// in the pure <see cref="WordValueCalculator"/> (thin-VM convention).
/// </summary>
[Tool("WordValue", ToolCategory.Text,
      Introduced = "2026-06-06", Updated = "2026-06-07",
      Keywords = ["word", "value", "digital", "root", "sum", "letters", "cross sum", "checksum",
                  "scrabble", "vanity", "keypad", "method",
                  "hodnota", "slova", "ciferný", "kořen", "součet", "písmena"])]
public sealed partial class WordValueViewModel : ToolViewModelBase, IToolStateful
{
    private readonly WordValueCalculator _calculator = new();
    private readonly UiDebouncer _debouncer = new(TimeSpan.FromMilliseconds(200));
    private readonly IClipboardService _clipboard;
    private readonly IShareService _share;
    private readonly IStringLocalizer _localizer;

    private bool _suppressRecompute;
    private WordValueMethod? _conversionMethod;

    public WordValueViewModel(
        ICatalogService catalog,
        IRecentsService recents,
        IFavoriteToolsService favorites,
        IStringLocalizer localizer,
        IClipboardService clipboard,
        IShareService share)
        : base("WordValue", catalog, recents, favorites, localizer)
    {
        _clipboard = clipboard;
        _share = share;
        _localizer = localizer;
        Recompute();
    }

    /// <summary>Every value scheme, in dropdown order, for the method picker.</summary>
    public IReadOnlyList<WordValueMethod> Methods { get; } = WordValueSchemes.All;

    [ObservableProperty]
    public partial WordValueMethod SelectedMethod { get; set; } = WordValueMethod.A1Z26;

    [ObservableProperty]
    public partial string InputText { get; set; } = string.Empty;

    /// <summary>Whether digits in the input are counted at all.</summary>
    [ObservableProperty]
    public partial bool CountNumbers { get; set; } = true;

    /// <summary>When counting numbers: 0 adds each digit inline to the one total (reference behavior);
    /// 1 sums multi-digit numbers in a separate total with its own digital root.</summary>
    [ObservableProperty]
    public partial int NumberModeIndex { get; set; }

    /// <summary>Fold accents to plain letters (é → e, ä → a, …) before counting.</summary>
    [ObservableProperty]
    public partial bool RemoveDiacritics { get; set; } = true;

    /// <summary><see langword="false"/> for the accent-extended schemes, where folding would discard the
    /// very characters that carry a value — the option is disabled in the UI then.</summary>
    [ObservableProperty]
    public partial bool CanRemoveDiacritics { get; set; } = true;

    /// <summary>Show the step-by-step "1 + 8 + 15 … = total" calculation.</summary>
    [ObservableProperty]
    public partial bool ShowCalculation { get; set; } = true;

    /// <summary>Also compute and list each whitespace-separated word's value.</summary>
    [ObservableProperty]
    public partial bool CountSeparateWords { get; set; }

    [ObservableProperty]
    public partial bool HasOutput { get; set; }

    /// <summary>The "Word value (Digital root)" line: <c>terms = total</c> (or just the total when the
    /// calculation is hidden).</summary>
    [ObservableProperty]
    public partial string WordValueLine { get; set; } = string.Empty;

    /// <summary>The "Summed to one digit" reduction chain, e.g. <c>782 = 17 = 8</c>.</summary>
    [ObservableProperty]
    public partial string ReductionLine { get; set; } = string.Empty;

    /// <summary>Per-word rows shown when <see cref="CountSeparateWords"/> is on. Assigned wholesale so a
    /// long paste replaces the list in one notification instead of one per word.</summary>
    [ObservableProperty]
    public partial IReadOnlyList<WordValueWordItem> Words { get; set; } = [];

    [ObservableProperty]
    public partial bool ShowSeparateWords { get; set; }

    /// <summary>The separate multi-digit numbers section (only in <see cref="SumNumbersSeparately"/> mode).</summary>
    [ObservableProperty]
    public partial bool ShowSeparateNumbers { get; set; }

    [ObservableProperty]
    public partial string NumbersLine { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string NumbersReductionLine { get; set; } = string.Empty;

    /// <summary>The conversion table for the active scheme (one cell per scored character).</summary>
    [ObservableProperty]
    public partial IReadOnlyList<WordValueConversionItem> Conversion { get; set; } = [];

    partial void OnSelectedMethodChanged(WordValueMethod value) => _debouncer.RunNow(Recompute);

    // Typing rebuilds the per-character calculation and (optionally) a row per word, so a long paste
    // is expensive — coalesce keystrokes then, but keep short/plain input instant.
    partial void OnInputTextChanged(string value)
    {
        if (CountSeparateWords || (ShowCalculation && value.Length > 200))
        {
            _debouncer.Debounce(Recompute);
        }
        else
        {
            _debouncer.RunNow(Recompute);
        }
    }

    partial void OnCountNumbersChanged(bool value) => _debouncer.RunNow(Recompute);

    partial void OnNumberModeIndexChanged(int value)
    {
        // Ignore the transient -1 a RadioButtons control can emit while re-templating.
        if (value is 0 or 1)
        {
            _debouncer.RunNow(Recompute);
        }
    }

    partial void OnRemoveDiacriticsChanged(bool value) => _debouncer.RunNow(Recompute);

    partial void OnShowCalculationChanged(bool value) => _debouncer.RunNow(Recompute);

    partial void OnCountSeparateWordsChanged(bool value) => _debouncer.RunNow(Recompute);

    partial void OnHasOutputChanged(bool value)
    {
        CopyOutputCommand.NotifyCanExecuteChanged();
        ShareOutputCommand.NotifyCanExecuteChanged();
    }

    private NumberHandling NumberMode => !CountNumbers
        ? NumberHandling.Ignore
        : NumberModeIndex == 1 ? NumberHandling.NumbersSeparate : NumberHandling.DigitsInTotal;

    private void Recompute()
    {
        if (_suppressRecompute)
        {
            return;
        }

        var scheme = WordValueSchemes.For(SelectedMethod);
        CanRemoveDiacritics = scheme.AllowsDiacriticRemoval;

        if (_conversionMethod != SelectedMethod)
        {
            BuildConversionTable(scheme);
            _conversionMethod = SelectedMethod;
        }

        var analysis = _calculator.Analyze(InputText, scheme, NumberMode, RemoveDiacritics);

        HasOutput = !string.IsNullOrEmpty(InputText) && analysis.HasContent;

        WordValueLine = FormatValueLine(analysis.Terms, analysis.Total);
        ReductionLine = FormatReduction(analysis.ReductionSteps);

        // Only materialize the per-word rows when they are actually shown.
        Words = CountSeparateWords ? [.. analysis.Words.Select(BuildWordItem)] : [];
        ShowSeparateWords = CountSeparateWords && Words.Count > 0;

        ShowSeparateNumbers = NumberMode == NumberHandling.NumbersSeparate && analysis.HasNumbers;
        if (ShowSeparateNumbers)
        {
            NumbersLine = $"{string.Join(" + ", analysis.Numbers.Select(Format))} = {analysis.NumberTotal}";
            NumbersReductionLine = FormatReduction(analysis.NumberReductionSteps);
        }
        else
        {
            NumbersLine = string.Empty;
            NumbersReductionLine = string.Empty;
        }
    }

    private WordValueWordItem BuildWordItem(WordValueWord word)
    {
        var detail = ShowCalculation && word.Terms.Count > 0
            ? $"{FormatTerms(word.Terms)} = {FormatReduction(word.ReductionSteps)}"
            : FormatReduction(word.ReductionSteps);
        return new WordValueWordItem(word.Text, detail);
    }

    private void BuildConversionTable(WordValueScheme scheme)
        => Conversion = [.. scheme.Characters.Select(ch => new WordValueConversionItem(ch.ToString(), scheme.Values[ch]))];

    /// <summary>The headline value line: <c>terms = total</c> with the calculation, just <c>total</c> without.</summary>
    private string FormatValueLine(IReadOnlyList<int> terms, long total)
        => ShowCalculation && terms.Count > 0 ? $"{FormatTerms(terms)} = {Format(total)}" : Format(total);

    private static string FormatTerms(IReadOnlyList<int> terms)
        => string.Join(" + ", terms.Select(t => t.ToString(CultureInfo.CurrentCulture)));

    /// <summary>Renders a reduction chain as <c>"782 = 17 = 8"</c> (a single value shows just itself).</summary>
    private static string FormatReduction(IReadOnlyList<long> steps)
        => string.Join(" = ", steps.Select(Format));

    private static string Format(long value) => value.ToString(CultureInfo.CurrentCulture);

    /// <summary>The plain-text summary the Copy/Share actions emit.</summary>
    private string BuildResultText()
    {
        var builder = new StringBuilder();
        builder.Append(_localizer["WordValueResultLabel"].Value).Append(": ").AppendLine(WordValueLine);
        builder.Append(_localizer["WordValueSummedLabel"].Value).Append(": ").AppendLine(ReductionLine);

        if (ShowSeparateNumbers)
        {
            builder.Append(_localizer["WordValueNumbersLabel"].Value).Append(": ").Append(NumbersLine)
                .Append("  (").Append(NumbersReductionLine).AppendLine(")");
        }

        if (ShowSeparateWords)
        {
            builder.AppendLine().AppendLine(_localizer["WordValueSeparateWordsLabel"].Value);
            foreach (var word in Words)
            {
                builder.Append(word.Word).Append(": ").AppendLine(word.Detail);
            }
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

    /// <summary>Resets the text and every option back to its default (the reference "Reset fields").</summary>
    [RelayCommand]
    private void Reset()
    {
        _suppressRecompute = true;
        InputText = string.Empty;
        SelectedMethod = WordValueMethod.A1Z26;
        CountNumbers = true;
        NumberModeIndex = 0;
        RemoveDiacritics = true;
        ShowCalculation = true;
        CountSeparateWords = false;
        _suppressRecompute = false;
        _debouncer.RunNow(Recompute);
    }

    public ToolInputState CaptureState()
    {
        ToolInputState state = new();
        state.Set(nameof(SelectedMethod), SelectedMethod);
        state.Set(nameof(CountNumbers), CountNumbers);
        state.Set(nameof(NumberModeIndex), NumberModeIndex);
        state.Set(nameof(RemoveDiacritics), RemoveDiacritics);
        state.Set(nameof(ShowCalculation), ShowCalculation);
        state.Set(nameof(CountSeparateWords), CountSeparateWords);
        state.Set(nameof(InputText), InputText);
        return state;
    }

    public void RestoreState(ToolInputState state)
    {
        SelectedMethod = state.GetEnum(nameof(SelectedMethod), SelectedMethod);
        CountNumbers = state.GetBool(nameof(CountNumbers), CountNumbers);
        NumberModeIndex = Math.Clamp(state.GetInt(nameof(NumberModeIndex), NumberModeIndex), 0, 1);
        RemoveDiacritics = state.GetBool(nameof(RemoveDiacritics), RemoveDiacritics);
        ShowCalculation = state.GetBool(nameof(ShowCalculation), ShowCalculation);
        CountSeparateWords = state.GetBool(nameof(CountSeparateWords), CountSeparateWords);
        InputText = state.GetString(nameof(InputText), InputText);
    }
}
