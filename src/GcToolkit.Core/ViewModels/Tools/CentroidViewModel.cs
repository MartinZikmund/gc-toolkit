using System.Globalization;
using System.Text;
using GcToolkit.Core.Catalog;
using GcToolkit.Core.Coordinates;
using GcToolkit.Core.Discovery;
using GcToolkit.Core.FavoriteTools;
using GcToolkit.Core.Recents;
using GcToolkit.Core.Services;
using Microsoft.Extensions.Localization;

namespace GcToolkit.Core.ViewModels.Tools;

/// <summary>
/// One row of the per-point table both coordinate-mean tools render: the point as entered (normalised
/// to the geocaching notation) plus where it sits relative to the mean. A plain record — the whole
/// result is copied from the action bar, so no per-row command is needed.
/// </summary>
public sealed record CoordinatePointRow(int Number, string Coordinate, string Distance, string Bearing);

/// <summary>Display formatting shared by the centroid and averaging tools.</summary>
internal static class CoordinateMeanFormat
{
    /// <summary>Metres under a kilometre, kilometres above — geocachers read both, never "0.012 km".</summary>
    public static string Meters(double meters) => meters < 1000.0
        ? $"{meters.ToString("N1", CultureInfo.CurrentCulture)} m"
        : $"{(meters / 1000.0).ToString("N3", CultureInfo.CurrentCulture)} km";

    public static string Bearing(double degrees) => $"{degrees.ToString("N1", CultureInfo.CurrentCulture)}°";

    /// <summary>Degrees and decimal minutes — the geocaching norm, and the tools' primary readout.</summary>
    public static string Ddm(GeoCoordinate c) => CoordinateFormatter.Format(c, CoordinateFormat.DegreesDecimalMinutes);

    public static string Dd(GeoCoordinate c) => CoordinateFormatter.Format(c, CoordinateFormat.DecimalDegrees);

    public static IReadOnlyList<CoordinatePointRow> BuildRows(GeoCoordinate centre, IReadOnlyList<GeoCoordinate> points)
        => [.. points.Select((p, i) => new CoordinatePointRow(
            i + 1,
            Ddm(p),
            Meters(Geodesy.DistanceMeters(centre, p)),
            Bearing(Geodesy.InitialBearingDegrees(centre, p))))];
}

/// <summary>
/// Centroid of a set of points (issue #141). Takes any number of coordinates, one per line and in any
/// notation, and reports the geographic centre two ways: the 3D unit-vector mean (correct across the
/// antimeridian and for widely separated points) and the plain arithmetic mean of the decimal degrees
/// (what several puzzle generators assume). When the two disagree — the antimeridian case — the
/// divergence is called out rather than hidden. Beyond the usual reference tools: both means side by
/// side, the spread of the set, and a per-point table of distance and bearing from the centre.
/// All math lives in <see cref="CoordinateMean"/>; this VM only formats it.
/// </summary>
[Tool("Centroid", ToolCategory.Coordinates,
      Introduced = "2026-09-03", Updated = "2026-09-03",
      Keywords = ["centroid", "center", "centre", "mean", "average", "middle", "těžiště", "střed", "průměr", "geografický střed", "body"])]
public sealed partial class CentroidViewModel : ToolViewModelBase, IToolTextInput
{
    private readonly IStringLocalizer _localizer;
    private readonly IClipboardService _clipboard;
    private readonly IShareService _share;

    /// <summary>Above this the two means are far enough apart to be worth warning about.</summary>
    private const double DivergenceWarningMeters = 1.0;

    public CentroidViewModel(
        ICatalogService catalog,
        IRecentsService recents,
        IFavoriteToolsService favorites,
        IStringLocalizer localizer,
        IClipboardService clipboard,
        IShareService share)
        : base("Centroid", catalog, recents, favorites, localizer)
    {
        _localizer = localizer;
        _clipboard = clipboard;
        _share = share;
    }

    /// <summary>The coordinate list, one per line, mixed notations allowed.</summary>
    [ObservableProperty]
    public partial string InputText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial bool HasResult { get; set; }

