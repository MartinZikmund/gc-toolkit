using System.Globalization;
using System.Xml;
using System.Xml.Linq;
using GcToolkit.Core.Coordinates;

namespace GcToolkit.Core.Geocaches.Gpx;

/// <summary>
/// Streams one GPX file and collects its caches and waypoints. Dialect handling is by element
/// local name and namespace prefix of the vendor (Groundspeak 1.0/1.0.1/1.1, GSAK), so the
/// <c>xmlns</c> prefixes a tool picks do not matter.
/// </summary>
internal sealed class GpxReader(string label, IProgress<int>? progress, Func<int> nextCacheNumber)
{
    private const string GroundspeakNamespace = "http://www.groundspeak.com/cache/";
    private const string GsakNamespace = "http://www.gsak.net/";

    public List<Geocache> Caches { get; } = [];

    /// <summary>Waypoints that are not caches, with the parent code a GSAK export named, if any.</summary>
    public List<(GpxWaypoint Waypoint, GeocacheCode? Parent)> Waypoints { get; } = [];

    public List<GpxImportIssue> Warnings { get; } = [];

    public List<GpxImportIssue> Errors { get; } = [];

    public async Task ReadAsync(Stream stream, CancellationToken cancellationToken)
    {
        XmlReaderSettings settings = new()
        {
            Async = true,
            DtdProcessing = DtdProcessing.Prohibit,
            XmlResolver = null,
            IgnoreComments = true,
            IgnoreWhitespace = true,
            IgnoreProcessingInstructions = true,
        };

        try
        {
            using var reader = XmlReader.Create(stream, settings);
            await reader.MoveToContentAsync();
            if (reader.NodeType != XmlNodeType.Element || reader.LocalName != "gpx")
            {
                Errors.Add(new(label, GpxImportIssueKind.NotGpx));
                return;
            }

            await ReadWaypointsAsync(reader, cancellationToken);
        }
        catch (XmlException ex)
        {
            Errors.Add(new(label, GpxImportIssueKind.InvalidXml, ex.Message));
        }
    }

