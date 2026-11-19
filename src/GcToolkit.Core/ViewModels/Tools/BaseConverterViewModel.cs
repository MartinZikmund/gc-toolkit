using System.Globalization;
using System.Numerics;
using System.Threading.Tasks;
using GcToolkit.Core.Catalog;
using GcToolkit.Core.Discovery;
using GcToolkit.Core.FavoriteTools;
using GcToolkit.Core.Infrastructure;
using GcToolkit.Core.Numbers;
using GcToolkit.Core.Recents;
using GcToolkit.Core.Services;
using Microsoft.Extensions.Localization;
using Microsoft.UI.Dispatching;

namespace GcToolkit.Core.ViewModels.Tools;

/// <summary>
/// Number base converter (issue #14). Parses integers written in a chosen source base (2–62) and shows
/// them live in binary, octal, decimal and hexadecimal at once plus an arbitrary target base. Matches the
/// geocachingtoolbox.com base-conversion tool — named From/To base dropdowns across bases 2–62, a
/// "show all bases" grid, whitespace-separated batch input whose unknown tokens are skipped, the
/// case-sensitivity switch and a one-tap From/To swap — and goes beyond it with arbitrary-precision
/// <see cref="BigInteger"/> values (no overflow), inline flagging of skipped tokens, negatives, and
/// copy/share. All parsing/formatting lives in the pure <see cref="NumberBaseConverter"/>.
/// </summary>
[Tool("BaseConverter", ToolCategory.Numbers,
      Introduced = "2026-02-10", Updated = "2026-08-07",
      Keywords = ["binary", "hex", "hexadecimal", "octal", "decimal", "base", "radix", "number", "base62", "soustava", "převod", "číslo", "dvojková", "šestnáctková"])]
public sealed partial class BaseConverterViewModel : ToolViewModelBase, IToolTextInput, IToolTextOutput
{
    string IToolTextOutput.OutputText => TargetOutput;

    /// <summary>The bases the reference site names rather than numbering.</summary>
    private static readonly Dictionary<int, string> _baseNameKeys = new()
    {
        [2] = "BaseNameBinary",
        [3] = "BaseNameTernary",
        [4] = "BaseNameQuaternary",
        [5] = "BaseNameQuinary",
        [6] = "BaseNameSenary",
        [7] = "BaseNameSeptenary",
        [8] = "BaseNameOctal",
        [9] = "BaseNameNonary",
        [10] = "BaseNameDecimal",
        [11] = "BaseNameUndecimal",
        [12] = "BaseNameDuodecimal",
        [16] = "BaseNameHexadecimal",
        [20] = "BaseNameVigesimal",
        [30] = "BaseNameTrigesimal",
    };

    private readonly NumberBaseConverter _converter = new();
    private readonly IClipboardService _clipboard;
    private readonly IShareService _share;
    private readonly IStringLocalizer _localizer;
    private readonly UiDebouncer _debouncer = new(TimeSpan.FromMilliseconds(200));
    private readonly DispatcherQueue? _dispatcher = UiDispatcher.TryGetForCurrentThread();

    /// <summary>"Base 2".."Base 62" row labels, resolved up front so the off-thread list build stays pure.</summary>
    private readonly string[] _allBaseLabels;

    private int _generation;
    private bool _suppressRecompute;

    public BaseConverterViewModel(
        ICatalogService catalog,
        IRecentsService recents,
        IFavoriteToolsService favorites,
        IStringLocalizer localizer,
        IClipboardService clipboard,
        IShareService share)
        : base("BaseConverter", catalog, recents, favorites, localizer)
    {
        _clipboard = clipboard;
        _share = share;
        _localizer = localizer;
        Bases = BuildBaseOptions(localizer);
        _allBaseLabels = [.. Bases.Select(b => Format("BaseConverterBaseN", b.Radix))];
    }

    /// <summary>Every selectable base (2–62), named where a traditional name exists.</summary>
    public IReadOnlyList<BaseOption> Bases { get; }

    /// <summary>The value(s) to convert, written in <see cref="FromBase"/>; whitespace separates a batch.</summary>
    [ObservableProperty]
    public partial string InputText { get; set; } = string.Empty;

    /// <summary>The radix the input is written in (2–62).</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(FromBaseIndex))]
    [NotifyPropertyChangedFor(nameof(IsCaseToggleEnabled))]
    [NotifyPropertyChangedFor(nameof(DigitReference))]
    public partial int FromBase { get; set; } = 10;

    /// <summary>The "convert to" radix (2–62) shown alongside the four common bases.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ToBaseIndex))]
    public partial int ToBase { get; set; } = 16;

