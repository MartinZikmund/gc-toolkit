namespace GcToolkit.Core.Geocaches;

/// <summary>Maps Groundspeak attribute ids to localization keys. Names live in the resource files.</summary>
public static class GeocacheAttributeCatalog
{
    public const int MaxKnownId = 71;

    public static bool IsKnown(int id) => id is >= 1 and <= MaxKnownId && id != 16;

    public static string ResourceKey(int id) => $"GeocacheAttribute_{id}";
}
