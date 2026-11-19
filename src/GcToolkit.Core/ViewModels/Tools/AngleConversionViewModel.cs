using System.Collections.ObjectModel;
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
/// Angle unit converter (issue #8). Converts an angle between every common unit live as the user types —
/// no Convert button — with a From/To picker, a swap button, a 0–15 decimals selector, a single target
/// result, and an "all units" table showing every unit at once with per-row copy. Input is parsed
/// leniently (comma or dot decimal, plus sexagesimal DMS for degrees). All math lives in the pure
/// <see cref="AngleConverter"/> (thin-VM convention). Matches the geocachingtoolbox.com converter's 10
/// units and goes beyond parity with arcminutes/arcseconds and DMS handling.
/// </summary>
[Tool("AngleConversion", ToolCategory.Numbers,
      Introduced = "2026-06-13", Updated = "2026-06-13",
      Keywords =
      [
          "angle", "degree", "degrees", "radian", "gradian", "mil", "turn", "compass point",
          "arcminute", "arcsecond", "convert", "conversion", "dms",
          "úhel", "stupně", "radián", "gradián", "převod", "převést", "mils", "otáčka"
      ])]
public sealed partial class AngleConversionViewModel : ToolViewModelBase, IToolTextInput, IToolTextOutput
{
    string IToolTextOutput.OutputText => ResultText;

    private const int MinDecimals = 0;
    private const int MaxDecimals = 15;
    private const int DefaultDecimals = 6;

    private readonly AngleConverter _converter = new();
    private readonly IClipboardService _clipboard;
    private readonly IShareService _share;
    private readonly IStringLocalizer _localizer;

    private bool _suppressRecompute;

    public AngleConversionViewModel(
        ICatalogService catalog,
        IRecentsService recents,
        IFavoriteToolsService favorites,
        IStringLocalizer localizer,
        IClipboardService clipboard,
        IShareService share)
        : base("AngleConversion", catalog, recents, favorites, localizer)
    {
        _clipboard = clipboard;
        _share = share;
        _localizer = localizer;

        UnitNames = [.. AngleConverter.Units.Select(UnitName)];
        FromUnitIndex = IndexOf(AngleUnit.Degrees);
        ToUnitIndex = IndexOf(AngleUnit.Radians);
        Recompute();
    }

    /// <summary>Localized unit names, in <see cref="AngleConverter.Units"/> order (drives both ComboBoxes).</summary>
    public IReadOnlyList<string> UnitNames { get; }

    /// <summary>The result shown in the "all units" table — one row per unit.</summary>
    public ObservableCollection<AngleUnitResult> AllUnits { get; } = [];

    [ObservableProperty]
    public partial string InputText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial int FromUnitIndex { get; set; }

    [ObservableProperty]
    public partial int ToUnitIndex { get; set; }

    /// <summary>Decimal places for every formatted value (0–15). A <see cref="double"/> so the bound
    /// NumberBox does not truncate fractional input before it reaches us — we round it in <see cref="Format"/>.</summary>
    [ObservableProperty]
    public partial double Decimals { get; set; } = DefaultDecimals;

    /// <summary>The single target result (the To unit), or empty when the input is invalid.</summary>
    [ObservableProperty]
    public partial string ResultText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial bool HasResult { get; set; }

    /// <summary><see langword="true"/> when the input is non-empty but cannot be parsed.</summary>
    [ObservableProperty]
    public partial bool HasError { get; set; }

    /// <summary>The DMS (D°M'S") rendering of the parsed angle in degrees — a beyond-parity convenience.</summary>
    [ObservableProperty]
    public partial string DmsText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial bool HasDms { get; set; }

    public double MinDecimalsValue => MinDecimals;

    public double MaxDecimalsValue => MaxDecimals;

    private AngleUnit FromUnit => UnitAt(FromUnitIndex);

    private AngleUnit ToUnit => UnitAt(ToUnitIndex);

    partial void OnInputTextChanged(string value)
    {
        if (!_suppressRecompute)
        {
            Recompute();
        }
    }

    partial void OnFromUnitIndexChanged(int value)
    {
        if (!_suppressRecompute)
        {
            Recompute();
        }
    }

