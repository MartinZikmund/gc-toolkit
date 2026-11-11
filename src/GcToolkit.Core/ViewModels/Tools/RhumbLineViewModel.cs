using System.Globalization;
using System.Text;
using System.Threading.Tasks;
using GcToolkit.Core.Catalog;
using GcToolkit.Core.Coordinates;
using GcToolkit.Core.Discovery;
using GcToolkit.Core.FavoriteTools;
using GcToolkit.Core.Recents;
using GcToolkit.Core.Services;
using Microsoft.Extensions.Localization;
using MZikmund.Toolkit.WinUI.Services;

namespace GcToolkit.Core.ViewModels.Tools;

/// <summary>What the Rhumb Line tool remembers between visits.</summary>
public sealed record RhumbLineState(int Mode, string Start, string End, string Bearing, string Distance, int Unit, int Format);

/// <summary>
/// Rhumb line (loxodrome) calculator (issue #265). Inverse mode turns two points into the constant
/// bearing and distance, next to the great-circle figures; direct mode projects a destination from a
/// start, a constant bearing and a distance. The math is <see cref="RhumbLine"/> / <see cref="Geodesy"/>.
/// </summary>
[Tool("RhumbLine", ToolCategory.Coordinates,
      Introduced = "2026-11-11", Updated = "2026-11-11",
      Keywords = ["rhumb", "loxodrome", "constant bearing", "course", "great circle", "orthodrome", "kurz", "loxodroma", "azimut", "ortodroma"])]
public sealed partial class RhumbLineViewModel : ToolViewModelBase
{
    private const string StorageKey = "rhumbline";

    private static readonly DistanceUnit[] _units =
    [
        DistanceUnit.Meter, DistanceUnit.Kilometer, DistanceUnit.Feet, DistanceUnit.Yard, DistanceUnit.Mile, DistanceUnit.NauticalMile,
    ];

    private static readonly string[] _unitSuffixes = ["m", "km", "ft", "yd", "mi", "nmi"];

    private static readonly CoordinateFormat[] _formats =
    [
        CoordinateFormat.DegreesDecimalMinutes, CoordinateFormat.DecimalDegrees, CoordinateFormat.DegreesMinutesSeconds,
        CoordinateFormat.Utm, CoordinateFormat.Mgrs,
    ];

    private readonly IStringLocalizer _localizer;
    private readonly IClipboardService _clipboard;
    private readonly IShareService _share;
    private readonly IPreferences _preferences;
    private bool _suspend;

    public RhumbLineViewModel(
        ICatalogService catalog,
        IRecentsService recents,
        IFavoriteToolsService favorites,
        IStringLocalizer localizer,
        IClipboardService clipboard,
        IShareService share,
        IPreferences preferences)
        : base("RhumbLine", catalog, recents, favorites, localizer)
    {
        _localizer = localizer;
        _clipboard = clipboard;
        _share = share;
        _preferences = preferences;
        Restore();
    }

    /// <summary>0 = two points to bearing and distance, 1 = start, bearing and distance to destination.</summary>
    [ObservableProperty]
    public partial int ModeIndex { get; set; }

    [ObservableProperty]
    public partial string StartText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string EndText { get; set; } = string.Empty;

    /// <summary>Direct mode: constant bearing in degrees clockwise from north (parsed invariantly).</summary>
    [ObservableProperty]
    public partial string BearingText { get; set; } = string.Empty;

    /// <summary>Direct mode: the distance in the selected unit (parsed invariantly).</summary>
    [ObservableProperty]
    public partial string DistanceText { get; set; } = string.Empty;

    /// <summary>0 m, 1 km, 2 ft, 3 yd, 4 mi, 5 nmi.</summary>
    [ObservableProperty]
    public partial int UnitIndex { get; set; } = 1;

    /// <summary>0 DDM, 1 DD, 2 DMS, 3 UTM, 4 MGRS: how computed coordinates are shown.</summary>
    [ObservableProperty]
    public partial int FormatIndex { get; set; }

    [ObservableProperty]
    public partial bool HasResult { get; set; }

    [ObservableProperty]
    public partial bool HasError { get; set; }

    [ObservableProperty]
    public partial string ErrorMessage { get; set; } = string.Empty;

    [ObservableProperty]
    public partial bool HasNote { get; set; }

