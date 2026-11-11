using System.Collections.Concurrent;
using GcToolkit.Core.Geocaches;

namespace GcToolkit.Core.Workbench;

/// <summary>Stand-in until the offline store (#408): scratchpads live for the app run only.</summary>
public sealed class MemoryScratchpadRepository : IScratchpadRepository
{
    private readonly ConcurrentDictionary<GeocacheCode, string> _texts = new();

    public Task<string?> LoadAsync(GeocacheCode code, CancellationToken cancellationToken = default)
        => Task.FromResult(_texts.TryGetValue(code, out var text) ? text : null);

    public Task SaveAsync(GeocacheCode code, string text, CancellationToken cancellationToken = default)
    {
        _texts[code] = text;
        return Task.CompletedTask;
    }
}
