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

/// <summary>
/// Caesar / ROT cipher (issue #13). Encodes and decodes a Caesar shift live as the user types, across
/// three alphabets — letters (ROT13), digits (ROT5) and printable ASCII (ROT47) — with an encode/decode
/// toggle, a shift slider, and a "show all shifts" mode that lists every rotation at once for
/// brute-forcing an unknown shift (the geocachingtoolbox.com parity feature). Goes beyond parity by
/// preserving case and non-alphabet characters, copying/sharing each candidate, and showing the live
/// substitution "key". All transform logic lives in the pure <see cref="CaesarCipher"/> (thin-VM convention).
/// </summary>
[Tool("CaesarCipher", ToolCategory.Ciphers,
      Introduced = "2026-06-01", Updated = "2026-06-01",
      Keywords = ["caesar", "rot13", "rot5", "rot47", "shift", "rotation", "cipher", "posun", "šifra", "rot"])]
public sealed partial class CaesarCipherViewModel : ToolViewModelBase, IToolStateful
{
    private readonly CaesarCipher _cipher = new();
    private readonly IClipboardService _clipboard;
    private readonly IShareService _share;

    private bool _suppressRecompute;

    public CaesarCipherViewModel(
        ICatalogService catalog,
        IRecentsService recents,
        IFavoriteToolsService favorites,
        IStringLocalizer localizer,
        IClipboardService clipboard,
        IShareService share)
        : base("CaesarCipher", catalog, recents, favorites, localizer)
    {
        _clipboard = clipboard;
        _share = share;
        Recompute();
    }

    /// <summary>Active alphabet: 0 = Letters (ROT13), 1 = Digits (ROT5), 2 = ASCII (ROT47).</summary>
    [ObservableProperty]
    public partial int ModeIndex { get; set; }

    /// <summary>0 = Encode (shift forward), 1 = Decode (shift back).</summary>
    [ObservableProperty]
    public partial int DirectionIndex { get; set; }

    [ObservableProperty]
    public partial int Shift { get; set; } = CaesarCipher.DefaultShift(CaesarAlphabet.Letters);

    /// <summary>Largest selectable shift for the active alphabet (size − 1).</summary>
    [ObservableProperty]
    public partial int MaxShift { get; set; } = CaesarCipher.LetterAlphabetSize - 1;

    [ObservableProperty]
    public partial string InputText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial bool ShowAllShifts { get; set; }

    [ObservableProperty]
    public partial string OutputText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial bool HasOutput { get; set; }

    /// <summary><see langword="true"/> when the current shift is its own inverse (ROT13/ROT5/ROT47) and a
    /// single result is shown — surfaces the "same shift encodes and decodes" hint.</summary>
    [ObservableProperty]
    public partial bool IsSelfInverse { get; set; }

    /// <summary>The plain alphabet row of the substitution "key" (e.g. <c>ABC…Z</c>).</summary>
    [ObservableProperty]
    public partial string KeyPlain { get; set; } = string.Empty;

    /// <summary>The shifted alphabet row of the substitution "key", aligned under <see cref="KeyPlain"/>.</summary>
    [ObservableProperty]
    public partial string KeyCipher { get; set; } = string.Empty;

    /// <summary>The candidates shown in "show all shifts" mode (one per non-trivial rotation).</summary>
    public ObservableCollection<CaesarShiftItem> ShiftResults { get; } = [];

    private CaesarAlphabet Alphabet => ModeIndex switch
    {
        1 => CaesarAlphabet.Digits,
        2 => CaesarAlphabet.Ascii,
        _ => CaesarAlphabet.Letters,
    };

    private bool IsDecode => DirectionIndex == 1;

    /// <summary>Decoding is encoding by the negated shift.</summary>
    private int EffectiveShift => IsDecode ? -Shift : Shift;

    partial void OnModeIndexChanged(int value)
    {
        // Ignore the transient -1 a RadioButtons control can emit while re-templating.
        if (value is not (0 or 1 or 2))
        {
            return;
        }

        // Switching alphabet resets to that alphabet's canonical ROT shift and clamps the slider range.
        _suppressRecompute = true;
        MaxShift = CaesarCipher.AlphabetSize(Alphabet) - 1;
        Shift = CaesarCipher.DefaultShift(Alphabet);
        _suppressRecompute = false;
        Recompute();
    }

    partial void OnDirectionIndexChanged(int value)
    {
        if (value is 0 or 1)
        {
            Recompute();
        }
    }

    partial void OnShiftChanged(int value)
    {
        if (!_suppressRecompute)
        {
            Recompute();
        }
    }

    partial void OnInputTextChanged(string value)
    {
        if (!_suppressRecompute)
        {
            Recompute();
        }
    }

    partial void OnShowAllShiftsChanged(bool value) => Recompute();

    partial void OnHasOutputChanged(bool value)
    {
        CopyOutputCommand.NotifyCanExecuteChanged();
        ShareOutputCommand.NotifyCanExecuteChanged();
    }

    private void Recompute()
    {
        var alphabet = Alphabet;

        // The live substitution key always reflects the current alphabet + effective shift.
        KeyPlain = _cipher.PlainAlphabet(alphabet);
        KeyCipher = _cipher.CipherAlphabet(EffectiveShift, alphabet);

        ShiftResults.Clear();

        if (string.IsNullOrEmpty(InputText))
        {
            OutputText = string.Empty;
            HasOutput = false;
            IsSelfInverse = false;
            return;
        }

        if (ShowAllShifts)
        {
            foreach (var candidate in _cipher.AllShifts(InputText, alphabet))
            {
                ShiftResults.Add(new CaesarShiftItem(candidate.Shift, candidate.Text, _clipboard.SetText));
            }

            OutputText = string.Empty;
            IsSelfInverse = false;
        }
        else
        {
            OutputText = _cipher.Transform(InputText, EffectiveShift, alphabet);

            // Even alphabets (all three here) have a half-size shift that is its own inverse.
            IsSelfInverse = Shift == CaesarCipher.DefaultShift(alphabet);
        }

        HasOutput = true;
    }

    /// <summary>The text the Copy/Share actions emit: the single result, or every shift line in "show all" mode.</summary>
    private string BuildResultText()
        => ShowAllShifts
            ? string.Join(Environment.NewLine, ShiftResults.Select(r => $"{r.Shift}: {r.Text}"))
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

    public ToolInputState CaptureState()
    {
        ToolInputState state = new();
        state.Set(nameof(ModeIndex), ModeIndex);
        state.Set(nameof(DirectionIndex), DirectionIndex);
        state.Set(nameof(Shift), Shift);
        state.Set(nameof(ShowAllShifts), ShowAllShifts);
        state.Set(nameof(InputText), InputText);
        return state;
    }

    public void RestoreState(ToolInputState state)
    {
        // The alphabet first: switching it resets the shift to that alphabet's default.
        ModeIndex = Math.Clamp(state.GetInt(nameof(ModeIndex), ModeIndex), 0, 2);
        DirectionIndex = Math.Clamp(state.GetInt(nameof(DirectionIndex), DirectionIndex), 0, 1);
        Shift = Math.Clamp(state.GetInt(nameof(Shift), Shift), 0, MaxShift);
        ShowAllShifts = state.GetBool(nameof(ShowAllShifts), ShowAllShifts);
        InputText = state.GetString(nameof(InputText), InputText);
    }
}
