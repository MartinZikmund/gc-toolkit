using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using GcToolkit.Core.Geocaches;
using GcToolkit.Core.ViewModels;

namespace GcToolkit.Core.Workbench;

/// <summary>One open cache in the workbench, with the tools opened for it.</summary>
public sealed partial class WorkbenchCacheTab : ObservableObject
{
    public WorkbenchCacheTab(GeocacheCode code)
    {
        Code = code;
        Tools.CollectionChanged += OnToolsCollectionChanged;
    }

    /// <summary>Raised when tools are added, removed or reordered, the selection settles, or a tool's inputs change.</summary>
    public event EventHandler? ToolsChanged;

    public GeocacheCode Code { get; }

    /// <summary>The cache's name once its listing has loaded; until then (or for a cache not in the store), its code.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Title))]
    public partial string? Name { get; set; }

    public string Title => string.IsNullOrWhiteSpace(Name) ? Code.Value : Name;

    public ObservableCollection<WorkbenchToolTab> Tools { get; } = [];

    public bool HasTools => Tools.Count > 0;

    [ObservableProperty]
    public partial WorkbenchToolTab? SelectedTool { get; set; }

    partial void OnSelectedToolChanged(WorkbenchToolTab? value)
    {
        // A tab strip rebuilding its items clears its selection for a moment; that is not a choice to keep.
        if (value is not null || Tools.Count == 0)
        {
            ToolsChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    private void OnToolsCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        // A drag reorder is a remove and an add of the same tab, so only the subscription moves.
        foreach (var tool in e.OldItems?.OfType<WorkbenchToolTab>() ?? [])
        {
            tool.StateChanged -= OnToolStateChanged;
        }

        foreach (var tool in e.NewItems?.OfType<WorkbenchToolTab>() ?? [])
        {
            tool.StateChanged += OnToolStateChanged;
        }

        OnPropertyChanged(nameof(HasTools));
        ToolsChanged?.Invoke(this, EventArgs.Empty);
    }

    private void OnToolStateChanged(object? sender, EventArgs e) => ToolsChanged?.Invoke(this, EventArgs.Empty);

    // List items without an explicit automation name are announced by their ToString.
    public override string ToString() => Title;
}

/// <summary>
/// A tool open in the workbench's tool pane, with its saved inputs. A class, not a record: the head keys
/// the hosted view on its identity.
/// </summary>
public sealed class WorkbenchToolTab(string toolId, string name, string? iconKey, Type viewModelType, ToolInputState? state = null)
{
    private IToolStateful? _tool;
    private IToolTextInput? _input;
    private IToolTextOutput? _output;
    private string? _pendingText;

    public string ToolId { get; } = toolId;

    public string Name { get; } = name;

    public string? IconKey { get; } = iconKey;

    public Type ViewModelType { get; } = viewModelType;

    /// <summary>The tool's inputs as last captured (or restored); null for tools without state.</summary>
    public ToolInputState? State { get; private set; } = state;

    /// <summary>Raised when the attached tool's inputs differ from <see cref="State"/>.</summary>
    public event EventHandler? StateChanged;

    /// <summary>The attached tool's text result; null when no tool is attached or it has no text result.</summary>
    public string? Output { get; private set; }

    public event EventHandler? OutputChanged;

    /// <summary>
    /// Connects the tool's view model once it exists: restores <see cref="State"/> into it, then tracks its
    /// edits. Text sent before the tool existed lands in its input last, over the restored state.
    /// </summary>
    public void Attach(object viewModel)
    {
        if (viewModel is IToolStateful tool && !ReferenceEquals(tool, _tool))
        {
            Detach();
            if (State is { } saved)
            {
                tool.RestoreState(saved);
            }

            _tool = tool;
            if (tool is INotifyPropertyChanged observable)
            {
                observable.PropertyChanged += OnToolPropertyChanged;
            }
        }

        _input = viewModel as IToolTextInput;
        if (_input is not null && _pendingText is { } text)
        {
            _pendingText = null;
            _input.InputText = text;
        }

        if (!ReferenceEquals(viewModel, _output))
        {
            DetachOutput();
            _output = viewModel as IToolTextOutput;
            if (_output is INotifyPropertyChanged source)
            {
                source.PropertyChanged += OnOutputSourceChanged;
            }
        }

        UpdateOutput();
    }

    /// <summary>Puts <paramref name="text"/> into the tool's input now, or as soon as the tool is attached.</summary>
    public void SendText(string text)
    {
        if (_input is not null)
        {
            _input.InputText = text;
        }
        else
        {
            _pendingText = text;
        }
    }

    public void Detach()
    {
        if (_tool is INotifyPropertyChanged observable)
        {
            observable.PropertyChanged -= OnToolPropertyChanged;
        }

        _tool = null;
        _input = null;
        DetachOutput();
        UpdateOutput();
    }

    private void DetachOutput()
    {
        if (_output is INotifyPropertyChanged source)
        {
            source.PropertyChanged -= OnOutputSourceChanged;
        }

        _output = null;
    }

    private void OnOutputSourceChanged(object? sender, PropertyChangedEventArgs e) => UpdateOutput();

    private void UpdateOutput()
    {
        var output = _output?.OutputText;
        if (output != Output)
        {
            Output = output;
            OutputChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    private void OnToolPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (_tool?.CaptureState() is not { } captured || captured.Equals(State))
        {
            return;
        }

        State = captured;
        StateChanged?.Invoke(this, EventArgs.Empty);
    }

    public override string ToString() => Name;
}

/// <summary>A tool offered by the "add tool" picker.</summary>
public sealed record ToolPickerItem(string ToolId, string Name, string? IconKey, Type ViewModelType)
{
    public override string ToString() => Name;
}
