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
/// Coordinate averaging (issue #155). Several GPS readings of the <em>same</em> spot go in, one per
/// line and in any notation; the best-estimate fix comes out, together with how tightly the readings
/// cluster — the farthest reading and the average distance from the fix. The clustering figure is the
/// point of the tool: an average of readings that disagree by 40 m is not a coordinate, it is a hint.
/// Shares its maths with the centroid tool via <see cref="CoordinateMean"/>; the arithmetic mean is
/// shown alongside so a puzzle that expects it still works.
/// </summary>
[Tool("CoordinateAveraging", ToolCategory.Coordinates,
      Introduced = "2026-09-03", Updated = "2026-09-03",
      Keywords = ["averaging", "average", "mean", "readings", "accuracy", "gps", "průměrování", "průměr", "měření", "přesnost", "zaměření"])]
public sealed partial class CoordinateAveragingViewModel : ToolViewModelBase, IToolTextInput
{
    private readonly IStringLocalizer _localizer;
    private readonly IClipboardService _clipboard;
    private readonly IShareService _share;

    /// <summary>A handheld GPS under open sky repeats to a few metres; these split "tight", "usable" and "suspect".</summary>
    private const double TightSpreadMeters = 5.0;
    private const double ModerateSpreadMeters = 15.0;

    /// <summary>Above this the arithmetic mean has drifted somewhere the readings never were.</summary>
    private const double DivergenceWarningMeters = 1.0;

    public CoordinateAveragingViewModel(
        ICatalogService catalog,
        IRecentsService recents,
        IFavoriteToolsService favorites,
        IStringLocalizer localizer,
        IClipboardService clipboard,
        IShareService share)
        : base("CoordinateAveraging", catalog, recents, favorites, localizer)
    {
        _localizer = localizer;
        _clipboard = clipboard;
        _share = share;
    }

    /// <summary>The readings, one per line, mixed notations allowed.</summary>
    [ObservableProperty]
    public partial string InputText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial bool HasResult { get; set; }

    [ObservableProperty]
    public partial string ReadingCountText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial bool HasParseErrors { get; set; }

    [ObservableProperty]
    public partial string ParseErrorMessage { get; set; } = string.Empty;

    /// <summary>The 3D unit-vector mean — the best estimate of the true position.</summary>
    [ObservableProperty]
    public partial string BestEstimateText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string BestEstimateDecimalText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string ArithmeticText { get; set; } = string.Empty;

    /// <summary>Set when the arithmetic mean has drifted away from the fix — the antimeridian case.</summary>
    [ObservableProperty]
    public partial bool HasDivergence { get; set; }

    [ObservableProperty]
    public partial string MaxDistanceText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string MeanDistanceText { get; set; } = string.Empty;

    /// <summary>How much to trust the fix, phrased for the reader and coloured by <see cref="QualitySeverity"/>.</summary>
    [ObservableProperty]
    public partial string QualityMessage { get; set; } = string.Empty;

    [ObservableProperty]
    public partial InfoBarSeverity QualitySeverity { get; set; } = InfoBarSeverity.Informational;

    [ObservableProperty]
    public partial IReadOnlyList<CoordinatePointRow> Readings { get; set; } = [];

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
                _localizer["CoordinateAveragingParseError"].Value,
                string.Join(", ", parsed.Errors.Select(e => e.LineNumber.ToString(CultureInfo.CurrentCulture))))
            : string.Empty;

        if (!CoordinateMean.TryCompute(parsed.Points, out var result))
        {
            ClearOutputs();
            return;
        }

        ReadingCountText = string.Format(CultureInfo.CurrentCulture, _localizer["CoordinateAveragingReadingCount"].Value, result.Count);
        BestEstimateText = CoordinateMeanFormat.Ddm(result.CartesianMean);
        BestEstimateDecimalText = CoordinateMeanFormat.Dd(result.CartesianMean);
        ArithmeticText = CoordinateMeanFormat.Ddm(result.ArithmeticMean);
        MaxDistanceText = CoordinateMeanFormat.Meters(result.MaxDistanceMeters);
        MeanDistanceText = CoordinateMeanFormat.Meters(result.MeanDistanceMeters);
        HasDivergence = result.DivergenceMeters >= DivergenceWarningMeters;
        Readings = CoordinateMeanFormat.BuildRows(result.CartesianMean, parsed.Points);
        UpdateQuality(result.Count, result.MaxDistanceMeters);
        HasResult = true;
    }

    private void UpdateQuality(int readingCount, double maxDistanceMeters)
    {
        // A single reading trivially has a spread of 0 m. Reporting that as "readings agree" is the
        // exact false confidence this verdict exists to prevent.
        if (readingCount < 2)
        {
            QualityMessage = _localizer["CoordinateAveragingQualitySingle"].Value;
            QualitySeverity = InfoBarSeverity.Informational;
            return;
        }

        var (key, severity) = maxDistanceMeters switch
        {
            <= TightSpreadMeters => ("CoordinateAveragingQualityTight", InfoBarSeverity.Success),
            <= ModerateSpreadMeters => ("CoordinateAveragingQualityModerate", InfoBarSeverity.Informational),
            _ => ("CoordinateAveragingQualitySpread", InfoBarSeverity.Warning),
        };

        QualityMessage = string.Format(CultureInfo.CurrentCulture, _localizer[key].Value, MaxDistanceText);
        QualitySeverity = severity;
    }

    private void ClearOutputs()
    {
        HasResult = false;
        ReadingCountText = string.Empty;
        BestEstimateText = string.Empty;
        BestEstimateDecimalText = string.Empty;
        ArithmeticText = string.Empty;
        MaxDistanceText = string.Empty;
        MeanDistanceText = string.Empty;
        HasDivergence = false;
        QualityMessage = string.Empty;
        QualitySeverity = InfoBarSeverity.Informational;
        Readings = [];
    }

    private string BuildSummary()
    {
        StringBuilder builder = new();
        builder.AppendLine($"{_localizer["CoordinateAveragingBestEstimate"].Value}: {BestEstimateText}");
        builder.AppendLine(ReadingCountText);
        builder.AppendLine($"{_localizer["CoordinateAveragingFarthest"].Value}: {MaxDistanceText}");
        builder.Append($"{_localizer["CoordinateAveragingMeanDistance"].Value}: {MeanDistanceText}");
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
