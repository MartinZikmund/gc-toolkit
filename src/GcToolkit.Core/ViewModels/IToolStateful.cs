namespace GcToolkit.Core.ViewModels;

/// <summary>
/// Opt-in for tool view models whose inputs should survive switching caches and relaunching the app
/// (the workbench remembers them per cache). Capture only what the user entered; results are recomputed.
/// </summary>
public interface IToolStateful
{
    ToolInputState CaptureState();

    /// <summary>Applies a saved snapshot. Keys missing from it keep their current value.</summary>
    void RestoreState(ToolInputState state);
}