    private async Task ReadWaypointsAsync(XmlReader reader, CancellationToken cancellationToken)
    {
        DateTimeOffset? fileTime = null;
        var isEmpty = reader.IsEmptyElement;
        await reader.ReadAsync();
        if (isEmpty)
        {
            return;
        }

        while (!reader.EOF && reader.NodeType != XmlNodeType.EndElement)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (reader.NodeType != XmlNodeType.Element)
            {
                await reader.SkipAsync();
            }
            else if (reader.LocalName == "wpt")
            {
                var element = (XElement)await XNode.ReadFromAsync(reader, cancellationToken);
                ReadWaypoint(element, fileTime);
            }
            else if (reader.LocalName == "time")
            {
                fileTime = ParseDate(await reader.ReadElementContentAsStringAsync());
            }
            else if (reader.LocalName == "metadata")
            {
                // GPX 1.1 keeps the file time here instead of directly under <gpx>.
                var metadata = (XElement)await XNode.ReadFromAsync(reader, cancellationToken);
                fileTime = ParseDate(Text(metadata, "time")) ?? fileTime;
            }
            else
            {
                await reader.SkipAsync();
            }
        }
    }

    private void ReadWaypoint(XElement wpt, DateTimeOffset? fileTime)
    {
        var name = Text(wpt, "name") ?? string.Empty;
        if (!TryReadCoordinate(wpt, out var coordinate))
        {
            Warnings.Add(new(name.Length > 0 ? name : label, GpxImportIssueKind.MissingCoordinates));
            return;
        }

        var cacheElement = wpt.Elements().FirstOrDefault(e => e.Name.LocalName == "cache" && e.Name.NamespaceName.StartsWith(GroundspeakNamespace, StringComparison.Ordinal));
        var gsak = wpt.Elements().FirstOrDefault(e => e.Name.LocalName == "wptExtension" && e.Name.NamespaceName.StartsWith(GsakNamespace, StringComparison.Ordinal));

        if (cacheElement is null)
        {
            ReadPlainWaypoint(wpt, name, coordinate, gsak);
            return;
        }

        if (!GeocacheCode.TryParse(name, out var code))
        {
            Warnings.Add(new(name.Length > 0 ? name : label, GpxImportIssueKind.InvalidCacheCode));
            return;
        }

        Caches.Add(BuildCache(wpt, cacheElement, gsak, code, coordinate, fileTime));
        progress?.Report(nextCacheNumber());
    }

    private void ReadPlainWaypoint(XElement wpt, string name, GeoCoordinate coordinate, XElement? gsak)
    {
        GeocacheCode? parent = GeocacheCode.TryParse(Text(gsak, "Parent"), out var parentCode) ? parentCode : null;
        GpxWaypoint waypoint = new(name, coordinate)
        {
            Description = Text(wpt, "desc"),
            Comment = Text(wpt, "cmt"),
            Symbol = Text(wpt, "sym"),
            Type = Text(wpt, "type"),
            Time = ParseDate(Text(wpt, "time")),
        };
        Waypoints.Add((waypoint, parent));
    }

    private Geocache BuildCache(XElement wpt, XElement cache, XElement? gsak, GeocacheCode code, GeoCoordinate coordinate, DateTimeOffset? fileTime)
    {
        var subject = code.Value;
        var typeText = Text(cache, "type") ?? WaypointTypeSuffix(Text(wpt, "type"));
        var sizeText = Text(cache, "container");

        var type = GeocacheType.Unknown;
        if (typeText is not null && !GpxVocabulary.TryParseType(typeText, out type))
        {
            Warnings.Add(new(subject, GpxImportIssueKind.UnknownCacheType, typeText));
        }

        var size = GeocacheSize.Unknown;
        if (sizeText is not null && !GpxVocabulary.TryParseSize(sizeText, out size))
        {
            Warnings.Add(new(subject, GpxImportIssueKind.UnknownContainerSize, sizeText));
        }

        var isArchived = IsTrue(cache.Attribute("archived")?.Value);
        var isAvailable = cache.Attribute("available") is not { } available || IsTrue(available.Value);
        var status = isArchived ? GeocacheStatus.Archived : isAvailable ? GeocacheStatus.Available : GeocacheStatus.Disabled;

        var (posted, userData) = ReadUserData(wpt, gsak, coordinate);
        var shortDescription = cache.Elements().FirstOrDefault(e => e.Name.LocalName == "short_description");
        var longDescription = cache.Elements().FirstOrDefault(e => e.Name.LocalName == "long_description");

        return new Geocache
        {
            Code = code,
            Name = Text(cache, "name") ?? Text(wpt, "urlname") ?? code.Value,
            Type = type,
            Size = size,
            Status = status,
            Difficulty = ParseDouble(Text(cache, "difficulty")),
            Terrain = ParseDouble(Text(cache, "terrain")),
            Coordinate = posted,
            Owner = Text(cache, "owner") ?? Text(cache, "placed_by"),
            PlacedOn = ParseDate(Text(wpt, "time")),
            FavoritePoints = int.TryParse(Text(gsak, "FavPoints"), NumberStyles.Integer, CultureInfo.InvariantCulture, out var favorites) ? favorites : null,
            Country = Text(cache, "country"),
            State = Text(cache, "state"),
            ShortDescription = NullIfEmpty(shortDescription?.Value),
            ShortDescriptionIsHtml = IsTrue(shortDescription?.Attribute("html")?.Value),
            LongDescription = NullIfEmpty(longDescription?.Value),
            LongDescriptionIsHtml = IsTrue(longDescription?.Attribute("html")?.Value),
            Hint = NullIfEmpty(Text(cache, "encoded_hints")),
            Attributes = ReadAttributes(cache),
            Logs = ReadLogs(cache, subject),
            SourceId = GpxImporter.SourceId,
            UpdatedAt = fileTime,
            UserData = userData,
        };
    }

    // GSAK (and exports from it) write the corrected spot as the waypoint position and keep the posted one in *BeforeCorrect.
    private static (GeoCoordinate Posted, GeocacheUserData UserData) ReadUserData(XElement wpt, XElement? gsak, GeoCoordinate coordinate)
    {
        var isFound = string.Equals(Text(wpt, "sym"), "Geocache Found", StringComparison.OrdinalIgnoreCase)
            || !string.IsNullOrWhiteSpace(Text(gsak, "FoundByMeDate"));

        var posted = coordinate;
        GeoCoordinate? corrected = null;
        if (ParseDouble(Text(gsak, "LatBeforeCorrect")) is { } lat && ParseDouble(Text(gsak, "LonBeforeCorrect")) is { } lon)
        {
            GeoCoordinate before = new(lat, lon);
            if (before.IsValid)
            {
                posted = before;
                corrected = coordinate;
            }
        }

        return (posted, new GeocacheUserData
        {
            CorrectedCoordinate = corrected,
            Note = Text(gsak, "GcNote"),
            IsFound = isFound,
        });
    }

    private static List<GeocacheAttribute> ReadAttributes(XElement cache)
    {
        List<GeocacheAttribute> attributes = [];
        var container = cache.Elements().FirstOrDefault(e => e.Name.LocalName == "attributes");
        foreach (var element in container?.Elements().Where(e => e.Name.LocalName == "attribute") ?? [])
        {
            if (int.TryParse(element.Attribute("id")?.Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var id))
            {
                attributes.Add(new(id, element.Attribute("inc")?.Value is not ("0" or "false" or "False")));
            }
        }

        return attributes;
    }

    private List<GeocacheLog> ReadLogs(XElement cache, string subject)
    {
        List<GeocacheLog> logs = [];
        var container = cache.Elements().FirstOrDefault(e => e.Name.LocalName == "logs");
        foreach (var element in container?.Elements().Where(e => e.Name.LocalName == "log") ?? [])
        {
            var typeText = Text(element, "type");
            var type = GeocacheLogType.Unknown;
            if (typeText is not null && !GpxVocabulary.TryParseLogType(typeText, out type))
            {
                Warnings.Add(new(subject, GpxImportIssueKind.UnknownLogType, typeText));
            }

            logs.Add(new GeocacheLog
            {
                Id = element.Attribute("id")?.Value,
                Type = type,
                Author = Text(element, "finder"),
                Date = ParseDate(Text(element, "date")) ?? default,
                Text = element.Elements().FirstOrDefault(e => e.Name.LocalName == "text")?.Value,
            });
        }

        return logs;
    }

    private static bool TryReadCoordinate(XElement wpt, out GeoCoordinate coordinate)
    {
        coordinate = default;
        if (ParseDouble(wpt.Attribute("lat")?.Value) is { } lat && ParseDouble(wpt.Attribute("lon")?.Value) is { } lon)
        {
            coordinate = new(lat, lon);
            return coordinate.IsValid;
        }

        return false;
    }

    private static string? WaypointTypeSuffix(string? wptType)
        => wptType is not null && wptType.StartsWith("Geocache|", StringComparison.OrdinalIgnoreCase) ? wptType["Geocache|".Length..] : null;

    private static string? Text(XElement? parent, string localName)
        => NullIfEmpty(parent?.Elements().FirstOrDefault(e => e.Name.LocalName == localName)?.Value.Trim());

    private static string? NullIfEmpty(string? value) => string.IsNullOrEmpty(value) ? null : value;

    private static bool IsTrue(string? value) => string.Equals(value, "true", StringComparison.OrdinalIgnoreCase);

    private static double? ParseDouble(string? text)
        => double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var value) ? value : null;

    // GPX dates without a zone are UTC by convention.
    internal static DateTimeOffset? ParseDate(string? text)
        => DateTimeOffset.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var value) ? value : null;
}
