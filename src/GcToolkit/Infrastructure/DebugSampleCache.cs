#if DEBUG
using GcToolkit.Core.Coordinates;
using GcToolkit.Core.Geocaches;
using GcToolkit.Core.ViewModels;

namespace GcToolkit.Infrastructure;

/// <summary>
/// TEMPORARY, debug builds only: seeds a realistic cache so the detail page can be reached before the
/// Caches page exists. Opened when the GCTOOLKIT_OPEN_SAMPLE_CACHE environment variable is set or ~/.gctoolkit-open-sample exists.
/// Remove once the Caches list can open a listing.
/// </summary>
internal static class DebugSampleCache
{
    private static readonly GeocacheCode _code = GeocacheCode.Parse("GC8KX2Q");

    public static async Task OpenIfRequestedAsync(IServiceProvider services)
    {
        var flagFile = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".gctoolkit-open-sample");
        if (Environment.GetEnvironmentVariable("GCTOOLKIT_OPEN_SAMPLE_CACHE") is not { Length: > 0 } && !File.Exists(flagFile)
            && !Environment.GetCommandLineArgs().Any(a => a.Contains("open-sample", StringComparison.Ordinal)))
        {
            return;
        }

        await services.GetRequiredService<IGeocacheStore>().UpsertAsync([Create()]);
        services.GetRequiredService<INavigationService>().Navigate<GeocacheDetailViewModel>(_code);
    }

    private static string Svg(string fill, string label) =>
        "data:image/svg+xml;utf8," + Uri.EscapeDataString(
            $"<svg xmlns='http://www.w3.org/2000/svg' width='480' height='240'><rect width='480' height='240' fill='{fill}'/>" +
            $"<text x='240' y='130' font-size='28' text-anchor='middle' fill='white' font-family='sans-serif'>{label}</text></svg>");

    private static Geocache Create() => new()
    {
        Code = _code,
        Name = "The Lost Legion",
        Type = GeocacheType.Mystery,
        Size = GeocacheSize.Small,
        Status = GeocacheStatus.Available,
        Difficulty = 3,
        Terrain = 1.5,
        Coordinate = new GeoCoordinate(49.20575, 16.57612),
        Owner = "Pathfinder",
        PlacedOn = new DateTimeOffset(2019, 4, 12, 0, 0, 0, TimeSpan.Zero),
        FavoritePoints = 128,
        Country = "Czechia",
        State = "South Moravia",
        Attributes =
        [
            new(8, true), new(13, true), new(7, true), new(47, true), new(1, false), new(44, true), new(53, true),
        ],
        ShortDescription = "<p>A Roman legion vanished near Brno. Can you follow its last orders?</p>",
        ShortDescriptionIsHtml = true,
        LongDescription =
            "<p style=\"color:#000000\"><font color=\"#000000\">In the year <b>179</b>, a detachment of the Legio X marched north and was never heard of again. " +
            "Their last message was found on a <a href=\"https://en.wikipedia.org/wiki/Legio_X_Gemina\">clay tablet</a> and it is not going to read itself.</font></p>" +
            $"<p><img src=\"{Svg("#1B5E3F", "Last known camp")}\" alt=\"Camp\"></p>" +
            "<h3>The tablet</h3><table><tr><th>Cohort</th><th>Cipher</th></tr><tr><td>I</td><td>Zruthb Ubxsr</td></tr><tr><td>II</td><td>Pnrfne jnf urer</td></tr></table>" +
            "<p>Final = <code>N 49° 12.AB5 E 016° 34.CDE</code>. Hint: the cohort number is the shift.</p>" +
            "<script>document.body.innerHTML='hacked'</script><p onclick=\"alert(1)\">This paragraph ignores its click handler.</p>",
        LongDescriptionIsHtml = true,
        Hint = "Look under the stone, not behind the tree.",
        Waypoints =
        [
            new Waypoint { Prefix = "PK", Name = "Parking", Type = WaypointType.Parking, Coordinate = new GeoCoordinate(49.2041, 16.5727), Note = "Free parking, ten minutes on foot." },
            new Waypoint { Prefix = "S1", Name = "Stage 1 - the tablet", Type = WaypointType.Question, Coordinate = new GeoCoordinate(49.2049, 16.5744) },
            new Waypoint { Prefix = "FI", Name = "Final location", Type = WaypointType.Final },
        ],
        Logs =
        [
            new GeocacheLog { Type = GeocacheLogType.Found, Author = "MapMonkey", Date = new(2025, 9, 14, 0, 0, 0, TimeSpan.Zero), Text = "Great puzzle, TFTC! Found after a short search." },
            new GeocacheLog { Type = GeocacheLogType.DidNotFind, Author = "NightOwl", Date = new(2025, 8, 2, 0, 0, 0, TimeSpan.Zero), Text = "Searched for an hour in the dark. Will return." },
            new GeocacheLog { Type = GeocacheLogType.Found, Author = "Trailblazer", Date = new(2025, 6, 21, 0, 0, 0, TimeSpan.Zero), Text = "<p>Solved at home, <b>found</b> on the walk.</p>", TextIsHtml = true },
            new GeocacheLog { Type = GeocacheLogType.WriteNote, Author = "Pathfinder", Date = new(2025, 5, 30, 0, 0, 0, TimeSpan.Zero), Text = "Replaced the logbook." },
            new GeocacheLog { Type = GeocacheLogType.Found, Author = "Compass", Date = new(2024, 11, 3, 0, 0, 0, TimeSpan.Zero), Text = "First to find after the winter break." },
        ],
        Images =
        [
            new GeocacheImage(Svg("#3E7D5C", "Spoiler 1"), "Trailhead"),
            new GeocacheImage(Svg("#7D5C3E", "Spoiler 2"), "The stone"),
        ],
        UserData = new GeocacheUserData
        {
            CorrectedCoordinate = new GeoCoordinate(49.21012, 16.57934),
            Note = "Cohort II -> ROT13. Check the bridge first.",
            IsSolved = true,
        },
    };
}
#endif
