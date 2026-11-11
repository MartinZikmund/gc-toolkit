using System.Text.Json;
using System.Diagnostics.CodeAnalysis;
using GcToolkit.Core.Coordinates;
using SQLite;

namespace GcToolkit.Core.Geocaches.Storage;

// sqlite-net reads these types by reflection, which the trimmer cannot see. DynamicallyAccessedMembers
// keeps every member of each one; the head's LinkerConfig.xml roots the namespace as a second guard.
// Public settable properties with a parameterless constructor are required by sqlite-net.

[Table("Geocaches")]
[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)]
internal sealed class GeocacheRow
{
    [PrimaryKey]
    public string Code { get; set; } = "";

    [Indexed]
    public string Name { get; set; } = "";

    /// <summary><see cref="Name"/> folded by <see cref="SearchText.Fold"/>, for accent-insensitive search and sorting.</summary>
    [Indexed]
    public string SearchName { get; set; } = "";

    [Indexed]
    public int Type { get; set; }

    public int Size { get; set; }

    public int Status { get; set; }

    public double? Difficulty { get; set; }

    public double? Terrain { get; set; }

    public double Latitude { get; set; }

    public double Longitude { get; set; }

    public double? CorrectedLatitude { get; set; }

    public double? CorrectedLongitude { get; set; }

    public string? Owner { get; set; }

    /// <summary>UTC ticks.</summary>
    public long? PlacedOn { get; set; }

    public bool IsPremiumOnly { get; set; }

    public int? FavoritePoints { get; set; }

    public string? Country { get; set; }

    public string? State { get; set; }

    public string? ShortDescription { get; set; }

    public bool ShortDescriptionIsHtml { get; set; }

    public string? LongDescription { get; set; }

    public bool LongDescriptionIsHtml { get; set; }

    public string? Hint { get; set; }

    public string? SourceId { get; set; }

    /// <summary>UTC ticks.</summary>
    public long? UpdatedAt { get; set; }

    public string? Note { get; set; }

    public bool IsSolved { get; set; }

    [Indexed]
    public bool IsFound { get; set; }

    public string AttributesJson { get; set; } = "[]";

    public string WaypointsJson { get; set; } = "[]";

    public string LogsJson { get; set; } = "[]";

    public string ImagesJson { get; set; } = "[]";

    public static GeocacheRow From(Geocache cache) => new()
    {
        Code = cache.Code.Value,
        Name = cache.Name,
        SearchName = SearchText.Fold(cache.Name),
        Type = (int)cache.Type,
        Size = (int)cache.Size,
        Status = (int)cache.Status,
        Difficulty = cache.Difficulty,
        Terrain = cache.Terrain,
        Latitude = cache.Coordinate.Latitude,
        Longitude = cache.Coordinate.Longitude,
        CorrectedLatitude = cache.UserData.CorrectedCoordinate?.Latitude,
        CorrectedLongitude = cache.UserData.CorrectedCoordinate?.Longitude,
        Owner = cache.Owner,
        PlacedOn = cache.PlacedOn?.UtcTicks,
        IsPremiumOnly = cache.IsPremiumOnly,
        FavoritePoints = cache.FavoritePoints,
        Country = cache.Country,
        State = cache.State,
        ShortDescription = cache.ShortDescription,
        ShortDescriptionIsHtml = cache.ShortDescriptionIsHtml,
        LongDescription = cache.LongDescription,
        LongDescriptionIsHtml = cache.LongDescriptionIsHtml,
        Hint = cache.Hint,
        SourceId = cache.SourceId,
        UpdatedAt = cache.UpdatedAt?.UtcTicks,
        Note = cache.UserData.Note,
        IsSolved = cache.UserData.IsSolved,
        IsFound = cache.UserData.IsFound,
        AttributesJson = JsonSerializer.Serialize(cache.Attributes.ToArray(), GeocacheJsonContext.Default.GeocacheAttributeArray),
        WaypointsJson = JsonSerializer.Serialize(cache.Waypoints.ToArray(), GeocacheJsonContext.Default.WaypointArray),
        LogsJson = JsonSerializer.Serialize(cache.Logs.ToArray(), GeocacheJsonContext.Default.GeocacheLogArray),
        ImagesJson = JsonSerializer.Serialize(cache.Images.ToArray(), GeocacheJsonContext.Default.GeocacheImageArray),
    };

