using GcToolkit.Core.Coordinates;

namespace GcToolkit.Core.Geocaches;

/// <summary>
/// A geocache as the app stores it, whatever source it came from (GPX, pasted listing, API).
/// Everything except <see cref="UserData"/> is owned by the source and replaced on re-import.
/// </summary>
public sealed record Geocache
{
    public required GeocacheCode Code { get; init; }

    public required string Name { get; init; }

    public GeocacheType Type { get; init; }

    public GeocacheSize Size { get; init; }

    public GeocacheStatus Status { get; init; }

    public double? Difficulty { get; init; }

    public double? Terrain { get; init; }

    /// <summary>The posted (listed) coordinates.</summary>
    public required GeoCoordinate Coordinate { get; init; }

    public string? Owner { get; init; }

    public DateTimeOffset? PlacedOn { get; init; }

    public bool IsPremiumOnly { get; init; }

    public int? FavoritePoints { get; init; }

    public string? Country { get; init; }

    public string? State { get; init; }

    public string? ShortDescription { get; init; }

    public bool ShortDescriptionIsHtml { get; init; }

    public string? LongDescription { get; init; }

    public bool LongDescriptionIsHtml { get; init; }

    /// <summary>The hint as published (usually plain text; encoding for display is a UI concern).</summary>
    public string? Hint { get; init; }

    public IReadOnlyList<GeocacheAttribute> Attributes { get; init; } = [];

    public IReadOnlyList<Waypoint> Waypoints { get; init; } = [];

    public IReadOnlyList<GeocacheLog> Logs { get; init; } = [];

    public IReadOnlyList<GeocacheImage> Images { get; init; } = [];

    /// <summary>Id of the <see cref="ICacheSource"/> that last provided this cache.</summary>
    public string? SourceId { get; init; }

    public DateTimeOffset? UpdatedAt { get; init; }

    public GeocacheUserData UserData { get; init; } = new();

    public GeoCoordinate EffectiveCoordinate => UserData.CorrectedCoordinate ?? Coordinate;

    /// <summary>
    /// Combines a fresh copy of this cache from a source with what is stored: source data is taken
    /// from <paramref name="incoming"/>, user data is kept and only gaps are filled from it.
    /// </summary>
    public Geocache MergeFrom(Geocache incoming)
    {
        if (incoming.Code != Code)
        {
            throw new ArgumentException($"Cannot merge {incoming.Code} into {Code}.", nameof(incoming));
        }

        return incoming with { UserData = UserData.FillGapsFrom(incoming.UserData) };
    }
}

/// <summary>What the user adds to a cache. Survives re-imports.</summary>
public sealed record GeocacheUserData
{
    public GeoCoordinate? CorrectedCoordinate { get; init; }

    public string? Note { get; init; }

    /// <summary>Private working notes; unlike <see cref="Note"/> this never syncs to geocaching.com.</summary>
    public string? Scratchpad { get; init; }

    public bool IsSolved { get; init; }

    public bool IsFound { get; init; }

    public GeocacheUserData FillGapsFrom(GeocacheUserData other) => new()
    {
        CorrectedCoordinate = CorrectedCoordinate ?? other.CorrectedCoordinate,
        Note = string.IsNullOrEmpty(Note) ? other.Note : Note,
        Scratchpad = string.IsNullOrEmpty(Scratchpad) ? other.Scratchpad : Scratchpad,
        IsSolved = IsSolved || other.IsSolved,
        IsFound = IsFound || other.IsFound,
    };
}

/// <summary>An attribute by its Groundspeak id; <see cref="IsPositive"/> is false for "no dogs" style negations.</summary>
public sealed partial record GeocacheAttribute(int Id, bool IsPositive);

public sealed record Waypoint
{
    public required string Prefix { get; init; }

    public required string Name { get; init; }

    public WaypointType Type { get; init; }

    /// <summary>Null for waypoints whose coordinates are hidden in the listing.</summary>
    public GeoCoordinate? Coordinate { get; init; }

    public string? Note { get; init; }

    /// <summary>Private working notes; unlike <see cref="Note"/> this never syncs to geocaching.com.</summary>
    public string? Scratchpad { get; init; }
}

public sealed partial record GeocacheLog
{
    public string? Id { get; init; }

    public GeocacheLogType Type { get; init; }

    public string? Author { get; init; }

    public DateTimeOffset Date { get; init; }

    public string? Text { get; init; }

    public bool TextIsHtml { get; init; }
}

public sealed record GeocacheImage(string Url, string? Name = null, string? Description = null);
