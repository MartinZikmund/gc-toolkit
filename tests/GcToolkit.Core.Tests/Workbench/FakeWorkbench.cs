using CommunityToolkit.Mvvm.ComponentModel;
using GcToolkit.Core.Geocaches;
using GcToolkit.Core.ViewModels;
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

/// <summary>A tool whose one input, <see cref="Text"/>, takes part in workbench state.</summary>
internal sealed partial class FakeStatefulTool : ObservableObject, IToolStateful
{
    [ObservableProperty]
    public partial string Text { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string Output { get; set; } = string.Empty;

    public static ToolInputState StateWith(string text)
    {
        ToolInputState state = new();
        state.Set("text", text);
        return state;
    }

    public ToolInputState CaptureState() => StateWith(Text);

    public void RestoreState(ToolInputState state) => Text = state.GetString("text");
}
