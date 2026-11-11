namespace GcToolkit.Core.Geocaches;

/// <summary>Where geocaches come from: GPX files today, the Geocaching API later.</summary>
public interface ICacheSource
{
    /// <summary>Stable id stored in <see cref="Geocache.SourceId"/>.</summary>
    string Id { get; }

    CacheSourceCapabilities Capabilities { get; }
}

[Flags]
public enum CacheSourceCapabilities
{
    None = 0,
    Import = 1,
    Refresh = 2,
    Search = 4,
}