    partial void OnToUnitIndexChanged(int value)
    {
        if (!_suppressRecompute)
        {
            Recompute();
        }
    }

    partial void OnDecimalsChanged(double value)
    {
        if (!_suppressRecompute)
        {
            Recompute();
        }
    }

    partial void OnHasResultChanged(bool value)
    {
        CopyResultCommand.NotifyCanExecuteChanged();
        ShareResultCommand.NotifyCanExecuteChanged();
    }

    private void Recompute()
    {
        AllUnits.Clear();

        if (string.IsNullOrWhiteSpace(InputText))
        {
            ResultText = string.Empty;
            HasResult = false;
            HasError = false;
            DmsText = string.Empty;
            HasDms = false;
            return;
        }

        if (!TryParseInput(out var degrees))
        {
            ResultText = string.Empty;
            HasResult = false;
            HasError = true;
            DmsText = string.Empty;
            HasDms = false;
            return;
        }

        HasError = false;

        // The single target result.
        var target = _converter.Convert(degrees, AngleUnit.Degrees, ToUnit);
        ResultText = Format(target);
        HasResult = true;

        // The DMS rendering of the parsed degrees value.
        DmsText = AngleConverter.FormatDms(degrees);
        HasDms = true;

        // The "all units" table — every unit at once.
        foreach (var unit in AngleConverter.Units)
        {
            var converted = _converter.Convert(degrees, AngleUnit.Degrees, unit);
            AllUnits.Add(new AngleUnitResult(UnitName(unit), Format(converted), _clipboard.SetText));
        }
    }

    /// <summary>Parses the input in the selected From unit, accepting DMS only when From is degrees.</summary>
    private bool TryParseInput(out double degrees)
    {
        if (FromUnit == AngleUnit.Degrees)
        {
            return AngleConverter.TryParseDegrees(InputText, out degrees);
        }

        if (TryParseNumber(InputText, out var value))
        {
            degrees = _converter.Convert(value, FromUnit, AngleUnit.Degrees);
            return true;
        }

        degrees = 0;
        return false;
    }

    private static bool TryParseNumber(string text, out double value)
        => double.TryParse(text.Trim().Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out value);

    private string Format(double value)
    {
        var decimals = Math.Clamp((int)Math.Round(Decimals), MinDecimals, MaxDecimals);
        var rounded = Math.Round(value, decimals, MidpointRounding.AwayFromZero);

        // Normalise a -0 result to 0 for display.
        if (rounded == 0)
        {
            rounded = 0;
        }

        return rounded.ToString("F" + decimals.ToString(CultureInfo.InvariantCulture), CultureInfo.CurrentCulture);
    }

    private string UnitName(AngleUnit unit) => _localizer[$"AngleUnit_{unit}"].Value;

    private static int IndexOf(AngleUnit unit)
    {
        for (var i = 0; i < AngleConverter.Units.Count; i++)
        {
            if (AngleConverter.Units[i] == unit)
            {
                return i;
            }
        }

        return 0;
    }

    private static AngleUnit UnitAt(int index)
        => index >= 0 && index < AngleConverter.Units.Count ? AngleConverter.Units[index] : AngleUnit.Degrees;

    /// <summary>Swaps the From and To units (the input is left as typed so the user keeps their value).</summary>
    [RelayCommand]
    private void Swap()
    {
        // Suppress the per-property recompute so the tuple swap doesn't fire Recompute twice
        // (and never momentarily shows From == To); recompute exactly once afterwards.
        _suppressRecompute = true;
        (FromUnitIndex, ToUnitIndex) = (ToUnitIndex, FromUnitIndex);
        _suppressRecompute = false;
        Recompute();
    }

    /// <summary>The text the Copy/Share actions emit: every unit line, or the single result.</summary>
    private string BuildResultText()
        => AllUnits.Count > 0
            ? string.Join(Environment.NewLine, AllUnits.Select(u => $"{u.Name}: {u.Value}"))
            : ResultText;

    [RelayCommand(CanExecute = nameof(HasResult))]
    private void CopyResult() => _clipboard.SetText(BuildResultText());

    [RelayCommand(CanExecute = nameof(HasResult))]
    private async Task ShareResultAsync()
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
