using System.Threading.Tasks;
using GcToolkit.Core.Alphabets;
using GcToolkit.Core.Catalog;
using GcToolkit.Core.Discovery;
using GcToolkit.Core.FavoriteTools;
using GcToolkit.Core.Infrastructure;
using GcToolkit.Core.Recents;
using GcToolkit.Core.Services;
using Microsoft.Extensions.Localization;
using Microsoft.UI.Dispatching;

namespace GcToolkit.Core.ViewModels.Tools;

/// <summary>One selectable spelling-alphabet variant with its localized display name.</summary>
public sealed record SpellingAlphabetVariantOption(SpellingAlphabetVariant Variant, string DisplayName);

/// <summary>
/// Bidirectional spelling-alphabet converter (issue #281, cachesleuth.com parity). Encodes text into
/// spoken code words and decodes them back across eleven variants (NATO/ICAO default, ITU 1932, Western
/// Union, Able Baker, RAF 1924, APCO/Police, Dutch, German, Swedish, and the two Russian alphabets),
/// with a live code-word chart that doubles as a tap keyboard plus copy/share. Goes beyond parity by
/// auto-detecting the direction and offering a multi-variant brute force when the variant is unknown.
/// All conversion logic lives in the pure <see cref="SpellingAlphabetCodec"/> (thin-VM convention).
/// </summary>
[Tool("SpellingAlphabet", ToolCategory.Alphabets,
      Introduced = "2026-06-13", Updated = "2026-06-13",
      Keywords =
      [
          "spelling", "alphabet", "phonetic", "nato", "icao", "alfa", "bravo", "charlie", "radio",
          "callsign", "hláskovací", "abeceda", "fonetická", "telefonní", "alfa bravo",
      ])]
public sealed partial class SpellingAlphabetViewModel : ToolViewModelBase, IToolTextInput
{
    private readonly SpellingAlphabetCodec _codec = new();
    private readonly IStringLocalizer _localizer;
    private readonly IClipboardService _clipboard;
    private readonly IShareService _share;
    private readonly UiDebouncer _debouncer = new(TimeSpan.FromMilliseconds(200));
    private readonly DispatcherQueue? _dispatcher = UiDispatcher.TryGetForCurrentThread();

    private int _guessGeneration;
    private bool _suppressRecompute;

    public SpellingAlphabetViewModel(
        ICatalogService catalog,
        IRecentsService recents,
        IFavoriteToolsService favorites,
        IStringLocalizer localizer,
        IClipboardService clipboard,
        IShareService share)
        : base("SpellingAlphabet", catalog, recents, favorites, localizer)
    {
        _localizer = localizer;
        _clipboard = clipboard;
        _share = share;

        Variants =
        [
            .. SpellingAlphabets.All.Select(v =>
                new SpellingAlphabetVariantOption(v, _localizer[$"SpellingAlphabetVariant_{v.Id}"].Value)),
        ];
        SelectedVariant = Variants[0];
    }

    public IReadOnlyList<SpellingAlphabetVariantOption> Variants { get; }

    [ObservableProperty]
    public partial SpellingAlphabetVariantOption SelectedVariant { get; set; } = null!;

    /// <summary>0 = Encode (text → code words), 1 = Decode (code words → text).</summary>
    [ObservableProperty]
    public partial int DirectionIndex { get; set; }

    /// <summary>When on, the direction is inferred from the input each time it changes (beyond parity).</summary>
    [ObservableProperty]
    public partial bool AutoDetectDirection { get; set; } = true;

    [ObservableProperty]
    public partial string InputText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string OutputText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial bool HasOutput { get; set; }

    /// <summary><see langword="true"/> when decoding hit code words with no match.</summary>
    [ObservableProperty]
    public partial bool HasUnknown { get; set; }

    [ObservableProperty]
    public partial string UnknownNotice { get; set; } = string.Empty;

    /// <summary>The code-word chart for the selected variant (also a tappable keyboard for encoding).</summary>
    [ObservableProperty]
    public partial IReadOnlyList<SpellingAlphabetChartItem> Chart { get; set; } = [];

    /// <summary>Per-variant best-guess decodings of the current input (beyond-parity brute force).</summary>
    [ObservableProperty]
    public partial IReadOnlyList<SpellingAlphabetGuess> VariantGuesses { get; set; } = [];

    [ObservableProperty]
    public partial bool ShowAllVariants { get; set; }

    private bool IsDecode => DirectionIndex == 1;

    private bool HasInput => !string.IsNullOrEmpty(InputText);

    partial void OnSelectedVariantChanged(SpellingAlphabetVariantOption value)
    {
        RebuildChart();
        Convert();
    }

    partial void OnDirectionIndexChanged(int value)
    {
        // Auto-detect drives this programmatically (suppressed); a manual flip is a one-tap round trip.
        if (_suppressRecompute || value is not (0 or 1))
        {
            return;
        }

        _suppressRecompute = true;
        InputText = OutputText;
        _suppressRecompute = false;
        Convert();
    }

    partial void OnAutoDetectDirectionChanged(bool value)
    {
        if (_suppressRecompute)
        {
            return;
        }

        Convert();
    }

