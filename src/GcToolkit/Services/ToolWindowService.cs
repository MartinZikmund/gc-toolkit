using GcToolkit.Core.Workbench;

namespace GcToolkit.Services;

/// <summary>
/// Opens workbench tools in their own windows on Windows and desktop. Each window gets its own
/// <see cref="WindowShell"/> (and scope) showing just the tool; the tool tab stays in its cache, so its
/// inputs keep being saved there. Closing the main window closes the tool windows too.
/// </summary>
public sealed class ToolWindowService(IServiceProvider services) : IToolWindowService
{
    private readonly Dictionary<WorkbenchToolTab, (Window Window, WindowShell Shell)> _windows = [];

    public bool IsSupported => !OperatingSystem.IsBrowser() && !OperatingSystem.IsAndroid() && !OperatingSystem.IsIOS();

    public void Show(WorkbenchToolTab tool, string title, Action closed)
    {
        if (_windows.TryGetValue(tool, out var open))
        {
            open.Window.Activate();
            return;
        }

        Window window = new() { Title = title };
        WindowShell shell = new(services, window);
        window.Content = shell;
        shell.ShowToolOnly(tool, title);
        _windows[tool] = (window, shell);
        window.Closed += (_, _) =>
        {
            _windows.Remove(tool);

            // Let go of the tool before the workbench takes it back.
            shell.ReleaseTool();
            closed();
        };

        try
        {
            window.AppWindow.Resize(new Windows.Graphics.SizeInt32(640, 800));
        }
        catch (Exception)
        {
            // Not every head can size a window; the default size works too.
        }

        window.Activate();
    }

    public void Close(WorkbenchToolTab tool)
    {
        if (_windows.TryGetValue(tool, out var open))
        {
            open.Window.Close();
        }
    }

    public void CloseAll()
    {
        foreach (var (window, _) in _windows.Values.ToList())
        {
            window.Close();
        }
    }
}
