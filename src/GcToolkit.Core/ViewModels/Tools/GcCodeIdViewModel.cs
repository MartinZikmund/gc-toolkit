using System.Collections.ObjectModel;
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
/// Converts a public Geocaching GC-code (e.g. <c>GC16XYD</c>) to and from its internal numeric ID
/// (issue #49). Converts live as the user types, supports auto-detecting the direction from the input
/// shape (with an explicit override matching geocachingtoolbox.com), and handles comma/space/newline
/// separated batches — correctly, unlike the currently broken live site. Goes beyond parity with
/// per-row results, a copy-all (CSV) export, share, and a "how it works" explainer. All conversion
/// logic lives in the pure <see cref="GcCodeIdConverter"/> (thin-VM convention).
/// </summary>
[Tool("GcCodeId", ToolCategory.Alphabets,
      Introduced = "2026-06-13", Updated = "2026-06-13",
      Keywords = ["gc", "code", "id", "waypoint", "geocache", "kód", "převod", "číslo", "identifikátor", "base31"])]
public sealed partial class GcCodeIdViewModel : ToolViewModelBase, IToolTextInput, IToolTextOutput
{
    private const string InvalidToken = "?";

    private readonly GcCodeIdConverter _converter = new();
    private readonly IClipboardService _clipboard;
    private readonly IShareService _share;

    private int _previousDirectionIndex;
    private bool _suppressRecompute;

    public GcCodeIdViewModel(
        ICatalogService catalog,
        IRecentsService recents,
        IFavoriteToolsService favorites,
        IStringLocalizer localizer,
        IClipboardService clipboard,
        IShareService share)
        : base("GcCodeId", catalog, recents, favorites, localizer)
    {
        _clipboard = clipboard;
        _share = share;
    }

    /// <summary>0 = auto-detect, 1 = GC-code → ID, 2 = ID → GC-code.</summary>
    [ObservableProperty]
    public partial int DirectionIndex { get; set; }

    [ObservableProperty]
    public partial string InputText { get; set; } = string.Empty;

    /// <summary>The combined, line-joined result text (also what Copy/Share emit).</summary>
    [ObservableProperty]
    public partial string OutputText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial bool HasOutput { get; set; }

    /// <summary><see langword="true"/> when more than one token was converted — surfaces the per-row list.</summary>
    [ObservableProperty]
    public partial bool IsBatch { get; set; }

    /// <summary><see langword="true"/> when at least one token could not be converted.</summary>
    [ObservableProperty]
    public partial bool HasInvalid { get; set; }

    /// <summary>Per-row results, shown for batch input so each token's outcome is visible.</summary>
    public ObservableCollection<GcCodeIdResultItem> Results { get; } = [];

    private Direction Mode => DirectionIndex switch
    {
        1 => Direction.CodeToId,
        2 => Direction.IdToCode,
        _ => Direction.Auto,
    };

    partial void OnInputTextChanged(string value)
    {
        if (_suppressRecompute)
        {
            return;
        }

        Convert();
    }

    partial void OnDirectionIndexChanged(int value)
    {
        // Ignore the transient -1 a RadioButtons control can emit while re-templating.
        if (value is not (0 or 1 or 2))
        {
            return;
        }

        // Auto-swap: flipping between the two explicit directions carries the previous result into
        // the input for a one-tap round-trip. Skipped for the Auto mode, which detects per token.
        if (_previousDirectionIndex is 1 or 2 && value is 1 or 2 && value != _previousDirectionIndex
            && !string.IsNullOrEmpty(OutputText))
        {
            _suppressRecompute = true;
            InputText = OutputText;
            _suppressRecompute = false;
        }

        _previousDirectionIndex = value;
        Convert();
    }

    partial void OnHasOutputChanged(bool value)
    {
        CopyOutputCommand.NotifyCanExecuteChanged();
        ShareOutputCommand.NotifyCanExecuteChanged();
        CopyAllCommand.NotifyCanExecuteChanged();
    }

    partial void OnIsBatchChanged(bool value) => CopyAllCommand.NotifyCanExecuteChanged();

    private void Convert()
    {
        Results.Clear();

        if (string.IsNullOrWhiteSpace(InputText))
        {
            OutputText = string.Empty;
            HasOutput = false;
            IsBatch = false;
            HasInvalid = false;
            return;
        }

        // Tokens are separated by comma, whitespace or newlines; empty entries are dropped.
        var tokens = InputText.Split([',', ' ', '\t', '\r', '\n'], StringSplitOptions.RemoveEmptyEntries);
        var outputs = new List<string>(tokens.Length);
        var anyValid = false;
        var anyInvalid = false;

        foreach (var token in tokens)
        {
            var converted = ConvertToken(token);
            var isValid = converted is not null;
            var output = converted ?? InvalidToken;

            outputs.Add(output);
            Results.Add(new GcCodeIdResultItem(token, output, isValid, _clipboard.SetText));
            anyValid |= isValid;
            anyInvalid |= !isValid;
        }

        OutputText = string.Join(Environment.NewLine, outputs);
        HasOutput = anyValid;
        HasInvalid = anyInvalid;
        IsBatch = tokens.Length > 1;
    }

    /// <summary>Converts one token according to the active (or auto-detected) direction.</summary>
    private string? ConvertToken(string token)
    {
        var mode = Mode == Direction.Auto ? DetectDirection(token) : Mode;

        if (mode == Direction.IdToCode)
        {
            return GcCodeIdConverter.TryParseId(token, out var id) ? _converter.IdToGcCode(id) : null;
        }

        var converted = _converter.GcCodeToId(token);
        return converted?.ToString(CultureInfo.InvariantCulture);
    }

    /// <summary>A token that is all digits is an ID; anything else (a GC-code) goes the other way.</summary>
    private static Direction DetectDirection(string token)
    {
        var trimmed = token.Trim();
        return trimmed.Length > 0 && trimmed.All(char.IsDigit)
            ? Direction.IdToCode
            : Direction.CodeToId;
    }

    [RelayCommand(CanExecute = nameof(HasOutput))]
    private void CopyOutput() => _clipboard.SetText(OutputText);

    /// <summary>Copies every row as <c>input,output</c> CSV lines — the beyond-parity batch export.</summary>
    [RelayCommand(CanExecute = nameof(CanCopyAll))]
    private void CopyAll()
        => _clipboard.SetText(string.Join(Environment.NewLine, Results.Select(r => $"{r.Input},{r.Output}")));

    private bool CanCopyAll => HasOutput && IsBatch;

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

    private enum Direction
    {
        Auto,
        CodeToId,
        IdToCode,
    }
}
