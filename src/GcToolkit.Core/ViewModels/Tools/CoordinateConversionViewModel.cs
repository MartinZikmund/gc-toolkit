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
/// Coordinate conversion (issue #4). Takes one coordinate as free text, auto-detects its notation via
/// <see cref="CoordinateParser"/>, and renders the same point simultaneously in every supported
/// notation — Decimal Degrees, Degrees Decimal Minutes, Degrees Minutes Seconds, UTM, MGRS, USNG, Dutch
/// RD and British OSGB grid — each on its own copyable row. Matching geocachingtoolbox.com, it also
/// offers an <see cref="InputDatum"/> and <see cref="OutputDatum"/> selector over the full datum table:
/// the input is interpreted on the chosen datum, converted to the WGS84 hub, and the angular notations
/// are re-expressed on the output datum (the grids keep their intrinsic datum). It improves on the
/// reference by auto-detecting the input, showing every format at once with per-row copy, reporting the
/// detected notation, and offering copy-all / share. All conversion math lives in the foundation
/// library (thin-VM convention).
/// </summary>
[Tool("CoordinateConversion", ToolCategory.Coordinates,
      Introduced = "2026-05-20", Updated = "2026-06-06",
      Keywords = ["wgs84", "utm", "mgrs", "convert", "notation", "dd", "ddm", "dms", "souřadnice", "převod", "gps", "formát"])]
public sealed partial class CoordinateConversionViewModel : ToolViewModelBase, IToolStateful
{
    private readonly IStringLocalizer _localizer;
    private readonly IClipboardService _clipboard;
    private readonly IShareService _share;

    public CoordinateConversionViewModel(
        ICatalogService catalog,
        IRecentsService recents,
        IFavoriteToolsService favorites,
        IStringLocalizer localizer,
        IClipboardService clipboard,
        IShareService share)
        : base("CoordinateConversion", catalog, recents, favorites, localizer)
    {
        _localizer = localizer;
        _clipboard = clipboard;
        _share = share;

        // WGS84 heads the registry, so the pickers start on the identity datum.
        SelectedInputDatum = DatumOptions[0];
        SelectedOutputDatum = DatumOptions[0];
    }

    [ObservableProperty]
    public partial string InputText { get; set; } = string.Empty;

    /// <summary><see langword="true"/> once a valid coordinate has been parsed and the formats populated.</summary>
    [ObservableProperty]
    public partial bool HasResult { get; set; }

    /// <summary><see langword="true"/> when the current (non-empty) input could not be parsed.</summary>
    [ObservableProperty]
    public partial bool HasError { get; set; }

    /// <summary>The notation auto-detected from the input (only meaningful while <see cref="HasResult"/>).</summary>
    [ObservableProperty]
    public partial CoordinateFormat DetectedFormat { get; set; }

    /// <summary>Resource key for the detected format's label — the single source of truth tests assert on.</summary>
    [ObservableProperty]
    public partial string DetectedFormatLabelKey { get; set; } = string.Empty;

    /// <summary>The detected format's localized display name — drives the "Detected notation" banner.</summary>
    [ObservableProperty]
    public partial string DetectedFormatLabel { get; set; } = string.Empty;

    /// <summary>The same point rendered in every supported notation, each row independently copyable.
    /// Assigned wholesale so the virtualizing list rebuilds once per recompute, not once per row.</summary>
    [ObservableProperty]
    public partial IReadOnlyList<CoordinateFormatRow> Results { get; set; } = [];

    /// <summary>Every datum the tool can read from / write to (WGS84 first, then the full table).</summary>
    public IReadOnlyList<Datum> Datums { get; } = DatumRegistry.All;

    /// <summary>The datum picker entries (display string + value) the View's ComboBoxes bind to.</summary>
    public IReadOnlyList<DatumOption> DatumOptions { get; } = [.. DatumRegistry.All.Select(d => new DatumOption(d))];

    /// <summary>The datum the typed coordinate is interpreted on (angular input is shifted to WGS84 from here).</summary>
    [ObservableProperty]
    public partial Datum InputDatum { get; set; } = DatumRegistry.Wgs84;

    /// <summary>The datum the angular output notations are expressed on (the grids keep their own datum).</summary>
    [ObservableProperty]
    public partial Datum OutputDatum { get; set; } = DatumRegistry.Wgs84;

    /// <summary>Picker-facing mirror of <see cref="InputDatum"/>; a ComboBox binds items, not raw structs.</summary>
    [ObservableProperty]
    public partial DatumOption? SelectedInputDatum { get; set; }

