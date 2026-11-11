using GcToolkit.Core.Geocaches;
using MZikmund.Toolkit.WinUI.Services;

namespace GcToolkit.Core.Workbench;

/// <summary>Which tools a cache has open in the workbench, the selected one, and each tool's inputs.</summary>
public interface IWorkbenchToolStore
{
    /// <summary>The saved tools, or <see langword="null"/> when the cache has none (or the data is unreadable).</summary>
    WorkbenchToolsState? Load(GeocacheCode code);

    /// <summary>Saves the cache's tools; an empty list forgets the cache.</summary>
    void Save(GeocacheCode code, WorkbenchToolsState state);
}

/// <summary>Persisted per-cache tool pane. Every type here is registered in <c>PreferencesJsonContext</c>.</summary>
public sealed record WorkbenchToolsState(List<WorkbenchToolState> Tools, int SelectedIndex);

/// <summary>One open tool; <see cref="Inputs"/> is null for tools that do not opt into state.</summary>
public sealed record WorkbenchToolState(string ToolId, Dictionary<string, string>? Inputs);

/// <summary>
/// Keeps each cache's tools in preferences under <c>workbench.tools.&lt;code&gt;</c>, until the offline
/// store (#408) can hold them with the rest of the cache's user data.
/// </summary>
public sealed class PreferencesWorkbenchToolStore(IPreferences preferences) : IWorkbenchToolStore
{
    public static string KeyFor(GeocacheCode code) => $"workbench.tools.{code.Value}";

    public WorkbenchToolsState? Load(GeocacheCode code)
    {
        try
        {
            return preferences.GetComplex<WorkbenchToolsState?>(KeyFor(code), null) is { Tools: not null } state ? state : null;
        }
        catch (Exception)
        {
            return null;
        }
    }

    public void Save(GeocacheCode code, WorkbenchToolsState state)
    {
        if (state.Tools.Count == 0)
        {
            preferences.Remove(KeyFor(code));
        }
        else
        {
            preferences.SetComplex(KeyFor(code), state);
        }
    }
}
