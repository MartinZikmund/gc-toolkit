using GcToolkit.Core.Geocaches;

namespace GcToolkit.Core.Tests.Fakes;

/// <summary>In-memory <see cref="IGeocacheStore"/> for consumers' tests. Held to the same contract as the SQLite store.</summary>
public sealed class InMemoryGeocacheStore : IGeocacheStore
{
    private readonly Dictionary<GeocacheCode, Geocache> _caches = [];
    private readonly Dictionary<int, (string Name, HashSet<GeocacheCode> Members)> _lists = [];
    private int _nextListId = 1;

    public Task<Geocache?> GetAsync(GeocacheCode code, CancellationToken cancellationToken = default)
        => Task.FromResult(_caches.GetValueOrDefault(code));

    public Task<GeocachePage> QueryAsync(GeocacheQuery query, CancellationToken cancellationToken = default)
    {
        var matches = _caches.Values.Where(c => Matches(c, query));
        Func<Geocache, object?> key = query.Sort switch
        {
            GeocacheSort.Code => c => c.Code.Value,
            GeocacheSort.Difficulty => c => c.Difficulty,
            GeocacheSort.Terrain => c => c.Terrain,
            GeocacheSort.PlacedOn => c => c.PlacedOn,
            GeocacheSort.FavoritePoints => c => c.FavoritePoints,
            _ => c => c.Name,
        };

        var comparer = Comparer<object?>.Create((a, b) => query.Sort is GeocacheSort.Name or GeocacheSort.Code
            ? string.Compare((string?)a, (string?)b, StringComparison.OrdinalIgnoreCase)
            : Comparer<object?>.Default.Compare(a, b));

        // Ties fall back to the code so paging is stable.
        var ordered = query.Descending
            ? matches.OrderByDescending(key, comparer).ThenBy(c => c.Code.Value, StringComparer.Ordinal)
            : matches.OrderBy(key, comparer).ThenBy(c => c.Code.Value, StringComparer.Ordinal);

        var all = ordered.ToList();
        var page = all.Skip(query.Skip);
        if (query.Take is { } take)
        {
            page = page.Take(take);
        }

        return Task.FromResult(new GeocachePage([.. page.Select(ToSummary)], all.Count));
    }

    public Task<UpsertResult> UpsertAsync(IReadOnlyCollection<Geocache> caches, CancellationToken cancellationToken = default)
    {
        int added = 0, updated = 0;
        foreach (var incoming in caches)
        {
            if (_caches.TryGetValue(incoming.Code, out var existing))
            {
                _caches[incoming.Code] = existing.MergeFrom(incoming);
                updated++;
            }
            else
            {
                _caches[incoming.Code] = incoming;
                added++;
            }
        }

        return Task.FromResult(new UpsertResult(added, updated));
    }

    public Task<bool> UpdateUserDataAsync(GeocacheCode code, GeocacheUserData userData, CancellationToken cancellationToken = default)
    {
        if (!_caches.TryGetValue(code, out var existing))
        {
            return Task.FromResult(false);
        }

        _caches[code] = existing with { UserData = userData };
        return Task.FromResult(true);
    }

    public Task<int> DeleteAsync(IReadOnlyCollection<GeocacheCode> codes, CancellationToken cancellationToken = default)
    {
        var deleted = codes.Distinct().Count(_caches.Remove);
        foreach (var list in _lists.Values)
        {
            list.Members.ExceptWith(codes);
        }

        return Task.FromResult(deleted);
    }

    public Task<IReadOnlyList<GeocacheList>> GetListsAsync(CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<GeocacheList>>([.. _lists.Select(l => ToList(l.Key)).OrderBy(l => l.Name, StringComparer.OrdinalIgnoreCase)]);

    public Task<GeocacheList> CreateListAsync(string name, CancellationToken cancellationToken = default)
    {
        EnsureNameFree(name, except: null);
        var id = _nextListId++;
        _lists[id] = (name, []);
        return Task.FromResult(ToList(id));
    }

    public Task<bool> RenameListAsync(int listId, string name, CancellationToken cancellationToken = default)
    {
        if (!_lists.TryGetValue(listId, out var list))
        {
            return Task.FromResult(false);
        }

        EnsureNameFree(name, except: listId);
        _lists[listId] = (name, list.Members);
        return Task.FromResult(true);
    }

    public Task<bool> DeleteListAsync(int listId, CancellationToken cancellationToken = default)
        => Task.FromResult(_lists.Remove(listId));

    public Task<int> AddToListAsync(int listId, IReadOnlyCollection<GeocacheCode> codes, CancellationToken cancellationToken = default)
    {
        if (!_lists.TryGetValue(listId, out var list))
        {
            return Task.FromResult(0);
        }

        return Task.FromResult(codes.Distinct().Count(c => _caches.ContainsKey(c) && list.Members.Add(c)));
    }

    public Task<int> RemoveFromListAsync(int listId, IReadOnlyCollection<GeocacheCode> codes, CancellationToken cancellationToken = default)
        => Task.FromResult(_lists.TryGetValue(listId, out var list) ? codes.Distinct().Count(list.Members.Remove) : 0);

    public Task<IReadOnlyList<GeocacheList>> GetListsForAsync(GeocacheCode code, CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<GeocacheList>>([.. _lists.Where(l => l.Value.Members.Contains(code)).Select(l => ToList(l.Key)).OrderBy(l => l.Name, StringComparer.OrdinalIgnoreCase)]);

    private bool Matches(Geocache c, GeocacheQuery q)
        => (string.IsNullOrWhiteSpace(q.Text)
                || c.Name.Contains(q.Text.Trim(), StringComparison.OrdinalIgnoreCase)
                || c.Code.Value.Contains(q.Text.Trim(), StringComparison.OrdinalIgnoreCase))
            && (q.Types is not { Count: > 0 } || q.Types.Contains(c.Type))
            && (q.Statuses is not { Count: > 0 } || q.Statuses.Contains(c.Status))
            && (q.IsFound is null || c.UserData.IsFound == q.IsFound)
            && (q.IsSolved is null || c.UserData.IsSolved == q.IsSolved)
            && (q.ListId is not { } id || (_lists.TryGetValue(id, out var list) && list.Members.Contains(c.Code)));

    private static GeocacheSummary ToSummary(Geocache c) => new()
    {
        Code = c.Code,
        Name = c.Name,
        Type = c.Type,
        Size = c.Size,
        Status = c.Status,
        Difficulty = c.Difficulty,
        Terrain = c.Terrain,
        Coordinate = c.EffectiveCoordinate,
        HasCorrectedCoordinate = c.UserData.CorrectedCoordinate is not null,
        FavoritePoints = c.FavoritePoints,
        PlacedOn = c.PlacedOn,
        IsFound = c.UserData.IsFound,
        IsSolved = c.UserData.IsSolved,
    };

    private GeocacheList ToList(int id) => new(id, _lists[id].Name, _lists[id].Members.Count);

    private void EnsureNameFree(string name, int? except)
    {
        if (_lists.Any(l => l.Key != except && string.Equals(l.Value.Name, name, StringComparison.OrdinalIgnoreCase)))
        {
            throw new InvalidOperationException($"A list named '{name}' already exists.");
        }
    }
}
