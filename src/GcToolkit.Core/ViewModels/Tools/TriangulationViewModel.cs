using System.Globalization;
using System.Threading.Tasks;
using GcToolkit.Core.Catalog;
using GcToolkit.Core.Coordinates;
using GcToolkit.Core.Discovery;
using GcToolkit.Core.FavoriteTools;
using GcToolkit.Core.Recents;
using GcToolkit.Core.Services;
using Microsoft.Extensions.Localization;

namespace GcToolkit.Core.ViewModels.Tools;

/// <summary>
/// Triangulation (issue #298): two known points, each with a bearing sighted from it, give the
/// unknown point where the two lines cross. Recomputed live; the math is in <see cref="Triangulation"/>.
/// </summary>
[Tool("Triangulation", ToolCategory.Coordinates,
      Introduced = "2026-11-11", Updated = "2026-11-11",
      Keywords = ["triangulation", "intersection", "bearing", "azimuth", "resection", "crossing", "triangulace", "průsečík", "azimut", "směr"])]
public sealed partial class TriangulationViewModel : ToolViewModelBase
{
    private readonly IClipboardService _clipboard;
    private readonly IShareService _share;
    private readonly IStringLocalizer _localizer;

    public TriangulationViewModel(
        ICatalogService catalog,
        IRecentsService recents,
        IFavoriteToolsService favorites,
        IStringLocalizer localizer,
        IClipboardService clipboard,
        IShareService share)
        : base("Triangulation", catalog, recents, favorites, localizer)
    {
        _clipboard = clipboard;
        _share = share;
        _localizer = localizer;
    }

    [ObservableProperty]
    public partial string CoordinateA { get; set; } = string.Empty;

