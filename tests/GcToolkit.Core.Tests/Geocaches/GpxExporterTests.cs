using System.Text;
using System.Xml.Linq;
using GcToolkit.Core.Coordinates;
using GcToolkit.Core.Geocaches;
using GcToolkit.Core.Geocaches.Gpx;

namespace GcToolkit.Core.Tests.Geocaches;

[TestClass]
public class GpxExporterTests
{
    private static readonly XNamespace _gpx = "http://www.topografix.com/GPX/1/0";
    private static readonly XNamespace _gs = "http://www.groundspeak.com/cache/1/0/1";
    private static readonly XNamespace _gsak = "http://www.gsak.net/xmlv1/6";

    private static readonly GeoCoordinate _posted = new(50.06666, 14.41666);
    private static readonly GeoCoordinate _corrected = new(50.06193, 14.4247);

    private readonly GpxExporter _exporter = new(new FixedTimeProvider(new DateTimeOffset(2026, 1, 2, 3, 4, 5, TimeSpan.Zero)));
    private readonly GpxImporter _importer = new();

    private static string FixturePath(string name) => Path.Combine(AppContext.BaseDirectory, "Geocaches", "Fixtures", name);

    private static Geocache RichCache(string code = "GC8KX2Q", GeoCoordinate? corrected = null) => new()
    {
        Code = GeocacheCode.Parse(code),
        Name = "The Lost Legion <&> \"Quotes\" ěščřž",
        Type = GeocacheType.Mystery,
        Size = GeocacheSize.Regular,
        Status = GeocacheStatus.Disabled,
        Difficulty = 3,
        Terrain = 2.5,
        Coordinate = _posted,
        Owner = "Martin & Co",
        PlacedOn = new DateTimeOffset(2013, 8, 24, 7, 0, 0, TimeSpan.Zero),
        IsPremiumOnly = true,
        FavoritePoints = 12,
        Country = "Czech Republic",
        State = "Praha",
        ShortDescription = "<b>Solve</b>",
        ShortDescriptionIsHtml = true,
        LongDescription = "Plain & simple\nsecond line",
        LongDescriptionIsHtml = false,
        Hint = "Magnet, low",
        Attributes = [new(13, true), new(1, false)],
        Waypoints =
        [
            new() { Prefix = "PK", Name = "Parking", Type = WaypointType.Parking, Coordinate = new(50.065, 14.415), Note = "Free after 18:00" },
            new() { Prefix = "FN", Name = "Final", Type = WaypointType.Final, Coordinate = null },
            new() { Prefix = "S1", Name = "Stage 1", Type = WaypointType.Question, Coordinate = new(50.067, 14.417), Note = "What year?" },
            new() { Prefix = "MY", Name = "My notes", Type = WaypointType.User, Coordinate = new(50.0, 14.0) },
        ],
        Logs =
        [
            new() { Id = "100", Type = GeocacheLogType.Publish, Author = "Reviewer", Date = new(2013, 8, 24, 19, 0, 0, TimeSpan.Zero), Text = "Published." },
            new() { Id = "101", Type = GeocacheLogType.DidNotFind, Author = "Sad Seeker", Date = new(2013, 9, 1, 10, 15, 30, 250, TimeSpan.Zero), Text = "Not <there>" },
        ],
        Images = [new("https://example.com/a.jpg", "Spoiler", "A photo")],
        SourceId = "gpx",
        UpdatedAt = new DateTimeOffset(2024, 6, 1, 8, 30, 0, TimeSpan.Zero),
        UserData = new()
        {
            CorrectedCoordinate = corrected,
            Note = "Look near the oak",
            IsSolved = corrected is not null,
            IsFound = true,
        },
    };

    private static Geocache MinimalCache() => new()
    {
        Code = GeocacheCode.Parse("GC1MINI"),
        Name = "Minimal",
        Coordinate = new(-33.8568, 151.2153),
        SourceId = "gpx",
    };

    private static XDocument Parse(string gpx) => XDocument.Parse(gpx);

