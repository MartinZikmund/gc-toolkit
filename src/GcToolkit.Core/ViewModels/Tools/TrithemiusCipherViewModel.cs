using System.Collections.ObjectModel;
using System.Threading.Tasks;
using GcToolkit.Core.Catalog;
using GcToolkit.Core.Ciphers;
using GcToolkit.Core.Discovery;
using GcToolkit.Core.FavoriteTools;
using GcToolkit.Core.Infrastructure;
using GcToolkit.Core.Recents;
using GcToolkit.Core.Services;
using Microsoft.Extensions.Localization;
using Microsoft.UI.Dispatching;

namespace GcToolkit.Core.ViewModels.Tools;

/// <summary>
/// Trithemius cipher (issue #300) — the keyless progressive polyalphabetic ancestor of Vigenère.
/// Encodes/decodes live as the user types: the i-th letter is shifted by <c>startOffset + i*step</c>
/// (mod 26), so with the defaults <c>AAAA</c> becomes <c>ABCD</c> (cachesleuth.com parity). Renders the
/// 26×26 tabula recta as a reference. Goes beyond parity with configurable start offset / step and a
/// "show all offsets" view that decodes at every starting row for cracking an unknown start. All
/// transform logic lives in the pure <see cref="TrithemiusCipher"/> (thin-VM convention).
/// </summary>
[Tool("TrithemiusCipher", ToolCategory.Ciphers,
      Introduced = "2026-06-13", Updated = "2026-06-13",
      Keywords = ["trithemius", "progressive", "polyalphabetic", "tabula recta", "vigenere", "cipher", "šifra", "progresivní", "polyalfabetická"])]
public sealed partial class TrithemiusCipherViewModel : ToolViewModelBase, IToolTextInput, IToolTextOutput
{
    private readonly TrithemiusCipher _cipher = new();
    private readonly IClipboardService _clipboard;
    private readonly IShareService _share;
    private readonly UiDebouncer _debouncer = new(TimeSpan.FromMilliseconds(200));
    private readonly DispatcherQueue? _dispatcher = UiDispatcher.TryGetForCurrentThread();

    private bool _suppressRecompute;
    private int _offsetGeneration;

    public TrithemiusCipherViewModel(
        ICatalogService catalog,
        IRecentsService recents,
        IFavoriteToolsService favorites,
        IStringLocalizer localizer,
        IClipboardService clipboard,
        IShareService share)
        : base("TrithemiusCipher", catalog, recents, favorites, localizer)
    {
        _clipboard = clipboard;
        _share = share;
        TabulaRecta = [.. _cipher.TabulaRecta()];
        Recompute();
    }

    /// <summary>0 = Encrypt (shift forward), 1 = Decrypt (shift back).</summary>
    [ObservableProperty]
    public partial int DirectionIndex { get; set; }

    [ObservableProperty]
    public partial string InputText { get; set; } = string.Empty;

    /// <summary>The starting row of the tabula recta (the shift applied to the first letter).</summary>
    [ObservableProperty]
    public partial int StartOffset { get; set; }

    /// <summary>How much the shift grows per letter. Default 1 gives the classic progression.</summary>
    [ObservableProperty]
    public partial int Step { get; set; } = 1;

    [ObservableProperty]
    public partial bool ShowAllOffsets { get; set; }

    [ObservableProperty]
    public partial string OutputText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial bool HasOutput { get; set; }

    /// <summary>The 26 rows of the tabula recta, shown as a monospace reference card.</summary>
    public ObservableCollection<string> TabulaRecta { get; }

    /// <summary>The decode candidates shown in "show all offsets" mode (one per starting row). Assigned wholesale so the virtualizing list gets a single notification.</summary>
    [ObservableProperty]
    public partial IReadOnlyList<TrithemiusOffsetItem> OffsetResults { get; set; } = [];

    private bool IsDecrypt => DirectionIndex == 1;

    partial void OnDirectionIndexChanged(int value)
    {
        if (_suppressRecompute || value is not (0 or 1))
        {
            return;
        }

        // Carry the previous result into the input for a one-tap round-trip. Show-all mode has no
        // single result (and always decodes), so leave the input untouched there.
        if (!ShowAllOffsets)
        {
            _suppressRecompute = true;
            InputText = OutputText;
            _suppressRecompute = false;
        }

        _debouncer.RunNow(Recompute);
    }

    // Typing while showing all offsets brute-forces 26 decodes and rebuilds 26 rows per keystroke;
    // debounce that path so it recomputes once typing pauses. The single result stays instant.
    partial void OnInputTextChanged(string value)
    {
        if (_suppressRecompute)
        {
            return;
        }

        if (ShowAllOffsets)
        {
            _debouncer.Debounce(Recompute);
        }
        else
        {
            _debouncer.RunNow(Recompute);
        }
    }

    partial void OnStartOffsetChanged(int value) => _debouncer.RunNow(Recompute);

    partial void OnStepChanged(int value) => _debouncer.RunNow(Recompute);

    partial void OnShowAllOffsetsChanged(bool value) => _debouncer.RunNow(Recompute);

    partial void OnHasOutputChanged(bool value)
    {
        CopyOutputCommand.NotifyCanExecuteChanged();
        ShareOutputCommand.NotifyCanExecuteChanged();
    }

    private void Recompute()
    {
        if (string.IsNullOrEmpty(InputText))
        {
            _offsetGeneration++; // discard any in-flight brute force
            OffsetResults = [];
            OutputText = string.Empty;
            HasOutput = false;
            return;
        }

        if (ShowAllOffsets)
        {
            OutputText = string.Empty;
            StartShowAllOffsets(InputText);
        }
        else
        {
            _offsetGeneration++;
            OffsetResults = [];
            OutputText = _cipher.Transform(InputText, IsDecrypt, StartOffset, Step);
            HasOutput = true;
        }
    }

    /// <summary>
    /// Decodes at every starting row on a background thread (also building the 26 rows), then publishes
    /// the candidates back on the UI thread. A generation guard drops results a newer keystroke/toggle
    /// has already superseded. Runs synchronously when there is no dispatcher (unit tests).
    /// </summary>
    private void StartShowAllOffsets(string input)
    {
        var generation = ++_offsetGeneration;

        if (_dispatcher is null)
        {
            OffsetResults = BuildOffsets(input);
            HasOutput = OffsetResults.Count > 0;
            return;
        }

        _ = Task.Run(() =>
        {
            var results = BuildOffsets(input);
            _dispatcher.TryEnqueue(() =>
            {
                if (generation != _offsetGeneration)
                {
                    return; // a newer input already superseded this result
                }

                OffsetResults = results;
                HasOutput = results.Count > 0;
            });
        });
    }

    private IReadOnlyList<TrithemiusOffsetItem> BuildOffsets(string input)
        => [.. _cipher.AllOffsets(input, Step).Select(c => new TrithemiusOffsetItem(c.StartOffset, c.Text, _clipboard.SetText))];

    /// <summary>The text Copy/Share emit: the single result, or every offset line in "show all" mode.</summary>
    private string BuildResultText()
        => ShowAllOffsets
            ? string.Join(Environment.NewLine, OffsetResults.Select(r => $"{r.StartOffset}: {r.Text}"))
            : OutputText;

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
