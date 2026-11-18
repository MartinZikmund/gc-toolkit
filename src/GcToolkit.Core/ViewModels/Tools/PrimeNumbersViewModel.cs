using System.Globalization;
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
/// Prime numbers toolbox (issue #46): primality test, n-th prime, position of a prime,
/// nearest prime, and prime factorization — geocachingtoolbox.com parity. Exceeds parity with
/// explicit previous/next primes, a composite's smallest divisor, π(x) info, superscript
/// factorization, and a nearest-prime range extended from 15,485,863 all the way to 2^53.
/// </summary>
[Tool("PrimeNumbers", ToolCategory.Numbers,
      Introduced = "2026-06-09", Updated = "2026-06-09",
      Keywords = ["prime", "primes", "factorization", "factor", "nth", "prvočíslo", "prvočísla", "rozklad", "dělitel"])]
public sealed partial class PrimeNumbersViewModel : ToolViewModelBase, IToolTextInput
{
    // Mode indices — must match the RadioButtons order in the view.
    private const int PrimalityMode = 0;
    private const int NthPrimeMode = 1;
    private const int PositionMode = 2;
    private const int NearestMode = 3;
    private const int FactorizationMode = 4;

    private readonly IClipboardService _clipboard;
    private readonly IShareService _share;
    private readonly IStringLocalizer _localizer;

    // Stamps each computation so a stale sieve await can't overwrite newer results.
    private int _computeVersion;

    public PrimeNumbersViewModel(
        ICatalogService catalog,
        IRecentsService recents,
        IFavoriteToolsService favorites,
        IStringLocalizer localizer,
        IClipboardService clipboard,
        IShareService share)
        : base("PrimeNumbers", catalog, recents, favorites, localizer)
    {
        _clipboard = clipboard;
        _share = share;
        _localizer = localizer;
        RangeText = FormatRangeHint();
    }

    /// <summary>Selected function: 0 primality, 1 n-th prime, 2 position, 3 nearest, 4 factorization.</summary>
    [ObservableProperty]
    public partial int ModeIndex { get; set; }

    [ObservableProperty]
    public partial string InputText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string ResultText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial bool HasResult { get; set; }

    [ObservableProperty]
    public partial bool HasError { get; set; }

    [ObservableProperty]
    public partial string ErrorText { get; set; } = string.Empty;

    /// <summary>The valid input range for the current mode, shown under the input box.</summary>
    [ObservableProperty]
    public partial string RangeText { get; set; }

    /// <summary><see langword="true"/> while the shared prime sieve builds on first use.</summary>
    [ObservableProperty]
    public partial bool IsBusy { get; set; }

    [ObservableProperty]
    public partial bool ShowNeighbors { get; set; }

    [ObservableProperty]
    public partial string PreviousPrimeText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string NextPrimeText { get; set; } = string.Empty;

    partial void OnInputTextChanged(string value) => _ = ComputeAsync();

    partial void OnModeIndexChanged(int value)
    {
        // Ignore the transient -1 a RadioButtons control can emit while re-templating.
        if (value < 0)
        {
            return;
        }

        RangeText = FormatRangeHint();
        _ = ComputeAsync();
    }

    partial void OnHasResultChanged(bool value)
    {
        CopyResultCommand.NotifyCanExecuteChanged();
        ShareResultCommand.NotifyCanExecuteChanged();
    }

    private async Task ComputeAsync()
    {
        var version = ++_computeVersion;
        ClearOutput();

        if (string.IsNullOrWhiteSpace(InputText))
        {
            return;
        }

        if (!TryParseInput(out var number, out var error))
        {
            ShowError(error);
            return;
        }

        var mode = ModeIndex;
        if (mode is NthPrimeMode or PositionMode)
        {
            var sieveTask = PrimeSieve.GetSharedAsync();
            if (!sieveTask.IsCompleted)
            {
                IsBusy = true;
            }

            var sieve = await sieveTask;
            if (version != _computeVersion)
            {
                return;
            }

            IsBusy = false;

            if (mode == NthPrimeMode)
            {
                ComputeNthPrime(sieve, number);
            }
            else
            {
                ComputePosition(sieve, number);
            }

            return;
        }

        switch (mode)
        {
            case PrimalityMode:
                ComputePrimality(number);
                break;
            case NearestMode:
                ComputeNearest(number);
                break;
            case FactorizationMode:
                ComputeFactorization(number);
                break;
        }
    }

