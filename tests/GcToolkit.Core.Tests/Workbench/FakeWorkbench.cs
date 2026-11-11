using GcToolkit.Core.Geocaches;
using GcToolkit.Core.Workbench;

namespace GcToolkit.Core.Tests.Workbench;

internal sealed class FakeGeocacheLookup(params string[] known) : IGeocacheLookup
{
    private readonly HashSet<string> _known = new(known, StringComparer.Ordinal);

    public bool Exists(GeocacheCode code) => _known.Contains(code.Value);
}

internal sealed class FakeProgressProvider : IWorkbenchProgressProvider
{
    public List<CacheProgress> Items { get; } = [];

    public event EventHandler? ProgressChanged;

    public IReadOnlyList<CacheProgress> GetProgress() => Items;

    public void Raise() => ProgressChanged?.Invoke(this, EventArgs.Empty);
}

internal sealed class FakeWorkbenchSession : IWorkbenchSessionService
{
    public List<GeocacheCode> Opened { get; } = [];

    public IReadOnlyList<GeocacheCode> OpenCaches => Opened;

    public GeocacheCode? ActiveCache { get; private set; }

    public int RestoredCount { get; set; }

    public void Open(GeocacheCode code)
    {
        Opened.Add(code);
        ActiveCache = code;
    }

    public void Close(GeocacheCode code) => Opened.Remove(code);

    public void Activate(GeocacheCode code) => ActiveCache = code;

    public void Move(GeocacheCode code, int newIndex)
    {
    }
}
