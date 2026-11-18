using System.Collections.ObjectModel;
using System.Threading.Tasks;
using GcToolkit.Core.Catalog;
using GcToolkit.Core.Ciphers;
using GcToolkit.Core.Discovery;
using GcToolkit.Core.FavoriteTools;
using GcToolkit.Core.Recents;
using GcToolkit.Core.Services;
using Microsoft.Extensions.Localization;

namespace GcToolkit.Core.ViewModels.Tools;

/// <summary>One row of the on-page reference chart: the plain <see cref="Letter"/> and its <see cref="Trigram"/>.</summary>
public readonly record struct KennyCodeTableRow(char Letter, string Trigram)
{
    /// <summary>Screen-reader label for the tile, e.g. <c>"A mmm"</c>.</summary>
    public string AutomationName => $"{Letter} {Trigram}";
}

/// <summary>
/// Kenny code (issue #21) — the South Park <c>mmm/mpp/mpf</c> cipher: a fixed Bacon-style base-3
/// substitution over <c>m &lt; p &lt; f</c>. Encodes and decodes live as the user types, with an
/// encode/decode toggle and a one-tap round-trip (switching direction carries the result into the input).
/// Goes beyond geocachingtoolbox.com parity with separator/case/structure options, lenient decoding that
/// flags incomplete or impossible groups, and an always-visible substitution chart. All transform logic
/// lives in the pure <see cref="KennyCodeCodec"/> (thin-VM convention).
/// </summary>
[Tool("KennyCode", ToolCategory.Ciphers,
      Introduced = "2026-06-13", Updated = "2026-06-13",
      Keywords = ["kenny", "south park", "mmm", "mpf", "mpp", "bacon", "trigram", "cipher", "šifra", "jihozápadní park"])]
public sealed partial class KennyCodeViewModel : ToolViewModelBase, IToolTextInput
{
    private readonly KennyCodeCodec _codec = new();
    private readonly IClipboardService _clipboard;
    private readonly IShareService _share;
    private readonly IStringLocalizer _localizer;

    private bool _suppressRecompute;

    public KennyCodeViewModel(
        ICatalogService catalog,
        IRecentsService recents,
        IFavoriteToolsService favorites,
        IStringLocalizer localizer,
        IClipboardService clipboard,
        IShareService share)
        : base("KennyCode", catalog, recents, favorites, localizer)
    {
        _clipboard = clipboard;
        _share = share;
        _localizer = localizer;

        foreach (var (letter, trigram) in _codec.ReferenceTable)
        {
            ReferenceTable.Add(new KennyCodeTableRow(letter, trigram));
        }

        Recompute();
    }

    /// <summary>0 = Encrypt (text → Kenny code), 1 = Decrypt (Kenny code → text).</summary>
    [ObservableProperty]
    public partial int DirectionIndex { get; set; }

    /// <summary>Separator between encoded trigrams: 0 = None, 1 = Space, 2 = Comma.</summary>
    [ObservableProperty]
    public partial int SeparatorIndex { get; set; } = 1;

    /// <summary>When <see langword="true"/>, encode to upper-case trigrams (<c>MPF</c>).</summary>
    [ObservableProperty]
    public partial bool UpperCase { get; set; }

    /// <summary>When <see langword="true"/>, keep word boundaries as gaps in the encoded output.</summary>
    [ObservableProperty]
    public partial bool PreserveStructure { get; set; } = true;

    [ObservableProperty]
    public partial string InputText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string OutputText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial bool HasOutput { get; set; }

    /// <summary><see langword="true"/> when decoding produced a validation warning (bad groups / wrong length).</summary>
    [ObservableProperty]
    public partial bool HasWarning { get; set; }

    [ObservableProperty]
    public partial string WarningMessage { get; set; } = string.Empty;

    /// <summary><see langword="true"/> while encoding — toggles the encode-only options in the view.</summary>
    [ObservableProperty]
    public partial bool IsEncoding { get; set; } = true;

    /// <summary>The 26-row substitution chart shown always on the page (Original ↔ Substitution).</summary>
    public ObservableCollection<KennyCodeTableRow> ReferenceTable { get; } = [];

    private bool IsDecoding => DirectionIndex == 1;

    private KennyCodeSeparator Separator => SeparatorIndex switch
    {
        0 => KennyCodeSeparator.None,
        2 => KennyCodeSeparator.Comma,
        _ => KennyCodeSeparator.Space,
    };

    partial void OnDirectionIndexChanged(int value)
    {
        // Ignore re-entrant carries and the transient -1 a RadioButtons control can emit.
        if (_suppressRecompute || value is not (0 or 1))
        {
            return;
        }

        IsEncoding = !IsDecoding;

        // Switching direction carries the previous result into the input, so a round-trip is one tap.
        _suppressRecompute = true;
        InputText = OutputText;
        _suppressRecompute = false;
        Recompute();
    }

    partial void OnInputTextChanged(string value)
    {
        if (!_suppressRecompute)
        {
            Recompute();
        }
    }

    partial void OnSeparatorIndexChanged(int value) => Recompute();

    partial void OnUpperCaseChanged(bool value) => Recompute();

    partial void OnPreserveStructureChanged(bool value) => Recompute();

    partial void OnHasOutputChanged(bool value)
    {
        CopyOutputCommand.NotifyCanExecuteChanged();
        ShareOutputCommand.NotifyCanExecuteChanged();
    }

    private void Recompute()
    {
        if (string.IsNullOrEmpty(InputText))
        {
            OutputText = string.Empty;
            HasOutput = false;
            HasWarning = false;
            WarningMessage = string.Empty;
            return;
        }

        if (IsDecoding)
        {
            var result = _codec.Decode(InputText);
            OutputText = result.Text;
            HasOutput = result.Text.Length > 0;
            HasWarning = result.HasErrors;
            WarningMessage = result.HasErrors ? _localizer["KennyCodeInvalidNotice"].Value : string.Empty;
        }
        else
        {
            var options = new KennyCodeEncodeOptions
            {
                Separator = Separator,
                UpperCase = UpperCase,
                PreserveStructure = PreserveStructure,
            };

            OutputText = _codec.Encode(InputText, options);
            HasOutput = OutputText.Length > 0;
            HasWarning = false;
            WarningMessage = string.Empty;
        }
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

    /// <summary>
    /// Appends a substitution-chart tile to the input so the chart doubles as an on-screen keypad:
    /// the plain letter while encoding, or its trigram (plus a trailing space for readability — the
    /// lenient decoder ignores it) while decoding.
    /// </summary>
    [RelayCommand]
    private void AppendFromTable(KennyCodeTableRow row) =>
        InputText += IsDecoding ? row.Trigram + " " : char.ToLowerInvariant(row.Letter).ToString();
}
