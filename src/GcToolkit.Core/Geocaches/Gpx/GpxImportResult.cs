using GcToolkit.Core.Coordinates;

namespace GcToolkit.Core.Geocaches.Gpx;

/// <summary>A note about one item (or one file) that did not import cleanly.</summary>
/// <param name="Subject">The cache code, waypoint name or file name the message is about.</param>
public sealed record GpxImportIssue(string Subject, string Message);

/// <summary>A GPX waypoint that is not a geocache and was not attached to one.</summary>
public sealed record GpxWaypoint(string Name, GeoCoordinate Coordinate)
{
    public string? Description { get; init; }

    public string? Comment { get; init; }

    public string? Symbol { get; init; }

    public string? Type { get; init; }

    public DateTimeOffset? Time { get; init; }

    /// <summary>Set on the waypoint GC Toolkit adds for corrected coordinates, so re-importing restores the correction instead of a waypoint.</summary>
    internal bool IsCorrectedCoordinateMarker { get; init; }
}

public sealed record GpxImportResult
{
    public IReadOnlyList<Geocache> Caches { get; init; } = [];

    /// <summary>Plain waypoints without a Groundspeak extension and without a parent cache in this import.</summary>
    public IReadOnlyList<GpxWaypoint> PlainWaypoints { get; init; } = [];

    /// <summary>Additional waypoints (<c>PK8KX2Q</c> style) that were attached to a cache.</summary>
    public int WaypointsMerged { get; init; }

    /// <summary>Items that were imported with a caveat or skipped (unknown type, bad coordinates, …).</summary>
    public IReadOnlyList<GpxImportIssue> Warnings { get; init; } = [];

    /// <summary>Files that could not be read (completely or partly), such as malformed XML.</summary>
    public IReadOnlyList<GpxImportIssue> Errors { get; init; } = [];
}
