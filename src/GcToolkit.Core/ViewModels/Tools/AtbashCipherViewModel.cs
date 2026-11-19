using System.Threading.Tasks;
using GcToolkit.Core.Catalog;
using GcToolkit.Core.Ciphers;
using GcToolkit.Core.Discovery;
using GcToolkit.Core.FavoriteTools;
using GcToolkit.Core.Recents;
using GcToolkit.Core.Services;
using Microsoft.Extensions.Localization;

namespace GcToolkit.Core.ViewModels.Tools;

/// <summary>
/// Atbash cipher (issue #5). Mirrors each Latin letter to its opposite (A↔Z, B↔Y, …) live as the user
/// types. Because Atbash is its own inverse, a single transform both encodes and decodes — there is no
/// direction toggle; the view surfaces that as a hint. Beyond geocachingtoolbox.com parity it preserves
/// letter case, passes full Unicode through unchanged, and shows the fixed substitution "key"
/// (A→Z, B→Y, …). All transform logic lives in the pure <see cref="AtbashCipher"/> (thin-VM convention).
/// </summary>
[Tool("AtbashCipher", ToolCategory.Ciphers,
      Introduced = "2026-06-06", Updated = "2026-06-06",
      Keywords = ["atbash", "mirror", "cipher", "reverse", "alphabet", "zrcadlo", "šifra", "zrcadlová", "obrácená abeceda"])]
public sealed partial class AtbashCipherViewModel : ToolViewModelBase, IToolTextInput, IToolTextOutput
{
    private readonly AtbashCipher _cipher = new();
    private readonly IClipboardService _clipboard;
    private readonly IShareService _share;

    public AtbashCipherViewModel(
        ICatalogService catalog,
        IRecentsService recents,
        IFavoriteToolsService favorites,
        IStringLocalizer localizer,
        IClipboardService clipboard,
        IShareService share)
        : base("AtbashCipher", catalog, recents, favorites, localizer)
    {
        _clipboard = clipboard;
        _share = share;
    }

    [ObservableProperty]
    public partial string InputText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string OutputText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial bool HasOutput { get; set; }

    /// <summary>The plain alphabet row of the substitution "key" (<c>A…Z</c>).</summary>
    public string KeyPlain => AtbashCipher.PlainAlphabet;

    /// <summary>The mirrored alphabet row of the substitution "key" (<c>Z…A</c>), aligned under <see cref="KeyPlain"/>.</summary>
    public string KeyCipher => AtbashCipher.CipherAlphabet;

    partial void OnInputTextChanged(string value)
    {
        OutputText = _cipher.Transform(value);
        HasOutput = OutputText.Length > 0;
    }

    partial void OnHasOutputChanged(bool value)
    {
        CopyOutputCommand.NotifyCanExecuteChanged();
        ShareOutputCommand.NotifyCanExecuteChanged();
        UseOutputAsInputCommand.NotifyCanExecuteChanged();
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

    /// <summary>
    /// Feeds the result back into the input so Atbash can be chained with other ciphers. Because the
    /// cipher is self-inverse, the assignment recomputes the output back to the original text.
    /// </summary>
    [RelayCommand(CanExecute = nameof(HasOutput))]
    private void UseOutputAsInput() => InputText = OutputText;

    [RelayCommand]
    private void Clear() => InputText = string.Empty;
}
