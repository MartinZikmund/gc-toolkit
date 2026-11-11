using System.ComponentModel;
using GcToolkit.Core.Geocaches.Detail;
using GcToolkit.Core.Navigation;
using GcToolkit.Core.ViewModels;
using Microsoft.UI.Xaml.Controls;
using Microsoft.Web.WebView2.Core;

namespace GcToolkit.Views;

[NavigationInfo(NavigationSection.Catalog)]
public partial class GeocacheDetailViewBase : ViewBase<GeocacheDetailViewModel> { }

public sealed partial class GeocacheDetailView : GeocacheDetailViewBase
{
    private string? _shownHtml;
    private bool _webViewReady;

    public GeocacheDetailView()
    {
        this.InitializeComponent();
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
        ActualThemeChanged += (_, _) => ApplyTheme();
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (ViewModel is null)
        {
            return;
        }

        ViewModel.PropertyChanged += OnViewModelPropertyChanged;
        ApplyTheme();
        await ShowListingAsync();
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        if (ViewModel is not null)
        {
            ViewModel.PropertyChanged -= OnViewModelPropertyChanged;
        }
    }

    private async void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(GeocacheDetailViewModel.ListingHtml))
        {
            await ShowListingAsync();
        }
    }

    private void ApplyTheme()
    {
        if (ViewModel is not null)
        {
            ViewModel.IsDarkTheme = ActualTheme == ElementTheme.Dark;
        }
    }

    private async Task ShowListingAsync()
    {
        var html = ViewModel?.ListingHtml;
        if (string.IsNullOrEmpty(html) || html == _shownHtml)
        {
            return;
        }

        if (!_webViewReady)
        {
            await ListingWebView.EnsureCoreWebView2Async();
            ListingWebView.CoreWebView2.NavigationStarting += OnNavigationStarting;
            ListingWebView.CoreWebView2.NewWindowRequested += OnNewWindowRequested;
            _webViewReady = true;
        }

        _shownHtml = html;
        ListingWebView.NavigateToString(html);
    }

    // The listing itself is a data: document; anything else is a link the user clicked.
    private static void OnNavigationStarting(CoreWebView2 sender, CoreWebView2NavigationStartingEventArgs args)
    {
        var action = ListingLinkPolicy.Decide(args.Uri);
        if (action == ListingLinkAction.Allow)
        {
            return;
        }

        args.Cancel = true;
        if (action == ListingLinkAction.OpenExternally)
        {
            OpenExternally(args.Uri);
        }
    }

    private static void OpenExternally(string? uri)
    {
        if (Uri.TryCreate(uri, UriKind.Absolute, out var target))
        {
            _ = Windows.System.Launcher.LaunchUriAsync(target);
        }
    }

    private static void OnNewWindowRequested(CoreWebView2 sender, CoreWebView2NewWindowRequestedEventArgs args)
    {
        args.Handled = true;
        if (ListingLinkPolicy.Decide(args.Uri) == ListingLinkAction.OpenExternally)
        {
            OpenExternally(args.Uri);
        }
    }

    private void OnSectionChanged(SelectorBar sender, SelectorBarSelectionChangedEventArgs args)
    {
        if (ViewModel is not null
            && sender.SelectedItem?.Tag is string tag
            && Enum.TryParse<GeocacheDetailSection>(tag, out var section))
        {
            ViewModel.SelectedSection = section;
        }
    }

    private void OnCopyWaypointClick(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { DataContext: WaypointItem waypoint })
        {
            ViewModel?.CopyWaypointCoordinateCommand.Execute(waypoint);
        }
    }
}
