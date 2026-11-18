using System.Collections.ObjectModel;
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

/// <summary>
/// Dvorak keyboard tool (issue #45). Treats text as if typed on one physical keyboard layout and
/// re-emits each character at the same key position on another — any-to-any across QWERTY and the
/// three Dvorak variants, matching geocachingtoolbox.com's "Dvorak keyboard" tool. Goes beyond parity
/// with a swap-direction button, a "try all directions" mode that lists every layout pairing at once
/// (useful when the source layout is unknown), copy/share, and a clear identity hint when the input
/// and output layouts are the same. All remap logic lives in the pure <see cref="DvorakCodec"/>.
/// </summary>
[Tool("DvorakKeyboard", ToolCategory.Alphabets,
      Introduced = "2026-06-13", Updated = "2026-06-13",
      Keywords = ["dvorak", "qwerty", "keyboard", "layout", "remap", "klávesnice", "rozložení", "rozlozeni", "klavesnice", "one-handed"])]
public sealed partial class DvorakKeyboardViewModel : ToolViewModelBase, IToolTextInput
{
    private const int RecomputeDebounceMs = 200;

    private readonly DvorakCodec _codec = new();
    private readonly IClipboardService _clipboard;
    private readonly IShareService _share;
    private readonly DispatcherQueueTimer? _debounceTimer;

    public DvorakKeyboardViewModel(
        ICatalogService catalog,
        IRecentsService recents,
        IFavoriteToolsService favorites,
        IStringLocalizer localizer,
        IClipboardService clipboard,
        IShareService share)
        : base("DvorakKeyboard", catalog, recents, favorites, localizer)
    {
        _clipboard = clipboard;
        _share = share;

        // A non-repeating UI-thread timer debounces the expensive "all directions" rebuild while
        // typing. Null in unit tests (no DispatcherQueue), where the recompute then runs synchronously.
        var dispatcher = UiDispatcher.TryGetForCurrentThread();
        if (dispatcher is not null)
        {
            _debounceTimer = dispatcher.CreateTimer();
            _debounceTimer.Interval = TimeSpan.FromMilliseconds(RecomputeDebounceMs);
            _debounceTimer.IsRepeating = false;
            _debounceTimer.Tick += (_, _) => Recompute();
        }

        LayoutNames =
        [
            localizer["DvorakLayoutQwerty"].Value,
            localizer["DvorakLayoutTwoHands"].Value,
            localizer["DvorakLayoutRightHand"].Value,
            localizer["DvorakLayoutLeftHand"].Value,
        ];

        Recompute();
    }

    /// <summary>The four layout display names, indexed the same as <see cref="DvorakCodec.Layouts"/>.</summary>
    public IReadOnlyList<string> LayoutNames { get; }

    /// <summary>Selected source layout (defaults to QWERTY).</summary>
    [ObservableProperty]
    public partial int InputLayoutIndex { get; set; }

    /// <summary>Selected target layout (defaults to Simplified Dvorak).</summary>
    [ObservableProperty]
    public partial int OutputLayoutIndex { get; set; } = 1;

    [ObservableProperty]
    public partial string InputText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string OutputText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial bool HasOutput { get; set; }

    /// <summary>True when input and output layouts match, so the output equals the input verbatim.</summary>
    [ObservableProperty]
    public partial bool IsIdentity { get; set; }

    /// <summary>When on, shows every layout pairing's result instead of the single selected direction.</summary>
    [ObservableProperty]
    public partial bool ShowAllDirections { get; set; }

    /// <summary>Candidate rows for "try all directions" mode (one per ordered layout pairing).</summary>
    public ObservableCollection<DvorakCandidateItem> Candidates { get; } = [];

    private DvorakLayout InputLayout => DvorakCodec.Layouts[Clamp(InputLayoutIndex)];

    private DvorakLayout OutputLayout => DvorakCodec.Layouts[Clamp(OutputLayoutIndex)];

    private static int Clamp(int index) => index < 0 || index >= DvorakCodec.Layouts.Count ? 0 : index;

    partial void OnInputLayoutIndexChanged(int value) => RequestRecompute(immediate: true);

    partial void OnOutputLayoutIndexChanged(int value) => RequestRecompute(immediate: true);

    // Typing in "try all directions" mode rebuilds 12 pairings synchronously, which freezes the UI
    // while typing fast — debounce that path so it only recomputes once typing pauses. The cheap
    // single-direction path stays instant.
    partial void OnInputTextChanged(string value) => RequestRecompute(immediate: !ShowAllDirections);

    partial void OnShowAllDirectionsChanged(bool value) => RequestRecompute(immediate: true);

    partial void OnHasOutputChanged(bool value)
    {
        CopyOutputCommand.NotifyCanExecuteChanged();
        ShareOutputCommand.NotifyCanExecuteChanged();
    }

    /// <summary>
    /// Requests a recompute. <paramref name="immediate"/> recomputes synchronously; otherwise it is
    /// debounced via a UI-thread timer (used for typing in the expensive "all directions" mode) so a
    /// burst of keystrokes coalesces into a single rebuild once the user pauses.
    /// </summary>
    private void RequestRecompute(bool immediate)
    {
        _debounceTimer?.Stop();

        if (immediate || _debounceTimer is null)
        {
            Recompute();
            return;
        }

        // Restart the idle window; the Tick handler runs Recompute on the UI thread when it elapses.
        _debounceTimer.Start();
    }

    private void Recompute()
    {
        Candidates.Clear();

        if (string.IsNullOrEmpty(InputText))
        {
            OutputText = string.Empty;
            HasOutput = false;
            IsIdentity = false;
            return;
        }

        if (ShowAllDirections)
        {
            // Every ordered pairing of distinct layouts — the brute-force view for an unknown source.
            foreach (var from in DvorakCodec.Layouts)
            {
                foreach (var to in DvorakCodec.Layouts)
                {
                    if (from == to)
                    {
                        continue;
                    }

                    var text = _codec.Remap(InputText, from, to);
                    Candidates.Add(new DvorakCandidateItem(NameOf(from), NameOf(to), text, _clipboard.SetText));
                }
            }

            OutputText = string.Empty;
            IsIdentity = false;
        }
        else
        {
            OutputText = _codec.Remap(InputText, InputLayout, OutputLayout);
            IsIdentity = InputLayout == OutputLayout;
        }

        HasOutput = true;
    }

    private string NameOf(DvorakLayout layout) => LayoutNames[(int)layout];

    /// <summary>The text the Copy/Share actions emit: the single result, or every pairing line in "try all" mode.</summary>
    private string BuildResultText()
        => ShowAllDirections
            ? string.Join(Environment.NewLine, Candidates.Select(c => $"{c.Pairing}: {c.Text}"))
            : OutputText;

    /// <summary>Swaps the input and output layouts and carries the current output back into the input.</summary>
    [RelayCommand]
    private void Swap()
    {
        // Capture before swapping: changing the layout indices recomputes OutputText synchronously.
        var carry = OutputText;

        (InputLayoutIndex, OutputLayoutIndex) = (OutputLayoutIndex, InputLayoutIndex);

        // Carry the previous result into the input so the swapped direction reverses it
        // (only meaningful for a single direction, not the brute-force list).
        if (!ShowAllDirections && !string.IsNullOrEmpty(carry))
        {
            InputText = carry;
        }
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
    private void Clear() => InputText = string.Empty;
}
