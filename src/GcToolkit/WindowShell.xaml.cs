using System.Diagnostics;
using GcToolkit.Core.Navigation;
using GcToolkit.Core.Services;
using GcToolkit.Core.Services.Settings;
using GcToolkit.Core.Services.Theming;
using GcToolkit.Core.ViewModels;
using GcToolkit.Helpers;
using GcToolkit.Infrastructure;
using GcToolkit.Services.Localization;
using GcToolkit.Services.Navigation;
using GcToolkit.Services.Settings;
using Microsoft.UI;
using Microsoft.UI.Composition.SystemBackdrops;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Data;
using Windows.Foundation.Metadata;
using Windows.Graphics;
using Windows.UI;
using IPreferences = MZikmund.Toolkit.WinUI.Services.IPreferences;

namespace GcToolkit;

public sealed partial class WindowShell : Page, IWindowShell
{
    private readonly IServiceScope _windowScope;
    private readonly Window _associatedWindow;
    private bool _isWindowClosed;
    private ViewModelBase? _currentPageViewModel;
    private bool _suppressSelectionChanged;

#if !HAS_UNO
    private const string WindowPlacementKey = "MainWindowPlacement";

    // Below these the title-bar search box and the NavigationView content stop being usable. They are
    // DIPs — AppWindow works in physical pixels, so they are scaled by the monitor's rasterization scale.
    private const int MinimumWindowWidth = 640;
    private const int MinimumWindowHeight = 480;

    private AppWindow? _appWindow;
    private RectInt32? _restoreBounds;
    private bool _isMaximized;
    private double _minimumSizeScale;
#endif

    public WindowShell(IServiceProvider serviceProvider, Window associatedWindow)
    {
        InitializeComponent();

        _windowScope = serviceProvider.CreateScope();
        var windowShellProvider = (WindowShellProvider)ServiceProvider.GetRequiredService<IWindowShellProvider>();
        windowShellProvider.SetShell(this, associatedWindow);

        var navigationService = ServiceProvider.GetRequiredService<INavigationService>();
        navigationService.Initialize();

        // Restore saved theme
        var settings = ServiceProvider.GetRequiredService<IAppPreferences>();
        var themeManager = ServiceProvider.GetRequiredService<IThemeManager>();
        themeManager.SetTheme(settings.Theme);

        _associatedWindow = associatedWindow;
        _associatedWindow.Closed += OnWindowClosed;

        ViewModel = ServiceProvider.GetRequiredService<WindowShellViewModel>();
        ViewModel.PropertyChanged += ViewModel_PropertyChanged;

        Search = ServiceProvider.GetRequiredService<SearchViewModel>();

        // After Search: the no-custom-title-bar fallback rehosts the search box in the navigation pane.
        CustomizeWindow();

        InnerFrame.Navigated += InnerFrame_Navigated;
        Loading += WindowShell_Loading;

        BuildToolNavigation();
        UpdateWindowTitle();
    }

    public IServiceProvider ServiceProvider => _windowScope.ServiceProvider;

    public WindowShellViewModel ViewModel { get; }

    public SearchViewModel Search { get; }

    public Frame RootFrame => InnerFrame;

    public bool HasCustomTitleBar { get; private set; }

