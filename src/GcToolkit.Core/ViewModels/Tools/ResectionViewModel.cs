using GcToolkit.Core.Catalog;
using GcToolkit.Core.Coordinates;
using GcToolkit.Core.Discovery;
using GcToolkit.Core.FavoriteTools;
using GcToolkit.Core.Recents;
using GcToolkit.Core.Services;

namespace GcToolkit.Core.ViewModels.Tools;

/// <summary>
/// Resection (issue #263): find your own position from bearings you sighted to two or more known landmarks.
/// Shares the rows, declination, residuals and sketch of <see cref="BearingFixViewModelBase"/>; the math is
/// in <see cref="Resection"/>.
/// </summary>
[Tool("Resection", ToolCategory.Coordinates,
      Introduced = "2026-11-11", Updated = "2026-11-11",
      Keywords = ["resection", "back bearing", "position fix", "landmark", "bearing", "azimuth", "compass", "declination", "protínání zpět", "zpětné protínání", "orientační bod", "azimut", "kompas", "deklinace"])]
public sealed partial class ResectionViewModel(
    ICatalogService catalog,
    IRecentsService recents,
    IFavoriteToolsService favorites,
    IStringLocalizer localizer,
    IClipboardService clipboard,
    IShareService share)
    : BearingFixViewModelBase("Resection", catalog, recents, favorites, localizer, clipboard, share)
{
    protected override string KeyPrefix => "Resection";

    protected override BearingFix Solve(IReadOnlyList<BearingObservation> observations) => Resection.Solve(observations);

    // Each landmark's line of position runs back from it, towards the observer.
    protected override BearingObservation PlotLine(BearingObservation observation)
        => observation with { BearingDegrees = observation.BearingDegrees + 180.0 };

    // Standing at Prague Castle, sighting Petřín tower, Žižkov TV tower and Vyšehrad, each a few tenths off.
    protected override IReadOnlyList<(string Coordinate, string Bearing)> ExampleRows =>
    [
        ("N 50 05.020 E 014 23.700", "206.4"),
        ("N 50 04.873 E 014 27.084", "106.2"),
        ("N 50 03.875 E 014 25.250", "153.9"),
    ];
}