    public Geocache ToGeocache() => new()
    {
        Code = GeocacheCode.Parse(Code),
        Name = Name,
        Type = (GeocacheType)Type,
        Size = (GeocacheSize)Size,
        Status = (GeocacheStatus)Status,
        Difficulty = Difficulty,
        Terrain = Terrain,
        Coordinate = new(Latitude, Longitude),
        Owner = Owner,
        PlacedOn = Utc(PlacedOn),
        IsPremiumOnly = IsPremiumOnly,
        FavoritePoints = FavoritePoints,
        Country = Country,
        State = State,
        ShortDescription = ShortDescription,
        ShortDescriptionIsHtml = ShortDescriptionIsHtml,
        LongDescription = LongDescription,
        LongDescriptionIsHtml = LongDescriptionIsHtml,
        Hint = Hint,
        SourceId = SourceId,
        UpdatedAt = Utc(UpdatedAt),
        Attributes = JsonSerializer.Deserialize(AttributesJson, GeocacheJsonContext.Default.GeocacheAttributeArray) ?? [],
        Waypoints = JsonSerializer.Deserialize(WaypointsJson, GeocacheJsonContext.Default.WaypointArray) ?? [],
        Logs = JsonSerializer.Deserialize(LogsJson, GeocacheJsonContext.Default.GeocacheLogArray) ?? [],
        Images = JsonSerializer.Deserialize(ImagesJson, GeocacheJsonContext.Default.GeocacheImageArray) ?? [],
        UserData = new()
        {
            CorrectedCoordinate = CorrectedLatitude is { } lat && CorrectedLongitude is { } lon ? new GeoCoordinate(lat, lon) : null,
            Note = Note,
            IsSolved = IsSolved,
            IsFound = IsFound,
        },
    };

    private static DateTimeOffset? Utc(long? ticks) => ticks is { } t ? new DateTimeOffset(t, TimeSpan.Zero) : null;
}

/// <summary>The columns a list screen needs; read with a hand-written SELECT.</summary>
[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)]
internal sealed class GeocacheSummaryRow
{
    public string Code { get; set; } = "";

    public string Name { get; set; } = "";

    public int Type { get; set; }

    public int Size { get; set; }

    public int Status { get; set; }

    public double? Difficulty { get; set; }

    public double? Terrain { get; set; }

    public double Latitude { get; set; }

    public double Longitude { get; set; }

    public double? CorrectedLatitude { get; set; }

    public double? CorrectedLongitude { get; set; }

    public int? FavoritePoints { get; set; }

    public long? PlacedOn { get; set; }

    public bool IsSolved { get; set; }

    public bool IsFound { get; set; }

    public const string Columns =
        "Code, Name, Type, Size, Status, Difficulty, Terrain, Latitude, Longitude, CorrectedLatitude, CorrectedLongitude, FavoritePoints, PlacedOn, IsSolved, IsFound";

    public GeocacheSummary ToSummary()
    {
        var hasCorrected = CorrectedLatitude is not null && CorrectedLongitude is not null;
        return new()
        {
            Code = GeocacheCode.Parse(Code),
            Name = Name,
            Type = (GeocacheType)Type,
            Size = (GeocacheSize)Size,
            Status = (GeocacheStatus)Status,
            Difficulty = Difficulty,
            Terrain = Terrain,
            Coordinate = hasCorrected ? new(CorrectedLatitude!.Value, CorrectedLongitude!.Value) : new(Latitude, Longitude),
            HasCorrectedCoordinate = hasCorrected,
            FavoritePoints = FavoritePoints,
            PlacedOn = PlacedOn is { } t ? new DateTimeOffset(t, TimeSpan.Zero) : null,
            IsFound = IsFound,
            IsSolved = IsSolved,
        };
    }
}

[Table("GeocacheLists")]
[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)]
internal sealed class GeocacheListRow
{
    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }

    public string Name { get; set; } = "";
}

[Table("GeocacheListMembers")]
[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)]
internal sealed class GeocacheListMemberRow
{
    [Indexed("UX_ListMember", 1, Unique = true)]
    public int ListId { get; set; }

    [Indexed("UX_ListMember", 2, Unique = true)]
    public string Code { get; set; } = "";
}

/// <summary>A list with its member count, from a joined SELECT.</summary>
[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)]
internal sealed class GeocacheListInfoRow
{
    public int Id { get; set; }

    public string Name { get; set; } = "";

    public int Count { get; set; }
}

[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)]
internal sealed class CountRow
{
    public int Count { get; set; }
}