    [ObservableProperty]
    public partial string NoteMessage { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string RhumbDistanceResult { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string RhumbBearingResult { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string GreatCircleDistanceResult { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string GreatCircleInitialBearingResult { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string GreatCircleFinalBearingResult { get; set; } = string.Empty;

    /// <summary>How much longer the rhumb line is than the great circle.</summary>
    [ObservableProperty]
    public partial string ExtraDistanceResult { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string DestinationResult { get; set; } = string.Empty;

    public bool IsInverse => ModeIndex == 0;

    public bool IsDirect => ModeIndex == 1;

    private DistanceUnit SelectedUnit => _units[Math.Clamp(UnitIndex, 0, _units.Length - 1)];

    private string UnitSuffix => _unitSuffixes[Math.Clamp(UnitIndex, 0, _unitSuffixes.Length - 1)];

    partial void OnModeIndexChanged(int value)
    {
        OnPropertyChanged(nameof(IsInverse));
        OnPropertyChanged(nameof(IsDirect));
        Recompute();
    }

    partial void OnStartTextChanged(string value) => Recompute();

    partial void OnEndTextChanged(string value) => Recompute();

    partial void OnBearingTextChanged(string value) => Recompute();

    partial void OnDistanceTextChanged(string value) => Recompute();

    partial void OnUnitIndexChanged(int value) => Recompute();

    partial void OnFormatIndexChanged(int value) => Recompute();

    partial void OnHasResultChanged(bool value)
    {
        CopyReportCommand.NotifyCanExecuteChanged();
        ShareReportCommand.NotifyCanExecuteChanged();
    }

    private void Recompute()
    {
        if (_suspend)
        {
            return;
        }

        Save();
        ClearResults();
        HasError = false;
        HasNote = false;

        if (IsInverse)
        {
            ComputeInverse();
        }
        else
        {
            ComputeDirect();
        }
    }

    private void ComputeInverse()
    {
        var startEntered = !string.IsNullOrWhiteSpace(StartText);
        var endEntered = !string.IsNullOrWhiteSpace(EndText);
        if (!startEntered && !endEntered)
        {
            return;
        }

        var start = default(GeoCoordinate);
        var end = default(GeoCoordinate);
        var startOk = startEntered && CoordinateParser.TryParse(StartText, out start, out _);
        var endOk = endEntered && CoordinateParser.TryParse(EndText, out end, out _);
        if ((startEntered && !startOk) || (endEntered && !endOk))
        {
            Fail(startEntered && !startOk ? "RhumbLineErrorStart" : "RhumbLineErrorEnd");
            return;
        }

        if (!startOk || !endOk)
        {
            return;
        }

        if (start == end)
        {
            Fail("RhumbLineErrorSamePoint");
            return;
        }

        var rhumb = RhumbLine.Inverse(start, end);
        var greatCircle = Geodesy.DistanceMeters(start, end);

        RhumbDistanceResult = FormatDistance(rhumb.DistanceMeters);
        RhumbBearingResult = FormatBearing(rhumb.BearingDegrees);
        GreatCircleDistanceResult = FormatDistance(greatCircle);
        GreatCircleInitialBearingResult = FormatBearing(Geodesy.InitialBearingDegrees(start, end));
        GreatCircleFinalBearingResult = FormatBearing(Geodesy.FinalBearingDegrees(start, end));

        var extra = rhumb.DistanceMeters - greatCircle;
        var percent = greatCircle > 0 ? extra / greatCircle * 100.0 : 0.0;
        ExtraDistanceResult = $"+{FormatDistance(Math.Max(extra, 0.0))} ({Math.Max(percent, 0.0).ToString("N2", CultureInfo.CurrentCulture)} %)";

        HasResult = true;
    }

    private void ComputeDirect()
    {
        if (string.IsNullOrWhiteSpace(StartText) && string.IsNullOrWhiteSpace(BearingText) && string.IsNullOrWhiteSpace(DistanceText))
        {
            return;
        }

        if (!CoordinateParser.TryParse(StartText, out var start, out _))
        {
            Fail("RhumbLineErrorStart");
            return;
        }

        if (!TryParseInvariant(BearingText, out var bearing))
        {
            Fail("RhumbLineErrorBearing");
            return;
        }

        if (!TryParseInvariant(DistanceText, out var distance) || distance < 0.0)
        {
            Fail("RhumbLineErrorDistance");
            return;
        }

        var meters = DistanceUnits.ToMeters(distance, SelectedUnit);
        var result = RhumbLine.Direct(start, bearing, meters);

        DestinationResult = CoordinateFormatter.Format(result.Destination, _formats[Math.Clamp(FormatIndex, 0, _formats.Length - 1)]);

        if (result.ReachedPole)
        {
            NoteMessage = _localizer["RhumbLineNotePole"].Value;
            HasNote = true;
        }
        else if (Math.Abs(start.Latitude) == 90.0 && Math.Abs(Math.Cos(bearing * Math.PI / 180.0)) < 1e-12)
        {
            NoteMessage = _localizer["RhumbLineNotePoleEastWest"].Value;
            HasNote = true;
        }

        HasResult = true;
    }

    private void Fail(string key)
    {
        ErrorMessage = _localizer[key].Value;
        HasError = true;
    }

    private void ClearResults()
    {
        HasResult = false;
        RhumbDistanceResult = string.Empty;
        RhumbBearingResult = string.Empty;
        GreatCircleDistanceResult = string.Empty;
        GreatCircleInitialBearingResult = string.Empty;
        GreatCircleFinalBearingResult = string.Empty;
        ExtraDistanceResult = string.Empty;
        DestinationResult = string.Empty;
    }

    private static bool TryParseInvariant(string text, out double value)
        => double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out value) && double.IsFinite(value);

    private string FormatDistance(double meters)
    {
        var value = DistanceUnits.FromMeters(meters, SelectedUnit);
        var format = SelectedUnit is DistanceUnit.Meter or DistanceUnit.Feet or DistanceUnit.Yard ? "N1" : "N3";
        return $"{value.ToString(format, CultureInfo.CurrentCulture)} {UnitSuffix}";
    }

    // 16-point label and NATO mils (6400 per circle) next to the degrees.
    private string FormatBearing(double degrees)
    {
        var point = _localizer[CompassViewModel._cardinalKeys[(int)Math.Round(degrees / 22.5, MidpointRounding.AwayFromZero) % 16]].Value;
        var mils = Math.Round(degrees / 360.0 * 6400.0) % 6400.0;
        return $"{degrees.ToString("N2", CultureInfo.CurrentCulture)}° {point} ({mils.ToString("N0", CultureInfo.CurrentCulture)} mil)";
    }

    private string BuildReport()
    {
        StringBuilder builder = new();
        if (IsInverse)
        {
            builder.AppendLine($"{_localizer["RhumbLineStart"].Value}: {StartText}");
            builder.AppendLine($"{_localizer["RhumbLineEnd"].Value}: {EndText}");
            builder.AppendLine($"{_localizer["RhumbLineRhumbDistance"].Value}: {RhumbDistanceResult}");
            builder.AppendLine($"{_localizer["RhumbLineRhumbBearing"].Value}: {RhumbBearingResult}");
            builder.AppendLine($"{_localizer["RhumbLineGreatCircleDistance"].Value}: {GreatCircleDistanceResult}");
            builder.AppendLine($"{_localizer["RhumbLineGreatCircleInitialBearing"].Value}: {GreatCircleInitialBearingResult}");
            builder.AppendLine($"{_localizer["RhumbLineGreatCircleFinalBearing"].Value}: {GreatCircleFinalBearingResult}");
            builder.Append($"{_localizer["RhumbLineExtraDistance"].Value}: {ExtraDistanceResult}");
        }
        else
        {
            builder.AppendLine($"{_localizer["RhumbLineStart"].Value}: {StartText}");
            builder.AppendLine($"{_localizer["RhumbLineBearing"].Value}: {BearingText}°");
            builder.AppendLine($"{_localizer["RhumbLineDistance"].Value}: {DistanceText} {UnitSuffix}");
            builder.Append($"{_localizer["RhumbLineDestination"].Value}: {DestinationResult}");
        }

        return builder.ToString();
    }

    [RelayCommand(CanExecute = nameof(HasResult))]
    private void CopyReport() => _clipboard.SetText(BuildReport());

    [RelayCommand]
    private void CopyText(string? text)
    {
        if (!string.IsNullOrEmpty(text))
        {
            _clipboard.SetText(text);
        }
    }

    [RelayCommand(CanExecute = nameof(HasResult))]
    private async Task ShareReportAsync()
    {
        try
        {
            await _share.ShareTextAsync(ToolName, BuildReport());
        }
        catch (Exception)
        {
            // Sharing is best-effort; a platform share failure must not crash the tool.
        }
    }

    /// <summary>Swaps start and end: the return leg is one tap away.</summary>
    [RelayCommand]
    private void Swap()
    {
        _suspend = true;
        (StartText, EndText) = (EndText, StartText);
        _suspend = false;
        Recompute();
    }

    [RelayCommand]
    private void Clear()
    {
        _suspend = true;
        StartText = string.Empty;
        EndText = string.Empty;
        BearingText = string.Empty;
        DistanceText = string.Empty;
        _suspend = false;
        Recompute();
    }

    /// <summary>Fills in a sample so the tool can be tried straight away.</summary>
    [RelayCommand]
    private void LoadExample()
    {
        _suspend = true;
        if (IsInverse)
        {
            StartText = "N 40° 36.000' W 073° 48.000'";
            EndText = "N 51° 36.000' W 000° 30.000'";
        }
        else
        {
            StartText = "N 49° 12.000' E 016° 36.000'";
            BearingText = "300";
            DistanceText = "500";
            UnitIndex = 1;
        }

        _suspend = false;
        Recompute();
    }

    private void Restore()
    {
        RhumbLineState? state;
        try
        {
            state = _preferences.GetComplex<RhumbLineState?>(StorageKey, null);
        }
        catch (Exception)
        {
            return;
        }

        if (state is null)
        {
            return;
        }

        _suspend = true;
        ModeIndex = Math.Clamp(state.Mode, 0, 1);
        StartText = state.Start ?? string.Empty;
        EndText = state.End ?? string.Empty;
        BearingText = state.Bearing ?? string.Empty;
        DistanceText = state.Distance ?? string.Empty;
        UnitIndex = Math.Clamp(state.Unit, 0, _units.Length - 1);
        FormatIndex = Math.Clamp(state.Format, 0, _formats.Length - 1);
        _suspend = false;
        Recompute();
    }

    private void Save()
        => _preferences.SetComplex(StorageKey, new RhumbLineState(ModeIndex, StartText, EndText, BearingText, DistanceText, UnitIndex, FormatIndex));
}