    /// <summary>When set, upper and lower case are distinct digits on input (forced on when the alphabet uses both).</summary>
    [ObservableProperty]
    public partial bool CaseSensitive { get; set; }

    /// <summary>The site's "manual" mode: the user supplies the source/target alphabets instead of picking a radix.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(UseStandardBases))]
    [NotifyPropertyChangedFor(nameof(IsCaseToggleEnabled))]
    [NotifyPropertyChangedFor(nameof(DigitReference))]
    public partial bool UseCustomAlphabet { get; set; }

    /// <summary>The ordered glyph set the input is written in while <see cref="UseCustomAlphabet"/> is on.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsCaseToggleEnabled))]
    [NotifyPropertyChangedFor(nameof(DigitReference))]
    [NotifyPropertyChangedFor(nameof(SourceAlphabetHint))]
    public partial string SourceAlphabet { get; set; } = string.Empty;

    /// <summary>The ordered glyph set the result is written in while <see cref="UseCustomAlphabet"/> is on.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(TargetAlphabetHint))]
    public partial string TargetAlphabet { get; set; } = string.Empty;

    /// <summary>Renders the value in every base 2–62 at once (the site's "show all bases").</summary>
    [ObservableProperty]
    public partial bool ShowAllBases { get; set; }

    /// <summary>The value rendered in <see cref="ToBase"/>.</summary>
    [ObservableProperty]
    public partial string TargetOutput { get; set; } = string.Empty;

    [ObservableProperty]
    public partial bool HasOutput { get; set; }

    /// <summary><see langword="true"/> when a single input has content that is not a valid number in <see cref="FromBase"/>.</summary>
    [ObservableProperty]
    public partial bool HasError { get; set; }

    [ObservableProperty]
    public partial string ErrorMessage { get; set; } = string.Empty;

    /// <summary><see langword="true"/> when the input holds more than one token, so the batch table is shown.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsSingleValue))]
    public partial bool IsBatch { get; set; }

    /// <summary>The four conventional bases (binary, octal, decimal, hex), each individually copyable.</summary>
    [ObservableProperty]
    public partial IReadOnlyList<BaseResultItem> CommonResults { get; set; } = [];

    /// <summary>Every base 2–62 for the current value, populated only while <see cref="ShowAllBases"/> is on.</summary>
    [ObservableProperty]
    public partial IReadOnlyList<BaseResultItem> AllBaseResults { get; set; } = [];

    /// <summary>One row per whitespace-separated token, invalid ones flagged instead of dropped.</summary>
    [ObservableProperty]
    public partial IReadOnlyList<BaseBatchItem> BatchResults { get; set; } = [];

    /// <summary>The single-value sections are hidden while a batch is being converted.</summary>
    public bool IsSingleValue => !IsBatch;

    /// <summary>The dropdown index for <see cref="FromBase"/> (the list is contiguous from <see cref="MinBase"/>).</summary>
    public int FromBaseIndex
    {
        get => FromBase - NumberBaseConverter.MinBase;
        set
        {
            if (value >= 0)
            {
                FromBase = value + NumberBaseConverter.MinBase;
            }
        }
    }

    /// <summary>The dropdown index for <see cref="ToBase"/>.</summary>
    public int ToBaseIndex
    {
        get => ToBase - NumberBaseConverter.MinBase;
        set
        {
            if (value >= 0)
            {
                ToBase = value + NumberBaseConverter.MinBase;
            }
        }
    }

    /// <summary>The base dropdowns are only in play while the custom-alphabet mode is off.</summary>
    public bool UseStandardBases => !UseCustomAlphabet;

    /// <summary>The glyphs the input is read with — the custom alphabet, or the standard digits of <see cref="FromBase"/>.</summary>
    public string DigitReference => UseCustomAlphabet ? SourceAlphabet : NumberBaseConverter.DigitsFor(FromBase);

    /// <summary>The glyphs the result is written with.</summary>
    private string TargetAlphabetInUse => UseCustomAlphabet ? TargetAlphabet : NumberBaseConverter.DigitsFor(ToBase);

    /// <summary>"Base {n}" derived from the custom source alphabet's length, or empty when it is unusable.</summary>
    public string SourceAlphabetHint => DescribeAlphabet(SourceAlphabet);

    /// <summary>"Base {n}" derived from the custom target alphabet's length, or empty when it is unusable.</summary>
    public string TargetAlphabetHint => DescribeAlphabet(TargetAlphabet);

    /// <summary>Case folding is impossible when the alphabet already uses both cases of a letter.</summary>
    public bool IsCaseToggleEnabled => NumberBaseConverter.CanFoldCase(DigitReference);

