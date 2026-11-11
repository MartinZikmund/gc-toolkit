using System.Diagnostics.CodeAnalysis;
using System.Text;
using GcToolkit.Core.Infrastructure;
using SQLite;

namespace GcToolkit.Core.Geocaches.Storage;

/// <summary>
/// <see cref="IGeocacheStore"/> on SQLite. Scalar fields are columns so lists can filter and sort in SQL;
/// attributes, waypoints, logs and images are JSON columns that are only parsed when one cache is loaded.
/// </summary>
/// <param name="databasePathProvider">
/// Resolved on first use. On WebAssembly it must create the storage folder first so the browser file
/// system is ready before SQLite opens the file.
/// </param>
/// <param name="flusher">Called after every committed write.</param>
[UnconditionalSuppressMessage("Trimming", "IL2026",
    Justification = "sqlite-net's Insert/Update(object) only ever see the row types, which keep all their members (DynamicallyAccessedMembers and LinkerConfig.xml).")]
public sealed class SqliteGeocacheStore(Func<Task<string>> databasePathProvider, IStorageFlusher flusher) : IGeocacheStore, IAsyncDisposable
{
    /// <summary>The schema version this build writes. Stored in <c>PRAGMA user_version</c>.</summary>
    public const int CurrentSchemaVersion = 1;

    // Index n upgrades a database from version n to n + 1.
    private static readonly Action<SQLiteConnection>[] _migrations =
    [
        CreateVersion1,
    ];

    private readonly SemaphoreSlim _openLock = new(1, 1);
    private SQLiteAsyncConnection? _db;

    public async Task<Geocache?> GetAsync(GeocacheCode code, CancellationToken cancellationToken = default)
    {
        var db = await OpenAsync(cancellationToken);
        var row = await db.FindAsync<GeocacheRow>(code.Value);
        return row?.ToGeocache();
    }

    public async Task<GeocachePage> QueryAsync(GeocacheQuery query, CancellationToken cancellationToken = default)
    {
        var db = await OpenAsync(cancellationToken);
        var (where, args) = BuildWhere(query);

        var total = (await db.QueryAsync<CountRow>($"SELECT COUNT(*) AS Count FROM Geocaches{where}", [.. args])).Single().Count;

        var sort = query.Sort switch
        {
            GeocacheSort.Code => "Code",
            GeocacheSort.Difficulty => "Difficulty",
            GeocacheSort.Terrain => "Terrain",
            GeocacheSort.PlacedOn => "PlacedOn",
            GeocacheSort.FavoritePoints => "FavoritePoints",
            _ => "Name COLLATE NOCASE",
        };
        var direction = query.Descending ? "DESC" : "ASC";

        // Code breaks ties so paging does not shuffle rows between pages.
        var pageArgs = new List<object?>(args) { query.Take ?? -1, Math.Max(0, query.Skip) };
        var rows = await db.QueryAsync<GeocacheSummaryRow>(
            $"SELECT {GeocacheSummaryRow.Columns} FROM Geocaches{where} ORDER BY {sort} {direction}, Code ASC LIMIT ? OFFSET ?",
            [.. pageArgs]);

        return new([.. rows.Select(r => r.ToSummary())], total);
    }

    public async Task<UpsertResult> UpsertAsync(IReadOnlyCollection<Geocache> caches, CancellationToken cancellationToken = default)
    {
        var db = await OpenAsync(cancellationToken);
        int added = 0, updated = 0;

        await db.RunInTransactionAsync(connection =>
        {
            foreach (var incoming in caches)
            {
                var existing = connection.Find<GeocacheRow>(incoming.Code.Value);
                if (existing is null)
                {
                    connection.Insert(GeocacheRow.From(incoming));
                    added++;
                }
                else
                {
                    connection.Update(GeocacheRow.From(existing.ToGeocache().MergeFrom(incoming)));
                    updated++;
                }
            }
        });

        await flusher.FlushAsync();
        return new(added, updated);
    }

    public async Task<bool> UpdateUserDataAsync(GeocacheCode code, GeocacheUserData userData, CancellationToken cancellationToken = default)
    {
        var db = await OpenAsync(cancellationToken);
        var found = false;

        await db.RunInTransactionAsync(connection =>
        {
            var existing = connection.Find<GeocacheRow>(code.Value);
            if (existing is null)
            {
                return;
            }

            connection.Update(GeocacheRow.From(existing.ToGeocache() with { UserData = userData }));
            found = true;
        });

        await flusher.FlushAsync();
        return found;
    }

