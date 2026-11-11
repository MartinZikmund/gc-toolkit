namespace GcToolkit.Core.Geocaches.Gpx;

/// <summary>
/// Groundspeak's GPX wording for cache types, sizes, log types and waypoint types. The first entry
/// per value is what the exporter writes; the rest are spellings other tools use.
/// </summary>
internal static class GpxVocabulary
{
    private static readonly (string Text, GeocacheType Value)[] _types =
    [
        ("Traditional Cache", GeocacheType.Traditional),
        ("Multi-cache", GeocacheType.Multi),
        ("Unknown Cache", GeocacheType.Mystery),
        ("Mystery Cache", GeocacheType.Mystery),
        ("Puzzle Cache", GeocacheType.Mystery),
        ("Letterbox Hybrid", GeocacheType.Letterbox),
        ("Letterbox", GeocacheType.Letterbox),
        ("Wherigo Cache", GeocacheType.Wherigo),
        ("Earthcache", GeocacheType.Earth),
        ("EarthCache", GeocacheType.Earth),
        ("Virtual Cache", GeocacheType.Virtual),
        ("Webcam Cache", GeocacheType.Webcam),
        ("Event Cache", GeocacheType.Event),
        ("Mega-Event Cache", GeocacheType.MegaEvent),
        ("Giga-Event Cache", GeocacheType.GigaEvent),
        ("Cache In Trash Out Event", GeocacheType.CacheInTrashOutEvent),
        ("Community Celebration Event", GeocacheType.CommunityCelebrationEvent),
        ("Geocaching HQ Celebration", GeocacheType.GeocachingHqEvent),
        ("Geocaching HQ Block Party", GeocacheType.GeocachingHqEvent),
        ("Groundspeak HQ", GeocacheType.GeocachingHqEvent),
        ("Lab Cache", GeocacheType.AdventureLab),
        ("Adventure Lab", GeocacheType.AdventureLab),
        ("Locationless Cache", GeocacheType.Locationless),
        ("Locationless (Reverse) Cache", GeocacheType.Locationless),
    ];

    private static readonly (string Text, GeocacheSize Value)[] _sizes =
    [
        ("Micro", GeocacheSize.Micro),
        ("Small", GeocacheSize.Small),
        ("Regular", GeocacheSize.Regular),
        ("Large", GeocacheSize.Large),
        ("Other", GeocacheSize.Other),
        ("Virtual", GeocacheSize.Virtual),
        ("Not chosen", GeocacheSize.NotChosen),
        ("Unknown", GeocacheSize.Unknown),
    ];

    private static readonly (string Text, GeocacheLogType Value)[] _logTypes =
    [
        ("Found it", GeocacheLogType.Found),
        ("Didn't find it", GeocacheLogType.DidNotFind),
        ("Write note", GeocacheLogType.WriteNote),
        ("Attended", GeocacheLogType.Attended),
        ("Will attend", GeocacheLogType.WillAttend),
        ("Webcam Photo Taken", GeocacheLogType.WebcamPhotoTaken),
        ("Needs Maintenance", GeocacheLogType.NeedsMaintenance),
        ("Owner Maintenance", GeocacheLogType.OwnerMaintenance),
        ("Needs Archived", GeocacheLogType.NeedsArchived),
        ("Temporarily Disable Listing", GeocacheLogType.TemporarilyDisableListing),
        ("Enable Listing", GeocacheLogType.EnableListing),
        ("Update Coordinates", GeocacheLogType.UpdateCoordinates),
        ("Publish Listing", GeocacheLogType.Publish),
        ("Archive", GeocacheLogType.Archive),
        ("Unarchive", GeocacheLogType.Unarchive),
        ("Post Reviewer Note", GeocacheLogType.ReviewerNote),
        ("Announcement", GeocacheLogType.Announcement),
    ];

    private static readonly (string Text, WaypointType Value)[] _waypointTypes =
    [
        ("Parking Area", WaypointType.Parking),
        ("Physical Stage", WaypointType.Stage),
        ("Virtual Stage", WaypointType.Stage),
        ("Question to Answer", WaypointType.Question),
        ("Final Location", WaypointType.Final),
        ("Trailhead", WaypointType.Trailhead),
        ("Reference Point", WaypointType.ReferencePoint),
        ("User Waypoint", WaypointType.User),
    ];

    public static bool TryParseType(string? text, out GeocacheType value) => TryFind(_types, text, out value);

    public static bool TryParseSize(string? text, out GeocacheSize value) => TryFind(_sizes, text, out value);

    public static bool TryParseLogType(string? text, out GeocacheLogType value) => TryFind(_logTypes, text, out value);

    public static bool TryParseWaypointType(string? text, out WaypointType value) => TryFind(_waypointTypes, text, out value);

    public static string? TypeText(GeocacheType value) => TextOfOrNull(_types, value);

    public static string? SizeText(GeocacheSize value) => TextOfOrNull(_sizes, value);

    public static string? LogTypeText(GeocacheLogType value) => TextOfOrNull(_logTypes, value);

    public static string? WaypointTypeText(WaypointType value) => TextOfOrNull(_waypointTypes, value);

    private static bool TryFind<T>((string Text, T Value)[] table, string? text, out T value)
    {
        value = default!;
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        foreach (var entry in table)
        {
            if (string.Equals(entry.Text, text.Trim(), StringComparison.OrdinalIgnoreCase))
            {
                value = entry.Value;
                return true;
            }
        }

        return false;
    }

    private static string? TextOfOrNull<T>((string Text, T Value)[] table, T value)
    {
        foreach (var entry in table)
        {
            if (EqualityComparer<T>.Default.Equals(entry.Value, value))
            {
                return entry.Text;
            }
        }

        return null;
    }
}
