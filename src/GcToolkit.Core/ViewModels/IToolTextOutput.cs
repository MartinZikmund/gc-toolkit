namespace GcToolkit.Core.ViewModels;

/// <summary>A tool with one obvious text result, so the workbench can send it to the variable sheet.</summary>
public interface IToolTextOutput
{
    string OutputText { get; }
}
