namespace GcToolkit.Core.Workbench;

/// <summary>Persisted workbench session. Stored through <c>IPreferences</c> under <c>workbench.session</c>.</summary>
public sealed record WorkbenchSessionState(List<string> OpenCaches, string? ActiveCache);
