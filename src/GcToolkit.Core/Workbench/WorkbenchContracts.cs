using GcToolkit.Core.Geocaches;

namespace GcToolkit.Core.Workbench;

/// <summary>Per-cache work state, implemented by the store, variables and scratchpad features.</summary>
public interface IWorkbenchProgressProvider
{
    /// <summary>Work state of every cache that has a record. Callers filter with <see cref="CacheProgress.IsInProgress"/>.</summary>
    IReadOnlyList<CacheProgress> GetProgress();

    event EventHandler? ProgressChanged;
}

public sealed record CacheProgress(
    GeocacheCode Code,
    string Name,
    int VariablesFilled,
    int VariablesTotal,
    int ScratchpadLineCount,
    int OpenToolCount,
    DateTimeOffset LastEdited,
    bool IsSolved,
    bool IsFound,
    bool IsRemoved)
{
    /// <summary>Has work (a variable, scratchpad text or an open tool) and was not marked found or removed.</summary>
    public bool IsInProgress =>
        !IsFound && !IsRemoved && (VariablesFilled > 0 || ScratchpadLineCount > 0 || OpenToolCount > 0);
}
