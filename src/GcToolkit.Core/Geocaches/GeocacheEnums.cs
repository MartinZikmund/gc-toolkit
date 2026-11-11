namespace GcToolkit.Core.Geocaches;

public enum GeocacheType
{
    Unknown,
    Traditional,
    Multi,
    Mystery,
    Letterbox,
    Wherigo,
    Earth,
    Virtual,
    Webcam,
    Event,
    MegaEvent,
    GigaEvent,
    CacheInTrashOutEvent,
    CommunityCelebrationEvent,
    GeocachingHqEvent,
    AdventureLab,
    Locationless,
}

public enum GeocacheSize
{
    Unknown,
    NotChosen,
    Micro,
    Small,
    Regular,
    Large,
    Other,
    Virtual,
}

public enum GeocacheStatus
{
    Available,
    Disabled,
    Archived,
}

public enum GeocacheLogType
{
    Unknown,
    Found,
    DidNotFind,
    WriteNote,
    Attended,
    WillAttend,
    WebcamPhotoTaken,
    NeedsMaintenance,
    OwnerMaintenance,
    NeedsArchived,
    TemporarilyDisableListing,
    EnableListing,
    UpdateCoordinates,
    Publish,
    Archive,
    Unarchive,
    ReviewerNote,
    Announcement,
}

public enum WaypointType
{
    Unknown,
    Parking,
    Stage,
    Question,
    Final,
    Trailhead,
    ReferencePoint,
    User,
}