    partial void OnInputTextChanged(string value)
    {
        if (_suppressRecompute)
        {
            return;
        }

        Convert(typing: true);
        // Copy/Share re-evaluate via OnHasOutputChanged; only Clear keys off the input directly.
        ClearCommand.NotifyCanExecuteChanged();
    }

    partial void OnShowAllVariantsChanged(bool value) => Convert();

    private void RebuildChart()
        => Chart = [.. SelectedVariant.Variant.Chart.Select(e => new SpellingAlphabetChartItem(e.Letter, e.Word, AppendLetter))];

    private void AppendLetter(char letter)
    {
        // Tapping the chart is an encoding aid; switch out of decode so the appended letter converts.
        if (IsDecode && !AutoDetectDirection)
        {
            _suppressRecompute = true;
            DirectionIndex = 0;
            _suppressRecompute = false;
        }

        InputText += letter;
    }

    private void Convert(bool typing = false)
    {
        var variant = SelectedVariant.Variant;

        if (string.IsNullOrEmpty(InputText))
        {
            OutputText = string.Empty;
            HasOutput = false;
            HasUnknown = false;
            UnknownNotice = string.Empty;
            _debouncer.RunNow(() => SetGuesses([]));
            return;
        }

        // Auto-detect flips the direction when the input already reads as code words.
        if (AutoDetectDirection)
        {
            _suppressRecompute = true;
            DirectionIndex = _codec.LooksLikeCodeWords(InputText, variant) ? 1 : 0;
            _suppressRecompute = false;
        }

        if (IsDecode)
        {
            var result = _codec.Decode(InputText, variant);
            OutputText = result.Text;
            HasUnknown = result.HasUnknown;
            UnknownNotice = result.HasUnknown
                ? string.Format(
                    System.Globalization.CultureInfo.CurrentCulture,
                    _localizer["SpellingAlphabetUnknownNotice"].Value,
                    string.Join(", ", result.UnknownWords))
                : string.Empty;
        }
        else
        {
            OutputText = _codec.Encode(InputText, variant);
            HasUnknown = false;
            UnknownNotice = string.Empty;
        }

        HasOutput = !string.IsNullOrEmpty(OutputText);

        // The main conversion above is cheap and stays instant; only the all-variant brute force is
        // debounced (while typing) and pushed off the UI thread so it can't freeze fast typing.
        ScheduleGuesses(typing);
    }

    private void ScheduleGuesses(bool typing)
    {
        if (!ShowAllVariants)
        {
            _debouncer.RunNow(() => SetGuesses([]));
            return;
        }

        if (typing)
        {
            _debouncer.Debounce(StartVariantGuesses);
        }
        else
        {
            _debouncer.RunNow(StartVariantGuesses);
        }
    }

    /// <summary>
    /// Builds the per-variant guesses on a background thread, then publishes them on the UI thread. A
    /// generation guard drops results a newer input/toggle has superseded. Runs synchronously when there
    /// is no dispatcher (unit tests).
    /// </summary>
    private void StartVariantGuesses()
    {
        var input = InputText;
        var decode = IsDecode;
        var generation = ++_guessGeneration;

        if (string.IsNullOrEmpty(input))
        {
            VariantGuesses = [];
            return;
        }

        if (_dispatcher is null)
        {
            VariantGuesses = BuildVariantGuesses(input, decode);
            return;
        }

        _ = Task.Run(() =>
        {
            var guesses = BuildVariantGuesses(input, decode);
            _dispatcher.TryEnqueue(() =>
            {
                if (generation != _guessGeneration)
                {
                    return; // a newer input already superseded this result
                }

                VariantGuesses = guesses;
            });
        });
    }

    private void SetGuesses(IReadOnlyList<SpellingAlphabetGuess> guesses)
    {
        _guessGeneration++; // discard any in-flight brute force
        VariantGuesses = guesses;
    }

    private IReadOnlyList<SpellingAlphabetGuess> BuildVariantGuesses(string input, bool decode)
        =>
        [
            .. Variants
                .Select(o => (o.DisplayName, Text: decode
                    ? _codec.Decode(input, o.Variant).Text
                    : _codec.Encode(input, o.Variant)))
                .Where(x => !string.IsNullOrEmpty(x.Text))
                .Select(x => new SpellingAlphabetGuess(x.DisplayName, x.Text, _clipboard.SetText)),
        ];

    [RelayCommand(CanExecute = nameof(HasInput))]
    private void Clear() => InputText = string.Empty;

    [RelayCommand]
    private void Swap()
    {
        // Feed the current output back as input and flip direction (turn off auto so it sticks).
        _suppressRecompute = true;
        AutoDetectDirection = false;
        InputText = OutputText;
        DirectionIndex = IsDecode ? 0 : 1;
        _suppressRecompute = false;
        Convert();
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

    partial void OnHasOutputChanged(bool value)
    {
        CopyOutputCommand.NotifyCanExecuteChanged();
        ShareOutputCommand.NotifyCanExecuteChanged();
    }
}
