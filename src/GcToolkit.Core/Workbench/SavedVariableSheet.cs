namespace GcToolkit.Core.Workbench;

/// <summary>A stage as stored in preferences.</summary>
public sealed record SavedStage(string Name, string Template);

/// <summary>The variable coordinate calculator's last sheet, as stored in preferences.</summary>
public sealed record SavedVariableSheet(
    Dictionary<string, string> Variables,
    List<SavedStage> Stages,
    string Posted,
    int FormatIndex);