    private static XElement Wpt(XDocument doc, string name)
        => doc.Root!.Elements(_gpx + "wpt").Single(w => (string?)w.Element(_gpx + "name") == name);

    private async Task<IReadOnlyList<Geocache>> RoundTrip(IEnumerable<Geocache> caches, GpxExportOptions? options = null)
    {
        using MemoryStream stream = new();
        await _exporter.ExportAsync(caches, stream, options);
        stream.Position = 0;
        var result = await _importer.ImportAsync(stream, "roundtrip.gpx");
        Assert.AreEqual(0, result.Errors.Count);
        Assert.AreEqual(0, result.Warnings.Count, string.Join("; ", result.Warnings.Select(w => $"{w.Subject}: {w.Kind} {w.Detail}")));
        Assert.AreEqual(0, result.PlainWaypoints.Count);
        return result.Caches;
    }

    private static void AssertSame(Geocache expected, Geocache actual)
    {
        CollectionAssert.AreEqual(expected.Attributes.ToArray(), actual.Attributes.ToArray(), "Attributes");
        CollectionAssert.AreEqual(expected.Waypoints.ToArray(), actual.Waypoints.ToArray(), "Waypoints");
        CollectionAssert.AreEqual(expected.Logs.ToArray(), actual.Logs.ToArray(), "Logs");
        CollectionAssert.AreEqual(expected.Images.ToArray(), actual.Images.ToArray(), "Images");

        Geocache Flat(Geocache c) => c with { Attributes = [], Waypoints = [], Logs = [], Images = [], SourceId = "gpx" };
        Assert.AreEqual(Flat(expected), Flat(actual));
    }

    [TestMethod]
    public void Export_NoCaches_IsEmptyValidGpx()
    {
        var doc = Parse(_exporter.Export([]));

        Assert.AreEqual(_gpx + "gpx", doc.Root!.Name);
        Assert.AreEqual(0, doc.Root.Elements(_gpx + "wpt").Count());
    }

    [TestMethod]
    public void Export_Cache_WritesGroundspeakStructure()
    {
        var doc = Parse(_exporter.Export([RichCache()]));

        var wpt = Wpt(doc, "GC8KX2Q");
        Assert.AreEqual("50.06666", (string?)wpt.Attribute("lat"));
        Assert.AreEqual("14.41666", (string?)wpt.Attribute("lon"));
        Assert.AreEqual("Geocache Found", (string?)wpt.Element(_gpx + "sym"));
        Assert.AreEqual("Geocache|Unknown Cache", (string?)wpt.Element(_gpx + "type"));
        Assert.AreEqual("https://coord.info/GC8KX2Q", (string?)wpt.Element(_gpx + "url"));

        var cache = wpt.Element(_gs + "cache")!;
        Assert.AreEqual("False", (string?)cache.Attribute("available"));
        Assert.AreEqual("False", (string?)cache.Attribute("archived"));
        Assert.AreEqual("Regular", (string?)cache.Element(_gs + "container"));
        Assert.AreEqual("The Lost Legion <&> \"Quotes\" ěščřž", (string?)cache.Element(_gs + "name"));
    }

    [TestMethod]
    public void Export_Header_UsesGpx10WithExportTime()
    {
        var doc = Parse(_exporter.Export([MinimalCache()]));

        Assert.AreEqual("1.0", (string?)doc.Root!.Attribute("version"));
        Assert.AreEqual("2026-01-02T03:04:05Z", (string?)doc.Root.Element(_gpx + "time"));
    }

    [TestMethod]
    public void Export_ExtraWaypointMode_KeepsPostedAndAddsFinal()
    {
        var doc = Parse(_exporter.Export([RichCache(corrected: _corrected)]));

        Assert.AreEqual("50.06666", (string?)Wpt(doc, "GC8KX2Q").Attribute("lat"));
        Assert.IsNull(Wpt(doc, "GC8KX2Q").Element(_gsak + "wptExtension")?.Element(_gsak + "LatBeforeCorrect"));

        var extra = Wpt(doc, "CC8KX2Q");
        Assert.AreEqual("50.06193", (string?)extra.Attribute("lat"));
        Assert.AreEqual("14.4247", (string?)extra.Attribute("lon"));
        Assert.AreEqual("Waypoint|Final Location", (string?)extra.Element(_gpx + "type"));
    }