    public int MinBase => NumberBaseConverter.MinBase;

    public int MaxBase => NumberBaseConverter.MaxBase;

    private bool EffectiveCaseSensitive => CaseSensitive || !NumberBaseConverter.CanFoldCase(DigitReference);

    // Typing while "show all bases" (61 renderings) or a batch is on rebuilds a whole list per keystroke,
    // which freezes the UI — debounce that path. A single value in one base stays instant.
    partial void OnInputTextChanged(string value)
    {
        if (ShowAllBases || NumberBaseConverter.Tokenize(value).Length > 1)
        {
            _debouncer.Debounce(Recompute);
        }
        else
        {
            _debouncer.RunNow(Recompute);
        }
    }

    partial void OnFromBaseChanged(int value) => _debouncer.RunNow(Recompute);

    partial void OnToBaseChanged(int value) => _debouncer.RunNow(Recompute);

    partial void OnCaseSensitiveChanged(bool value) => _debouncer.RunNow(Recompute);

    partial void OnShowAllBasesChanged(bool value) => _debouncer.RunNow(Recompute);

    partial void OnSourceAlphabetChanged(string value) => _debouncer.Debounce(Recompute);

    partial void OnTargetAlphabetChanged(string value) => _debouncer.Debounce(Recompute);

    // Entering manual mode seeds the alphabets from the current bases, so the user edits rather than types
    // 62 glyphs from scratch; leaving it falls back to the standard digits.
    partial void OnUseCustomAlphabetChanged(bool value)
    {
        if (value)
        {
            _suppressRecompute = true;
            if (!NumberBaseConverter.IsValidAlphabet(SourceAlphabet))
            {
                SourceAlphabet = NumberBaseConverter.DigitsFor(FromBase);
            }

            if (!NumberBaseConverter.IsValidAlphabet(TargetAlphabet))
            {
                TargetAlphabet = NumberBaseConverter.DigitsFor(ToBase);
            }

            _suppressRecompute = false;
        }

        _debouncer.RunNow(Recompute);
    }

    partial void OnHasOutputChanged(bool value)
    {
        CopyOutputCommand.NotifyCanExecuteChanged();
        ShareOutputCommand.NotifyCanExecuteChanged();
    }

    private void Recompute()
    {
        if (_suppressRecompute)
        {
            return;
        }

        var tokens = NumberBaseConverter.Tokenize(InputText);
        if (tokens.Length == 0)
        {
            _generation++; // discard any in-flight list build
            ResetOutputs();
            HasError = false;
            ErrorMessage = string.Empty;
            return;
        }

        // A half-typed custom alphabet isn't a conversion failure — say what's wrong with the alphabet.
        if (UseCustomAlphabet
            && (!NumberBaseConverter.IsValidAlphabet(SourceAlphabet) || !NumberBaseConverter.IsValidAlphabet(TargetAlphabet)))
        {
            _generation++;
            ResetOutputs();
            HasError = true;
            ErrorMessage = _localizer["BaseConverterAlphabetInvalid"].Value;
            return;
        }

        if (tokens.Length > 1)
        {
            RecomputeBatch();
            return;
        }

        IsBatch = false;
        BatchResults = [];

        if (!_converter.TryParse(tokens[0], DigitReference, EffectiveCaseSensitive, out var value))
        {
            _generation++;
            ResetOutputs();
            HasError = true;
            ErrorMessage = UseCustomAlphabet
                ? Format("BaseConverterInvalidNotice", DigitReference.Length)
                : Format("BaseConverterInvalidNotice", FromBase);
            return;
        }

        HasError = false;
        ErrorMessage = string.Empty;

        var common = _converter.ToCommonBases(value);
        CommonResults =
        [
            new BaseResultItem(_localizer["BaseConverterBinary"].Value, common.Binary, _clipboard.SetText),
            new BaseResultItem(_localizer["BaseConverterOctal"].Value, common.Octal, _clipboard.SetText),
            new BaseResultItem(_localizer["BaseConverterDecimal"].Value, common.Decimal, _clipboard.SetText),
            new BaseResultItem(_localizer["BaseConverterHex"].Value, common.Hexadecimal, _clipboard.SetText),
        ];

        TargetOutput = _converter.Format(value, TargetAlphabetInUse);
        HasOutput = true;

        if (ShowAllBases)
        {
            RunOffThread(() => BuildAllBases(value), rows => AllBaseResults = rows);
        }
        else
        {
            _generation++;
            AllBaseResults = [];
        }
    }

