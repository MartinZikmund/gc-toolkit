using System.Threading.Tasks;
using GcToolkit.Core.Catalog;
using GcToolkit.Core.Discovery;
using GcToolkit.Core.FavoriteTools;
using GcToolkit.Core.Recents;
using GcToolkit.Core.Services;
using GcToolkit.Core.Text;
using Microsoft.Extensions.Localization;

namespace GcToolkit.Core.ViewModels.Tools;

/// <summary>
/// Numbers ↔ letters / A1Z26 tool (issue #12). Converts live in either direction: letters → their
/// position numbers (A=1 … Z=26) and numbers → letters. Beyond geocachingtoolbox.com parity (which
/// only decodes numbers → letters with a fixed space separator) this tool does both directions in one
/// view, takes a custom separator, can keep or strip non-letters when encoding, lets the output case be
/// chosen, and either wraps out-of-range values modulo 26 or flags them. All transform logic lives in
/// the pure <see cref="AlphabetNumbers"/> codec (thin-VM convention).
/// </summary>
[Tool("NumbersToLetters", ToolCategory.Text,
      Introduced = "2026-06-06", Updated = "2026-06-07",
      Keywords = ["a1z26", "numbers", "letters", "alphabet", "position", "index", "method", "umlaut", "german", "nordic", "conversion table", "číslapísmena", "čísla", "písmena", "abeceda", "pozice", "metoda", "převodní tabulka", "letter to number", "number to letter"])]
public sealed partial class NumbersToLettersViewModel : ToolViewModelBase, IToolTextInput
{
    private readonly AlphabetNumbers _codec = new();
    private readonly IClipboardService _clipboard;
    private readonly IShareService _share;
    private readonly IStringLocalizer _localizer;
    private bool _suppressRecompute;

    public NumbersToLettersViewModel(
        ICatalogService catalog,
        IRecentsService recents,
        IFavoriteToolsService favorites,
        IStringLocalizer localizer,
        IClipboardService clipboard,
        IShareService share)
        : base("NumbersToLetters", catalog, recents, favorites, localizer)
    {
        _clipboard = clipboard;
        _share = share;
        _localizer = localizer;
        InputPlaceholder = _localizer["NumbersInputPlaceholderLetters"].Value;
        MethodLabels = [.. AlphabetMethods.All.Select(m => m.Label)];
        ConversionTable = SelectedMethodDefinition.Entries;
    }

    /// <summary>The eight method formulas shown in the picker (locale-neutral data, e.g. <c>A=1 ... Z=26</c>).</summary>
    public IReadOnlyList<string> MethodLabels { get; }

    /// <summary>0 = letters → numbers, 1 = numbers → letters.</summary>
    [ObservableProperty]
    public partial int DirectionIndex { get; set; }

    /// <summary>Index into <see cref="MethodLabels"/> / <see cref="AlphabetMethods.All"/>; the selected numbering scheme.</summary>
    [ObservableProperty]
    public partial int MethodIndex { get; set; }

    private AlphabetMethodDefinition SelectedMethodDefinition
        => AlphabetMethods.All[Math.Clamp(MethodIndex, 0, AlphabetMethods.All.Count - 1)];

    [ObservableProperty]
    public partial string InputText { get; set; } = string.Empty;

    /// <summary>The string placed between numbers (encode) / split on (decode). Defaults to a space.</summary>
    [ObservableProperty]
    public partial string Separator { get; set; } = " ";

    /// <summary>When decoding, wrap values outside the selected method's range back into range instead of flagging them.</summary>
    [ObservableProperty]
    public partial bool WrapModulo { get; set; }

    /// <summary>When decoding, emit upper-case letters (otherwise lower-case).</summary>
    [ObservableProperty]
    public partial bool UpperCase { get; set; } = true;

    /// <summary>When encoding, keep non-letter characters verbatim instead of dropping them.</summary>
    [ObservableProperty]
    public partial bool KeepNonLetters { get; set; }