    private void ComputePrimality(ulong number)
    {
        if (PrimeMath.IsPrime(number))
        {
            ShowResult(_localizer["PrimeIsPrime", Group(number)].Value);
        }
        else if (number < 2)
        {
            // 0 and 1 are accepted here so the classic "is 1 prime?" question gets an answer.
            ShowResult(_localizer["PrimeIsNotPrimeSimple", Group(number)].Value);
        }
        else
        {
            var smallestDivisor = PrimeMath.Factorize(number)[0].Prime;
            ShowResult(_localizer["PrimeIsNotPrime", Group(number), Group(smallestDivisor)].Value);
        }
    }

    private void ComputeNthPrime(PrimeSieve sieve, ulong ordinal)
        => ShowResult(_localizer["PrimeNthResult", Group(ordinal), Group((ulong)sieve.NthPrime((int)ordinal))].Value);

    private void ComputePosition(PrimeSieve sieve, ulong number)
    {
        if (sieve.PositionOf((long)number) is { } position)
        {
            ShowResult(_localizer["PrimePositionResult", Group(number), Group((ulong)position)].Value);
        }
        else
        {
            // Exceeds parity: a composite still gets π(x) and its prime neighbors.
            var count = sieve.CountUpTo((long)number);
            ShowResult(_localizer["PrimePositionNotPrime", Group(number), Group((ulong)count)].Value);
            ShowNeighborsFor(number);
        }
    }

    private void ComputeNearest(ulong number)
    {
        var nearest = PrimeMath.NearestPrime(number);
        ShowResult(nearest == number
            ? _localizer["PrimeNearestSelf", Group(number)].Value
            : _localizer["PrimeNearestResult", Group(number), Group(nearest)].Value);
        ShowNeighborsFor(number);
    }

    private void ComputeFactorization(ulong number)
    {
        var factors = PrimeMath.Factorize(number);
        ShowResult(factors is [{ Exponent: 1 }]
            ? _localizer["PrimeFactorizationPrime", Group(number)].Value
            : $"{number.ToString(CultureInfo.InvariantCulture)} = {PrimeFormat.FormatFactorization(factors)}");
    }

    private void ShowNeighborsFor(ulong number)
    {
        var previous = PrimeMath.PreviousPrime(number);
        var next = PrimeMath.NextPrime(number);
        PreviousPrimeText = previous is { } p ? Group(p) : "—";
        NextPrimeText = next is { } x ? Group(x) : "—";
        ShowNeighbors = true;
    }

    private bool TryParseInput(out ulong number, out string error)
    {
        number = 0;

        // Be forgiving about pasted values: whitespace (incl. no-break) and comma separators are fine.
        var digits = string.Concat(InputText.Where(c => !char.IsWhiteSpace(c) && c != ','));
        if (digits.Length == 0 || !digits.All(char.IsAsciiDigit))
        {
            error = _localizer["PrimeInvalidNumber"].Value;
            return false;
        }

        var (min, max) = CurrentRange();
        if (!ulong.TryParse(digits, NumberStyles.None, CultureInfo.InvariantCulture, out number)
            || number < min
            || number > max)
        {
            error = _localizer["PrimeOutOfRange", Group(min), Group(max)].Value;
            return false;
        }

        error = string.Empty;
        return true;
    }

    private (ulong Min, ulong Max) CurrentRange() => ModeIndex switch
    {
        PrimalityMode => (0, PrimeMath.MaxValue),
        NthPrimeMode => (1, PrimeSieve.PrimeCount),
        PositionMode => (2, PrimeSieve.Limit),
        _ => (2, PrimeMath.MaxValue),
    };

    private string FormatRangeHint()
    {
        var (min, max) = CurrentRange();
        return _localizer["PrimeRangeHint", Group(min), Group(max)].Value;
    }

    private static string Group(ulong value) => value.ToString("N0", CultureInfo.CurrentCulture);

    private void ShowResult(string text)
    {
        ResultText = text;
        HasResult = true;
    }

    private void ShowError(string text)
    {
        ErrorText = text;
        HasError = true;
    }

    private void ClearOutput()
    {
        IsBusy = false; // a superseded sieve await must not leave the busy state stuck
        ResultText = string.Empty;
        HasResult = false;
        HasError = false;
        ErrorText = string.Empty;
        ShowNeighbors = false;
        PreviousPrimeText = string.Empty;
        NextPrimeText = string.Empty;
    }

    [RelayCommand(CanExecute = nameof(HasResult))]
    private void CopyResult() => _clipboard.SetText(ResultText);

    [RelayCommand(CanExecute = nameof(HasResult))]
    private async Task ShareResultAsync()
    {
        try
        {
            await _share.ShareTextAsync(ToolName, ResultText);
        }
        catch (Exception)
        {
            // Sharing is best-effort; a platform share failure must not crash the tool.
        }
    }

    [RelayCommand]
    private void Clear() => InputText = string.Empty;
}
