namespace GcToolkit.Core.Geocaches;

/// <summary>The offline collection of geocaches the app works with, plus the named lists they are grouped in.</summary>
public interface IGeocacheStore
{
    Task<Geocache?> GetAsync(GeocacheCode code, CancellationToken cancellationToken = default);

    /// <summary>Lightweight rows for list screens: no descriptions, logs or waypoints are loaded.</summary>
    Task<GeocachePage> QueryAsync(GeocacheQuery query, CancellationToken cancellationToken = default);

    /// <summary>
    /// Inserts new caches and merges changed ones into what is stored (see <see cref="Geocache.MergeFrom"/>),
    /// so notes, corrected coordinates and found/solved flags survive a re-import. All-or-nothing.
    /// </summary>
    Task<UpsertResult> UpsertAsync(IReadOnlyCollection<Geocache> caches, CancellationToken cancellationToken = default);

    /// <summary>Replaces only the user data of a stored cache. Returns false when the cache is not stored.</summary>
    Task<bool> UpdateUserDataAsync(GeocacheCode code, GeocacheUserData userData, CancellationToken cancellationToken = default);

    /// <summary>Deletes the caches and their list memberships. Returns how many were stored.</summary>
    Task<int> DeleteAsync(IReadOnlyCollection<GeocacheCode> codes, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<GeocacheList>> GetListsAsync(CancellationToken cancellationToken = default);

    /// <exception cref="InvalidOperationException">A list with that name (ignoring case) already exists.</exception>
    Task<GeocacheList> CreateListAsync(string name, CancellationToken cancellationToken = default);

    /// <exception cref="InvalidOperationException">Another list already has that name (ignoring case).</exception>
    Task<bool> RenameListAsync(int listId, string name, CancellationToken cancellationToken = default);

    /// <summary>Deletes the list only; its caches stay in the store.</summary>
    Task<bool> DeleteListAsync(int listId, CancellationToken cancellationToken = default);

    /// <summary>Adds stored caches to a list. Codes already in the list or not stored are skipped; returns how many were added.</summary>
    Task<int> AddToListAsync(int listId, IReadOnlyCollection<GeocacheCode> codes, CancellationToken cancellationToken = default);

    Task<int> RemoveFromListAsync(int listId, IReadOnlyCollection<GeocacheCode> codes, CancellationToken cancellationToken = default);

    /// <summary>The lists a cache belongs to (a cache can be in several).</summary>
    Task<IReadOnlyList<GeocacheList>> GetListsForAsync(GeocacheCode code, CancellationToken cancellationToken = default);
}
