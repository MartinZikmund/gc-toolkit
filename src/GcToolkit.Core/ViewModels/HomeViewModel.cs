using System.Collections.ObjectModel;
using GcToolkit.Core.Catalog;
using GcToolkit.Core.Discovery;
using GcToolkit.Core.FavoriteTools;
using GcToolkit.Core.Recents;
using GcToolkit.Core.Services;
using Microsoft.Extensions.Localization;

namespace GcToolkit.Core.ViewModels;

/// <summary>
/// Landing screen: a search entry into the catalog plus the user's favorite tools and recently
/// used tools. Stays in sync via the favorite-tools and recents change events.
/// </summary>
public partial class HomeViewModel : ViewModelBase
{
    private readonly IFavoriteToolsService _favorites;
    private readonly IRecentsService _recents;
    private readonly ICatalogService _catalog;
    private readonly INavigationService _navigation;
    private readonly IStringLocalizer _localizer;
    private bool _subscribed;

    public HomeViewModel(
        IFavoriteToolsService favorites,
        IRecentsService recents,
        ICatalogService catalog,
        INavigationService navigation,
        IStringLocalizer localizer,
        SearchViewModel search,
        ContinueSolvingViewModel continueSolving)
    {
        _favorites = favorites;
        _recents = recents;
        _catalog = catalog;
        _navigation = navigation;
        _localizer = localizer;
        Search = search;
        ContinueSolving = continueSolving;
        PageTitle = _localizer["ApplicationName"];
    }

    public SearchViewModel Search { get; }

    /// <summary>Caches with work in progress, shown first on Home.</summary>
    public ContinueSolvingViewModel ContinueSolving { get; }

    public ObservableCollection<ToolListItem> FavoriteTools { get; } = new();

    public ObservableCollection<ToolListItem> RecentTools { get; } = new();

    public ObservableCollection<ToolListItem> NewAndUpdatedTools { get; } = new();

    [ObservableProperty]
    public partial bool HasFavoriteTools { get; set; }

    [ObservableProperty]
    public partial bool HasRecentTools { get; set; }

    [ObservableProperty]
    public partial bool HasNewAndUpdatedTools { get; set; }

    public override void ViewCreated()
    {
        if (!_subscribed)
        {
            _favorites.FavoriteToolsChanged += OnDataChanged;
            _recents.RecentsChanged += OnDataChanged;
            _subscribed = true;
        }

        Refresh();
        ContinueSolving.ViewCreated();
    }

    public override void ViewLoaded()
    {
        base.ViewLoaded();
        ContinueSolving.ViewLoaded();
    }

    public override void ViewUnloaded()
    {
        base.ViewUnloaded();
        ContinueSolving.ViewUnloaded();
    }

    public override void OnNavigatedTo(object? parameter)
    {
        base.OnNavigatedTo(parameter);
        Refresh();
    }

    [RelayCommand]
    private void GoToCatalog() => _navigation.Navigate<CatalogViewModel>();

    private void OnDataChanged(object? sender, EventArgs e) => Refresh();

    private void Refresh()
    {
        Populate(FavoriteTools, _favorites.GetFavoriteToolIds());
        Populate(RecentTools, _recents.GetRecentToolIds());
        PopulateNewAndUpdated();
        HasFavoriteTools = FavoriteTools.Count > 0;
        HasRecentTools = RecentTools.Count > 0;
        HasNewAndUpdatedTools = NewAndUpdatedTools.Count > 0;
    }

    private void PopulateNewAndUpdated()
    {
        NewAndUpdatedTools.Clear();
        var today = DateOnly.FromDateTime(DateTime.Now);
        foreach (var tool in _catalog.GetTools())
        {
            if (ToolRecencyClassifier.Classify(tool.IntroducedDate, tool.UpdatedDate, today) != ToolRecency.None)
            {
                NewAndUpdatedTools.Add(CreateItem(tool));
            }
        }
    }

    private void Populate(ObservableCollection<ToolListItem> target, IReadOnlyList<string> toolIds)
    {
        target.Clear();
        foreach (var id in toolIds)
        {
            var tool = _catalog.GetTools().FirstOrDefault(t => string.Equals(t.Id, id, StringComparison.Ordinal));
            if (tool is not null)
            {
                target.Add(CreateItem(tool));
            }
        }
    }

    private ToolListItem CreateItem(ToolDescriptor tool)
    {
        ToolListItem item = new(
            tool.Id,
            _localizer[tool.NameKey].Value,
            tool.TooltipKey is null ? null : _localizer[tool.TooltipKey].Value,
            tool.IconKey,
            _favorites.IsFavorite(tool.Id),
            _localizer,
            OpenTool,
            ToggleFavorite);
        item.BadgeString = RecencyBadge(tool);
        return item;
    }

    private string RecencyBadge(ToolDescriptor tool)
        => ToolRecencyClassifier.Classify(tool.IntroducedDate, tool.UpdatedDate, DateOnly.FromDateTime(DateTime.Now)) switch
        {
            ToolRecency.New => _localizer["Badge_New"].Value,
            ToolRecency.Updated => _localizer["Badge_Updated"].Value,
            _ => string.Empty,
        };

    private void OpenTool(string toolId)
    {
        var tool = _catalog.GetTools().FirstOrDefault(t => string.Equals(t.Id, toolId, StringComparison.Ordinal));
        if (tool is not null)
        {
            _navigation.Navigate(tool.ViewModelType);
        }
    }

    private void ToggleFavorite(string toolId) => _ = _favorites.ToggleAsync(toolId);
}
