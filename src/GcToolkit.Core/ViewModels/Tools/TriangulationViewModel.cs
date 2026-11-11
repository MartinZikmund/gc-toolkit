using GcToolkit.Core.Catalog;
using GcToolkit.Core.Coordinates;
using GcToolkit.Core.Discovery;
using GcToolkit.Core.FavoriteTools;
using GcToolkit.Core.Recents;
using GcToolkit.Core.Services;

namespace GcToolkit.Core.ViewModels.Tools;

/// <summary>
/// Triangulation (issue #298): two or more known points, each with a bearing sighted from it, give the
/// unknown point where the lines cross (least-squares best fit for three or more). The shared engine is
/// <see cref="BearingFixViewModelBase"/>; the math is in <see cref="Triangulation"/>.
/// </summary>
[Tool("Triangulation", ToolCategory.Coordinates,
      Introduced = "2026-11-11", Updated = "2026-11-11",
      Keywords = ["triangulation", "intersection", "bearing", "azimuth", "crossing", "least squares", "declination", "magnetic", "triangulace", "průsečík", "azimut", "směr", "deklinace", "magnetický"])]
public sealed partial class TriangulationViewModel(
    ICatalogService catalog,
    IRecentsService recents,
    IFavoriteToolsService favorites,
    IStringLocalizer localizer,
    IClipboardService clipboard,
    IShareService share)
    : BearingFixViewModelBase("Triangulation", catalog, recents, favorites, localizer, clipboard, share)
{
    protected override string KeyPrefix => "Triangulation";

    protected override BearingFix Solve(IReadOnlyList<BearingObservation> observations) => Triangulation.Solve(observations);

    // Petřín tower, Žižkov TV tower and Vyšehrad sighting Prague Castle, each a few tenths of a degree off.
    protected override IReadOnlyList<(string Coordinate, string Bearing)> ExampleRows =>
    [
        ("N 50 05.020 E 014 23.700", "26.5"),
        ("N 50 04.873 E 014 27.084", "286.2"),
        ("N 50 03.875 E 014 25.250", "333.9"),
    ];
}
