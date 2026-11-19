namespace GcToolkit.Core.Workbench;

/// <summary>Opens a workbench tool in its own window. Only desktop heads have windows to open.</summary>
public interface IToolWindowService
{
    bool IsSupported { get; }

    /// <summary>Shows <paramref name="tool"/> in a new window; <paramref name="closed"/> runs once that window is gone.</summary>
    void Show(WorkbenchToolTab tool, string title, Action closed);

    /// <summary>Closes the tool's window, if it has one.</summary>
    void Close(WorkbenchToolTab tool);
}

/// <summary>For heads without windows (phones, the browser).</summary>
public sealed class NoToolWindowService : IToolWindowService
{
    public bool IsSupported => false;

    public void Show(WorkbenchToolTab tool, string title, Action closed)
    {
    }

    public void Close(WorkbenchToolTab tool)
    {
    }
}
