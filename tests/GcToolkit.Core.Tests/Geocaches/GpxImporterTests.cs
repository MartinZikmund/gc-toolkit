using System.IO.Compression;
using System.Text;
using GcToolkit.Core.Coordinates;
using GcToolkit.Core.Geocaches;
using GcToolkit.Core.Geocaches.Gpx;

namespace GcToolkit.Core.Tests.Geocaches;

[TestClass]
public class GpxImporterTests
{
    private readonly GpxImporter _importer = new();

    private static string FixturePath(string name) => Path.Combine(AppContext.BaseDirectory, "Geocaches", "Fixtures", name);

    private static string Fixture(string name) => File.ReadAllText(FixturePath(name));

    private static MemoryStream Utf8(string text) => new(Encoding.UTF8.GetBytes(text));

    private Task<GpxImportResult> ImportFixture(string name, CancellationToken ct = default)
        => _importer.ImportAsync(File.OpenRead(FixturePath(name)), name, cancellationToken: ct);

    private static Geocache Cache(GpxImportResult result, string code)
        => result.Caches.Single(c => c.Code == GeocacheCode.Parse(code));

    private static MemoryStream PocketQueryZip()
    {
        MemoryStream zip = new();
        using (ZipArchive archive = new(zip, ZipArchiveMode.Create, leaveOpen: true))
        {
            AddEntry(archive, "12345.gpx", Fixture("groundspeak-1.0.1-pq.gpx"));
            AddEntry(archive, "12345-wpts.gpx", Fixture("groundspeak-1.0.1-pq-wpts.gpx"));
        }

        zip.Position = 0;
        return zip;
    }

    private static void AddEntry(ZipArchive archive, string name, string content)
    {
        using StreamWriter writer = new(archive.CreateEntry(name).Open(), new UTF8Encoding(false));
        writer.Write(content);
    }

    [TestMethod]
    public void Source_Identity_IsGpxImport()
    {
        Assert.AreEqual("gpx", _importer.Id);
        Assert.AreEqual(CacheSourceCapabilities.Import, _importer.Capabilities);
    }

    [TestMethod]
    public async Task Import_Groundspeak10_ReadsCoreFields()
    {
        var result = await ImportFixture("groundspeak-1.0.gpx");

        var cache = Cache(result, "GC1A2B3");
        Assert.AreEqual(1, result.Caches.Count);
        Assert.AreEqual("Old Town Square", cache.Name);
        Assert.AreEqual(GeocacheType.Traditional, cache.Type);
        Assert.AreEqual(GeocacheSize.Small, cache.Size);
        Assert.AreEqual(1.5, cache.Difficulty);
        Assert.AreEqual(1.0, cache.Terrain);
        Assert.AreEqual(new GeoCoordinate(50.087465, 14.421254), cache.Coordinate);
        Assert.AreEqual("Prague Team", cache.Owner);
        Assert.AreEqual(new DateTimeOffset(2004, 5, 12, 7, 0, 0, TimeSpan.Zero), cache.PlacedOn);
        Assert.AreEqual("Czech Republic", cache.Country);
        Assert.AreEqual("Praha", cache.State);
        Assert.AreEqual("Under the bench", cache.Hint);
        Assert.AreEqual("gpx", cache.SourceId);
        Assert.AreEqual(new DateTimeOffset(2010, 3, 14, 10, 0, 0, TimeSpan.Zero), cache.UpdatedAt);
        Assert.AreEqual(GeocacheStatus.Available, cache.Status);
        Assert.AreEqual(1, cache.Logs.Count);
        Assert.AreEqual("9001", cache.Logs[0].Id);
        Assert.AreEqual(GeocacheLogType.Found, cache.Logs[0].Type);
        Assert.AreEqual("Lucky Finder", cache.Logs[0].Author);
        Assert.AreEqual("TFTC!", cache.Logs[0].Text);
        Assert.AreEqual(0, result.Warnings.Count);
        Assert.AreEqual(0, result.Errors.Count);
    }

