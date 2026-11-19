using GcToolkit.Core.Navigation;
using GcToolkit.Core.ViewModels;
using GcToolkit.Services.Localization;

namespace GcToolkit.Views;

[NavigationInfo(NavigationSection.Home)]
public partial class HomeViewBase : ViewBase<HomeViewModel> { }

public sealed partial class HomeView : HomeViewBase
{
    public HomeView()
    {
        this.InitializeComponent();
    }

    public string RecentsHeader { get; } = SectionCaption("RecentlyUsed");

    public string FavoritesHeader { get; } = SectionCaption("FavoriteTools");

    public string NewAndUpdatedHeader { get; } = SectionCaption("NewAndUpdated");

    public string ContinueSolvingHeader { get; } = SectionCaption("Workbench_ContinueSolving");

    public static Visibility ShowContinueSolving(bool hasItems, bool restoredNotice)
        => hasItems || restoredNotice ? Visibility.Visible : Visibility.Collapsed;

    public static string ProgressLine(string variables, string scratchpad)
        => string.Join(" · ", new[] { variables, scratchpad }.Where(part => part.Length > 0));

    // A solved cache is opened to check or edit, not resumed.
    public static string ResumeText(bool isSolved) => Localizer.Instance[isSolved ? "Workbench_Open" : "Workbench_Resume"];

    public static string ResumeLabel(string name, bool isSolved) => $"{ResumeText(isSolved)} {name}";

    private void OnResumeClick(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { Tag: ContinueSolvingItem item })
        {
            ViewModel?.ContinueSolving.ResumeCommand.Execute(item);
        }
    }

    private void OnRemoveClick(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { Tag: ContinueSolvingItem item })
        {
            ViewModel?.ContinueSolving.RemoveCommand.Execute(item);
        }
    }

    // XAML has no text-transform, so the uppercase section captions are cased here. Invariant is
    // safe for the shipped languages and a language switch already requires a restart.
    private static string SectionCaption(string key) => Localizer.Instance[key].ToUpperInvariant();

    private string HeaderStatus(bool hasFavorites, bool hasRecents)
        => Localizer.Instance[hasFavorites || hasRecents ? "HomeStatusReady" : "HomeStatusEmpty"];
}