    /// <summary>Picker-facing mirror of <see cref="OutputDatum"/>.</summary>
    [ObservableProperty]
    public partial DatumOption? SelectedOutputDatum { get; set; }

    /// <summary>A string parameter (e.g. from a cache listing) pre-fills the input.</summary>
    public override void OnNavigatedTo(object? parameter)
    {
        base.OnNavigatedTo(parameter);

        if (parameter is string text && !string.IsNullOrWhiteSpace(text))
        {
            InputText = text;
        }
    }

    partial void OnInputTextChanged(string value) => Recompute();

    partial void OnInputDatumChanged(Datum value)
    {
        SelectedInputDatum = OptionFor(value);
        Recompute();
    }

    partial void OnOutputDatumChanged(Datum value)
    {
        SelectedOutputDatum = OptionFor(value);
        Recompute();
    }

    partial void OnSelectedInputDatumChanged(DatumOption? value)
    {
        if (value is not null)
        {
            InputDatum = value.Value;
        }
    }

    partial void OnSelectedOutputDatumChanged(DatumOption? value)
    {
        if (value is not null)
        {
            OutputDatum = value.Value;
        }
    }

    partial void OnHasResultChanged(bool value)
    {
        CopyAllCommand.NotifyCanExecuteChanged();
        ShareCommand.NotifyCanExecuteChanged();
    }

    private DatumOption OptionFor(Datum datum)
        => DatumOptions.FirstOrDefault(o => o.Value.Equals(datum)) ?? new DatumOption(datum);

    private void Recompute()
    {
        if (string.IsNullOrWhiteSpace(InputText))
        {
            Results = [];
            HasResult = false;
            HasError = false;
            DetectedFormatLabelKey = string.Empty;
            DetectedFormatLabel = string.Empty;
            return;
        }

        if (!CoordinateParser.TryParse(InputText, out var coordinate, out var detected))
        {
            Results = [];
            HasResult = false;
            HasError = true;
            DetectedFormatLabelKey = string.Empty;
            DetectedFormatLabel = string.Empty;
            return;
        }

        DetectedFormat = detected;
        DetectedFormatLabelKey = CoordinateFormatResources.LabelKey(detected);
        DetectedFormatLabel = _localizer[DetectedFormatLabelKey].Value;

        // Angular input is expressed on the chosen input datum; lift it to the WGS84 hub. Grids carry
        // their own intrinsic datum, so the parser already produced WGS84 for them.
        var wgs84 = IsAngular(detected) ? DatumTransform.ToWgs84(coordinate, InputDatum) : coordinate;

        Results = [.. CoordinateConversions.ToAllFormats(wgs84, OutputDatum).Select(BuildRow)];

        HasError = false;
        HasResult = true;
    }

    private CoordinateFormatRow BuildRow(CoordinateFormatResult result)
    {
        var label = _localizer[CoordinateFormatResources.LabelKey(result.Format)].Value;
        var copyName = string.Format(_localizer["CoordConvCopyFormat"].Value, label);
        return new CoordinateFormatRow(result.Format, label, result.Value, copyName, _clipboard.SetText);
    }

    /// <summary>The text Copy-all / Share emit: one <c>Label: value</c> line per format.</summary>
    private string BuildResultText()
        => string.Join(Environment.NewLine, Results.Select(r => $"{r.Label}: {r.Value}"));

    [RelayCommand(CanExecute = nameof(HasResult))]
    private void CopyAll() => _clipboard.SetText(BuildResultText());

    [RelayCommand(CanExecute = nameof(HasResult))]
    private async Task ShareAsync()
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

    private static bool IsAngular(CoordinateFormat format) => format is
        CoordinateFormat.DecimalDegrees or
        CoordinateFormat.DegreesDecimalMinutes or
        CoordinateFormat.DegreesMinutesSeconds;

    public ToolInputState CaptureState()
    {
        ToolInputState state = new();
        state.Set(nameof(InputDatum), InputDatum.Code);
        state.Set(nameof(OutputDatum), OutputDatum.Code);
        state.Set(nameof(InputText), InputText);
        return state;
    }

    public void RestoreState(ToolInputState state)
    {
        InputDatum = DatumRegistry.Find(state.GetString(nameof(InputDatum))) ?? InputDatum;
        OutputDatum = DatumRegistry.Find(state.GetString(nameof(OutputDatum))) ?? OutputDatum;
        InputText = state.GetString(nameof(InputText), InputText);
    }
}