    [TestMethod]
    public async Task Import_Groundspeak101_ReadsDescriptionsAttributesAndLogs()
    {
        var result = await ImportFixture("groundspeak-1.0.1-pq.gpx");

        var cache = Cache(result, "GC8KX2Q");
        Assert.AreEqual(GeocacheType.Mystery, cache.Type);
        Assert.AreEqual(GeocacheSize.Regular, cache.Size);
        Assert.AreEqual("Martin & Co", cache.Owner);
        Assert.AreEqual("<b>Solve the puzzle</b>", cache.ShortDescription);
        Assert.IsTrue(cache.ShortDescriptionIsHtml);
        Assert.IsTrue(cache.LongDescriptionIsHtml);
        Assert.AreEqual("<p>Legion &amp; cache. Final is near <i>the tree</i>.</p>", cache.LongDescription);
        CollectionAssert.AreEqual(
            new[] { new GeocacheAttribute(13, true), new GeocacheAttribute(1, false) },
            cache.Attributes.ToArray());
        Assert.AreEqual(2, cache.Logs.Count);
        Assert.AreEqual(GeocacheLogType.Publish, cache.Logs[0].Type);
        Assert.AreEqual(GeocacheLogType.DidNotFind, cache.Logs[1].Type);
        Assert.AreEqual(new DateTimeOffset(2013, 9, 1, 10, 15, 0, TimeSpan.Zero), cache.Logs[1].Date);
    }

    [TestMethod]
    public async Task Import_ArchivedAndDisabled_MapStatus()
    {
        var result = await ImportFixture("groundspeak-1.0.1-pq.gpx");

        Assert.AreEqual(GeocacheStatus.Archived, Cache(result, "GC5ARCH").Status);
        Assert.AreEqual(GeocacheStatus.Disabled, Cache(result, "GC5DISB").Status);
        Assert.AreEqual(GeocacheSize.NotChosen, Cache(result, "GC5DISB").Size);
        Assert.AreEqual(GeocacheType.Multi, Cache(result, "GC5ARCH").Type);
    }

    [TestMethod]
    public async Task Import_TimeWithoutZone_IsTreatedAsUtc()
    {
        var result = await ImportFixture("groundspeak-1.0.1-pq.gpx");

        Assert.AreEqual(new DateTimeOffset(2015, 1, 10, 0, 0, 0, TimeSpan.Zero), Cache(result, "GC5ARCH").PlacedOn);
    }

    [TestMethod]
    public async Task Import_UnparsableCoordinates_SkipsWithWarning()
    {
        var result = await ImportFixture("groundspeak-1.0.1-pq.gpx");

        Assert.AreEqual(3, result.Caches.Count);
        Assert.AreEqual(0, result.PlainWaypoints.Count);
        Assert.IsTrue(result.Warnings.Any(w => w.Subject == "GCBADBAD"));
    }

    [TestMethod]
    public async Task Import_Groundspeak11_ReadsEarthcache()
    {
        var result = await ImportFixture("groundspeak-1.1.gpx");

        var cache = Cache(result, "GC9EART");
        Assert.AreEqual(GeocacheType.Earth, cache.Type);
        Assert.AreEqual(GeocacheSize.Virtual, cache.Size);
        Assert.AreEqual(4.0, cache.Difficulty);
        Assert.AreEqual(1.5, cache.Terrain);
        Assert.AreEqual(GeocacheLogType.Attended, cache.Logs.Single().Type);
        Assert.IsNull(cache.Hint);
        Assert.IsNull(cache.ShortDescription);
        Assert.AreEqual(new GeocacheAttribute(24, true), cache.Attributes.Single());
    }

