using GcToolkit.Core.Geocaches;
using GcToolkit.Core.Workbench;

namespace GcToolkit.Core.Tests.Workbench;

internal sealed class FakeProgressProvider : IWorkbenchProgressProvider
{
    public List<CacheProgress> Items { get; } = [];

    private EventHandler? _progressChanged;

    public event EventHandler? ProgressChanged
    {
        add
        {
            _progressChanged += value;
            HandlerCount++;
        }

        remove
        {
            _progressChanged -= value;
            HandlerCount--;
        }
    }

    public int HandlerCount { get; private set; }

    public IReadOnlyList<CacheProgress> GetProgress() => Items;

    public void Raise() => _progressChanged?.Invoke(this, EventArgs.Empty);
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