    [TestMethod]
    public void Export_MainCoordinatesMode_PutsCorrectedFirstAndKeepsPostedForGsak()
    {
        var doc = Parse(_exporter.Export([RichCache(corrected: _corrected)], new() { CorrectedCoordinates = CorrectedCoordinateMode.MainCoordinates }));

        var wpt = Wpt(doc, "GC8KX2Q");
        Assert.AreEqual("50.06193", (string?)wpt.Attribute("lat"));
        Assert.AreEqual("14.4247", (string?)wpt.Attribute("lon"));
        Assert.AreEqual("50.06666", (string?)wpt.Element(_gsak + "wptExtension")!.Element(_gsak + "LatBeforeCorrect"));
        Assert.AreEqual("14.41666", (string?)wpt.Element(_gsak + "wptExtension")!.Element(_gsak + "LonBeforeCorrect"));
        Assert.AreEqual(0, doc.Root!.Elements(_gpx + "wpt").Count(w => ((string?)w.Element(_gpx + "name"))!.StartsWith("CC", StringComparison.Ordinal)));
    }

    [TestMethod]
    [DataRow(CorrectedCoordinateMode.ExtraWaypoint)]
    [DataRow(CorrectedCoordinateMode.MainCoordinates)]
    public void Export_NoCorrection_WritesNoCorrectionArtifacts(CorrectedCoordinateMode mode)
    {
        var doc = Parse(_exporter.Export([RichCache()], new() { CorrectedCoordinates = mode }));

        Assert.AreEqual("GC8KX2Q", (string?)doc.Root!.Elements(_gpx + "wpt").First().Element(_gpx + "name"));
        Assert.IsNull(Wpt(doc, "GC8KX2Q").Element(_gsak + "wptExtension")!.Element(_gsak + "LatBeforeCorrect"));
        Assert.AreEqual(0, doc.Root.Elements(_gpx + "wpt").Count(w => ((string?)w.Element(_gpx + "name"))!.StartsWith("CC", StringComparison.Ordinal)));
    }

    [TestMethod]
    public void Export_UserNote_GoesToGsakGcNote()
    {
        var doc = Parse(_exporter.Export([RichCache()]));

        Assert.AreEqual("Look near the oak", (string?)Wpt(doc, "GC8KX2Q").Element(_gsak + "wptExtension")!.Element(_gsak + "GcNote"));
    }

    [TestMethod]
    public void Export_AdditionalWaypoints_AreNamedAfterTheCache()
    {
        var doc = Parse(_exporter.Export([RichCache()]));

        var parking = Wpt(doc, "PK8KX2Q");
        Assert.AreEqual("Parking", (string?)parking.Element(_gpx + "desc"));
        Assert.AreEqual("Parking Area", (string?)parking.Element(_gpx + "sym"));
        Assert.AreEqual("Waypoint|Parking Area", (string?)parking.Element(_gpx + "type"));
        Assert.AreEqual("Free after 18:00", (string?)parking.Element(_gpx + "cmt"));
        Assert.AreEqual("0", (string?)Wpt(doc, "FN8KX2Q").Attribute("lat"));
    }

    [TestMethod]
    public void Export_ExcludeLogs_OmitsThem()
    {
        var doc = Parse(_exporter.Export([RichCache()], new() { IncludeLogs = false }));

        Assert.IsNull(Wpt(doc, "GC8KX2Q").Element(_gs + "cache")!.Element(_gs + "logs"));
    }

    [TestMethod]
    public void Export_ControlCharactersInText_StillProducesParsableXml()
    {
        var cache = MinimalCache() with { Name = "Bad\u0001Name", LongDescription = "Tab\there\u0008" };

        var doc = Parse(_exporter.Export([cache]));

        Assert.AreEqual("BadName", (string?)Wpt(doc, "GC1MINI").Element(_gs + "cache")!.Element(_gs + "name"));
    }