    [TestMethod]
    public async Task Import_PocketQueryZip_MergesAdditionalWaypoints()
    {
        var result = await _importer.ImportAsync(PocketQueryZip(), "12345.zip");

        var waypoints = Cache(result, "GC8KX2Q").Waypoints;
        Assert.AreEqual(3, waypoints.Count);
        Assert.AreEqual(3, result.WaypointsMerged);

        var parking = waypoints.Single(w => w.Type == WaypointType.Parking);
        Assert.AreEqual("PK", parking.Prefix);
        Assert.AreEqual("Parking", parking.Name);
        Assert.AreEqual("Park here, free after 18:00", parking.Note);
        Assert.AreEqual(new GeoCoordinate(50.065, 14.415), parking.Coordinate);

        Assert.AreEqual(WaypointType.Question, waypoints.Single(w => w.Prefix == "S1").Type);
    }

    [TestMethod]
    public async Task Import_PocketQueryZip_HiddenCoordinatesBecomeNull()
    {
        var result = await _importer.ImportAsync(PocketQueryZip(), "12345.zip");

        var final = Cache(result, "GC8KX2Q").Waypoints.Single(w => w.Type == WaypointType.Final);
        Assert.IsNull(final.Coordinate);
    }

    [TestMethod]
    public async Task Import_PocketQueryZip_OrphanWaypointIsReportedNotDropped()
    {
        var result = await _importer.ImportAsync(PocketQueryZip(), "12345.zip");

        Assert.AreEqual("PK0NONE", result.PlainWaypoints.Single().Name);
    }

    [TestMethod]
    public async Task Import_ZipWithoutSeeking_Works()
    {
        using var source = PocketQueryZip();
        using NonSeekableStream stream = new(source);

        var result = await _importer.ImportAsync(stream);

        Assert.AreEqual(3, Cache(result, "GC8KX2Q").Waypoints.Count);
    }

    [TestMethod]
    public async Task Import_TwoFilesPickedSeparately_MergeWaypoints()
    {
        var result = await _importer.ImportAsync(
            [
                new GpxInput(File.OpenRead(FixturePath("groundspeak-1.0.1-pq-wpts.gpx")), "q-wpts.gpx"),
                new GpxInput(File.OpenRead(FixturePath("groundspeak-1.0.1-pq.gpx")), "q.gpx"),
            ]);

        Assert.AreEqual(3, Cache(result, "GC8KX2Q").Waypoints.Count);
    }

    [TestMethod]
    public async Task Import_Gsak_MapsCorrectedCoordinatesAndUserData()
    {
        var result = await ImportFixture("gsak.gpx");

        var cache = Cache(result, "GC8KX2Q");
        Assert.AreEqual(new GeoCoordinate(50.06666, 14.41666), cache.Coordinate);
        Assert.AreEqual(new GeoCoordinate(50.06193, 14.4247), cache.UserData.CorrectedCoordinate);
        Assert.AreEqual("Look near the old oak", cache.UserData.Note);
        Assert.IsTrue(cache.UserData.IsFound);
        Assert.IsFalse(cache.UserData.IsSolved);
        Assert.AreEqual(12, cache.FavoritePoints);
    }

    [TestMethod]
    public async Task Import_GsakChildWaypoints_AttachByParentElement()
    {
        var result = await ImportFixture("gsak.gpx");

        var waypoints = Cache(result, "GC8KX2Q").Waypoints;
        Assert.AreEqual(2, waypoints.Count);
        Assert.AreEqual("MY", waypoints.Single(w => w.Name == "Car").Prefix);
        Assert.AreEqual(0, result.PlainWaypoints.Count);
    }

    [TestMethod]
    public async Task Import_NoCorrection_LeavesUserDataEmpty()
    {
        var result = await ImportFixture("groundspeak-1.0.1-pq.gpx");

        Assert.AreEqual(new GeocacheUserData(), Cache(result, "GC8KX2Q").UserData);
    }

