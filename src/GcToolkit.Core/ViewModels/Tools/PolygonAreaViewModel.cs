using System.Globalization;
using System.Text;
using GcToolkit.Core.Catalog;
using GcToolkit.Core.Coordinates;
using GcToolkit.Core.Discovery;
using GcToolkit.Core.FavoriteTools;
using GcToolkit.Core.Recents;
using GcToolkit.Core.Services;
using Microsoft.Extensions.Localization;
using MZikmund.Toolkit.WinUI.Services;

namespace GcToolkit.Core.ViewModels.Tools;

/// <summary>One row of the per-edge table: which vertices it joins, how long it is and which way it heads.</summary>
public sealed record PolygonEdgeRow(string Label, string Length, string Bearing);

/// <summary>
/// Polygon area, perimeter and centroid (issue #254). Vertices go in one per line, in any notation the
/// coordinate parser knows. The area is geodesic on the WGS84 ellipsoid, the perimeter is the sum of the
/// geodesic edges (closing edge included), and the centroid is the area-weighted centre — reported next
/// to the plain vertex mean so a puzzle that means either one can be answered. All math lives in
/// <see cref="PolygonGeometry"/>; this VM parses, converts units and formats.
/// </summary>
[Tool("PolygonArea", ToolCategory.Coordinates,
      Introduced = "2026-11-11", Updated = "2026-11-11",
      Keywords =
      [
          "polygon", "area", "perimeter", "centroid", "centre", "center", "boundary", "field", "acre", "hectare", "shape",
          "mnohoúhelník", "polygon", "plocha", "obvod", "těžiště", "střed", "hranice", "pozemek", "hektar"
      ])]
public sealed partial class PolygonAreaViewModel : ToolViewModelBase
{
    private const string InputKey = "PolygonArea.Input";
    private const string AreaUnitKey = "PolygonArea.AreaUnit";
    private const string LengthUnitKey = "PolygonArea.LengthUnit";

    private const string ExampleInput =
        "N 50° 05.100' E 014° 25.000'\nN 50° 05.100' E 014° 25.400'\nN 50° 04.800' E 014° 25.400'\nN 50° 04.800' E 014° 25.000'";

    private static readonly PolygonAreaUnit[] _areaUnits =
    [
        PolygonAreaUnit.SquareMeter, PolygonAreaUnit.SquareKilometer, PolygonAreaUnit.Hectare,
        PolygonAreaUnit.Acre, PolygonAreaUnit.SquareFoot, PolygonAreaUnit.SquareMile,
    ];

    private static readonly DistanceUnit[] _lengthUnits =
        [DistanceUnit.Meter, DistanceUnit.Kilometer, DistanceUnit.Feet, DistanceUnit.Mile];

    private static readonly string[] _areaSymbols = ["m²", "km²", "ha", "ac", "ft²", "mi²"];
    private static readonly string[] _lengthSymbols = ["m", "km", "ft", "mi"];

    private readonly IStringLocalizer _localizer;
    private readonly IClipboardService _clipboard;
    private readonly IShareService _share;
    private readonly IPreferences _preferences;

    private PolygonResult? _result;

    public PolygonAreaViewModel(
        ICatalogService catalog,
        IRecentsService recents,
        IFavoriteToolsService favorites,
        IStringLocalizer localizer,
        IClipboardService clipboard,
        IShareService share,
        IPreferences preferences)
        : base("PolygonArea", catalog, recents, favorites, localizer)
    {
        _localizer = localizer;
        _clipboard = clipboard;
        _share = share;
        _preferences = preferences;

        AreaUnitIndex = ReadUnitIndex(AreaUnitKey, _areaUnits.Length);
        LengthUnitIndex = ReadUnitIndex(LengthUnitKey, _lengthUnits.Length);
        InputText = _preferences.Get(InputKey, string.Empty);
    }

    public IReadOnlyList<string> AreaUnitNames => _areaSymbols;

    public IReadOnlyList<string> LengthUnitNames => _lengthSymbols;

    /// <summary>The vertices, one per line, in order around the shape.</summary>
    [ObservableProperty]
    public partial string InputText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial int AreaUnitIndex { get; set; }

    [ObservableProperty]
    public partial int LengthUnitIndex { get; set; }

    [ObservableProperty]
    public partial bool HasResult { get; set; }

    /// <summary>Input is present but fewer than three usable vertices came out of it.</summary>
    [ObservableProperty]
    public partial bool NeedsMorePoints { get; set; }

    [ObservableProperty]
    public partial bool HasParseErrors { get; set; }

    [ObservableProperty]
    public partial string ParseErrorMessage { get; set; } = string.Empty;

    [ObservableProperty]
    public partial bool HasSelfIntersection { get; set; }

    /// <summary>The input repeated its first vertex to close the ring; the repeat was ignored.</summary>
    [ObservableProperty]
    public partial bool WasClosed { get; set; }

