using GcToolkit.Core.Geocaches;

namespace GcToolkit.Core.Workbench;

/// <summary>The app-wide set of open caches (workbench tabs) and which one is active.</summary>
public interface IWorkbenchSessionService
{
    IReadOnlyList<GeocacheCode> OpenCaches { get; }

    GeocacheCode? ActiveCache { get; }

    /// <summary>How many caches were reopened from the previous run.</summary>
    int RestoredCount { get; }

    /// <summary>Adds the cache as a tab (if needed) and activates it.</summary>
    void Open(GeocacheCode code);

    /// <summary>Closes the tab; if it was active, the tab that takes its place (or the previous one) becomes active.</summary>
    void Close(GeocacheCode code);

    /// <summary>Activates an already open cache; ignored otherwise.</summary>
    void Activate(GeocacheCode code);

    /// <summary>Moves an open tab to <paramref name="newIndex"/> (clamped).</summary>
    void Move(GeocacheCode code, int newIndex);
}
