using GcToolkit.Core.Services.Dialogs;
using GcToolkit.Services.Localization;

namespace GcToolkit.Services.Dialogs;

public sealed class TextPromptService(IDialogCoordinator dialogCoordinator, IXamlRootProvider xamlRootProvider) : ITextPromptService
{
    public async Task<string?> PromptAsync(string title, string label, string? initialText = null)
    {
        TextBox input = new() { Header = label, Text = initialText ?? "", MinWidth = 280 };
        input.SelectAll();

        ContentDialog dialog = new()
        {
            Title = title,
            Content = input,
            PrimaryButtonText = Localizer.Instance["Ok"],
            CloseButtonText = Localizer.Instance["CachesCancel"],
            DefaultButton = ContentDialogButton.Primary,
            XamlRoot = xamlRootProvider.XamlRoot,
        };

        var result = await dialogCoordinator.ShowAsync(dialog);
        return result == ContentDialogResult.Primary ? input.Text : null;
    }
}