    public async Task<int> DeleteAsync(IReadOnlyCollection<GeocacheCode> codes, CancellationToken cancellationToken = default)
    {
        var db = await OpenAsync(cancellationToken);
        var deleted = 0;

        await db.RunInTransactionAsync(connection =>
        {
            foreach (var code in codes.Distinct())
            {
                connection.Execute("DELETE FROM GeocacheListMembers WHERE Code = ?", code.Value);
                deleted += connection.Delete<GeocacheRow>(code.Value);
            }
        });

        await flusher.FlushAsync();
        return deleted;
    }

    public async Task<IReadOnlyList<GeocacheList>> GetListsAsync(CancellationToken cancellationToken = default)
    {
        var db = await OpenAsync(cancellationToken);
        var rows = await db.QueryAsync<GeocacheListInfoRow>(ListInfoSql(null));
        return [.. rows.Select(ToList)];
    }

    public async Task<GeocacheList> CreateListAsync(string name, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        var db = await OpenAsync(cancellationToken);
        GeocacheListRow row = new() { Name = name.Trim() };

        await db.RunInTransactionAsync(connection =>
        {
            EnsureNameFree(connection, row.Name, exceptId: null);
            connection.Insert(row);
        });

        await flusher.FlushAsync();
        return new(row.Id, row.Name, 0);
    }

    public async Task<bool> RenameListAsync(int listId, string name, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        var db = await OpenAsync(cancellationToken);
        var renamed = false;

        await db.RunInTransactionAsync(connection =>
        {
            var row = connection.Find<GeocacheListRow>(listId);
            if (row is null)
            {
                return;
            }

            EnsureNameFree(connection, name.Trim(), exceptId: listId);
            row.Name = name.Trim();
            connection.Update(row);
            renamed = true;
        });

        await flusher.FlushAsync();
        return renamed;
    }

    public async Task<bool> DeleteListAsync(int listId, CancellationToken cancellationToken = default)
    {
        var db = await OpenAsync(cancellationToken);
        var deleted = false;

        await db.RunInTransactionAsync(connection =>
        {
            connection.Execute("DELETE FROM GeocacheListMembers WHERE ListId = ?", listId);
            deleted = connection.Delete<GeocacheListRow>(listId) > 0;
        });

        await flusher.FlushAsync();
        return deleted;
    }

    public async Task<int> AddToListAsync(int listId, IReadOnlyCollection<GeocacheCode> codes, CancellationToken cancellationToken = default)
    {
        var db = await OpenAsync(cancellationToken);
        var added = 0;

        await db.RunInTransactionAsync(connection =>
        {
            if (connection.Find<GeocacheListRow>(listId) is null)
            {
                return;
            }

            foreach (var code in codes.Distinct())
            {
                // Skips unknown caches and existing members in one statement.
                added += connection.Execute(
                    "INSERT OR IGNORE INTO GeocacheListMembers (ListId, Code) SELECT ?, Code FROM Geocaches WHERE Code = ?",
                    listId,
                    code.Value);
            }
        });

        await flusher.FlushAsync();
        return added;
    }

    public async Task<int> RemoveFromListAsync(int listId, IReadOnlyCollection<GeocacheCode> codes, CancellationToken cancellationToken = default)
    {
        var db = await OpenAsync(cancellationToken);
        var removed = 0;

        await db.RunInTransactionAsync(connection =>
        {
            foreach (var code in codes.Distinct())
            {
                removed += connection.Execute("DELETE FROM GeocacheListMembers WHERE ListId = ? AND Code = ?", listId, code.Value);
            }
        });

        await flusher.FlushAsync();
        return removed;
    }

    public async Task<IReadOnlyList<GeocacheList>> GetListsForAsync(GeocacheCode code, CancellationToken cancellationToken = default)
    {
        var db = await OpenAsync(cancellationToken);
        var rows = await db.QueryAsync<GeocacheListInfoRow>(
            ListInfoSql("WHERE l.Id IN (SELECT ListId FROM GeocacheListMembers WHERE Code = ?)"),
            code.Value);
        return [.. rows.Select(ToList)];
    }

    public async ValueTask DisposeAsync()
    {
        if (_db is { } db)
        {
            _db = null;
            await db.CloseAsync();
        }

        _openLock.Dispose();
    }

    private async Task<SQLiteAsyncConnection> OpenAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (_db is { } open)
        {
            return open;
        }

