using System.IO.Compression;
using GcToolkit.Core.Coordinates;

namespace GcToolkit.Core.Geocaches.Gpx;

/// <summary>Reads caches from GPX files and pocket query ZIPs.</summary>
public sealed class GpxImporter : ICacheSource
{
    public const string SourceId = "gpx";

    public string Id => SourceId;

    public CacheSourceCapabilities Capabilities => CacheSourceCapabilities.Import;

    /// <summary>Imports one GPX file or one ZIP (a pocket query holds the caches and a <c>-wpts</c> file).</summary>
    public Task<GpxImportResult> ImportAsync(
        Stream stream,
        string? fileName = null,
        IProgress<int>? progress = null,
        CancellationToken cancellationToken = default)
        => ImportAsync([new GpxInput(stream, fileName)], progress, cancellationToken);

    /// <summary>
    /// Imports several files together, so a waypoints file picked next to its caches file still merges.
    /// <paramref name="progress"/> receives the running number of caches read.
    /// </summary>
    public async Task<GpxImportResult> ImportAsync(
        IReadOnlyList<GpxInput> inputs,
        IProgress<int>? progress = null,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var cacheCount = 0;
        List<Geocache> caches = [];
        List<(GpxWaypoint Waypoint, GeocacheCode? Parent)> waypoints = [];
        List<GpxImportIssue> warnings = [];
        List<GpxImportIssue> errors = [];

        foreach (var input in inputs)
        {
            await foreach (var (label, stream) in OpenGpxStreamsAsync(input, errors, cancellationToken))
            {
                await using (stream)
                {
                    GpxReader reader = new(label, progress, () => ++cacheCount);
                    await reader.ReadAsync(stream, cancellationToken);
                    caches.AddRange(reader.Caches);
                    waypoints.AddRange(reader.Waypoints);
                    warnings.AddRange(reader.Warnings);
                    errors.AddRange(reader.Errors);
                }
            }
        }

        return Combine(caches, waypoints, warnings, errors);
    }

    private static GpxImportResult Combine(
        List<Geocache> read,
        List<(GpxWaypoint Waypoint, GeocacheCode? Parent)> waypoints,
        List<GpxImportIssue> warnings,
        List<GpxImportIssue> errors)
    {
        Dictionary<GeocacheCode, int> indexByCode = [];
        List<Geocache> caches = [];
        foreach (var cache in read)
        {
            if (indexByCode.TryGetValue(cache.Code, out var index))
            {
                warnings.Add(new(cache.Code.Value, "Appears more than once in this import; the last copy was used."));
                caches[index] = cache;
            }
            else
            {
                indexByCode[cache.Code] = caches.Count;
                caches.Add(cache);
            }
        }

        Dictionary<GeocacheCode, List<Waypoint>> attached = [];
        List<GpxWaypoint> plain = [];
        var merged = 0;
        foreach (var (waypoint, parent) in waypoints)
        {
            if (FindParent(waypoint, parent, indexByCode) is { } parentCode)
            {
                if (!attached.TryGetValue(parentCode, out var list))
                {
                    attached[parentCode] = list = [];
                }

                list.Add(ToCacheWaypoint(waypoint));
                merged++;
            }
            else
            {
                plain.Add(waypoint);
            }
        }

        foreach (var (code, list) in attached)
        {
            var index = indexByCode[code];
            caches[index] = caches[index] with { Waypoints = list };
        }

        return new GpxImportResult
        {
            Caches = caches,
            PlainWaypoints = plain,
            WaypointsMerged = merged,
            Warnings = warnings,
            Errors = errors,
        };
    }

    // Pocket query child waypoints are named like the parent with the "GC" swapped for two other letters: PK8KX2Q -> GC8KX2Q.
    private static GeocacheCode? FindParent(GpxWaypoint waypoint, GeocacheCode? declaredParent, Dictionary<GeocacheCode, int> caches)
    {
        if (declaredParent is { } declared && caches.ContainsKey(declared))
        {
            return declared;
        }

        if (waypoint.Name.Length >= 3
            && !waypoint.Name.StartsWith("GC", StringComparison.OrdinalIgnoreCase)
            && GeocacheCode.TryParse("GC" + waypoint.Name[2..], out var guessed)
            && caches.ContainsKey(guessed))
        {
            return guessed;
        }

        return null;
    }

    private static Waypoint ToCacheWaypoint(GpxWaypoint source)
    {
        var typeText = source.Symbol;
        if (!GpxVocabulary.TryParseWaypointType(typeText, out var type)
            && !GpxVocabulary.TryParseWaypointType(source.Type?.Split('|').Last(), out type))
        {
            type = WaypointType.Unknown;
        }

        // Hidden waypoints (puzzle finals) come with 0,0.
        GeoCoordinate? coordinate = source.Coordinate is { Latitude: 0, Longitude: 0 } ? null : source.Coordinate;
        return new Waypoint
        {
            Prefix = source.Name.Length >= 2 ? source.Name[..2].ToUpperInvariant() : source.Name.ToUpperInvariant(),
            Name = source.Description ?? source.Symbol ?? source.Name,
            Type = type,
            Coordinate = coordinate,
            Note = source.Comment,
        };
    }

    private static async IAsyncEnumerable<(string Label, Stream Stream)> OpenGpxStreamsAsync(
        GpxInput input,
        List<GpxImportIssue> errors,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var label = input.FileName ?? "GPX file";
        var stream = input.Stream;
        if (!stream.CanSeek)
        {
            MemoryStream buffer = new();
            await stream.CopyToAsync(buffer, cancellationToken);
            buffer.Position = 0;
            stream = buffer;
        }

        var start = stream.Position;
        var isZip = stream.ReadByte() == 'P' && stream.ReadByte() == 'K';
        stream.Position = start;
        if (!isZip)
        {
            yield return (label, stream);
            yield break;
        }

        List<(string Label, Stream Stream)> entries = [];
        try
        {
            using ZipArchive archive = new(stream, ZipArchiveMode.Read, leaveOpen: true);
            foreach (var entry in archive.Entries.Where(e => e.Name.EndsWith(".gpx", StringComparison.OrdinalIgnoreCase)))
            {
                cancellationToken.ThrowIfCancellationRequested();
                MemoryStream copy = new();
                await using (var source = await entry.OpenAsync(cancellationToken))
                {
                    await source.CopyToAsync(copy, cancellationToken);
                }

                copy.Position = 0;
                entries.Add(($"{label}/{entry.Name}", copy));
            }
        }
        catch (InvalidDataException ex)
        {
            errors.Add(new(label, $"The ZIP archive is damaged ({ex.Message})."));
            yield break;
        }

        if (entries.Count == 0)
        {
            errors.Add(new(label, "The ZIP archive contains no GPX files."));
        }

        foreach (var entry in entries)
        {
            yield return entry;
        }
    }
}

/// <summary>A GPX or ZIP stream; <paramref name="FileName"/> only labels messages.</summary>
public sealed record GpxInput(Stream Stream, string? FileName = null);
