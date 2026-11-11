using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Globalization;
using System.Threading.Tasks;
using GcToolkit.Core.Catalog;
using GcToolkit.Core.Coordinates;
using GcToolkit.Core.Coordinates.Magnetic;
using GcToolkit.Core.FavoriteTools;
using GcToolkit.Core.Recents;
using GcToolkit.Core.Services;

namespace GcToolkit.Core.ViewModels.Tools;

/// <summary>
/// The shared engine of the bearing tools (Triangulation, Resection): 2..N point + bearing rows, optional
/// magnetic-to-true conversion (manual or offline WMM2025 declination), the fix with its residuals, and the
/// schematic plot. Subclasses decide what the bearings mean via <see cref="Solve"/> and supply an example.
/// Localized keys are looked up as <c>{KeyPrefix}Error...</c>/<c>{KeyPrefix}Warn...</c> so each tool words its own messages.
/// </summary>
public abstract partial class BearingFixViewModelBase : ToolViewModelBase
{
    private const int MinimumRows = 2;

    private readonly IStringLocalizer _localizer;
    private readonly IClipboardService _clipboard;
    private readonly IShareService _share;
    private bool _suspendRecompute;

    protected BearingFixViewModelBase(
        string toolId,
        ICatalogService catalog,
        IRecentsService recents,
        IFavoriteToolsService favorites,
        IStringLocalizer localizer,
        IClipboardService clipboard,
        IShareService share)
        : base(toolId, catalog, recents, favorites, localizer)
    {
        _localizer = localizer;
        _clipboard = clipboard;
        _share = share;

        Rows.CollectionChanged += OnRowsChanged;
        ResetRows(MinimumRows);
    }

    /// <summary>Prefix of the tool-specific resource keys, e.g. <c>Triangulation</c>.</summary>
    protected abstract string KeyPrefix { get; }

    /// <summary>Turns true-bearing observations into a fix.</summary>
    protected abstract BearingFix Solve(IReadOnlyList<BearingObservation> observations);

    /// <summary>The line drawn from each row's point; by default the entered bearing itself.</summary>
    protected virtual BearingObservation PlotLine(BearingObservation observation) => observation;

    /// <summary>Example rows: (coordinate, bearing).</summary>
    protected abstract IReadOnlyList<(string Coordinate, string Bearing)> ExampleRows { get; }

    public ObservableCollection<BearingObservationRow> Rows { get; } = new();

    // ---- Magnetic declination -------------------------------------------------

    /// <summary>The bearings were read off a compass, so they need converting to true North.</summary>
    [ObservableProperty]
    public partial bool BearingsAreMagnetic { get; set; }

    /// <summary>Compute the declination per point from WMM2025; otherwise use <see cref="ManualDeclination"/>.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(UseManualDeclination))]
    public partial bool UseAutomaticDeclination { get; set; } = true;

    public bool UseManualDeclination
    {
        get => !UseAutomaticDeclination;
        set => UseAutomaticDeclination = !value;
    }

    /// <summary>Degrees, East positive; a trailing E/W is also accepted.</summary>
    [ObservableProperty]
    public partial string ManualDeclination { get; set; } = string.Empty;

    /// <summary>The date the compass bearings were taken, for the magnetic model.</summary>
    [ObservableProperty]
    public partial DateTimeOffset? DeclinationDate { get; set; } = DateTimeOffset.Now;

    // ---- Results ----------------------------------------------------------------

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
    public partial bool HasAlternate { get; set; }

    /// <summary>RMS distance of the lines from the fix; only meaningful with three or more lines.</summary>
    [ObservableProperty]
    public partial string AccuracyText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial bool HasAccuracy { get; set; }

    /// <summary>Distance between the two points, for the two-row case.</summary>
    [ObservableProperty]
    public partial string BaselineDistance { get; set; } = string.Empty;

    [ObservableProperty]
    public partial bool HasBaseline { get; set; }

    [ObservableProperty]
    public partial bool HasResult { get; set; }

    [ObservableProperty]
    public partial bool HasWarning { get; set; }

    [ObservableProperty]
    public partial string WarningMessage { get; set; } = string.Empty;

    [ObservableProperty]
    public partial bool HasError { get; set; }