    [TestMethod]
    public async Task Import_Cgeo_ReadsFoundLetterboxAndWaypoint()
    {
        var result = await ImportFixture("cgeo.gpx");

        var cache = Cache(result, "GC3CGEO");
        Assert.AreEqual(GeocacheType.Letterbox, cache.Type);
        Assert.AreEqual(GeocacheSize.Other, cache.Size);
        Assert.IsTrue(cache.UserData.IsFound);
        Assert.IsFalse(cache.ShortDescriptionIsHtml);
        Assert.IsTrue(cache.LongDescriptionIsHtml);
        Assert.AreEqual(2.0, cache.Difficulty);
        Assert.AreEqual("Hans", cache.Owner);
        Assert.AreEqual("Behind the pub", cache.Waypoints.Single().Note);
    }

    [TestMethod]
    public async Task Import_UnknownTypeAndSize_FallBackWithWarning()
    {
        var result = await ImportFixture("cgeo.gpx");

        var cache = Cache(result, "GC4NEW1");
        Assert.AreEqual(GeocacheType.Unknown, cache.Type);
        Assert.AreEqual(GeocacheSize.Unknown, cache.Size);
        Assert.AreEqual(2, result.Warnings.Count(w => w.Subject == "GC4NEW1"));
    }

    [TestMethod]
    public async Task Import_PlainGpx_ReportsWaypointsAndIgnoresTracks()
    {
        var result = await ImportFixture("plain.gpx");

        Assert.AreEqual(0, result.Caches.Count);
        Assert.AreEqual(2, result.PlainWaypoints.Count);
        var viewpoint = result.PlainWaypoints[0];
        Assert.AreEqual("Viewpoint", viewpoint.Name);
        Assert.AreEqual(new GeoCoordinate(50.1, 14.1), viewpoint.Coordinate);
        Assert.AreEqual("Rock with a view", viewpoint.Description);
        Assert.AreEqual("Great view", viewpoint.Comment);
        Assert.AreEqual("Flag, Blue", viewpoint.Symbol);
        Assert.AreEqual("Scenic", viewpoint.Type);
        Assert.AreEqual(new DateTimeOffset(2023, 9, 9, 9, 9, 9, TimeSpan.Zero), viewpoint.Time);
    }

    [TestMethod]
    public async Task Import_SameCodeTwice_LastOneWinsWithWarning()
    {
        var result = await _importer.ImportAsync(
            [
                new GpxInput(File.OpenRead(FixturePath("groundspeak-1.0.1-pq.gpx")), "a.gpx"),
                new GpxInput(File.OpenRead(FixturePath("gsak.gpx")), "b.gpx"),
            ]);

        Assert.AreEqual(3, result.Caches.Count);
        Assert.IsNotNull(Cache(result, "GC8KX2Q").UserData.CorrectedCoordinate);
        Assert.IsTrue(result.Warnings.Any(w => w.Subject == "GC8KX2Q"));
    }

    [TestMethod]
    public async Task Import_TruncatedXml_KeepsWhatWasReadAndReportsError()
    {
        var text = Fixture("groundspeak-1.0.1-pq.gpx");
        var truncated = text[..(text.IndexOf("<wpt lat=\"49.195000\"", StringComparison.Ordinal) + 40)];

        var result = await _importer.ImportAsync(Utf8(truncated), "broken.gpx");

        Assert.IsNotNull(Cache(result, "GC8KX2Q"));
        Assert.AreEqual("broken.gpx", result.Errors.Single().Subject);
    }

    [TestMethod]
    public async Task Import_NotXml_ReportsError()
    {
        var result = await _importer.ImportAsync(Utf8("definitely not a gpx"), "notes.txt");

        Assert.AreEqual(0, result.Caches.Count);
        Assert.AreEqual("notes.txt", result.Errors.Single().Subject);
    }

