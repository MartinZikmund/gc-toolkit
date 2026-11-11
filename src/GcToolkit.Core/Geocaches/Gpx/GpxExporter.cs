using System.Globalization;
using System.Text;
using System.Xml;
using System.Xml.Linq;
using GcToolkit.Core.Coordinates;

namespace GcToolkit.Core.Geocaches.Gpx;

/// <summary>How a cache's corrected coordinates leave the app.</summary>
public enum CorrectedCoordinateMode
{
    /// <summary>Posted coordinates stay the main position; the corrected spot is added as an extra "Final Location" waypoint.</summary>
    ExtraWaypoint,

    /// <summary>The corrected spot becomes the main position, so a GPS unit navigates straight to it. The posted one is kept in GSAK's <c>*BeforeCorrect</c> fields.</summary>
    MainCoordinates,
}

public sealed record GpxExportOptions
{
    public CorrectedCoordinateMode CorrectedCoordinates { get; init; } = CorrectedCoordinateMode.ExtraWaypoint;

    public bool IncludeLogs { get; init; } = true;
}

/// <summary>
/// Writes caches as Groundspeak 1.0.1 GPX that c:geo, GSAK, Locus and Garmin units read. Things the
/// format has no room for (solved state, premium flag, images) go into a <c>gct:</c> extension that
/// other apps ignore and GC Toolkit reads back, so exporting and re-importing loses nothing.
/// </summary>
public sealed class GpxExporter(TimeProvider? timeProvider = null)
{
    private static readonly XNamespace _gpx = GpxNamespaces.Gpx;
    private static readonly XNamespace _gs = GpxNamespaces.Groundspeak;
    private static readonly XNamespace _gsak = GpxNamespaces.Gsak;
    private static readonly XNamespace _gct = GpxNamespaces.Gct;

    private readonly TimeProvider _timeProvider = timeProvider ?? TimeProvider.System;

    public string Export(IEnumerable<Geocache> caches, GpxExportOptions? options = null)
    {
        using MemoryStream buffer = new();
        Write(caches, buffer, options ?? new(), CancellationToken.None);
        return Encoding.UTF8.GetString(buffer.ToArray());
    }

    public async Task ExportAsync(IEnumerable<Geocache> caches, Stream destination, GpxExportOptions? options = null, CancellationToken cancellationToken = default)
    {
        await using MemoryStream buffer = new();
        Write(caches, buffer, options ?? new(), cancellationToken);
        buffer.Position = 0;
        await buffer.CopyToAsync(destination, cancellationToken);
    }

    private void Write(IEnumerable<Geocache> caches, Stream output, GpxExportOptions options, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        XElement root = new(
            _gpx + "gpx",
            new XAttribute("version", "1.0"),
            new XAttribute("creator", GpxNamespaces.ExporterName),
            new XAttribute(XNamespace.Xmlns + "groundspeak", _gs.NamespaceName),
            new XAttribute(XNamespace.Xmlns + "gsak", _gsak.NamespaceName),
            new XAttribute(XNamespace.Xmlns + "gct", _gct.NamespaceName),
            new XElement(_gpx + "name", "GC Toolkit export"),
            new XElement(_gpx + "time", FormatDate(_timeProvider.GetUtcNow())));

        foreach (var cache in caches)
        {
            cancellationToken.ThrowIfCancellationRequested();
            root.Add(CacheWaypoint(cache, options));
            foreach (var waypoint in cache.Waypoints)
            {
                root.Add(AdditionalWaypoint(cache, waypoint));
            }

            if (options.CorrectedCoordinates == CorrectedCoordinateMode.ExtraWaypoint && cache.UserData.CorrectedCoordinate is { } corrected)
            {
                root.Add(CorrectedWaypoint(cache, corrected));
            }
        }

        XmlWriterSettings settings = new()
        {
            Encoding = new UTF8Encoding(false),
            Indent = true,
            NewLineHandling = NewLineHandling.Entitize,
        };
        using var writer = XmlWriter.Create(output, settings);
        new XDocument(root).Save(writer);
    }

