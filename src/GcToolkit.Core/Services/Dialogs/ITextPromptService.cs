namespace GcToolkit.Core.Services.Dialogs;

public interface ITextPromptService
{
    /// <summary>Asks for one line of text. Returns null when the user cancels.</summary>
    Task<string?> PromptAsync(string title, string label, string? initialText = null);
}