    [TestMethod]
    public async Task Import_DoctypeDeclaration_IsRejectedAsError()
    {
        const string xml = "<?xml version=\"1.0\"?><!DOCTYPE gpx [<!ENTITY x \"boom\">]><gpx><wpt lat=\"1\" lon=\"1\"><name>&x;</name></wpt></gpx>";

        var result = await _importer.ImportAsync(Utf8(xml), "evil.gpx");

        Assert.AreEqual(0, result.PlainWaypoints.Count);
        Assert.AreEqual(1, result.Errors.Count);
    }

    [TestMethod]
    public async Task Import_ZipWithoutGpx_ReportsError()
    {
        MemoryStream zip = new();
        using (ZipArchive archive = new(zip, ZipArchiveMode.Create, leaveOpen: true))
        {
            AddEntry(archive, "readme.txt", "hi");
        }

        zip.Position = 0;
        var result = await _importer.ImportAsync(zip, "x.zip");

        Assert.AreEqual(1, result.Errors.Count);
    }

    [TestMethod]
    public async Task Import_AlreadyCancelled_Throws()
    {
        using CancellationTokenSource cts = new();
        await cts.CancelAsync();

        await Assert.ThrowsAsync<OperationCanceledException>(() => ImportFixture("groundspeak-1.0.1-pq.gpx", cts.Token));
    }

    [TestMethod]
    public async Task Import_CancelledMidway_Throws()
    {
        using CancellationTokenSource cts = new();
        SyncProgress syncProgress = new(count =>
        {
            if (count >= 10)
            {
                cts.Cancel();
            }
        });

        await Assert.ThrowsAsync<OperationCanceledException>(
            () => _importer.ImportAsync(Utf8(BuildLargeGpx(500)), "big.gpx", syncProgress, cts.Token));
    }

    [TestMethod]
    public async Task Import_LargeFile_ParsesEveryCache()
    {
        const int count = 5000;
        List<int> reports = [];

        var result = await _importer.ImportAsync(Utf8(BuildLargeGpx(count)), "big.gpx", new SyncProgress(reports.Add));

        Assert.AreEqual(count, result.Caches.Count);
        Assert.AreEqual(count, reports[^1]);
    }

    private static string BuildLargeGpx(int count)
    {
        StringBuilder sb = new();
        sb.Append("<?xml version=\"1.0\"?><gpx version=\"1.0\" xmlns=\"http://www.topografix.com/GPX/1/0\">");
        for (var i = 0; i < count; i++)
        {
            var code = $"GC{(i + 1000):X}";
            sb.Append($"<wpt lat=\"50.{i:D4}\" lon=\"14.{i:D4}\"><name>{code}</name><type>Geocache|Traditional Cache</type>")
              .Append("<groundspeak:cache id=\"1\" available=\"True\" archived=\"False\" xmlns:groundspeak=\"http://www.groundspeak.com/cache/1/0/1\">")
              .Append($"<groundspeak:name>Cache {i}</groundspeak:name><groundspeak:type>Traditional Cache</groundspeak:type>")
              .Append("<groundspeak:container>Small</groundspeak:container><groundspeak:difficulty>1</groundspeak:difficulty><groundspeak:terrain>1</groundspeak:terrain>")
              .Append("<groundspeak:long_description html=\"False\">Some words to make the file chunkier.</groundspeak:long_description>")
              .Append("</groundspeak:cache></wpt>");
        }

        return sb.Append("</gpx>").ToString();
    }

    private sealed class SyncProgress(Action<int> report) : IProgress<int>
    {
        public void Report(int value) => report(value);
    }

    private sealed class NonSeekableStream(Stream inner) : Stream
    {
        public override bool CanRead => true;

        public override bool CanSeek => false;

        public override bool CanWrite => false;

        public override long Length => throw new NotSupportedException();

        public override long Position
        {
            get => throw new NotSupportedException();
            set => throw new NotSupportedException();
        }

        public override void Flush()
        {
        }

        public override int Read(byte[] buffer, int offset, int count) => inner.Read(buffer, offset, count);

        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

        public override void SetLength(long value) => throw new NotSupportedException();

        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }
}