    /// <summary>Bearing sighted from point A, degrees clockwise from true North.</summary>
    [ObservableProperty]
    public partial string BearingA { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string CoordinateB { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string BearingB { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string DecimalDegrees { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string DegreesDecimalMinutes { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string DegreesMinutesSeconds { get; set; } = string.Empty;

    /// <summary>The antipodal crossing, as DDM.</summary>
    [ObservableProperty]
    public partial string AlternateDegreesDecimalMinutes { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string DistanceFromA { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string DistanceFromB { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string BearingToResultFromA { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string BearingToResultFromB { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string BaselineDistance { get; set; } = string.Empty;

    [ObservableProperty]
    public partial bool HasResult { get; set; }

    [ObservableProperty]
    public partial bool HasAlternate { get; set; }

    [ObservableProperty]
    public partial bool HasWarning { get; set; }

    [ObservableProperty]
    public partial string WarningMessage { get; set; } = string.Empty;

    [ObservableProperty]
    public partial bool HasError { get; set; }

    [ObservableProperty]
    public partial string ErrorMessage { get; set; } = string.Empty;

    partial void OnCoordinateAChanged(string value) => Recompute();

    partial void OnBearingAChanged(string value) => Recompute();

    partial void OnCoordinateBChanged(string value) => Recompute();

    partial void OnBearingBChanged(string value) => Recompute();

    partial void OnHasResultChanged(bool value)
    {
        CopyOutputCommand.NotifyCanExecuteChanged();
        ShareOutputCommand.NotifyCanExecuteChanged();
    }

    private void Recompute()
    {
        if (string.IsNullOrWhiteSpace(CoordinateA) && string.IsNullOrWhiteSpace(BearingA) &&
            string.IsNullOrWhiteSpace(CoordinateB) && string.IsNullOrWhiteSpace(BearingB))
        {
            ClearResult();
            HasError = false;
            ErrorMessage = string.Empty;
            return;
        }

        if (!CoordinateParser.TryParse(CoordinateA, out var a, out _))
        {
            Fail("TriangulationErrorPointA");
            return;
        }

        if (!TryParseInvariant(BearingA, out var bearingA))
        {
            Fail("TriangulationErrorBearingA");
            return;
        }

        if (!CoordinateParser.TryParse(CoordinateB, out var b, out _))
        {
            Fail("TriangulationErrorPointB");
            return;
        }

        if (!TryParseInvariant(BearingB, out var bearingB))
        {
            Fail("TriangulationErrorBearingB");
            return;
        }

        var result = Triangulation.Intersect(a, bearingA, b, bearingB);
        if (result.Primary is not { } primary)
        {
            Fail(result.Status switch
            {
                TriangulationStatus.Coincident => "TriangulationErrorCoincident",
                TriangulationStatus.IdenticalPoints => "TriangulationErrorIdentical",
                _ => "TriangulationErrorPointA",
            });
            return;
        }

        DecimalDegrees = CoordinateFormatter.Format(primary.Point, CoordinateFormat.DecimalDegrees);
        DegreesDecimalMinutes = CoordinateFormatter.Format(primary.Point, CoordinateFormat.DegreesDecimalMinutes);
        DegreesMinutesSeconds = CoordinateFormatter.Format(primary.Point, CoordinateFormat.DegreesMinutesSeconds);

        if (result.Alternate is { } alternate)
        {
            AlternateDegreesDecimalMinutes = CoordinateFormatter.Format(alternate.Point, CoordinateFormat.DegreesDecimalMinutes);
            HasAlternate = true;
        }

        DistanceFromA = FormatDistance(primary.DistanceFromAMeters);
        DistanceFromB = FormatDistance(primary.DistanceFromBMeters);
        BearingToResultFromA = FormatBearing(primary.BearingFromA);
        BearingToResultFromB = FormatBearing(primary.BearingFromB);
        BaselineDistance = FormatDistance(result.BaselineMeters);

        List<string> warnings = new();
        if (!primary.IsAheadOfA)
        {
            warnings.Add(_localizer["TriangulationWarnBehindA"].Value);
        }

        if (!primary.IsAheadOfB)
        {
            warnings.Add(_localizer["TriangulationWarnBehindB"].Value);
        }

        if (result.IsIllConditioned)
        {
            warnings.Add(_localizer["TriangulationWarnIllConditioned"].Value);
        }

        WarningMessage = string.Join(Environment.NewLine, warnings);
        HasWarning = warnings.Count > 0;

        HasError = false;
        ErrorMessage = string.Empty;
        HasResult = true;
    }

    private void Fail(string messageKey)
    {
        ClearResult();
        ErrorMessage = _localizer[messageKey].Value;
        HasError = true;
    }

    private void ClearResult()
    {
        DecimalDegrees = string.Empty;
        DegreesDecimalMinutes = string.Empty;
        DegreesMinutesSeconds = string.Empty;
        AlternateDegreesDecimalMinutes = string.Empty;
        DistanceFromA = string.Empty;
        DistanceFromB = string.Empty;
        BearingToResultFromA = string.Empty;
        BearingToResultFromB = string.Empty;
        BaselineDistance = string.Empty;
        WarningMessage = string.Empty;
        HasWarning = false;
        HasAlternate = false;
        HasResult = false;
    }

    private static bool TryParseInvariant(string text, out double value)
        => double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out value) && double.IsFinite(value);

    private static string FormatDistance(double meters)
        => meters < 1000.0
            ? string.Format(CultureInfo.CurrentCulture, "{0:0.#} m", meters)
            : string.Format(CultureInfo.CurrentCulture, "{0:0.###} km", meters / 1000.0);

    private static string FormatBearing(double degrees)
        => string.Format(CultureInfo.CurrentCulture, "{0:0.0}°", degrees);

    [RelayCommand(CanExecute = nameof(HasResult))]
    private void CopyOutput() => _clipboard.SetText(DegreesDecimalMinutes);

    [RelayCommand]
    private void CopyText(string? text)
    {
        if (!string.IsNullOrEmpty(text))
        {
            _clipboard.SetText(text);
        }
    }

    [RelayCommand(CanExecute = nameof(HasResult))]
    private async Task ShareOutputAsync()
    {
        try
        {
            await _share.ShareTextAsync(
                ToolName,
                string.Join(Environment.NewLine, DegreesDecimalMinutes, DecimalDegrees, DegreesMinutesSeconds));
        }
        catch (Exception)
        {
            // Sharing is best-effort; a platform share failure must not crash the tool.
        }
    }

    // Veness' published intersection example, in DDM.
    [RelayCommand]
    private void LoadExample()
    {
        CoordinateA = "N 51 53.118 E 000 15.270";
        BearingA = "108.55";
        CoordinateB = "N 49 00.204 E 002 34.410";
        BearingB = "32.44";
    }

    [RelayCommand]
    private void Clear()
    {
        CoordinateA = string.Empty;
        BearingA = string.Empty;
        CoordinateB = string.Empty;
        BearingB = string.Empty;
    }
}
