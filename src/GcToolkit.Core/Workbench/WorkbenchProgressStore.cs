using GcToolkit.Core.Geocaches;
using MZikmund.Toolkit.WinUI.Services;

namespace GcToolkit.Core.Workbench;

/// <summary>Where the workbench reports what the user did to a cache.</summary>
public interface IWorkbenchProgressRecorder
{
    /// <summary>Updates the given counts. Counts as an edit (moves "last edited") only when one of them changed.</summary>
    void RecordWork(GeocacheCode code, int? variablesFilled = null, int? variablesTotal = null, int? scratchpadLines = null, int? openTools = null);

    /// <summary>Name and found/solved flags as the listing shows them; never counts as an edit.</summary>
    void RecordDetails(GeocacheCode code, string name, bool isSolved, bool isFound);
}

/// <summary>A cache's progress as stored in preferences.</summary>
public sealed record SavedCacheProgress(
    string Code,
    string Name,
    int VariablesFilled,
    int VariablesTotal,
    int ScratchpadLineCount,
    int OpenToolCount,
    DateTimeOffset LastEdited,
    bool IsSolved,
    bool IsFound,
    bool IsRemoved);

/// <summary>
/// Per-cache work summary for Home's "Continue solving", kept small in preferences so the list
/// shows without opening every cache's sheet, scratchpad and tools.
/// </summary>
public sealed class WorkbenchProgressStore(IPreferences preferences, TimeProvider time)
    : IWorkbenchProgressProvider, IWorkbenchProgressRecorder
{
    private const string PreferencesKey = "workbench.progress";

    private List<SavedCacheProgress>? _items;

    public event EventHandler? ProgressChanged;

    public IReadOnlyList<CacheProgress> GetProgress()
        => [.. Items.Select(p => new CacheProgress(
            GeocacheCode.Parse(p.Code),
            p.Name,
            p.VariablesFilled,
            p.VariablesTotal,
            p.ScratchpadLineCount,
            p.OpenToolCount,
            p.LastEdited,
            p.IsSolved,
            p.IsFound,
            p.IsRemoved))];

    public void RecordWork(GeocacheCode code, int? variablesFilled = null, int? variablesTotal = null, int? scratchpadLines = null, int? openTools = null)
    {
        var current = Find(code);
        var changed = current with
        {
            VariablesFilled = variablesFilled ?? current.VariablesFilled,
            VariablesTotal = variablesTotal ?? current.VariablesTotal,
            ScratchpadLineCount = scratchpadLines ?? current.ScratchpadLineCount,
            OpenToolCount = openTools ?? current.OpenToolCount,
        };
        if (changed != current)
        {
            Store(changed with { LastEdited = time.GetUtcNow(), IsRemoved = false });
        }
    }

    public void RecordDetails(GeocacheCode code, string name, bool isSolved, bool isFound)
    {
        var current = Find(code);
        var changed = current with
        {
            Name = string.IsNullOrWhiteSpace(name) ? current.Name : name,
            IsSolved = isSolved,
            IsFound = isFound,
        };
        if (changed != current)
        {
            Store(changed);
        }
    }

    public void Remove(GeocacheCode code)
    {
        var current = Find(code);
        if (!current.IsRemoved)
        {
            Store(current with { IsRemoved = true });
        }
    }

    private List<SavedCacheProgress> Items => _items ??= Load();

    private SavedCacheProgress Find(GeocacheCode code)
        => Items.FirstOrDefault(p => p.Code == code.Value)
            ?? new(code.Value, code.Value, 0, 0, 0, 0, DateTimeOffset.MinValue, false, false, false);

    private void Store(SavedCacheProgress progress)
    {
        var index = Items.FindIndex(p => p.Code == progress.Code);
        if (index < 0)
        {
            Items.Add(progress);
        }
        else
        {
            Items[index] = progress;
        }

        try
        {
            preferences.SetComplex(PreferencesKey, Items);
        }
        catch (Exception)
        {
            // Losing the summary only empties "Continue solving"; the work itself is stored elsewhere.
        }

        ProgressChanged?.Invoke(this, EventArgs.Empty);
    }

    private List<SavedCacheProgress> Load()
    {
        try
        {
            if (preferences.TryGetComplex<List<SavedCacheProgress>>(PreferencesKey, out var stored) && stored is not null)
            {
                return [.. stored.Where(p => GeocacheCode.TryParse(p.Code, out _))];
            }
        }
        catch (Exception)
        {
            // Unreadable stored data is the same as none.
        }

        return [];
    }
}