    /// <summary>"5 points" — reassures the user the parser saw what they pasted.</summary>
    [ObservableProperty]
    public partial string PointCountText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial bool HasParseErrors { get; set; }

    [ObservableProperty]
    public partial string ParseErrorMessage { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string GeodesicText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string GeodesicDecimalText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string ArithmeticText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string ArithmeticDecimalText { get; set; } = string.Empty;

    /// <summary>Distance between the two means — visible only when they actually differ.</summary>
    [ObservableProperty]
    public partial string DivergenceText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial bool HasDivergence { get; set; }

    [ObservableProperty]
    public partial string FarthestText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string MeanDistanceText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial IReadOnlyList<CoordinatePointRow> Points { get; set; } = [];

    partial void OnInputTextChanged(string value) => Recompute();

    partial void OnHasResultChanged(bool value)
    {
        CopyOutputCommand.NotifyCanExecuteChanged();
        ShareOutputCommand.NotifyCanExecuteChanged();
    }

    private void Recompute()
    {
        var parsed = CoordinateMean.ParseLines(InputText);

        HasParseErrors = parsed.Errors.Count > 0;
        ParseErrorMessage = HasParseErrors
            ? string.Format(
                CultureInfo.CurrentCulture,
                _localizer["CentroidParseError"].Value,
                string.Join(", ", parsed.Errors.Select(e => e.LineNumber.ToString(CultureInfo.CurrentCulture))))
            : string.Empty;

        if (!CoordinateMean.TryCompute(parsed.Points, out var result))
        {
            ClearOutputs();
            return;
        }

        PointCountText = string.Format(CultureInfo.CurrentCulture, _localizer["CentroidPointCount"].Value, result.Count);
        GeodesicText = CoordinateMeanFormat.Ddm(result.CartesianMean);
        GeodesicDecimalText = CoordinateMeanFormat.Dd(result.CartesianMean);
        ArithmeticText = CoordinateMeanFormat.Ddm(result.ArithmeticMean);
        ArithmeticDecimalText = CoordinateMeanFormat.Dd(result.ArithmeticMean);
        FarthestText = CoordinateMeanFormat.Meters(result.MaxDistanceMeters);
        MeanDistanceText = CoordinateMeanFormat.Meters(result.MeanDistanceMeters);
        DivergenceText = CoordinateMeanFormat.Meters(result.DivergenceMeters);
        HasDivergence = result.DivergenceMeters >= DivergenceWarningMeters;
        Points = CoordinateMeanFormat.BuildRows(result.CartesianMean, parsed.Points);
        HasResult = true;
    }

    private void ClearOutputs()
    {
        HasResult = false;
        PointCountText = string.Empty;
        GeodesicText = string.Empty;
        GeodesicDecimalText = string.Empty;
        ArithmeticText = string.Empty;
        ArithmeticDecimalText = string.Empty;
        FarthestText = string.Empty;
        MeanDistanceText = string.Empty;
        DivergenceText = string.Empty;
        HasDivergence = false;
        Points = [];
    }

    private string BuildSummary()
    {
        StringBuilder builder = new();
        builder.AppendLine(PointCountText);
        builder.AppendLine($"{_localizer["CentroidGeodesicMean"].Value}: {GeodesicText}");
        builder.AppendLine($"{_localizer["CentroidArithmeticMean"].Value}: {ArithmeticText}");
        builder.AppendLine($"{_localizer["CentroidFarthest"].Value}: {FarthestText}");
        builder.Append($"{_localizer["CentroidMeanDistance"].Value}: {MeanDistanceText}");
        return builder.ToString();
    }

    [RelayCommand(CanExecute = nameof(HasResult))]
    private void CopyOutput() => _clipboard.SetText(BuildSummary());

    [RelayCommand(CanExecute = nameof(HasResult))]
    private async Task ShareOutputAsync()
    {
        try
        {
            await _share.ShareTextAsync(ToolName, BuildSummary());
        }
        catch (Exception)
        {
            // Sharing is best-effort; a platform share failure must not crash the tool.
        }
    }

    [RelayCommand]
    private void Clear() => InputText = string.Empty;
}