    [ObservableProperty]
    public partial string ErrorMessage { get; set; } = string.Empty;

    /// <summary>The schematic: whatever rows parse, plus the fix once there is one.</summary>
    [ObservableProperty]
    public partial BearingPlotGeometry? Plot { get; set; }

    [ObservableProperty]
    public partial bool HasPlot { get; set; }

    partial void OnBearingsAreMagneticChanged(bool value) => Recompute();

    partial void OnUseAutomaticDeclinationChanged(bool value) => Recompute();

    partial void OnManualDeclinationChanged(string value) => Recompute();

    partial void OnDeclinationDateChanged(DateTimeOffset? value) => Recompute();

    partial void OnHasResultChanged(bool value)
    {
        CopyOutputCommand.NotifyCanExecuteChanged();
        ShareOutputCommand.NotifyCanExecuteChanged();
    }

    // ---- Rows -------------------------------------------------------------------

    [RelayCommand]
    private void AddRow() => Rows.Add(new BearingObservationRow(RemoveRow));

    private void RemoveRow(BearingObservationRow row)
    {
        if (Rows.Count > MinimumRows)
        {
            Rows.Remove(row);
        }
    }

    private void ResetRows(int count)
    {
        _suspendRecompute = true;
        ResetRowsCore(count);
        _suspendRecompute = false;
        Relabel();
        Recompute();
    }

    private void OnRowsChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        foreach (var row in e.OldItems?.OfType<BearingObservationRow>() ?? [])
        {
            row.PropertyChanged -= OnRowPropertyChanged;
        }

        foreach (var row in e.NewItems?.OfType<BearingObservationRow>() ?? [])
        {
            row.PropertyChanged += OnRowPropertyChanged;
        }