    /// <summary>
    /// When decoding, the string a number that maps to no letter is replaced with (empty = drop it).
    /// Ignored while <see cref="KeepOriginalUnknown"/> is on.
    /// </summary>
    [ObservableProperty]
    public partial string ReplaceUnknownText { get; set; } = "?";

    /// <summary>When decoding, keep an unmapped number as typed instead of replacing it.</summary>
    [ObservableProperty]
    public partial bool KeepOriginalUnknown { get; set; }

    /// <summary><see langword="true"/> when the replacement text box is editable (i.e. not keeping originals).</summary>
    [ObservableProperty]
    public partial bool CanEditReplacement { get; set; } = true;

    /// <summary>The character ↔ value pairs of the selected method, shown in the conversion table.</summary>
    [ObservableProperty]
    public partial IReadOnlyList<AlphabetEntry> ConversionTable { get; set; } = [];

    [ObservableProperty]
    public partial string OutputText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial bool HasOutput { get; set; }

    /// <summary><see langword="true"/> while the letters → numbers direction is active (drives which option set shows).</summary>
    [ObservableProperty]
    public partial bool IsLettersToNumbers { get; set; } = true;

    /// <summary>Direction-aware placeholder for the input box.</summary>
    [ObservableProperty]
    public partial string InputPlaceholder { get; set; } = string.Empty;

    private bool IsDecode => DirectionIndex == 1;

    partial void OnDirectionIndexChanged(int value)
    {
        // Ignore re-entrant carries and the transient -1 a RadioButtons control can emit while re-templating.
        if (_suppressRecompute || value is not (0 or 1))
        {
            return;
        }

        IsLettersToNumbers = value == 0;
        InputPlaceholder = _localizer[IsLettersToNumbers ? "NumbersInputPlaceholderLetters" : "NumbersInputPlaceholderNumbers"].Value;

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

    partial void OnSeparatorChanged(string value) => Recompute();

    partial void OnWrapModuloChanged(bool value) => Recompute();

    partial void OnUpperCaseChanged(bool value) => Recompute();

    partial void OnKeepNonLettersChanged(bool value) => Recompute();

    partial void OnReplaceUnknownTextChanged(string value) => Recompute();

    partial void OnKeepOriginalUnknownChanged(bool value)
    {
        CanEditReplacement = !value;
        Recompute();
    }

    partial void OnMethodIndexChanged(int value)
    {
        // A ComboBox can momentarily report -1 while its items load.
        if (value < 0)
        {
            return;
        }

        ConversionTable = SelectedMethodDefinition.Entries;
        Recompute();
    }

    partial void OnHasOutputChanged(bool value)
    {
        CopyOutputCommand.NotifyCanExecuteChanged();
        ShareOutputCommand.NotifyCanExecuteChanged();
        SwapCommand.NotifyCanExecuteChanged();
    }

    private void Recompute()
    {
        if (string.IsNullOrEmpty(InputText))
        {
            OutputText = string.Empty;
            HasOutput = false;
            return;
        }

        // An empty separator box falls back to the default single space (the parity convention).
        var options = new AlphabetNumberOptions
        {
            Method = SelectedMethodDefinition.Method,
            Separator = string.IsNullOrEmpty(Separator) ? " " : Separator,
            WrapModulo = WrapModulo,
            UpperCase = UpperCase,
            KeepNonLetters = KeepNonLetters,
            KeepOriginalUnknown = KeepOriginalUnknown,
            UnknownReplacement = ReplaceUnknownText,
        };

        OutputText = IsDecode
            ? _codec.NumbersToLetters(InputText, options)
            : _codec.LettersToNumbers(InputText, options);

        HasOutput = OutputText.Length > 0;
    }

    /// <summary>Flips the direction; the direction handler carries the result into the input for a one-tap round trip.</summary>
    [RelayCommand(CanExecute = nameof(HasOutput))]
    private void Swap() => DirectionIndex = IsDecode ? 0 : 1;

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
}
