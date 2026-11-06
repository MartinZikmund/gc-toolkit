using System.Collections.ObjectModel;
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
/// Text ⇄ UTF-8 byte converter (issue #308). Encodes to hex (or decimal/binary/octal/percent/code points/
/// UTF-16/UTF-32), decodes forgivingly with strict validation, and shows a per-character breakdown.
/// </summary>
[Tool("Utf8", ToolCategory.Numbers,
      Introduced = "2026-10-15", Updated = "2026-10-15",
      Keywords = ["utf8", "utf-8", "unicode", "hex", "bytes", "code point", "kódování", "bajty", "utf16", "utf32"])]
public sealed partial class Utf8ViewModel : ToolViewModelBase
{
    private static readonly Utf8Representation[] _representations = Enum.GetValues<Utf8Representation>();
    private static readonly Utf8Separator[] _separators = Enum.GetValues<Utf8Separator>();

    private readonly IClipboardService _clipboard;
    private readonly IShareService _share;
    private readonly IStringLocalizer _localizer;

    private bool _suppressConvert;

    public Utf8ViewModel(
        ICatalogService catalog,
        IRecentsService recents,
        IFavoriteToolsService favorites,
        IStringLocalizer localizer,
        IClipboardService clipboard,
        IShareService share)
        : base("Utf8", catalog, recents, favorites, localizer)
    {
        _clipboard = clipboard;
        _share = share;
        _localizer = localizer;
    }

    /// <summary>0 = text → bytes, 1 = bytes → text.</summary>
    [ObservableProperty]
    public partial int DirectionIndex { get; set; }

    /// <summary>Index into <see cref="Utf8Representation"/> (declaration order).</summary>
    [ObservableProperty]
    public partial int RepresentationIndex { get; set; }

    /// <summary>Index into <see cref="Utf8Separator"/> (declaration order).</summary>
    [ObservableProperty]
    public partial int SeparatorIndex { get; set; }

    [ObservableProperty]
    public partial bool Uppercase { get; set; } = true;

    [ObservableProperty]
    public partial bool EmitBom { get; set; }

    [ObservableProperty]
    public partial string InputText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string OutputText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial bool HasOutput { get; set; }

    [ObservableProperty]
    public partial bool HasError { get; set; }

    [ObservableProperty]
    public partial string ErrorMessage { get; set; } = string.Empty;

    [ObservableProperty]
    public partial bool ShowBreakdown { get; set; }

    public ObservableCollection<Utf8CharInfo> BreakdownRows { get; } = [];

    /// <summary>Encoding options only apply when going text → bytes.</summary>
    public bool IsEncoding => DirectionIndex != 1;

    partial void OnInputTextChanged(string value) => ConvertIfAllowed();

    partial void OnRepresentationIndexChanged(int value) => ConvertIfAllowed();

    partial void OnSeparatorIndexChanged(int value) => ConvertIfAllowed();

    partial void OnUppercaseChanged(bool value) => ConvertIfAllowed();

    partial void OnEmitBomChanged(bool value) => ConvertIfAllowed();

    partial void OnHasOutputChanged(bool value)
    {
        CopyOutputCommand.NotifyCanExecuteChanged();
        ShareOutputCommand.NotifyCanExecuteChanged();
    }

    partial void OnDirectionIndexChanged(int value)
    {
        if (_suppressConvert || value is not (0 or 1))
        {
            return;
        }

        OnPropertyChanged(nameof(IsEncoding));

        // Carry the previous result into the input so a round-trip is one tap.
        _suppressConvert = true;
        InputText = OutputText;
        _suppressConvert = false;
        Convert();
    }

    private void ConvertIfAllowed()
    {
        if (!_suppressConvert)
        {
            Convert();
        }
    }

    private void Convert()
    {
        BreakdownRows.Clear();
        ShowBreakdown = false;

        Utf8Result result;
        string plaintext;
        if (IsEncoding)
        {
            result = Utf8Codec.Encode(InputText, new(
                _representations[Math.Clamp(RepresentationIndex, 0, _representations.Length - 1)],
                _separators[Math.Clamp(SeparatorIndex, 0, _separators.Length - 1)],
                Uppercase,
                EmitBom));
            plaintext = InputText;
        }
        else
        {
            result = Utf8Codec.Decode(InputText);
            plaintext = result.Output;
        }

        OutputText = result.Output;
        HasOutput = result.Error == Utf8Error.None && result.Output.Length > 0;
        HasError = result.Error != Utf8Error.None;
        ErrorMessage = HasError ? _localizer[$"Utf8Error{result.Error}"].Value : string.Empty;

        if (HasOutput)
        {
            foreach (var row in Utf8Codec.Breakdown(plaintext))
            {
                BreakdownRows.Add(row);
            }

            ShowBreakdown = BreakdownRows.Count > 0;
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

    [RelayCommand]
    private void Example() => InputText = IsEncoding ? "Ahoj, světe! 😀" : "41 68 6F 6A 2C 20 73 76 C4 9B 74 65 21 20 F0 9F 98 80";
}