        if (!_suspendRecompute)
        {
            Relabel();
            Recompute();
        }
    }

    private void OnRowPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(BearingObservationRow.Coordinate) or nameof(BearingObservationRow.Bearing))
        {
            Recompute();
        }
    }

    private void Relabel()
    {
        for (var i = 0; i < Rows.Count; i++)
        {
            var row = Rows[i];
            row.Label = BearingPlot.Label(i);
            row.CoordinateHeader = Format($"{KeyPrefix}PointHeader", row.Label);
            row.BearingHeader = Format($"{KeyPrefix}BearingHeader", row.Label);
            row.CanRemove = Rows.Count > MinimumRows;
        }
    }

    // ---- Computation ------------------------------------------------------------

    private void Recompute()
    {
        if (_suspendRecompute)
        {
            return;
        }

        ClearResult();
        foreach (var row in Rows)
        {
            row.DeclinationText = string.Empty;
            row.HasDeclination = false;
        }

        var filled = Rows.Where(r => !r.IsBlank).ToList();
        if (filled.Count == 0)
        {
            return;
        }

        List<string> warnings = new();
        double? manualDeclination = null;
        if (BearingsAreMagnetic && UseManualDeclination)
        {
            if (!TryParseDeclination(ManualDeclination, out var d))
            {
                Fail(_localizer["BearingFixErrorDeclination"].Value);
                return;
            }

            manualDeclination = d;
        }

        var decimalYear = WorldMagneticModel.DecimalYear(DateOnly.FromDateTime((DeclinationDate ?? DateTimeOffset.Now).Date));
        if (BearingsAreMagnetic && UseAutomaticDeclination && !WorldMagneticModel.Wmm2025.IsValidFor(decimalYear))
        {
            warnings.Add(_localizer["BearingFixWarnModelRange"].Value);
        }

        List<BearingObservation> observations = new();
        List<BearingObservationRow> used = new();
        string? error = null;
        foreach (var row in filled)
        {
            if (!CoordinateParser.TryParse(row.Coordinate, out var point, out _))
            {
                error ??= Format($"{KeyPrefix}ErrorPoint", row.Label);
                continue;
            }

            if (!TryParseInvariant(row.Bearing, out var bearing))
            {
                error ??= Format($"{KeyPrefix}ErrorBearing", row.Label);
                continue;
            }

            if (BearingsAreMagnetic)
            {
                var declination = manualDeclination
                    ?? WorldMagneticModel.Wmm2025.DeclinationDegrees(point.Latitude, point.Longitude, decimalYear);
                bearing = Normalize360(bearing + declination);
                row.DeclinationText = string.Format(
                    CultureInfo.CurrentCulture,
                    _localizer["BearingFixDeclinationApplied"].Value,
                    FormatDeclination(declination),
                    FormatBearing(bearing));
                row.HasDeclination = true;
            }

            observations.Add(new BearingObservation(point, bearing));
            used.Add(row);
        }

        if (error is null && observations.Count < MinimumRows)
        {
            error = _localizer[$"{KeyPrefix}ErrorTooFew"].Value;
        }

        if (error is not null)
        {
            UpdatePlot(observations, null, 0);
            Fail(error);
            return;
        }

        var fix = Solve(observations);
        if (fix.Point is not { } fixPoint)
        {
            UpdatePlot(observations, null, 0);
            Fail(_localizer[fix.Status switch
            {
                TriangulationStatus.Coincident => $"{KeyPrefix}ErrorCoincident",
                TriangulationStatus.IdenticalPoints => $"{KeyPrefix}ErrorIdentical",
                _ => $"{KeyPrefix}ErrorTooFew",
            }].Value);
            return;
        }

        ShowFix(fix, fixPoint, used, warnings);
        UpdatePlot(observations, fixPoint, used.Count > 2 ? fix.RmsOffsetMeters : 0);
    }

    private void ShowFix(BearingFix fix, GeoCoordinate point, List<BearingObservationRow> used, List<string> warnings)
    {
        DecimalDegrees = CoordinateFormatter.Format(point, CoordinateFormat.DecimalDegrees);
        DegreesDecimalMinutes = CoordinateFormatter.Format(point, CoordinateFormat.DegreesDecimalMinutes);
        DegreesMinutesSeconds = CoordinateFormatter.Format(point, CoordinateFormat.DegreesMinutesSeconds);

        if (fix.Alternate is { } alternate)
        {
            AlternateDegreesDecimalMinutes = CoordinateFormatter.Format(alternate, CoordinateFormat.DegreesDecimalMinutes);
            HasAlternate = true;
        }

        var overdetermined = used.Count > 2;
        for (var i = 0; i < used.Count; i++)
        {
            var row = used[i];
            var residual = fix.Residuals[i];
            DescribeResidual(row, residual);
            row.OffsetText = overdetermined
                ? string.Format(
                    CultureInfo.CurrentCulture,
                    _localizer["BearingFixOffsetFormat"].Value,
                    FormatDistance(residual.OffsetMeters),
                    residual.BearingErrorDegrees)
                : string.Empty;

            if (!residual.IsAhead)
            {
                warnings.Add(Format($"{KeyPrefix}WarnBehind", row.Label));
            }
        }

        if (overdetermined)
        {
            AccuracyText = string.Format(
                CultureInfo.CurrentCulture,
                _localizer["BearingFixAccuracyFormat"].Value,
                FormatDistance(fix.RmsOffsetMeters),
                FormatDistance(fix.MaxOffsetMeters));
            HasAccuracy = true;
        }
        else
        {
            BaselineDistance = FormatDistance(Geodesy.DistanceMeters(fix.Residuals[0].Point, fix.Residuals[1].Point));
            HasBaseline = true;
        }

        if (fix.IsIllConditioned)
        {
            warnings.Add(_localizer[$"{KeyPrefix}WarnIllConditioned"].Value);
        }

        WarningMessage = string.Join(Environment.NewLine, warnings);
        HasWarning = warnings.Count > 0;
        HasResult = true;
    }

    /// <summary>Fills a row's distance / bearing readouts; Resection reports them from the fix instead.</summary>
    protected virtual void DescribeResidual(BearingObservationRow row, ObservationResidual residual)
    {
        row.DistanceText = FormatDistance(residual.DistanceMeters);
        row.BearingToFixText = FormatBearing(residual.BearingToFixDegrees);
    }

    private void UpdatePlot(IReadOnlyList<BearingObservation> observations, GeoCoordinate? fix, double radiusMeters)
    {
        Plot = observations.Count == 0 ? null : BearingPlot.Build([.. observations.Select(PlotLine)], fix, radiusMeters);
        HasPlot = Plot is not null;
    }

    private void Fail(string message)
    {
        ErrorMessage = message;
        HasError = true;
    }

    private void ClearResult()
    {
        DecimalDegrees = string.Empty;
        DegreesDecimalMinutes = string.Empty;
        DegreesMinutesSeconds = string.Empty;
        AlternateDegreesDecimalMinutes = string.Empty;
        AccuracyText = string.Empty;
        BaselineDistance = string.Empty;
        WarningMessage = string.Empty;
        ErrorMessage = string.Empty;
        HasWarning = false;
        HasError = false;
        HasAlternate = false;
        HasAccuracy = false;
        HasBaseline = false;
        HasResult = false;
        Plot = null;
        HasPlot = false;
        foreach (var row in Rows)
        {
            row.ClearReadouts();
        }
    }

    private string Format(string key, string label)
        => string.Format(CultureInfo.CurrentCulture, _localizer[key].Value, label);

    // ---- Parsing & formatting ---------------------------------------------------

    /// <summary>Parses a declination in degrees, East positive: "4.5", "-3", "3W", "4,5° E".</summary>
    public static bool TryParseDeclination(string text, out double degrees)
    {
        degrees = 0;
        var t = text.Trim().Replace("°", string.Empty, StringComparison.Ordinal).Trim();
        if (t.Length == 0)
        {
            return false;
        }

        var sign = 1.0;
        var last = char.ToUpperInvariant(t[^1]);
        if (last is 'E' or 'W')
        {
            sign = last == 'W' ? -1.0 : 1.0;
            t = t[..^1].Trim();
        }

        if (!TryParseInvariant(t, out var value) || Math.Abs(value) > 180.0)
        {
            return false;
        }

        degrees = sign * value;
        return true;
    }

    protected static bool TryParseInvariant(string text, out double value)
        => double.TryParse(
            text.Trim().Replace(',', '.'),
            NumberStyles.Float,
            CultureInfo.InvariantCulture,
            out value) && double.IsFinite(value);

    protected static double Normalize360(double degrees) => ((degrees % 360.0) + 360.0) % 360.0;

    protected static string FormatDistance(double meters)
        => meters < 1000.0
            ? string.Format(CultureInfo.CurrentCulture, "{0:0.#} m", meters)
            : string.Format(CultureInfo.CurrentCulture, "{0:0.###} km", meters / 1000.0);

    protected static string FormatBearing(double degrees)
        => string.Format(CultureInfo.CurrentCulture, "{0:0.0}°", degrees);

    private static string FormatDeclination(double degrees)
        => string.Format(CultureInfo.CurrentCulture, "{0:0.0}° {1}", Math.Abs(degrees), degrees < 0 ? "W" : "E");

    // ---- Commands ---------------------------------------------------------------

    [RelayCommand(CanExecute = nameof(HasResult))]
    private void CopyOutput() => _clipboard.SetText(DegreesDecimalMinutes);

    [RelayCommand(CanExecute = nameof(HasResult))]
    private async Task ShareOutputAsync()
    {
        try
        {
            List<string> lines = [DegreesDecimalMinutes, DecimalDegrees, DegreesMinutesSeconds];
            if (HasAccuracy)
            {
                lines.Add(AccuracyText);
            }

            await _share.ShareTextAsync(ToolName, string.Join(Environment.NewLine, lines));
        }
        catch (Exception)
        {
            // Sharing is best-effort; a platform share failure must not crash the tool.
        }
    }

    [RelayCommand]
    private void LoadExample()
    {
        var example = ExampleRows;
        _suspendRecompute = true;
        BearingsAreMagnetic = false;
        ResetRowsCore(example.Count);
        for (var i = 0; i < example.Count; i++)
        {
            Rows[i].Coordinate = example[i].Coordinate;
            Rows[i].Bearing = example[i].Bearing;
        }

        _suspendRecompute = false;
        Relabel();
        Recompute();
    }

    [RelayCommand]
    private void Clear()
    {
        _suspendRecompute = true;
        BearingsAreMagnetic = false;
        ManualDeclination = string.Empty;
        _suspendRecompute = false;
        ResetRows(MinimumRows);
    }

    private void ResetRowsCore(int count)
    {
        Rows.Clear();
        for (var i = 0; i < count; i++)
        {
            Rows.Add(new BearingObservationRow(RemoveRow));
        }
    }
}