    [TestMethod]
    public async Task RoundTrip_EmojiAndLoneCarriageReturn_Survive()
    {
        var original = MinimalCache() with { Name = "Treasure \U0001F3F4‍☠️", LongDescription = "a\rb\nc" };

        var back = await RoundTrip([original]);

        AssertSame(original, back.Single());
    }

    [TestMethod]
    public async Task ExportAsync_WritesUtf8WithDeclaration()
    {
        using MemoryStream stream = new();

        await _exporter.ExportAsync([RichCache()], stream);

        var text = Encoding.UTF8.GetString(stream.ToArray());
        StringAssert.StartsWith(text, "<?xml version=\"1.0\" encoding=\"utf-8\"");
        StringAssert.Contains(text, "ěščřž");
        Assert.AreNotEqual(0xEF, stream.ToArray()[0], "No byte order mark");
    }

    [TestMethod]
    public async Task ExportAsync_AlreadyCancelled_Throws()
    {
        using CancellationTokenSource cts = new();
        await cts.CancelAsync();

        await Assert.ThrowsAsync<OperationCanceledException>(() => _exporter.ExportAsync([RichCache()], new MemoryStream(), cancellationToken: cts.Token));
    }

    [TestMethod]
    [DataRow(CorrectedCoordinateMode.ExtraWaypoint)]
    [DataRow(CorrectedCoordinateMode.MainCoordinates)]
    public async Task RoundTrip_RichCacheWithCorrection_LosesNothing(CorrectedCoordinateMode mode)
    {
        var original = RichCache(corrected: _corrected);

        var back = await RoundTrip([original], new() { CorrectedCoordinates = mode });

        AssertSame(original, back.Single());
    }

    [TestMethod]
    [DataRow(CorrectedCoordinateMode.ExtraWaypoint)]
    [DataRow(CorrectedCoordinateMode.MainCoordinates)]
    public async Task RoundTrip_RichCacheWithoutCorrection_LosesNothing(CorrectedCoordinateMode mode)
    {
        var original = RichCache();

        var back = await RoundTrip([original], new() { CorrectedCoordinates = mode });

        AssertSame(original, back.Single());
    }

    [TestMethod]
    public async Task RoundTrip_MinimalCache_LosesNothing()
    {
        var original = MinimalCache();

        var back = await RoundTrip([original]);

        AssertSame(original, back.Single());
    }

    [TestMethod]
    public async Task RoundTrip_SeveralCaches_KeepsOrderAndData()
    {
        Geocache[] originals = [RichCache("GC8KX2Q", _corrected), MinimalCache(), RichCache("GC9ZZZZ")];

        var back = await RoundTrip(originals);

        Assert.AreEqual(3, back.Count);
        for (var i = 0; i < originals.Length; i++)
        {
            AssertSame(originals[i], back[i]);
        }
    }

    [TestMethod]
    [DataRow("groundspeak-1.0.gpx")]
    [DataRow("groundspeak-1.1.gpx")]
    [DataRow("gsak.gpx")]
    [DataRow("cgeo.gpx")]
    public async Task RoundTrip_ImportedFixture_Survives(string fixture)
    {
        var imported = await _importer.ImportAsync(File.OpenRead(FixturePath(fixture)), fixture);
        // The c:geo fixture carries deliberately unknown values, which come back as Unknown (and a warning on the way in).
        var originals = imported.Caches.Where(c => c.Type != GeocacheType.Unknown).ToList();

        using MemoryStream stream = new();
        await _exporter.ExportAsync(originals, stream);
        stream.Position = 0;
        var back = await _importer.ImportAsync(stream);

        Assert.AreEqual(originals.Count, back.Caches.Count);
        for (var i = 0; i < originals.Count; i++)
        {
            AssertSame(originals[i], back.Caches[i]);
        }
    }

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