    private static XElement CacheWaypoint(Geocache cache, GpxExportOptions options)
    {
        var corrected = cache.UserData.CorrectedCoordinate;
        var useCorrectedAsMain = corrected is not null && options.CorrectedCoordinates == CorrectedCoordinateMode.MainCoordinates;
        var main = useCorrectedAsMain ? corrected!.Value : cache.Coordinate;
        var typeText = GpxVocabulary.TypeText(cache.Type);

        XElement wpt = new(
            _gpx + "wpt",
            Lat(main),
            Lon(main),
            Optional(_gpx + "time", cache.PlacedOn is { } placed ? FormatDate(placed) : null),
            new XElement(_gpx + "name", cache.Code.Value),
            new XElement(_gpx + "desc", Clean(Describe(cache, typeText))),
            new XElement(_gpx + "url", $"https://coord.info/{cache.Code}"),
            new XElement(_gpx + "urlname", Clean(cache.Name)),
            new XElement(_gpx + "sym", cache.UserData.IsFound ? "Geocache Found" : "Geocache"),
            new XElement(_gpx + "type", typeText is null ? "Geocache" : $"Geocache|{typeText}"),
            GroundspeakCache(cache, options));

        var gsak = GsakExtension(cache, useCorrectedAsMain ? cache.Coordinate : null);
        if (gsak is not null)
        {
            wpt.Add(gsak);
        }

        var gct = CacheExtension(cache);
        if (gct is not null)
        {
            wpt.Add(gct);
        }

        return wpt;
    }

    private static XElement GroundspeakCache(Geocache cache, GpxExportOptions options)
    {
        XElement element = new(
            _gs + "cache",
            new XAttribute("available", cache.Status == GeocacheStatus.Disabled ? "False" : "True"),
            new XAttribute("archived", cache.Status == GeocacheStatus.Archived ? "True" : "False"),
            new XElement(_gs + "name", Clean(cache.Name)),
            Optional(_gs + "placed_by", cache.Owner),
            Optional(_gs + "owner", cache.Owner),
            Optional(_gs + "type", GpxVocabulary.TypeText(cache.Type)),
            Optional(_gs + "container", GpxVocabulary.SizeText(cache.Size)));

        if (cache.Attributes.Count > 0)
        {
            element.Add(new XElement(
                _gs + "attributes",
                cache.Attributes.Select(a => new XElement(
                    _gs + "attribute",
                    new XAttribute("id", a.Id.ToString(CultureInfo.InvariantCulture)),
                    new XAttribute("inc", a.IsPositive ? "1" : "0")))));
        }

        element.Add(
            Optional(_gs + "difficulty", cache.Difficulty is { } d ? Number(d) : null),
            Optional(_gs + "terrain", cache.Terrain is { } t ? Number(t) : null),
            Optional(_gs + "country", cache.Country),
            Optional(_gs + "state", cache.State),
            Description(_gs + "short_description", cache.ShortDescription, cache.ShortDescriptionIsHtml),
            Description(_gs + "long_description", cache.LongDescription, cache.LongDescriptionIsHtml),
            Optional(_gs + "encoded_hints", cache.Hint));

        if (options.IncludeLogs && cache.Logs.Count > 0)
        {
            element.Add(new XElement(_gs + "logs", cache.Logs.Select(Log)));
        }

        return element;
    }

    private static XElement Log(GeocacheLog log)
    {
        XElement element = new(_gs + "log");
        if (log.Id is not null)
        {
            element.Add(new XAttribute("id", log.Id));
        }

        element.Add(
            new XElement(_gs + "date", FormatDate(log.Date)),
            Optional(_gs + "type", GpxVocabulary.LogTypeText(log.Type)),
            Optional(_gs + "finder", log.Author),
            log.Text is null ? null : new XElement(_gs + "text", new XAttribute("encoded", "False"), Clean(log.Text)));
        return element;
    }

    // GSAK's convention: the waypoint position is the corrected spot and the posted one lives in *BeforeCorrect.
    private static XElement? GsakExtension(Geocache cache, GeoCoordinate? posted)
    {
        if (cache.FavoritePoints is null && string.IsNullOrEmpty(cache.UserData.Note) && posted is null)
        {
            return null;
        }

        return new XElement(
            _gsak + "wptExtension",
            Optional(_gsak + "FavPoints", cache.FavoritePoints?.ToString(CultureInfo.InvariantCulture)),
            Optional(_gsak + "GcNote", cache.UserData.Note),
            posted is { } p ? new XElement(_gsak + "LatBeforeCorrect", Number(p.Latitude)) : null,
            posted is { } q ? new XElement(_gsak + "LonBeforeCorrect", Number(q.Longitude)) : null);
    }

