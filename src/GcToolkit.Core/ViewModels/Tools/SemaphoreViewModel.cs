using System.Threading.Tasks;
using GcToolkit.Core.Alphabets;
using GcToolkit.Core.Catalog;
using GcToolkit.Core.Discovery;
using GcToolkit.Core.FavoriteTools;
using GcToolkit.Core.Infrastructure;
using GcToolkit.Core.Recents;
using GcToolkit.Core.Services;
using Microsoft.Extensions.Localization;

namespace GcToolkit.Core.ViewModels.Tools;

/// <summary>
/// Flag-semaphore converter (issue #60). Typing renders the figure sequence live with the reference
/// mode semantics (Numbers sign before digits, Letters sign to return, Space keeps number mode);
/// tapping chart figures builds text the other way, tracking the mode statefully. Exceeds the
/// geocachingtoolbox.com page with digit captions, arm-position tooltips, accent folding and
/// copy/share — see <see cref="SemaphoreCodec"/>.
/// </summary>
[Tool("Semaphore", ToolCategory.Alphabets,
      Introduced = "2026-06-10", Updated = "2026-06-10",
      Keywords = ["semaphore", "flags", "flag", "semafor", "vlajková abeceda", "vlajky", "abeceda", "paže"])]
public sealed partial class SemaphoreViewModel : ToolViewModelBase, IToolTextInput
{
    // A long paste rebuilds one figure item per character, so coalesce those into a single rebuild;
    // short input (the common case) still converts on every keystroke.
    private const int DebounceThreshold = 120;

    private readonly SemaphoreCodec _codec = new();
    private readonly UiDebouncer _debouncer = new(TimeSpan.FromMilliseconds(200));
    private readonly IClipboardService _clipboard;
    private readonly IShareService _share;
    private readonly IStringLocalizer _localizer;
    private readonly Dictionary<SemaphoreArmPosition, string> _armNames;

    // Mode override set by tapping the Numbers/Letters chart signs; consumed by the next letter tap
    // and discarded when the user edits the text directly.
    private SemaphoreMode? _pendingTapMode;
    private bool _suppressPendingReset;

    public SemaphoreViewModel(
        ICatalogService catalog,
        IRecentsService recents,
        IFavoriteToolsService favorites,
        IStringLocalizer localizer,
        IClipboardService clipboard,
        IShareService share)
        : base("Semaphore", catalog, recents, favorites, localizer)
    {
        _clipboard = clipboard;
        _share = share;
        _localizer = localizer;
        _armNames = Enum.GetValues<SemaphoreArmPosition>().ToDictionary(p => p, LocalizeArmName);
        Palette = [.. SemaphoreAlphabet.ChartFigures.Select(CreatePaletteItem)];
        SpecialSignals = [.. SemaphoreAlphabet.SpecialFigures.Select(CreateSpecialItem)];
    }

    /// <summary>The tappable chart: A–Z (with digit captions) plus the Space/Letters/Numbers signs.</summary>
    public IReadOnlyList<SemaphoreFigureItem> Palette { get; }

    /// <summary>Reference-only special signals (Cancel, Error) shown below the chart — not tappable.</summary>
    public IReadOnlyList<SemaphoreFigureItem> SpecialSignals { get; }

    [ObservableProperty]
    public partial string InputText { get; set; } = string.Empty;

    /// <summary>The rendered figure sequence for <see cref="InputText"/>.</summary>
    [ObservableProperty]
    public partial IReadOnlyList<SemaphoreFigureItem> Figures { get; set; } = [];

    [ObservableProperty]
    public partial bool HasFigures { get; set; }

    [ObservableProperty]
    public partial bool HasText { get; set; }

    /// <summary><see langword="true"/> when the input contained characters with no semaphore figure.</summary>
    [ObservableProperty]
    public partial bool HasUnknown { get; set; }

    partial void OnInputTextChanged(string value)
    {
        if (!_suppressPendingReset)
        {
            _pendingTapMode = null;
        }

        if (value.Length > DebounceThreshold)
        {
            _debouncer.Debounce(Convert);
        }
        else
        {
            _debouncer.RunNow(Convert);
        }
    }

    private void Convert()
    {
        var result = _codec.Encode(InputText);
        Figures = [.. result.Tokens.Select(CreateSequenceItem)];
        HasFigures = Figures.Count > 0;
        HasText = InputText.Length > 0;
        HasUnknown = result.HasUnknown;
        CopyTextCommand.NotifyCanExecuteChanged();
        ShareTextCommand.NotifyCanExecuteChanged();
    }