        await _openLock.WaitAsync(cancellationToken);
        try
        {
            if (_db is null)
            {
                var path = await databasePathProvider();
                SQLiteAsyncConnection db = new(path);
                try
                {
                    await MigrateAsync(db);
                }
                catch
                {
                    await db.CloseAsync();
                    throw;
                }

                _db = db;
            }

            return _db;
        }
        finally
        {
            _openLock.Release();
        }
    }

    private static async Task MigrateAsync(SQLiteAsyncConnection db)
    {
        var version = await db.ExecuteScalarAsync<int>("PRAGMA user_version");
        if (version > CurrentSchemaVersion)
        {
            throw new InvalidOperationException(
                $"The geocache database is version {version}, but this app only understands up to {CurrentSchemaVersion}. Update the app.");
        }

        if (version == CurrentSchemaVersion)
        {
            return;
        }

        await db.RunInTransactionAsync(connection =>
        {
            for (var v = version; v < CurrentSchemaVersion; v++)
            {
                _migrations[v](connection);
            }

            // PRAGMA takes no bound parameters; the value is a constant of ours.
            connection.Execute($"PRAGMA user_version = {CurrentSchemaVersion}");
        });
    }

    private static void CreateVersion1(SQLiteConnection connection)
    {
        connection.CreateTable<GeocacheRow>();
        connection.CreateTable<GeocacheListRow>();
        connection.CreateTable<GeocacheListMemberRow>();
        connection.Execute("CREATE UNIQUE INDEX IF NOT EXISTS UX_GeocacheLists_Name ON GeocacheLists (Name COLLATE NOCASE)");
    }

    private static (string Where, List<object?> Args) BuildWhere(GeocacheQuery query)
    {
        List<string> clauses = [];
        List<object?> args = [];

        if (!string.IsNullOrWhiteSpace(query.Text))
        {
            var pattern = $"%{query.Text.Trim().Replace("\\", "\\\\").Replace("%", "\\%").Replace("_", "\\_")}%";
            clauses.Add("(Name LIKE ? ESCAPE '\\' OR Code LIKE ? ESCAPE '\\' OR Owner LIKE ? ESCAPE '\\')");
            args.Add(pattern);
            args.Add(pattern);
            args.Add(pattern);
        }

        // Enum values are our own integers, so they are safe to inline.
        if (query.Types is { Count: > 0 })
        {
            clauses.Add($"Type IN ({string.Join(',', query.Types.Select(t => (int)t))})");
        }

        if (query.Sizes is { Count: > 0 })
        {
            clauses.Add($"Size IN ({string.Join(',', query.Sizes.Select(s => (int)s))})");
        }

        if (query.Statuses is { Count: > 0 })
        {
            clauses.Add($"Status IN ({string.Join(',', query.Statuses.Select(s => (int)s))})");
        }

        AddRange("Difficulty", query.MinDifficulty, query.MaxDifficulty);
        AddRange("Terrain", query.MinTerrain, query.MaxTerrain);

        if (query.HasCorrectedCoordinate is { } corrected)
        {
            clauses.Add(corrected ? "CorrectedLatitude IS NOT NULL" : "CorrectedLatitude IS NULL");
        }

        if (query.IsFound is { } found)
        {
            clauses.Add("IsFound = ?");
            args.Add(found);
        }

        if (query.IsSolved is { } solved)
        {
            clauses.Add("IsSolved = ?");
            args.Add(solved);
        }

        if (query.ListId is { } listId)
        {
            clauses.Add("Code IN (SELECT Code FROM GeocacheListMembers WHERE ListId = ?)");
            args.Add(listId);
        }

        void AddRange(string column, double? min, double? max)
        {
            if (min is { } lo)
            {
                clauses.Add($"{column} >= ?");
                args.Add(lo);
            }

            if (max is { } hi)
            {
                clauses.Add($"{column} <= ?");
                args.Add(hi);
            }
        }

        StringBuilder where = new();
        if (clauses.Count > 0)
        {
            where.Append(" WHERE ").AppendJoin(" AND ", clauses);
        }

        return (where.ToString(), args);
    }

    private static string ListInfoSql(string? where)
        => $"SELECT l.Id AS Id, l.Name AS Name, COUNT(m.Code) AS Count FROM GeocacheLists l LEFT JOIN GeocacheListMembers m ON m.ListId = l.Id {where} GROUP BY l.Id, l.Name ORDER BY l.Name COLLATE NOCASE";

    private static GeocacheList ToList(GeocacheListInfoRow row) => new(row.Id, row.Name, row.Count);

    private static void EnsureNameFree(SQLiteConnection connection, string name, int? exceptId)
    {
        var taken = connection.ExecuteScalar<int>(
            "SELECT COUNT(*) FROM GeocacheLists WHERE Name = ? COLLATE NOCASE AND Id <> ?",
            name,
            exceptId ?? -1);

        if (taken > 0)
        {
            throw new InvalidOperationException($"A list named '{name}' already exists.");
        }
    }
}
