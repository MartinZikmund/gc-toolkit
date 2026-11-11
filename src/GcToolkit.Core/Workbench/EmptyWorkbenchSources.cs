using GcToolkit.Core.Geocaches;

namespace GcToolkit.Core.Workbench;

/// <summary>Stand-in until the offline store (#408) provides real caches: nothing exists.</summary>
public sealed class EmptyGeocacheLookup : IGeocacheLookup
{
    public bool Exists(GeocacheCode code) => false;
}

/// <summary>Stand-in until variables and scratchpad exist: no cache has any work.</summary>
public sealed class EmptyWorkbenchProgressProvider : IWorkbenchProgressProvider
{
    public event EventHandler? ProgressChanged
    {
        add { }
        remove { }
    }

    public IReadOnlyList<CacheProgress> GetProgress() => [];
}