    [RelayCommand]
    private void Clear() => InputText = string.Empty;

    [RelayCommand(CanExecute = nameof(HasText))]
    private void CopyText() => _clipboard.SetText(InputText);

    [RelayCommand(CanExecute = nameof(HasText))]
    private async Task ShareTextAsync()
    {
        try
        {
            await _share.ShareTextAsync(ToolName, InputText);
        }
        catch (Exception)
        {
            // Sharing is best-effort; a platform share failure must not crash the tool.
        }
    }

    private void TapFigure(SemaphoreFigure figure)
    {
        switch (figure.Kind)
        {
            case SemaphoreFigureKind.NumbersSign:
                _pendingTapMode = SemaphoreMode.Numbers;
                break;
            case SemaphoreFigureKind.LettersSign:
                _pendingTapMode = SemaphoreMode.Letters;
                break;
            case SemaphoreFigureKind.Space:
                AppendText(" "); // the Space sign does not change the mode
                break;
            default:
                var mode = _pendingTapMode ?? _codec.GetFinalMode(InputText);
                _pendingTapMode = null;
                var character = mode == SemaphoreMode.Numbers && figure.Digit is char digit
                    ? digit
                    : char.ToLowerInvariant(figure.Letter);
                AppendText(character.ToString());
                break;
        }
    }

    private void AppendText(string text)
    {
        _suppressPendingReset = true;
        try
        {
            InputText += text;
        }
        finally
        {
            _suppressPendingReset = false;
        }
    }

    private SemaphoreFigureItem CreateSequenceItem(SemaphoreToken token)
    {
        var caption = token.Figure.Kind == SemaphoreFigureKind.Letter ? token.Display : SignName(token.Figure.Kind);
        return new SemaphoreFigureItem(token.Figure, caption, caption, DescribeArms(token.Figure));
    }

    private SemaphoreFigureItem CreatePaletteItem(SemaphoreFigure figure)
    {
        var caption = figure.Kind switch
        {
            SemaphoreFigureKind.Letter when figure.Digit is char digit => $"{figure.Letter} / {digit}",
            SemaphoreFigureKind.Letter => figure.Letter.ToString(),
            _ => SignName(figure.Kind),
        };

        return new SemaphoreFigureItem(
            figure,
            caption,
            caption,
            DescribeArms(figure),
            new RelayCommand(() => TapFigure(figure)));
    }

    private SemaphoreFigureItem CreateSpecialItem(SemaphoreFigure figure)
    {
        var (captionKey, tooltipKey) = figure.Kind == SemaphoreFigureKind.Cancel
            ? ("SemaphoreCancelSign", "SemaphoreCancelTooltip")
            : ("SemaphoreErrorSign", "SemaphoreErrorTooltip");
        var caption = _localizer[captionKey].Value;

        // No tap command — these are a non-clickable reference, not part of the encoder.
        return new SemaphoreFigureItem(figure, caption, caption, _localizer[tooltipKey].Value);
    }

    private string SignName(SemaphoreFigureKind kind) => kind switch
    {
        SemaphoreFigureKind.Space => _localizer["SemaphoreSpaceSign"].Value,
        SemaphoreFigureKind.NumbersSign => _localizer["SemaphoreNumbersSign"].Value,
        _ => _localizer["SemaphoreLettersSign"].Value,
    };

    private string DescribeArms(SemaphoreFigure figure)
        => _localizer["SemaphoreArmsTooltip", _armNames[figure.LeftArm], _armNames[figure.RightArm]].Value;

    private string LocalizeArmName(SemaphoreArmPosition position) => position switch
    {
        SemaphoreArmPosition.Down => _localizer["SemaphoreArmDown"].Value,
        SemaphoreArmPosition.Low => _localizer["SemaphoreArmLow"].Value,
        SemaphoreArmPosition.Out => _localizer["SemaphoreArmOut"].Value,
        SemaphoreArmPosition.High => _localizer["SemaphoreArmHigh"].Value,
        SemaphoreArmPosition.Up => _localizer["SemaphoreArmUp"].Value,
        SemaphoreArmPosition.AcrossLow => _localizer["SemaphoreArmAcrossLow"].Value,
        _ => _localizer["SemaphoreArmAcrossHigh"].Value,
    };
}