    private void ViewModel_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(WindowShellViewModel.Title))
        {
            UpdateWindowTitle();
        }
    }

    private void UpdateWindowTitle()
    {
        if (ViewModel.Title is not null && !_isWindowClosed)
        {
            _associatedWindow.Title = ViewModel.Title;
        }
    }

    private void InnerFrame_Navigated(object sender, Microsoft.UI.Xaml.Navigation.NavigationEventArgs e)
    {
        // Unsubscribe from previous page's ViewModel
        if (_currentPageViewModel is not null)
        {
            _currentPageViewModel.PropertyChanged -= PageViewModel_PropertyChanged;
            _currentPageViewModel = null;
        }

        // Subscribe to new page's ViewModel for title updates
        if (e.Content is FrameworkElement { DataContext: ViewModelBase pageViewModel })
        {
            _currentPageViewModel = pageViewModel;
            _currentPageViewModel.PropertyChanged += PageViewModel_PropertyChanged;
            UpdatePageTitle();
        }
        else
        {
            ViewModel.Title = ServiceProvider.GetRequiredService<IStringLocalizer>()["ApplicationName"];
        }

        ViewModel.NotifyCanGoBackChanged();
        UpdateNavigationViewSelection();
    }

    private void PageViewModel_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(ViewModelBase.PageTitle))
        {
            UpdatePageTitle();
        }
    }

    private void UpdatePageTitle()
    {
        if (_currentPageViewModel is not null)
        {
            ViewModel.Title = _currentPageViewModel.PageTitle
                ?? ServiceProvider.GetRequiredService<IStringLocalizer>()["ApplicationName"];
        }
    }

    private void OnWindowClosed(object sender, WindowEventArgs args)
    {
        _isWindowClosed = true;
#if !HAS_UNO
        SaveWindowPlacement();
        DetachWindowIntegration();
#endif
        _associatedWindow.Closed -= OnWindowClosed;
        InnerFrame.Navigated -= InnerFrame_Navigated;
        ViewModel.PropertyChanged -= ViewModel_PropertyChanged;
        if (_currentPageViewModel is not null)
        {
            _currentPageViewModel.PropertyChanged -= PageViewModel_PropertyChanged;
            _currentPageViewModel = null;
        }

        // Dispose the window-scoped container so scoped services (e.g. ThemeManager,
        // which subscribes to UISettings.ColorValuesChanged) release their handlers/resources.
        _windowScope.Dispose();
    }

    private void WindowShell_Loading(FrameworkElement sender, object args)
    {
        var windowShellProvider = (WindowShellProvider)ServiceProvider.GetRequiredService<IWindowShellProvider>();
        windowShellProvider.SetXamlRoot(XamlRoot ?? throw new InvalidOperationException("XamlRoot must be set."));

#if !HAS_UNO
        // The XamlRoot (and with it the rasterization scale) only exists from here on.
        ApplyMinimumWindowSize();
#endif
    }

    public void SetTitleBar(UIElement? titleBar)
    {
#if !HAS_UNO
        // The WinUI TitleBar control (AppTitleBar) is the Windows custom title bar; other heads — and
        // Windows builds without title bar customization — use the system title bar, so this is a no-op.
        if (!_isWindowClosed && HasCustomTitleBar)
        {
            _associatedWindow.SetTitleBar(titleBar ?? AppTitleBar);
        }
#endif
    }

    private void CustomizeWindow()
    {
#if !HAS_UNO
        _appWindow = _associatedWindow.AppWindow;

        HasCustomTitleBar = AppWindowTitleBar.IsCustomizationSupported();
        if (HasCustomTitleBar)
        {
            _associatedWindow.ExtendsContentIntoTitleBar = true;
            _appWindow.TitleBar.PreferredHeightOption = TitleBarHeightOption.Tall;
            _associatedWindow.SetTitleBar(AppTitleBar);

            // Caption buttons are drawn by the system, so their colours must follow the effective theme
            // — including a live OS light/dark switch, which is what ActualThemeChanged reports.
            ActualThemeChanged += OnActualThemeChanged;
            ApplyCaptionButtonColors();
        }
        else
        {
            // Windows 10 draws its own caption bar. Keeping ours as well would stack a second title bar
            // and strand the search box in it, so search moves into the pane as on the non-Windows heads.
            AppTitleBar.Visibility = Visibility.Collapsed;
            NavView.AutoSuggestBox = CreatePaneSearchBox();
        }

        RestoreWindowPlacement();
        TrackWindowPlacement();
        _appWindow.Changed += OnAppWindowChanged;
#endif

        if (MicaController.IsSupported())
        {
            _associatedWindow.SystemBackdrop = new MicaBackdrop();
            Background = null;
        }
    }

