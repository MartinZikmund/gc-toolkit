using System.Globalization;
using System.Text;
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
/// Base64 encoder/decoder (issue #122). Converts text to Base64 and back live as the user types, with
/// the direction auto-detected from the input and overridable at any time. Goes beyond a plain
/// converter by forgiving what field-found Base64 actually looks like — line breaks, stray spaces,
/// missing or surplus padding, URL-safe characters — and by naming the offending character when the
/// input is genuinely not Base64. All transform logic lives in the pure <see cref="Base64Codec"/>
/// (thin-VM convention).
/// </summary>
[Tool("Base64", ToolCategory.Numbers,
      Introduced = "2026-09-03", Updated = "2026-09-03",
      Keywords = ["base64", "base 64", "encode", "decode", "encoder", "decoder", "encoding", "rfc 4648", "kódování", "zakódovat", "dekódovat", "dekodér", "převod textu"])]
public sealed partial class Base64ViewModel : ToolViewModelBase, IToolStateful
{
    private const int AutoDirection = 0;
    private const int EncodeDirection = 1;
    private const int DecodeDirection = 2;

    private readonly Base64Codec _codec = new();
    private readonly IClipboardService _clipboard;
    private readonly IShareService _share;
    private readonly IStringLocalizer _localizer;

    private bool _suppressConvert;

    public Base64ViewModel(
        ICatalogService catalog,
        IRecentsService recents,
        IFavoriteToolsService favorites,
        IStringLocalizer localizer,
        IClipboardService clipboard,
        IShareService share)
        : base("Base64", catalog, recents, favorites, localizer)
    {
        _clipboard = clipboard;
        _share = share;
        _localizer = localizer;
    }

    /// <summary>0 = auto-detect, 1 = encode, 2 = decode.</summary>
    [ObservableProperty]
    public partial int DirectionIndex { get; set; }

    /// <summary>Emit the trailing <c>=</c> padding when encoding (off suits URLs and short puzzle strings).</summary>
    [ObservableProperty]
    public partial bool IncludePadding { get; set; } = true;

    [ObservableProperty]
    public partial string InputText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string OutputText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial bool HasOutput { get; set; }

    /// <summary>The direction actually used — shows the padding option, which only applies when encoding.</summary>
    [ObservableProperty]
    public partial bool IsEncoding { get; set; } = true;

    /// <summary>Set only under auto-detect, so the user can see which way the tool went.</summary>
    [ObservableProperty]
    public partial bool ShowDetectedDirection { get; set; }

    [ObservableProperty]
    public partial string DetectedDirectionName { get; set; } = string.Empty;

    /// <summary><see langword="true"/> when the input needed whitespace, padding or URL-safe fix-ups.</summary>
    [ObservableProperty]
    public partial bool ShowRepairedNote { get; set; }

    /// <summary><see langword="true"/> when the decoded bytes are not valid UTF-8 text.</summary>
    [ObservableProperty]
    public partial bool ShowBinaryNote { get; set; }

    [ObservableProperty]
    public partial bool HasError { get; set; }

    [ObservableProperty]
    public partial string ErrorMessage { get; set; } = string.Empty;

    /// <summary>"Bytes: 29" — the payload size, which is what a puzzle usually turns on.</summary>
    [ObservableProperty]
    public partial string ByteCountText { get; set; } = string.Empty;

    partial void OnDirectionIndexChanged(int value)
    {
        // Ignore the transient -1 a RadioButtons control can emit while re-templating.
        if (value is AutoDirection or EncodeDirection or DecodeDirection)
        {
            ConvertUnlessSuppressed();
        }
    }

    partial void OnIncludePaddingChanged(bool value) => ConvertUnlessSuppressed();

    partial void OnInputTextChanged(string value) => ConvertUnlessSuppressed();

    partial void OnHasOutputChanged(bool value)
    {
        CopyOutputCommand.NotifyCanExecuteChanged();
        ShareOutputCommand.NotifyCanExecuteChanged();
        SwapCommand.NotifyCanExecuteChanged();
    }

    private void ConvertUnlessSuppressed()
    {
        if (!_suppressConvert)
        {
            Convert();
        }
    }

    private void Convert()
    {
        ShowDetectedDirection = false;
        DetectedDirectionName = string.Empty;
        ShowRepairedNote = false;
        ShowBinaryNote = false;
        HasError = false;
        ErrorMessage = string.Empty;
        ByteCountText = string.Empty;

        if (string.IsNullOrEmpty(InputText))
        {
            IsEncoding = DirectionIndex != DecodeDirection;
            OutputText = string.Empty;
            HasOutput = false;
            return;
        }

        var decode = DirectionIndex switch
        {
            EncodeDirection => false,
            DecodeDirection => true,
            _ => Base64Codec.LooksLikeBase64(InputText),
        };

        IsEncoding = !decode;

        if (DirectionIndex == AutoDirection)
        {
            DetectedDirectionName = _localizer[decode ? "Base64DirectionDecode" : "Base64DirectionEncode"].Value;
            ShowDetectedDirection = true;
        }

        if (decode)
        {
            Decode();
        }
        else
        {
            OutputText = _codec.Encode(InputText, IncludePadding);
            SetByteCount(Encoding.UTF8.GetByteCount(InputText));
            HasOutput = OutputText.Length > 0;
        }
    }

    private void Decode()
    {
        var result = _codec.Decode(InputText);

        if (!result.Success)
        {
            OutputText = string.Empty;
            HasOutput = false;
            HasError = true;
            ErrorMessage = result.Error switch
            {
                Base64DecodeError.InvalidCharacter => string.Format(
                    CultureInfo.CurrentCulture,
                    _localizer["Base64InvalidCharacter"].Value,
                    result.InvalidCharacter),
                _ => _localizer["Base64InvalidLength"].Value,
            };
            return;
        }

        OutputText = result.Text;
        HasOutput = OutputText.Length > 0;
        ShowRepairedNote = result.WasRepaired && HasOutput;
        ShowBinaryNote = result.IsBinary;
        SetByteCount(result.ByteCount);
    }

    private void SetByteCount(int byteCount)
        => ByteCountText = byteCount == 0
            ? string.Empty
            : string.Format(CultureInfo.CurrentCulture, _localizer["Base64ByteCount"].Value, byteCount);

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

    /// <summary>Feeds the result back as input and flips the direction, so a round-trip is one tap.</summary>
    [RelayCommand(CanExecute = nameof(HasOutput))]
    private void Swap()
    {
        // Both writes feed the same recompute, so suppress the half-swapped intermediate pass.
        _suppressConvert = true;
        DirectionIndex = IsEncoding ? DecodeDirection : EncodeDirection;
        InputText = OutputText;
        _suppressConvert = false;

        Convert();
    }

    [RelayCommand]
    private void Clear() => InputText = string.Empty;

    public ToolInputState CaptureState()
    {
        ToolInputState state = new();
        state.Set(nameof(DirectionIndex), DirectionIndex);
        state.Set(nameof(IncludePadding), IncludePadding);
        state.Set(nameof(InputText), InputText);
        return state;
    }

    public void RestoreState(ToolInputState state)
    {
        DirectionIndex = Math.Clamp(state.GetInt(nameof(DirectionIndex), DirectionIndex), AutoDirection, DecodeDirection);
        IncludePadding = state.GetBool(nameof(IncludePadding), IncludePadding);
        InputText = state.GetString(nameof(InputText), InputText);
    }
}
