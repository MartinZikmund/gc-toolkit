using GcToolkit.Core.Coordinates;

namespace GcToolkit.Core.Geocaches;

public enum GeocacheSort
{
    Name,
    Code,
    Difficulty,
    Terrain,
    PlacedOn,
    FavoritePoints,
}

/// <summary>Filter, sort and page for <see cref="IGeocacheStore.QueryAsync"/>. Unset filters match everything.</summary>
public sealed record GeocacheQuery
{
    /// <summary>Matches when the name or code contains this text, ignoring case.</summary>
    public string? Text { get; init; }

    public IReadOnlyCollection<GeocacheType>? Types { get; init; }

    public IReadOnlyCollection<GeocacheStatus>? Statuses { get; init; }

    public int? ListId { get; init; }

    public bool? IsFound { get; init; }

    public bool? IsSolved { get; init; }

    public GeocacheSort Sort { get; init; }

    public bool Descending { get; init; }

    public int Skip { get; init; }

    /// <summary>Null returns everything after <see cref="Skip"/>.</summary>
    public int? Take { get; init; }
}

/// <summary>What a list row needs, without the heavy parts of <see cref="Geocache"/>.</summary>
public sealed record GeocacheSummary
{
    public required GeocacheCode Code { get; init; }

    public required string Name { get; init; }

    public GeocacheType Type { get; init; }

    public GeocacheSize Size { get; init; }

    public GeocacheStatus Status { get; init; }

    public double? Difficulty { get; init; }

    public double? Terrain { get; init; }

    /// <summary>The corrected coordinate when the user set one, otherwise the posted one.</summary>
    public required GeoCoordinate Coordinate { get; init; }

    public bool HasCorrectedCoordinate { get; init; }

    public int? FavoritePoints { get; init; }

    public DateTimeOffset? PlacedOn { get; init; }

    public bool IsFound { get; init; }

    public bool IsSolved { get; init; }
}

/// <param name="TotalCount">Matches ignoring <see cref="GeocacheQuery.Skip"/> and <see cref="GeocacheQuery.Take"/>.</param>
public sealed record GeocachePage(IReadOnlyList<GeocacheSummary> Items, int TotalCount);

public sealed record UpsertResult(int Added, int Updated);

public sealed partial record GeocacheList(int Id, string Name, int Count);
