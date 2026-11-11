using System.Collections.ObjectModel;
using System.Globalization;
using System.Threading.Tasks;
using GcToolkit.Core.Catalog;
using GcToolkit.Core.Discovery;
using GcToolkit.Core.FavoriteTools;
using GcToolkit.Core.Numbers;
using GcToolkit.Core.Recents;
using GcToolkit.Core.Services;
using Microsoft.Extensions.Localization;

namespace GcToolkit.Core.ViewModels.Tools;

/// <summary>
/// Bidirectional Roman numeral converter (issue #48). Converts live as the user types in either
/// direction, supports multiple space-separated values (geocachingtoolbox.com parity), copies/shares
/// the result, and shows a per-result breakdown. Goes beyond parity with the <see cref="RomanNumeralCodec"/>:
/// the vinculum extends the range to 3,999,999 and decoding is lenient (non-standard forms convert but
/// are flagged).
/// </summary>
[Tool("RomanNumerals", ToolCategory.Numbers,
      Introduced = "2026-06-01", Updated = "2026-06-01",
      Keywords = ["roman", "numeral", "číslice", "římské", "latin", "vinculum", "I", "V", "X"])]
public sealed partial class RomanNumeralsViewModel : ToolViewModelBase, IToolStateful
{
    private const string InvalidToken = "?";

    private readonly RomanNumeralCodec _codec = new();
    private readonly IClipboardService _clipboard;
    private readonly IShareService _share;
    private readonly IStringLocalizer _localizer;

    private bool _suppressConvert;

    public RomanNumeralsViewModel(
        ICatalogService catalog,
        IRecentsService recents,
        IFavoriteToolsService favorites,
        IStringLocalizer localizer,
        IClipboardService clipboard,
        IShareService share)
        : base("RomanNumerals", catalog, recents, favorites, localizer)
    {
        _clipboard = clipboard;
        _share = share;
        _localizer = localizer;
    }

    /// <summary>0 = number → Roman, 1 = Roman → number.</summary>
    [ObservableProperty]
    public partial int DirectionIndex { get; set; }

    [ObservableProperty]
    public partial string InputText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string OutputText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial bool HasOutput { get; set; }

    /// <summary><see langword="true"/> when the result carries a warning (non-standard or partly invalid input).</summary>
    [ObservableProperty]
    public partial bool HasWarning { get; set; }

    [ObservableProperty]
    public partial string WarningMessage { get; set; } = string.Empty;

    /// <summary><see langword="true"/> when a single value is shown with its additive <see cref="BreakdownParts"/>.</summary>
    [ObservableProperty]
    public partial bool ShowBreakdown { get; set; }

    /// <summary>The additive decomposition of the result (e.g. <c>"M = 1,000"</c>), shown only for
    /// single-value input.</summary>
    public ObservableCollection<string> BreakdownLines { get; } = [];

    private bool IsNumberToRoman => DirectionIndex != 1;

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
        BreakdownLines.Clear();
        ShowBreakdown = false;

        if (string.IsNullOrWhiteSpace(InputText))
        {
            OutputText = string.Empty;
            HasOutput = false;
            HasWarning = false;
            WarningMessage = string.Empty;
            return;
        }

        // Each whitespace-separated token converts independently; results re-join with spaces.
        var tokens = InputText.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
        var results = new List<string>(tokens.Length);
        var anyInvalid = false;
        var anyNonStandard = false;
        long? singleValue = null;

        foreach (var token in tokens)
        {
            if (TryConvertToken(token, out var result, out var value, out var isCanonical))
            {
                results.Add(result);
                singleValue = value;
                anyNonStandard |= !isCanonical;
            }
            else
            {
                results.Add(InvalidToken);
                anyInvalid = true;
            }
        }

        OutputText = string.Join(" ", results);
        HasOutput = results.Exists(r => r != InvalidToken);

        // Invalid input is the more important message; otherwise flag a non-standard numeral.
        if (anyInvalid)
        {
            HasWarning = true;
            WarningMessage = _localizer["RomanInvalidNotice"].Value;
        }
        else if (anyNonStandard)
        {
            HasWarning = true;
            WarningMessage = _localizer["RomanNonStandardNotice"].Value;
        }
        else
        {
            HasWarning = false;
            WarningMessage = string.Empty;
        }

        // The breakdown only makes sense for a single, valid value.
        if (tokens.Length == 1 && !anyInvalid && singleValue is long only)
        {
            foreach (var part in _codec.Explain(only))
            {
                BreakdownLines.Add($"{part.Symbol} = {part.Value.ToString("N0", CultureInfo.CurrentCulture)}");
            }

            ShowBreakdown = BreakdownLines.Count > 0;
        }
    }

    private bool TryConvertToken(string token, out string result, out long value, out bool isCanonical)
    {
        if (IsNumberToRoman)
        {
            isCanonical = true;
            if (TryParseNumber(token, out value))
            {
                result = _codec.Encode(value);
                return true;
            }
        }
        else if (_codec.TryDecode(token, out value, out isCanonical))
        {
            result = value.ToString(CultureInfo.InvariantCulture);
            return true;
        }

        result = string.Empty;
        value = 0;
        isCanonical = true;
        return false;
    }

    private static bool TryParseNumber(string token, out long number)
    {
        // Plain integer only; out-of-range is rejected here so the codec never throws.
        if (long.TryParse(token, NumberStyles.None, CultureInfo.InvariantCulture, out number)
            && number >= RomanNumeralCodec.MinValue
            && number <= RomanNumeralCodec.MaxValue)
        {
            return true;
        }

        number = 0;
        return false;
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

    public ToolInputState CaptureState()
    {
        ToolInputState state = new();
        state.Set(nameof(DirectionIndex), DirectionIndex);
        state.Set(nameof(InputText), InputText);
        return state;
    }

    // Direction first: switching it carries the current output into the input.
    public void RestoreState(ToolInputState state)
    {
        DirectionIndex = Math.Clamp(state.GetInt(nameof(DirectionIndex), DirectionIndex), 0, 1);
        InputText = state.GetString(nameof(InputText), InputText);
    }
}