    private void RecomputeBatch()
    {
        IsBatch = true;
        HasError = false;
        ErrorMessage = string.Empty;
        CommonResults = [];
        AllBaseResults = [];
        TargetOutput = string.Empty;

        var (input, from, to, caseSensitive) = (InputText, DigitReference, TargetAlphabetInUse, EffectiveCaseSensitive);
        var skipped = Format("BaseConverterSkipped", UseCustomAlphabet ? from.Length : FromBase);
        RunOffThread(
            () => BuildBatch(input, from, to, caseSensitive, skipped),
            rows =>
            {
                BatchResults = rows;
                HasOutput = rows.Any(r => r.IsValid);
            });
    }

    private IReadOnlyList<BaseResultItem> BuildAllBases(BigInteger value)
        => [.. _converter.ToAllBases(value)
            .Select(r => new BaseResultItem(_allBaseLabels[r.Radix - NumberBaseConverter.MinBase], r.Text, _clipboard.SetText))];

    private IReadOnlyList<BaseBatchItem> BuildBatch(string input, string fromAlphabet, string toAlphabet, bool caseSensitive, string skippedLabel)
        => [.. _converter.ConvertBatch(input, fromAlphabet, toAlphabet, caseSensitive)
            .Select(c => new BaseBatchItem(c.Input, c.Output, c.IsValid, skippedLabel, _clipboard.SetText))];

    /// <summary>"Base {n}" for a usable alphabet, otherwise nothing (the invalid notice covers the rest).</summary>
    private string DescribeAlphabet(string alphabet)
        => NumberBaseConverter.IsValidAlphabet(alphabet) ? Format("BaseConverterBaseN", alphabet.Length) : string.Empty;

    /// <summary>
    /// Runs a list build off the UI thread and publishes it back through the captured dispatcher, dropping
    /// results a newer keystroke has already superseded. Runs synchronously with no dispatcher (unit tests).
    /// </summary>
    private void RunOffThread<T>(Func<T> work, Action<T> publish)
    {
        var generation = ++_generation;

        if (_dispatcher is null)
        {
            publish(work());
            return;
        }

        _ = Task.Run(() =>
        {
            var result = work();
            _dispatcher.TryEnqueue(() =>
            {
                if (generation == _generation)
                {
                    publish(result);
                }
            });
        });
    }

    private void ResetOutputs()
    {
        TargetOutput = string.Empty;
        CommonResults = [];
        AllBaseResults = [];
        BatchResults = [];
        IsBatch = false;
        HasOutput = false;
    }

    private string Format(string key, params object[] args)
        => string.Format(CultureInfo.CurrentCulture, _localizer[key].Value, args);

    private static IReadOnlyList<BaseOption> BuildBaseOptions(IStringLocalizer localizer)
    {
        var options = new List<BaseOption>(NumberBaseConverter.MaxBase - NumberBaseConverter.MinBase + 1);
        for (var radix = NumberBaseConverter.MinBase; radix <= NumberBaseConverter.MaxBase; radix++)
        {
            var display = _baseNameKeys.TryGetValue(radix, out var key)
                ? string.Format(CultureInfo.CurrentCulture, "{0} ({1})", localizer[key].Value, radix)
                : string.Format(CultureInfo.CurrentCulture, localizer["BaseConverterBaseN"].Value, radix);
            options.Add(new BaseOption(radix, display));
        }

        return options;
    }

    /// <summary>The Copy/Share payload for whichever mode is showing.</summary>
    private string BuildResultText()
    {
        if (IsBatch)
        {
            return string.Join(Environment.NewLine, BatchResults.Select(r => r.ToString()));
        }

        var lines = CommonResults.Select(r => $"{r.Label}: {r.Value}").ToList();
        lines.Add(Format("BaseConverterTargetLine", UseCustomAlphabet ? TargetAlphabetInUse.Length : ToBase, TargetOutput));
        if (ShowAllBases)
        {
            lines.AddRange(AllBaseResults.Select(r => $"{r.Label}: {r.Value}"));
        }

        return string.Join(Environment.NewLine, lines);
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

    /// <summary>
    /// Swaps From/To and carries the result back into the input, so one tap round-trips the conversion
    /// (the reference site converts "backwards" purely by swapping the two dropdowns).
    /// </summary>
    [RelayCommand]
    private void SwapBases()
    {
        _suppressRecompute = true;
        (FromBase, ToBase) = (ToBase, FromBase);
        (SourceAlphabet, TargetAlphabet) = (TargetAlphabet, SourceAlphabet);
        if (!IsBatch && HasOutput && TargetOutput.Length > 0)
        {
            InputText = TargetOutput;
        }

        _suppressRecompute = false;
        _debouncer.RunNow(Recompute);
    }

    [RelayCommand]
    private void Clear() => InputText = string.Empty;
}
