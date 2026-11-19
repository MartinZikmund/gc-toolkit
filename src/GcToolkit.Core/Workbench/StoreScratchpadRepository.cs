using GcToolkit.Core.Geocaches;
using MZikmund.Toolkit.WinUI.Services;

namespace GcToolkit.Core.Workbench;

/// <summary>
/// Keeps a cache's scratchpad in its user data in the offline store. A cache that is not stored (opened by
/// typing its code) keeps it in preferences until it is imported; the next save then moves it into the store.
/// </summary>
public sealed class StoreScratchpadRepository(IGeocacheStore store, IPreferences preferences) : IScratchpadRepository
{
    private const string KeyPrefix = "workbench.scratchpad.";

    public async Task<string?> LoadAsync(GeocacheCode code, CancellationToken cancellationToken = default)
    {
        var cache = await store.GetAsync(code, cancellationToken);
        if (!string.IsNullOrEmpty(cache?.UserData.Scratchpad))
        {
            return cache.UserData.Scratchpad;
        }

        var unstored = preferences.Get(KeyPrefix + code.Value, string.Empty);
        return unstored.Length == 0 ? null : unstored;
    }

    public async Task SaveAsync(GeocacheCode code, string text, CancellationToken cancellationToken = default)
    {
        var cache = await store.GetAsync(code, cancellationToken);
        if (cache is not null
            && await store.UpdateUserDataAsync(code, cache.UserData with { Scratchpad = text }, cancellationToken))
        {
            preferences.Remove(KeyPrefix + code.Value);
            return;
        }

        preferences.Set(KeyPrefix + code.Value, text);
    }
}