#if !HAS_UNO
    #region Windows window integration

    /// <summary>
    /// The pane search box used when the title bar cannot host one. It mirrors the XAML declaration in
    /// <c>NavigationView.AutoSuggestBox</c> that the other heads use.
    /// </summary>
    private AutoSuggestBox CreatePaneSearchBox()
    {
        var placeholder = Localizer.Instance.GetString("SearchPlaceholder");
        var searchBox = new AutoSuggestBox
        {
            PlaceholderText = placeholder,
            QueryIcon = new SymbolIcon(Symbol.Find),
        };

        AutomationProperties.SetName(searchBox, placeholder);
        searchBox.SetBinding(
            AutoSuggestBox.TextProperty,
            new Binding
            {
                Source = Search,
                Path = new PropertyPath(nameof(SearchViewModel.Query)),
                Mode = BindingMode.TwoWay,
            });

        return searchBox;
    }

    private void OnActualThemeChanged(FrameworkElement sender, object args)
    {
        ApplyCaptionButtonColors();

        // ThemeManager refreshes the same colours when the system colours change, but derives them from
        // the app-level theme, which does not follow a live OS switch. Re-applying at low priority makes
        // the effective theme the last write of that turn.
        DispatcherQueue.TryEnqueue(DispatcherQueuePriority.Low, ApplyCaptionButtonColors);
    }

    private void ApplyCaptionButtonColors()
    {
        try
        {
            if (_appWindow is null)
            {
                return;
            }

            var titleBar = _appWindow.TitleBar;
            var isDark = ActualTheme == ElementTheme.Dark;
            var foreground = isDark ? Colors.White : Colors.Black;

            titleBar.BackgroundColor = Colors.Transparent;
            titleBar.ButtonBackgroundColor = Colors.Transparent;
            titleBar.ButtonInactiveBackgroundColor = Colors.Transparent;
            titleBar.ButtonForegroundColor = foreground;
            titleBar.ButtonHoverForegroundColor = foreground;
            titleBar.ButtonPressedForegroundColor = foreground;
            titleBar.ButtonInactiveForegroundColor = Colors.Gray;
            titleBar.ButtonHoverBackgroundColor = isDark
                ? Color.FromArgb(100, 100, 100, 100)
                : Color.FromArgb(100, 200, 200, 200);
            titleBar.ButtonPressedBackgroundColor = isDark
                ? Color.FromArgb(200, 100, 100, 100)
                : Color.FromArgb(200, 200, 200, 200);
        }
        catch (Exception exception)
        {
            LogFailure("update the caption button colors", exception);
        }
    }

    private void RestoreWindowPlacement()
    {
        try
        {
            var placement = ServiceProvider.GetRequiredService<IPreferences>()
                .GetComplex<WindowPlacement?>(WindowPlacementKey, null);
            if (placement is null || _appWindow is null)
            {
                return;
            }

            // Clamping keeps a window saved on a now-disconnected monitor on screen.
            var bounds = ClampToWorkArea(new RectInt32(placement.Left, placement.Top, placement.Width, placement.Height));
            _appWindow.MoveAndResize(bounds);
            _restoreBounds = bounds;
            _isMaximized = placement.IsMaximized;

            if (placement.IsMaximized && _appWindow.Presenter is OverlappedPresenter presenter)
            {
                presenter.Maximize();
            }
        }
        catch (Exception exception)
        {
            LogFailure("restore the saved window placement", exception);
        }
    }

    private void SaveWindowPlacement()
    {
        if (_restoreBounds is not { } bounds)
        {
            return;
        }

        try
        {
            ServiceProvider.GetRequiredService<IPreferences>().SetComplex(
                WindowPlacementKey,
                new WindowPlacement(bounds.X, bounds.Y, bounds.Width, bounds.Height, _isMaximized));
        }
        catch (Exception exception)
        {
            LogFailure("save the window placement", exception);
        }
    }

    private static RectInt32 ClampToWorkArea(RectInt32 bounds)
    {
        var workArea = DisplayArea.GetFromRect(bounds, DisplayAreaFallback.Nearest).WorkArea;
        var width = Math.Clamp(bounds.Width, 1, workArea.Width);
        var height = Math.Clamp(bounds.Height, 1, workArea.Height);

        return new RectInt32(
            Math.Clamp(bounds.X, workArea.X, workArea.X + workArea.Width - width),
            Math.Clamp(bounds.Y, workArea.Y, workArea.Y + workArea.Height - height),
            width,
            height);
    }

    private void OnAppWindowChanged(AppWindow sender, AppWindowChangedEventArgs args)
    {
        if (_isWindowClosed)
        {
            return;
        }

        TrackWindowPlacement();

        // Moving to a differently scaled monitor changes what the DIP minimums are in pixels.
        if (XamlRoot is { } xamlRoot && Math.Abs(xamlRoot.RasterizationScale - _minimumSizeScale) > 0.01)
        {
            ApplyMinimumWindowSize();
        }
    }

    /// <summary>Remembers the latest restored bounds — that, not the maximized rectangle, is what is persisted.</summary>
    private void TrackWindowPlacement()
    {
        try
        {
            if (_appWindow?.Presenter is not OverlappedPresenter presenter)
            {
                return;
            }

            _isMaximized = presenter.State == OverlappedPresenterState.Maximized;
            if (presenter.State == OverlappedPresenterState.Restored)
            {
                _restoreBounds = new RectInt32(
                    _appWindow.Position.X,
                    _appWindow.Position.Y,
                    _appWindow.Size.Width,
                    _appWindow.Size.Height);
            }
        }
        catch (Exception exception)
        {
            LogFailure("track the window placement", exception);
        }
    }

    private void ApplyMinimumWindowSize()
    {
        try
        {
            if (_appWindow?.Presenter is not OverlappedPresenter presenter)
            {
                return;
            }

            var scale = XamlRoot?.RasterizationScale ?? 1d;
            var workArea = DisplayArea.GetFromWindowId(_appWindow.Id, DisplayAreaFallback.Nearest).WorkArea;

            // Never demand more than the display can give — a small or heavily scaled screen wins.
            presenter.PreferredMinimumWidth = Math.Min((int)Math.Round(MinimumWindowWidth * scale), workArea.Width);
            presenter.PreferredMinimumHeight = Math.Min((int)Math.Round(MinimumWindowHeight * scale), workArea.Height);
            _minimumSizeScale = scale;
        }
        catch (Exception exception)
        {
            LogFailure("apply the minimum window size", exception);
        }
    }

    private void DetachWindowIntegration()
    {
        try
        {
            if (_appWindow is not null)
            {
                _appWindow.Changed -= OnAppWindowChanged;
                _appWindow = null;
            }

            if (HasCustomTitleBar)
            {
                ActualThemeChanged -= OnActualThemeChanged;
            }
        }
        catch (Exception exception)
        {
            LogFailure("detach the window event handlers", exception);
        }
    }

    /// <summary>Best-effort logging: window integration must never take the app down, nor fail silently.</summary>
    private void LogFailure(string operation, Exception exception)
    {
        try
        {
            ServiceProvider.GetService<ILogger<WindowShell>>()?.LogWarning(exception, "Failed to {Operation}.", operation);
        }
        catch (Exception loggingFailure)
        {
            Trace.WriteLine($"Failed to {operation}: {exception}. Logging that failure also failed: {loggingFailure}");
        }
    }

    #endregion
