using System.ComponentModel;
using GcToolkit.Core.Geocaches.Detail;
using GcToolkit.Core.ViewModels;
using Microsoft.Web.WebView2.Core;
using Windows.Foundation;

namespace GcToolkit.Controls;

/// <summary>A cache's listing, hint, waypoints and logs. Used by the cache page and the workbench.</summary>
public sealed partial class GeocacheListingPanel : UserControl
{
    private string? _shownHtml;
    private bool _webViewReady;
#if __WASM__
    // WebAssembly never raises WebMessageReceived (Uno gap), so the selection is read on a timer instead.
    private readonly DispatcherTimer _selectionPoll = new() { Interval = TimeSpan.FromMilliseconds(600) };
    private string _polledText = string.Empty;
#endif

    public GeocacheListingPanel()
    {
        InitializeComponent();
        Loaded += (_, _) => Attach(ViewModel);
        Unloaded += (_, _) => Detach(ViewModel);
#if __WASM__
        _selectionPoll.Tick += async (_, _) => await PollSelectionAsync();
        Loaded += (_, _) => _selectionPoll.Start();
        Unloaded += (_, _) => _selectionPoll.Stop();
#endif
        ActualThemeChanged += (_, _) => ApplyTheme();
    }

    public static readonly DependencyProperty ViewModelProperty = DependencyProperty.Register(
        nameof(ViewModel),
        typeof(GeocacheDetailViewModel),
        typeof(GeocacheListingPanel),
        new PropertyMetadata(null, (d, e) => ((GeocacheListingPanel)d).OnViewModelChanged(e)));

    public GeocacheDetailViewModel? ViewModel
    {
        get => (GeocacheDetailViewModel?)GetValue(ViewModelProperty);
        set => SetValue(ViewModelProperty, value);
    }

    public static readonly DependencyProperty NoDescriptionContentProperty = DependencyProperty.Register(
        nameof(NoDescriptionContent), typeof(object), typeof(GeocacheListingPanel), new PropertyMetadata(null));

    /// <summary>Shown above the listing; the host decides when (the workbench's paste box for caches without a description).</summary>
    public object? NoDescriptionContent
    {
        get => GetValue(NoDescriptionContentProperty);
        set => SetValue(NoDescriptionContentProperty, value);
    }

    /// <summary>Text selected in the listing, hint, a waypoint or a log, with where it is (top centre, in this panel's coordinates).</summary>
    public event EventHandler<ListingSelectionEventArgs>? SelectionChanged;

    private void OnViewModelChanged(DependencyPropertyChangedEventArgs e)
    {
        if (!IsLoaded)
        {
            return;
        }

        Detach(e.OldValue as GeocacheDetailViewModel);
        Attach(e.NewValue as GeocacheDetailViewModel);
    }

    private async void Attach(GeocacheDetailViewModel? viewModel)
    {
        if (viewModel is null)
        {
            return;
        }

        viewModel.PropertyChanged -= OnViewModelPropertyChanged;
        viewModel.PropertyChanged += OnViewModelPropertyChanged;
        ApplyTheme();
        await ShowListingAsync();
    }

    private void Detach(GeocacheDetailViewModel? viewModel)
    {
        if (viewModel is not null)
        {
            viewModel.PropertyChanged -= OnViewModelPropertyChanged;
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
            ListingWebView.CoreWebView2.WebMessageReceived += OnWebMessageReceived;
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

    private void OnWebMessageReceived(CoreWebView2 sender, CoreWebView2WebMessageReceivedEventArgs args)
    {
        if (ListingSelection.TryParse(args.WebMessageAsJson, out var selection))
        {
            RaiseWebSelection(selection);
        }
    }

    private void RaiseWebSelection(ListingSelection selection)
    {
        var anchor = ListingWebView.TransformToVisual(this).TransformPoint(new Point(selection.X + (selection.Width / 2), selection.Y));
        SelectionChanged?.Invoke(this, new(selection.Text, anchor));
    }

#if __WASM__
    private async Task PollSelectionAsync()
    {
        if (!_webViewReady || ViewModel?.IsListingSelected != true)
        {
            return;
        }

        try
        {
            var json = await ListingWebView.ExecuteScriptAsync("String(window.gcGetSelection ? window.gcGetSelection() : '')");
            if (ListingSelection.TryParse(json, out var selection) && selection.Text != _polledText)
            {
                _polledText = selection.Text;
                RaiseWebSelection(selection);
            }
        }
        catch (Exception)
        {
            // The page may be mid-navigation; the next tick tries again.
        }
    }
#endif

    private void OnTextSelectionChanged(object sender, RoutedEventArgs e)
    {
        if (sender is TextBlock text)
        {
            var anchor = text.TransformToVisual(this).TransformPoint(new Point(text.ActualWidth / 2, 0));
            SelectionChanged?.Invoke(this, new(text.SelectedText, anchor));
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

public sealed class ListingSelectionEventArgs(string text, Point anchor) : EventArgs
{
    public string Text { get; } = text;

    public Point Anchor { get; } = anchor;
}