    private static XElement? CacheExtension(Geocache cache)
    {
        if (!cache.UserData.IsSolved && !cache.IsPremiumOnly && cache.UpdatedAt is null && cache.Images.Count == 0)
        {
            return null;
        }

        return new XElement(
            _gct + "cacheExtension",
            cache.UserData.IsSolved ? new XElement(_gct + "solved", "true") : null,
            cache.IsPremiumOnly ? new XElement(_gct + "premiumOnly", "true") : null,
            cache.UpdatedAt is { } updated ? new XElement(_gct + "updatedAt", FormatDate(updated)) : null,
            cache.Images.Select(i => new XElement(
                _gct + "image",
                new XAttribute("url", Clean(i.Url)),
                i.Name is null ? null : new XAttribute("name", Clean(i.Name)),
                i.Description is null ? null : new XAttribute("description", Clean(i.Description)))));
    }

    private static XElement AdditionalWaypoint(Geocache cache, Waypoint waypoint)
    {
        var symbol = GpxVocabulary.WaypointTypeText(waypoint.Type);
        var coordinate = waypoint.Coordinate ?? default;
        return new XElement(
            _gpx + "wpt",
            Lat(coordinate),
            Lon(coordinate),
            new XElement(_gpx + "name", ChildName(cache, waypoint.Prefix)),
            Optional(_gpx + "cmt", waypoint.Note),
            new XElement(_gpx + "desc", Clean(waypoint.Name)),
            new XElement(_gpx + "url", $"https://coord.info/{cache.Code}"),
            new XElement(_gpx + "urlname", Clean(cache.Name)),
            Optional(_gpx + "sym", symbol),
            new XElement(_gpx + "type", symbol is null ? "Waypoint" : $"Waypoint|{symbol}"),
            ParentExtension(cache));
    }

    // Plain Final Location waypoint for other apps; the marker lets GC Toolkit turn it back into a correction.
    private static XElement CorrectedWaypoint(Geocache cache, GeoCoordinate corrected)
        => new(
            _gpx + "wpt",
            Lat(corrected),
            Lon(corrected),
            new XElement(_gpx + "name", ChildName(cache, "CC")),
            new XElement(_gpx + "cmt", "Corrected coordinates"),
            new XElement(_gpx + "desc", "Corrected coordinates"),
            new XElement(_gpx + "url", $"https://coord.info/{cache.Code}"),
            new XElement(_gpx + "urlname", Clean(cache.Name)),
            new XElement(_gpx + "sym", "Final Location"),
            new XElement(_gpx + "type", "Waypoint|Final Location"),
            ParentExtension(cache),
            new XElement(_gct + "correctedFinal"));

    private static XElement ParentExtension(Geocache cache)
        => new(_gsak + "wptExtension", new XElement(_gsak + "Parent", cache.Code.Value));

    // Same naming as pocket queries: PK8KX2Q for the parking of GC8KX2Q.
    private static string ChildName(Geocache cache, string prefix) => prefix + cache.Code.Value[2..];

    private static string Describe(Geocache cache, string? typeText)
    {
        StringBuilder sb = new(cache.Name);
        if (cache.Owner is not null)
        {
            sb.Append(" by ").Append(cache.Owner);
        }

        if (typeText is not null)
        {
            sb.Append(", ").Append(typeText);
        }

        if (cache.Difficulty is { } d && cache.Terrain is { } t)
        {
            sb.Append(" (").Append(Number(d)).Append('/').Append(Number(t)).Append(')');
        }

        return sb.ToString();
    }

    private static XElement? Description(XName name, string? text, bool isHtml)
        => text is null ? null : new XElement(name, new XAttribute("html", isHtml ? "True" : "False"), Clean(text));

    private static XElement? Optional(XName name, string? value)
        => value is null ? null : new XElement(name, Clean(value));

    private static XAttribute Lat(GeoCoordinate coordinate) => new("lat", Number(coordinate.Latitude));

    private static XAttribute Lon(GeoCoordinate coordinate) => new("lon", Number(coordinate.Longitude));

    private static string Number(double value) => value.ToString("R", CultureInfo.InvariantCulture);

    private static string FormatDate(DateTimeOffset value)
        => value.UtcDateTime.ToString("yyyy-MM-dd'T'HH:mm:ss.FFFFFFF'Z'", CultureInfo.InvariantCulture);

    // XML 1.0 cannot carry most control characters at all; drop them instead of failing the whole export.
    private static string Clean(string value)
    {
        StringBuilder sb = new(value.Length);
        for (var i = 0; i < value.Length; i++)
        {
            if (char.IsHighSurrogate(value[i]) && i + 1 < value.Length && char.IsLowSurrogate(value[i + 1]))
            {
                sb.Append(value[i]).Append(value[i + 1]);
                i++;
            }
            else if (XmlConvert.IsXmlChar(value[i]))
            {
                sb.Append(value[i]);
            }
        }

        return sb.ToString();
    }
}