    [ObservableProperty]
    public partial string VertexCountText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string AreaText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string PerimeterText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string CentroidText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string CentroidDecimalText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string VertexMeanText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string VertexMeanDecimalText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial IReadOnlyList<PolygonEdgeRow> Edges { get; set; } = [];

    partial void OnInputTextChanged(string value)
    {
        _preferences.Set(InputKey, value);
        Recompute();
    }

    partial void OnAreaUnitIndexChanged(int value)
    {
        _preferences.Set(AreaUnitKey, value);
        Format();
    }

    partial void OnLengthUnitIndexChanged(int value)
    {
        _preferences.Set(LengthUnitKey, value);
        Format();
    }

    partial void OnHasResultChanged(bool value)
    {
        CopyOutputCommand.NotifyCanExecuteChanged();
        ShareOutputCommand.NotifyCanExecuteChanged();
    }

    private int ReadUnitIndex(string key, int count)
    {
        var stored = _preferences.Get(key, 0);
        return stored >= 0 && stored < count ? stored : 0;
    }

    private void Recompute()
    {
        var parsed = CoordinateMean.ParseLines(InputText);

        HasParseErrors = parsed.Errors.Count > 0;
        ParseErrorMessage = HasParseErrors
            ? string.Format(
                CultureInfo.CurrentCulture,
                _localizer["PolygonAreaParseError"].Value,
                string.Join(", ", parsed.Errors.Select(e => e.LineNumber.ToString(CultureInfo.CurrentCulture))))
            : string.Empty;

        _result = PolygonGeometry.Compute(parsed.Points);
        NeedsMorePoints = _result is null && !string.IsNullOrWhiteSpace(InputText);
        Format();
    }

    private void Format()
    {
        if (_result is not { } r)
        {
            ClearOutputs();
            return;
        }

        var area = _areaUnits[AreaUnitIndex];
        var length = _lengthUnits[LengthUnitIndex];
        var lengthSymbol = _lengthSymbols[LengthUnitIndex];

        VertexCountText = string.Format(CultureInfo.CurrentCulture, _localizer["PolygonAreaVertexCount"].Value, r.VertexCount);
        AreaText = $"{FormatNumber(AreaUnits.FromSquareMeters(r.AreaSquareMeters, area))} {_areaSymbols[AreaUnitIndex]}";
        PerimeterText = FormatLength(r.PerimeterMeters, length, lengthSymbol);
        CentroidText = CoordinateMeanFormat.Ddm(r.Centroid);
        CentroidDecimalText = CoordinateMeanFormat.Dd(r.Centroid);
        VertexMeanText = CoordinateMeanFormat.Ddm(r.VertexMean);
        VertexMeanDecimalText = CoordinateMeanFormat.Dd(r.VertexMean);
        WasClosed = r.WasClosed;
        HasSelfIntersection = r.IsSelfIntersecting;
        Edges =
        [
            .. r.Edges.Select(e => new PolygonEdgeRow(
                $"{e.FromIndex + 1} → {e.ToIndex + 1}",
                FormatLength(e.LengthMeters, length, lengthSymbol),
                CoordinateMeanFormat.Bearing(e.InitialBearingDegrees)))
        ];
        HasResult = true;
    }

    private void ClearOutputs()
    {
        HasResult = false;
        HasSelfIntersection = false;
        WasClosed = false;
        VertexCountText = string.Empty;
        AreaText = string.Empty;
        PerimeterText = string.Empty;
        CentroidText = string.Empty;
        CentroidDecimalText = string.Empty;
        VertexMeanText = string.Empty;
        VertexMeanDecimalText = string.Empty;
        Edges = [];
    }

    private static string FormatLength(double meters, DistanceUnit unit, string symbol)
        => $"{FormatNumber(DistanceUnits.FromMeters(meters, unit))} {symbol}";

    /// <summary>Few decimals for big numbers, more for small ones, so the digits stay meaningful.</summary>
    private static string FormatNumber(double value)
    {
        var format = Math.Abs(value) switch
        {
            >= 1e6 => "N0",
            >= 1e3 => "N1",
            >= 1.0 => "N2",
            _ => "N4",
        };
        return value.ToString(format, CultureInfo.CurrentCulture);
    }

    private string BuildSummary()
    {
        StringBuilder builder = new();
        builder.AppendLine(VertexCountText);
        builder.AppendLine($"{_localizer["PolygonAreaArea"].Value}: {AreaText}");
        builder.AppendLine($"{_localizer["PolygonAreaPerimeter"].Value}: {PerimeterText}");
        builder.AppendLine($"{_localizer["PolygonAreaCentroid"].Value}: {CentroidText}");
        builder.AppendLine($"{_localizer["PolygonAreaVertexMean"].Value}: {VertexMeanText}");

        foreach (var edge in Edges)
        {
            builder.AppendLine($"{edge.Label}: {edge.Length}, {edge.Bearing}");
        }

        return builder.ToString().TrimEnd();
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
    private void LoadExample() => InputText = ExampleInput;

    [RelayCommand]
    private void Clear() => InputText = string.Empty;
}
