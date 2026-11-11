
namespace GcToolkit.Core.Workbench;

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
