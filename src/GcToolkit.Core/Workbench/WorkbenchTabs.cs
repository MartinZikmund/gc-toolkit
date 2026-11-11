using System.Collections.ObjectModel;
using GcToolkit.Core.Geocaches;

namespace GcToolkit.Core.Workbench;

/// <summary>One open cache in the workbench, with the tools opened for it (kept in memory only).</summary>
public sealed partial class WorkbenchCacheTab : ObservableObject
{
    public WorkbenchCacheTab(GeocacheCode code)
    {
        Code = code;
        Tools.CollectionChanged += (_, _) => OnPropertyChanged(nameof(HasTools));
    }

    public GeocacheCode Code { get; }

    /// <summary>The cache code until the offline store (#408) can supply the cache name.</summary>
    public string Title => Code.Value;

    public ObservableCollection<WorkbenchToolTab> Tools { get; } = [];

    public bool HasTools => Tools.Count > 0;

    [ObservableProperty]
    public partial WorkbenchToolTab? SelectedTool { get; set; }

    // List items without an explicit automation name are announced by their ToString.
    public override string ToString() => Title;
}

/// <summary>A tool open in the workbench's tool pane. A class, not a record: the head keys the hosted view on its identity.</summary>
public sealed class WorkbenchToolTab(string toolId, string name, string? iconKey, Type viewModelType)
{
    public string ToolId { get; } = toolId;

    public string Name { get; } = name;

    public string? IconKey { get; } = iconKey;

    public Type ViewModelType { get; } = viewModelType;

    public override string ToString() => Name;
}

/// <summary>A tool offered by the "add tool" picker.</summary>
public sealed record ToolPickerItem(string ToolId, string Name, string? IconKey, Type ViewModelType)
{
    public override string ToString() => Name;
}
