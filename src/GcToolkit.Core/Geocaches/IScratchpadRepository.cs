namespace GcToolkit.Core.Geocaches;

/// <summary>
/// Loads and saves a cache's scratchpad text. Backed by the offline store
/// (<see cref="GeocacheUserData.Scratchpad"/>); kept separate from the geocaching.com personal note.
/// </summary>
public interface IScratchpadRepository
{
    /// <summary>The saved text, or <see langword="null"/> when the cache has none.</summary>
    Task<string?> LoadAsync(GeocacheCode code, CancellationToken cancellationToken = default);

    Task SaveAsync(GeocacheCode code, string text, CancellationToken cancellationToken = default);
}