#endif

    #region Navigation

    private void NavView_Loaded(object sender, RoutedEventArgs e)
        => NavigateToSection(NavigationSection.Home);

    private void NavView_SelectionChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args)
    {
        // Ignore programmatic selection sync (UpdateNavigationViewSelection) so it cannot re-trigger a
        // navigation that would override a category-scoped catalog.
        if (_suppressSelectionChanged || args.SelectedItemContainer is not NavigationViewItem item)
        {
            return;
        }

        var nav = ServiceProvider.GetRequiredService<INavigationService>();
        switch (item.Tag)
        {
            // A discovered tool node carries its ViewModel type; open it through the shared host.
            case Type toolViewModelType:
                nav.Navigate(toolViewModelType);
                return;
            // A category node opens its scoped Catalog (chevron-expand is handled separately by NavigationView).
            case CategoryNavTag category:
                nav.Navigate<CatalogViewModel>(category.CategoryId);
                return;
        }

        var tag = item.Tag?.ToString();
        if (tag is null && item == (NavigationViewItem)sender.SettingsItem)
        {
            tag = NavigationSection.Settings.ToString();
        }

        if (Enum.TryParse<NavigationSection>(tag, out var section))
        {
            NavigateToSection(section);
        }
    }

    /// <summary>
    /// Renders the generated <see cref="GeneratedToolCatalog.NavigationTree"/> into the pane: groups as
    /// non-selectable headers, categories as expandable-and-clickable items holding their tools, and
    /// tools as selectable leaves. Empty categories/groups are already omitted by the tree builder.
    /// </summary>
    private void BuildToolNavigation()
    {
        foreach (var group in GeneratedToolCatalog.NavigationTree.Roots)
        {
            if (group.GroupId is not null && group.NameKey is not null)
            {
                NavView.MenuItems.Add(new NavigationViewItemHeader { Content = Localizer.Instance.GetString(group.NameKey) });
            }

            foreach (var category in group.Categories)
            {
                NavView.MenuItems.Add(BuildCategoryItem(category));
            }
        }
    }

    private static NavigationViewItem BuildCategoryItem(NavCategoryNode category)
    {
        var item = new NavigationViewItem
        {
            Content = Localizer.Instance.GetString(category.NameKey),
            Tag = new CategoryNavTag(category.CategoryId),
            // Clicking the body navigates; the chevron expand/collapse is independent (research R11).
            SelectsOnInvoked = true,
            Icon = new ImageIcon { Source = ToolIcons.For(category.IconKey, ToolIconKind.Category) },
        };

        foreach (var tool in category.Tools)
        {
            var toolItem = new NavigationViewItem
            {
                Content = Localizer.Instance.GetString(tool.NameKey),
                Tag = tool.ViewModelType,
                Icon = new ImageIcon { Source = ToolIcons.For(tool.IconKey, ToolIconKind.Tool) },
            };

            ToolTipService.SetToolTip(toolItem, Localizer.Instance.GetString(tool.TooltipKey));
            item.MenuItems.Add(toolItem);
        }

        return item;
    }

    private sealed record CategoryNavTag(string CategoryId);

    private void NavigateToSection(NavigationSection section)
    {
        var nav = ServiceProvider.GetRequiredService<INavigationService>();
        switch (section)
        {
            case NavigationSection.Home:
                nav.Navigate<HomeViewModel>();
                break;
            case NavigationSection.Catalog:
                nav.Navigate<CatalogViewModel>();
                break;
            case NavigationSection.Settings:
                nav.Navigate<SettingsViewModel>();
                break;
            default:
                throw new NotSupportedException($"Navigation section not supported: {section}");
        }
    }

    private void NavView_BackRequested(NavigationView sender, NavigationViewBackRequestedEventArgs args)
    {
        var nav = ServiceProvider.GetRequiredService<INavigationService>();
        if (nav.GoBack())
        {
            UpdateNavigationViewSelection();
        }
    }

    private void UpdateNavigationViewSelection()
    {
        var section = ServiceProvider.GetRequiredService<INavigationService>().CurrentSection;

        // Assigning SelectedItem re-raises SelectionChanged; suppress it so this programmatic sync does
        // not start another navigation.
        _suppressSelectionChanged = true;
        try
        {
            NavView.SelectedItem = section switch
            {
                NavigationSection.Home => HomeNavItem,
                // Keep a clicked category node highlighted for its scoped catalog; otherwise select the
                // top-level Catalog item for the unscoped catalog.
                NavigationSection.Catalog => NavView.SelectedItem is NavigationViewItem { Tag: CategoryNavTag }
                    ? NavView.SelectedItem
                    : CatalogNavItem,
                NavigationSection.Settings => NavView.SettingsItem,
                // Tool host has no menu item — keep the current selection.
                _ => NavView.SelectedItem,
            };
        }
        finally
        {
            _suppressSelectionChanged = false;
        }
    }

    #endregion
}
