namespace GcToolkit.Core.ViewModels;

/// <summary>A tool with one main text input, so the workbench can send selected listing text into it.</summary>
public interface IToolTextInput
{
    string InputText { get; set; }
}
