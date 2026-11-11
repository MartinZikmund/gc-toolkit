using GcToolkit.Core.Services.Dialogs;

namespace GcToolkit.Core.Tests.Fakes;

public sealed class FakeConfirmationDialogService : IConfirmationDialogService
{
    public ConfirmationResult Answer { get; set; } = ConfirmationResult.Confirmed;

    public int Asked { get; private set; }

    public Task<ConfirmationResult> ShowAsync(string title, string text)
    {
        Asked++;
        return Task.FromResult(Answer);
    }
}

public sealed class FakeTextPromptService : ITextPromptService
{
    public string? Answer { get; set; }

    public Task<string?> PromptAsync(string title, string label, string? initialText = null)
        => Task.FromResult(Answer);
}
