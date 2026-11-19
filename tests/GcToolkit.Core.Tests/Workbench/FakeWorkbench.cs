using CommunityToolkit.Mvvm.ComponentModel;
using GcToolkit.Core.Geocaches;
using GcToolkit.Core.ViewModels;
using GcToolkit.Core.Workbench;

namespace GcToolkit.Core.Tests.Workbench;

internal sealed class FakeToolWindowService : IToolWindowService
{
    private readonly Dictionary<WorkbenchToolTab, Action> _open = [];

    public bool IsSupported { get; set; } = true;

    public IReadOnlyCollection<WorkbenchToolTab> Open => _open.Keys;

    public string? LastTitle { get; private set; }

    public void Show(WorkbenchToolTab tool, string title, Action closed)
    {
        LastTitle = title;
        _open[tool] = closed;
    }

    public void Close(WorkbenchToolTab tool)
    {
        if (_open.Remove(tool, out var closed))
        {
            closed();
        }
    }

    /// <summary>The user closes the tool's window.</summary>
    public void UserCloses(WorkbenchToolTab tool) => Close(tool);
}

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

    public List<GeocacheCode> Removed { get; } = [];

    public void Remove(GeocacheCode code)
    {
        Removed.Add(code);
        Items.RemoveAll(p => p.Code == code);
        _progressChanged?.Invoke(this, EventArgs.Empty);
    }

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

/// <summary>A tool whose one input, <see cref="Text"/>, takes part in workbench state.</summary>
internal sealed partial class FakeStatefulTool : ObservableObject, IToolStateful, IToolTextInput, IToolTextOutput
{
    string IToolTextOutput.OutputText => Output;

    public string InputText
    {
        get => Text;
        set => Text = value;
    }

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

/// <summary>A tool with no text output, like a map or a figure.</summary>
internal sealed partial class FakePlainTool : ObservableObject
{
    [ObservableProperty]
    public partial string Text { get; set; } = string.Empty;
}
