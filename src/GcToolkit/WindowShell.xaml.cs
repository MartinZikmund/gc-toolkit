using GcToolkit.Core.Catalog;
using GcToolkit.Core.Discovery;
using GcToolkit.Core.Navigation;
using GcToolkit.Core.Services;
using GcToolkit.Core.Services.Settings;
using GcToolkit.Core.Services.Theming;
using GcToolkit.Core.ViewModels;
using GcToolkit.Helpers;
using GcToolkit.Infrastructure;
using GcToolkit.Services.Localization;
using GcToolkit.Services.Navigation;
using Microsoft.UI.Composition.SystemBackdrops;
using Microsoft.UI.Windowing;
using Windows.Foundation.Metadata;

namespace GcToolkit;

public sealed partial class WindowShell : Page, IWindowShell
{
    // Field is a task, not a tool bucket: it is lifted out of the Tools subtree into its own
    // top-level section, without touching the enum or the generated catalog.
    private const string PromotedCategoryId = nameof(ToolCategory.Field);
    private const string PromotedCategoryNameKey = "Field";

    private readonly IServiceScope _windowScope;
    private readonly Window _associatedWindow;
    private bool _isWindowClosed;
    private ViewModelBase? _currentPageViewModel;
    private bool _suppressSelectionChanged;

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
        CustomizeWindow();

        ViewModel = ServiceProvider.GetRequiredService<WindowShellViewModel>();
        ViewModel.PropertyChanged += ViewModel_PropertyChanged;

        Search = ServiceProvider.GetRequiredService<SearchViewModel>();

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
    }

    public void SetTitleBar(UIElement? titleBar)
    {
#if !HAS_UNO
        // The WinUI TitleBar control (AppTitleBar) is the Windows custom title bar; other heads use
        // the system title bar, so SetTitleBar is a no-op there.
        if (!_isWindowClosed)
        {
            _associatedWindow.SetTitleBar(titleBar ?? AppTitleBar);
        }
#endif
    }

    private void CustomizeWindow()
    {
#if !HAS_UNO
        if (AppWindowTitleBar.IsCustomizationSupported())
        {
            _associatedWindow.ExtendsContentIntoTitleBar = true;
            _associatedWindow.AppWindow.TitleBar.PreferredHeightOption = TitleBarHeightOption.Tall;
            _associatedWindow.SetTitleBar(AppTitleBar);
            HasCustomTitleBar = true;
        }
#endif

        if (MicaController.IsSupported())
        {
            _associatedWindow.SystemBackdrop = new MicaBackdrop();
            Background = null;
        }
    }

    #region Navigation

    private void NavView_Loaded(object sender, RoutedEventArgs e)
    {
        NavigateToSection(NavigationSection.Home);
#if DEBUG
        _ = Infrastructure.DebugSampleCache.OpenIfRequestedAsync(ServiceProvider);
#endif
    }

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

        // Home / Tools / Settings carry their section name as a plain string tag.
        if (Enum.TryParse<NavigationSection>(item.Tag?.ToString(), out var section))
        {
            NavigateToSection(section);
        }
    }

    /// <summary>
    /// Renders <see cref="ICatalogService.GetNavigationTree"/> into the pane: categories
    /// nest under the Tools section as expandable-and-clickable items holding their tools, and tools are
    /// selectable leaves. The <see cref="PromotedCategoryId"/> category is lifted to a top-level task
    /// section instead. Empty categories/groups are already omitted by the tree builder.
    /// </summary>
    private void BuildToolNavigation()
    {
        // The catalog's tree, not the generated one: the generated tree lists every tool that
        // compiled, including those this device has no hardware for.
        var catalog = ServiceProvider.GetRequiredService<ICatalogService>();

        foreach (var group in catalog.GetNavigationTree().Roots)
        {
            if (group.GroupId is not null && group.NameKey is not null)
            {
                ToolsNavItem.MenuItems.Add(new NavigationViewItemHeader { Content = Localizer.Instance.GetString(group.NameKey) });
            }

            foreach (var category in group.Categories)
            {
                if (string.Equals(category.CategoryId, PromotedCategoryId, StringComparison.Ordinal))
                {
                    // Appended after the XAML-declared items, so it lands directly below Tools.
                    NavView.MenuItems.Add(BuildCategoryItem(
                        category,
                        PromotedCategoryNameKey,
                        new FontIcon { Glyph = "\uE707" }));
                    continue;
                }

                ToolsNavItem.MenuItems.Add(BuildCategoryItem(category));
            }
        }
    }

    private static NavigationViewItem BuildCategoryItem(
        NavCategoryNode category,
        string? nameKeyOverride = null,
        IconElement? iconOverride = null)
    {
        var item = new NavigationViewItem
        {
            Content = Localizer.Instance.GetString(nameKeyOverride ?? category.NameKey),
            Tag = new CategoryNavTag(category.CategoryId),
            // Clicking the body navigates; the chevron expand/collapse is independent (research R11).
            SelectsOnInvoked = true,
            Icon = iconOverride ?? ToolIcons.NavIcon(category.IconKey),
        };

        foreach (var tool in category.Tools)
        {
            var toolItem = new NavigationViewItem
            {
                Content = Localizer.Instance.GetString(tool.NameKey),
                Tag = tool.ViewModelType,
                Icon = ToolIcons.NavIcon(tool.IconKey),
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
            case NavigationSection.Caches:
                nav.Navigate<CachesViewModel>();
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
                NavigationSection.Caches => CachesNavItem,
                // Keep a clicked category node highlighted for its scoped catalog — nested under Tools or
                // promoted to top level, the container itself is the selection either way. Otherwise fall
                // back to Tools, which owns the unscoped catalog.
                NavigationSection.Catalog => NavView.SelectedItem is NavigationViewItem { Tag: CategoryNavTag }
                    ? NavView.SelectedItem
                    : ToolsNavItem,
                NavigationSection.Settings => SettingsNavItem,
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
