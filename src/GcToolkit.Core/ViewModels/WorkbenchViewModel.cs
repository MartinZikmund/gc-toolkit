using System.Collections.ObjectModel;
using System.Collections.Specialized;
using CommunityToolkit.Mvvm.Messaging;
using GcToolkit.Core.Catalog;
using GcToolkit.Core.FavoriteTools;
using GcToolkit.Core.Geocaches;
using GcToolkit.Core.Messages;
using GcToolkit.Core.Recents;
using GcToolkit.Core.ViewModels.Tools;
using GcToolkit.Core.Workbench;
using MZikmund.Toolkit.WinUI.Services;

namespace GcToolkit.Core.ViewModels;

/// <summary>
/// The workbench: open caches as tabs, a listing, a tool pane with tool tabs and a docked
/// Variables/Scratchpad panel. Owns which layout mode applies and what is open; the view only
/// reports its width and maps these properties onto visual states.
/// </summary>
public partial class WorkbenchViewModel : ViewModelBase
{
    // Derived layout properties; only the ones whose value changed are raised, so x:Bind functions
    // (e.g. row heights) are not re-applied over what a splitter set.
    private static readonly (string Name, Func<WorkbenchViewModel, object> Get)[] _layoutProperties =
    [
        (nameof(IsCompact), vm => vm.IsCompact),
        (nameof(IsMedium), vm => vm.IsMedium),
        (nameof(IsWide), vm => vm.IsWide),
        (nameof(IsToolPaneOpen), vm => vm.IsToolPaneOpen),
        (nameof(IsToolPaneInline), vm => vm.IsToolPaneInline),
        (nameof(PaneLength), vm => vm.PaneLength),
        (nameof(IsListingVisible), vm => vm.IsListingVisible),
        (nameof(IsToolTabsVisible), vm => vm.IsToolTabsVisible),
        (nameof(IsDockVisible), vm => vm.IsDockVisible),
        (nameof(IsDockSelectorVisible), vm => vm.IsDockSelectorVisible),
        (nameof(IsVariablesVisible), vm => vm.IsVariablesVisible),
        (nameof(IsScratchpadVisible), vm => vm.IsScratchpadVisible),
        (nameof(IsPhoneListingSelected), vm => vm.IsPhoneListingSelected),
        (nameof(IsPhoneToolSelected), vm => vm.IsPhoneToolSelected),
        (nameof(IsPhoneVariablesSelected), vm => vm.IsPhoneVariablesSelected),
        (nameof(IsPhoneNotesSelected), vm => vm.IsPhoneNotesSelected),
        (nameof(IsDockVariablesSelected), vm => vm.IsDockVariablesSelected),
        (nameof(IsDockScratchpadSelected), vm => vm.IsDockScratchpadSelected),
    ];

    private const string ToolPaneWidthKey = "workbench.layout.toolPaneWidth";
    private const string DockHeightKey = "workbench.layout.dockHeight";
    private const string PinnedKey = "workbench.layout.pinned";
    private const string PastedListingKeyPrefix = "workbench.pasted.";

    private object[] _layoutSnapshot = [];

    private readonly IWorkbenchSessionService _session;
    private readonly ICatalogService _catalog;
    private readonly IStringLocalizer _localizer;
    private readonly IWorkbenchToolStore _toolStore;
    private readonly IFavoriteToolsService _favorites;
    private readonly IRecentsService _recents;
    private readonly IPreferences _preferences;
    private readonly IWorkbenchProgressRecorder _progress;
    private readonly IGeocacheStore _store;
    private int _suggestionVersion;
    private readonly Dictionary<GeocacheCode, WorkbenchCacheTab> _tabs = [];
    private double _availableWidth;
    private double _requestedToolPaneWidth = WorkbenchLayout.DefaultToolPaneWidth;
    private bool _isOverlayOpen;
    private bool _syncing;
    private GeocacheCode? _loadedCode;
    private string _pastedListing = string.Empty;
    private WorkbenchCacheTab? _watchedCache;
    private WorkbenchToolTab? _resultTool;
    private (string Name, string Previous)? _lastSend;
    private GeocacheCode? _scratchpadCode;
    private ScratchpadSaveState _scratchpadState;

    public WorkbenchViewModel(
        IWorkbenchSessionService session,
        ICatalogService catalog,
        IStringLocalizer localizer,
        IWorkbenchToolStore toolStore,
        IFavoriteToolsService favorites,
        IRecentsService recents,
        ScratchpadViewModel scratchpad,
        GeocacheDetailViewModel listing,
        VariableCoordinateCalculatorViewModel variables,
        IWorkbenchProgressRecorder progress,
        IGeocacheStore store,
        IPreferences preferences,
        IMessenger? messenger = null)
    {
        _session = session;
        _catalog = catalog;
        _localizer = localizer;
        _toolStore = toolStore;
        _favorites = favorites;
        _recents = recents;
        Scratchpad = scratchpad;
        Listing = listing;
        Listing.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName is nameof(GeocacheDetailViewModel.IsLoaded) or nameof(GeocacheDetailViewModel.HasDescription))
            {
                OnPropertyChanged(nameof(ShowPasteBox));
            }
        };
        Variables = variables;
        Variables.Edited += (_, _) => OnVariablesEdited();
        _progress = progress;
        _store = store;
        Scratchpad.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(ScratchpadViewModel.SaveState))
            {
                OnScratchpadSaveStateChanged();
            }
        };
        _preferences = preferences;
        RestoreLayout();

        OpenCaches.CollectionChanged += OnOpenCachesChanged;
        (messenger ?? WeakReferenceMessenger.Default)
            .Register<WorkbenchViewModel, WorkbenchSessionChangedMessage>(this, (vm, _) => vm.SyncFromSession());

        RefreshToolPicker();
        _layoutSnapshot = [.. _layoutProperties.Select(p => p.Get(this))];
    }

    public ScratchpadViewModel Scratchpad { get; }

    /// <summary>The active cache's listing, hint, waypoints and logs; reloaded when the active cache changes.</summary>
    public GeocacheDetailViewModel Listing { get; }

    /// <summary>The active cache's variable sheet: its coordinate templates, variables and solved finals.</summary>
    public VariableCoordinateCalculatorViewModel Variables { get; }

    // Caches

    public ObservableCollection<WorkbenchCacheTab> OpenCaches { get; } = [];

    public bool HasOpenCaches => OpenCaches.Count > 0;

    [ObservableProperty]
    public partial WorkbenchCacheTab? ActiveCache { get; set; }

    /// <summary>Stored caches whose name or code contains <see cref="NewCacheCode"/>.</summary>
    public ObservableCollection<CacheSuggestion> CacheSuggestions { get; } = [];

    /// <summary>Text typed into the "open cache" box: part of a stored cache's name, or any GC code.</summary>
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(OpenCacheCommand))]
    public partial string NewCacheCode { get; set; } = string.Empty;

    // Layout

    [ObservableProperty]
    public partial WorkbenchLayoutMode LayoutMode { get; private set; } = WorkbenchLayoutMode.Wide;

    [ObservableProperty]
    public partial WorkbenchPhoneSection PhoneSection { get; set; }

    [ObservableProperty]
    public partial WorkbenchDockSection DockSection { get; set; }

    [ObservableProperty]
    public partial bool IsToolPanePinned { get; private set; }

    public bool IsCompact => LayoutMode == WorkbenchLayoutMode.Compact;

    public bool IsMedium => LayoutMode == WorkbenchLayoutMode.Medium;

    public bool IsWide => LayoutMode == WorkbenchLayoutMode.Wide;

    /// <summary>Always open when wide; toggled (or light-dismissed) when medium; follows the section on phones.</summary>
    public bool IsToolPaneOpen
    {
        get => LayoutMode switch
        {
            WorkbenchLayoutMode.Wide => true,
            WorkbenchLayoutMode.Medium => _isOverlayOpen,
            _ => PhoneSection != WorkbenchPhoneSection.Listing,
        };
        set
        {
            if (IsMedium && _isOverlayOpen != value)
            {
                _isOverlayOpen = value;
                RaiseLayoutChanged();
            }
            else if (value != IsToolPaneOpen)
            {
                // Rejected (e.g. a close while wide): tell the binding to read the real value back.
                OnPropertyChanged();
            }
        }
    }

    /// <summary>Inline pushes the listing aside; otherwise the pane overlays it.</summary>
    public bool IsToolPaneInline => !IsMedium || IsToolPanePinned;

    /// <summary>Width of the tool pane as the user dragged it, kept within what the window allows.</summary>
    public double ToolPaneWidth
    {
        get => WorkbenchLayout.ClampToolPaneWidth(_requestedToolPaneWidth, _availableWidth);
        set
        {
            var previous = ToolPaneWidth;
            _requestedToolPaneWidth = WorkbenchLayout.ClampToolPaneWidth(value, _availableWidth);
            _preferences.Set(ToolPaneWidthKey, _requestedToolPaneWidth);
            if (previous != ToolPaneWidth)
            {
                OnPropertyChanged();
                RaiseLayoutChanged();
            }
        }
    }

    /// <summary>Height of the Variables/Scratchpad panel under the tools, as the user dragged it.</summary>
    public double DockHeight
    {
        get;
        set
        {
            if (SetProperty(ref field, Math.Max(WorkbenchLayout.MinDockHeight, value)))
            {
                _preferences.Set(DockHeightKey, field);
            }
        }
    } = WorkbenchLayout.DefaultDockHeight;

    public double PaneLength => LayoutMode switch
    {
        WorkbenchLayoutMode.Wide => ToolPaneWidth,
        WorkbenchLayoutMode.Medium => WorkbenchLayout.OverlayPaneWidth,
        _ => _availableWidth,
    };

    public bool IsListingVisible => !IsCompact || PhoneSection == WorkbenchPhoneSection.Listing;

    public bool IsToolTabsVisible => !IsCompact || PhoneSection == WorkbenchPhoneSection.Tool;

    public bool IsDockVisible => !IsCompact || PhoneSection is WorkbenchPhoneSection.Variables or WorkbenchPhoneSection.Notes;

    /// <summary>Phones have no Variables·Scratchpad switch: each is its own section there.</summary>
    public bool IsDockSelectorVisible => !IsCompact;

    public bool IsVariablesVisible => IsCompact
        ? PhoneSection == WorkbenchPhoneSection.Variables
        : DockSection == WorkbenchDockSection.Variables;

    public bool IsScratchpadVisible => IsCompact
        ? PhoneSection == WorkbenchPhoneSection.Notes
        : DockSection == WorkbenchDockSection.Scratchpad;

    public bool IsPhoneListingSelected => PhoneSection == WorkbenchPhoneSection.Listing;

    public bool IsPhoneToolSelected => PhoneSection == WorkbenchPhoneSection.Tool;

    public bool IsPhoneVariablesSelected => PhoneSection == WorkbenchPhoneSection.Variables;

    public bool IsPhoneNotesSelected => PhoneSection == WorkbenchPhoneSection.Notes;

    public bool IsDockVariablesSelected => DockSection == WorkbenchDockSection.Variables;

    public bool IsDockScratchpadSelected => DockSection == WorkbenchDockSection.Scratchpad;

    // Tools

    [ObservableProperty]
    public partial string ToolSearchQuery { get; set; } = string.Empty;

    public ObservableCollection<ToolPickerItem> ToolPickerResults { get; } = [];

    // Selection → tool

    /// <summary>Text the user selected in the listing (or the pasted listing); set by the view.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasSelection))]
    [NotifyCanExecuteChangedFor(nameof(AddSelectionToScratchpadCommand))]
    public partial string SelectedText { get; set; } = string.Empty;

    public bool HasSelection => !string.IsNullOrWhiteSpace(SelectedText);

    /// <summary>Tools that fit the selection, best first; the first is offered as the suggested one.</summary>
    public ObservableCollection<ToolPickerItem> SelectionSuggestions { get; } = [];

    public ToolPickerItem? SuggestedTool => SelectionSuggestions.FirstOrDefault();

    /// <summary>The tool picker is open to choose where the selection goes, not to add an empty tool.</summary>
    public bool IsPickingForSelection { get; private set; }

    /// <summary>The cache was imported without a description, so the workbench offers a box to paste it into.</summary>
    public bool ShowPasteBox => Listing.IsLoaded && !Listing.HasDescription;

    /// <summary>Listing text the user pasted for a cache without a description; kept per cache.</summary>
    public string PastedListing
    {
        get => _pastedListing;
        set
        {
            if (SetProperty(ref _pastedListing, value) && _loadedCode is { } code)
            {
                _preferences.Set(PastedListingKeyPrefix + code.Value, value);
            }
        }
    }

    // Tool result → variables

    /// <summary>The selected tool has a text result that can go to the variable sheet.</summary>
    public bool HasToolResult => !string.IsNullOrWhiteSpace(_resultTool?.Output);

    /// <summary>The letter the result goes to unless the user picks another: the next empty one.</summary>
    [ObservableProperty]
    public partial string? ResultTarget { get; private set; }

    public IReadOnlyList<string> VariableChoices => Variables.VariableChoices;

    /// <summary>What the last "To variables" did, e.g. "B = 42"; empty once undone or after switching caches.</summary>
    [ObservableProperty]
    public partial string LastSentToVariables { get; private set; } = string.Empty;

    [RelayCommand(CanExecute = nameof(HasToolResult))]
    private void SendResultToVariables(string? name)
    {
        var target = string.IsNullOrWhiteSpace(name) ? ResultTarget : name.Trim().ToUpperInvariant();
        if (target is null || _resultTool?.Output is not { } output)
        {
            return;
        }

        var value = output.Trim();
        var previous = Variables.SetVariable(target, value);
        _lastSend = (target, previous);
        LastSentToVariables = $"{target} = {value}";
        UndoSendToVariablesCommand.NotifyCanExecuteChanged();
    }

    [RelayCommand(CanExecute = nameof(CanUndoSendToVariables))]
    private void UndoSendToVariables()
    {
        if (_lastSend is not { } last)
        {
            return;
        }

        ForgetLastSend();
        Variables.SetVariable(last.Name, last.Previous);
    }

    private bool CanUndoSendToVariables() => _lastSend is not null;

    private void ForgetLastSend()
    {
        _lastSend = null;
        LastSentToVariables = string.Empty;
        UndoSendToVariablesCommand.NotifyCanExecuteChanged();
    }

    private void OnVariablesEdited()
    {
        RefreshResultTarget();
        if (_loadedCode is { } code)
        {
            _progress.RecordWork(code, Variables.Variables.Count(v => !v.IsMissing), Variables.Variables.Count);
        }
    }

    // A finished save is the scratchpad's real edit; loading a cache's text is not.
    private void OnScratchpadSaveStateChanged()
    {
        var previous = _scratchpadState;
        _scratchpadState = Scratchpad.SaveState;
        if (previous == ScratchpadSaveState.Saving && _scratchpadState != ScratchpadSaveState.Saving && _scratchpadCode is { } code)
        {
            _progress.RecordWork(code, scratchpadLines: CountLines(Scratchpad.Text));
        }
    }

    private static int CountLines(string text)
        => text.ReplaceLineEndings("\n").Split('\n').Count(line => !string.IsNullOrWhiteSpace(line));

    private void RefreshResultTarget()
    {
        ResultTarget = Variables.NextEmptyVariable;
        OnPropertyChanged(nameof(VariableChoices));
    }

    private void WatchActiveCache()
    {
        if (ReferenceEquals(_watchedCache, ActiveCache))
        {
            return;
        }

        if (_watchedCache is not null)
        {
            _watchedCache.PropertyChanged -= OnWatchedCacheChanged;
        }

        _watchedCache = ActiveCache;
        if (_watchedCache is not null)
        {
            _watchedCache.PropertyChanged += OnWatchedCacheChanged;
        }

        WatchSelectedTool();
    }

    private void OnWatchedCacheChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(WorkbenchCacheTab.SelectedTool))
        {
            WatchSelectedTool();
        }
    }

    private void WatchSelectedTool()
    {
        var tool = _watchedCache?.SelectedTool;
        if (!ReferenceEquals(tool, _resultTool))
        {
            if (_resultTool is not null)
            {
                _resultTool.OutputChanged -= OnToolResultChanged;
            }

            _resultTool = tool;
            if (_resultTool is not null)
            {
                _resultTool.OutputChanged += OnToolResultChanged;
            }
        }

        OnToolResultChanged(this, EventArgs.Empty);
    }

    private void OnToolResultChanged(object? sender, EventArgs e)
    {
        OnPropertyChanged(nameof(HasToolResult));
        SendResultToVariablesCommand.NotifyCanExecuteChanged();
    }

    /// <summary>Called by the view whenever its width changes.</summary>
    public void UpdateAvailableWidth(double width)
    {
        if (width == _availableWidth)
        {
            return;
        }

        _availableWidth = width;
        LayoutMode = WorkbenchLayout.ModeFor(width);
        OnPropertyChanged(nameof(ToolPaneWidth));
        RaiseLayoutChanged();
    }

    public void AddTool(ToolPickerItem item)
    {
        if (ActiveCache is not { } cache)
        {
            return;
        }

        WorkbenchToolTab tab = new(item.ToolId, item.Name, item.IconKey, item.ViewModelType);
        cache.Tools.Add(tab);
        cache.SelectedTool = tab;
        ToolSearchQuery = string.Empty;
        RevealTools();
    }

    /// <summary>Sends the selection to <paramref name="item"/>, reusing the cache's tab for that tool if it has one.</summary>
    public void SendSelectionTo(ToolPickerItem item)
    {
        if (ActiveCache is not { } cache || !HasSelection)
        {
            return;
        }

        if (cache.Tools.FirstOrDefault(t => t.ToolId == item.ToolId) is { } open)
        {
            cache.SelectedTool = open;
            RevealTools();
        }
        else
        {
            AddTool(item);
        }

        cache.SelectedTool?.SendText(SelectedText.Trim());
    }

    [RelayCommand(CanExecute = nameof(HasSelection))]
    private void AddSelectionToScratchpad() => Scratchpad.Append(SelectedText.Trim());

    [RelayCommand(CanExecute = nameof(CanSendToSuggested))]
    private void SendSelectionToSuggested()
    {
        if (SuggestedTool is { } tool)
        {
            SendSelectionTo(tool);
        }
    }

    private bool CanSendToSuggested() => HasSelection && SuggestedTool is not null;

    /// <summary>The next tool picked in the picker receives the selection.</summary>
    public void BeginSendToOtherTool() => IsPickingForSelection = HasSelection;

    /// <summary>Called when a tool is chosen in the picker.</summary>
    public void PickTool(ToolPickerItem item)
    {
        if (IsPickingForSelection)
        {
            IsPickingForSelection = false;
            SendSelectionTo(item);
        }
        else
        {
            AddTool(item);
        }
    }

    partial void OnSelectedTextChanged(string value)
    {
        IsPickingForSelection = false;
        SelectionSuggestions.Clear();
        var available = _catalog.GetTools()
            .Where(t => typeof(IToolTextInput).IsAssignableFrom(t.ViewModelType))
            .ToDictionary(t => t.Id);
        foreach (var id in ToolSuggester.Suggest(value))
        {
            if (available.TryGetValue(id, out var tool))
            {
                SelectionSuggestions.Add(new ToolPickerItem(tool.Id, _localizer[tool.NameKey].Value, tool.IconKey, tool.ViewModelType));
            }
        }

        OnPropertyChanged(nameof(SuggestedTool));
        SendSelectionToSuggestedCommand.NotifyCanExecuteChanged();
    }

    private void RevealTools()
    {
        if (IsCompact)
        {
            PhoneSection = WorkbenchPhoneSection.Tool;
        }
        else if (IsMedium)
        {
            IsToolPaneOpen = true;
        }
    }

    public void CloseTool(WorkbenchToolTab tool)
    {
        var cache = OpenCaches.FirstOrDefault(c => c.Tools.Contains(tool));
        if (cache is null)
        {
            return;
        }

        var index = cache.Tools.IndexOf(tool);
        var wasSelected = cache.SelectedTool == tool;
        tool.Detach();
        cache.Tools.RemoveAt(index);
        if (wasSelected)
        {
            cache.SelectedTool = cache.Tools.Count == 0 ? null : cache.Tools[Math.Min(index, cache.Tools.Count - 1)];
        }
    }

    public void CloseCache(WorkbenchCacheTab cache) => _session.Close(cache.Code);

    public override void ViewCreated()
    {
        base.ViewCreated();
        SyncFromSession();
    }

    public override void OnNavigatedTo(object? parameter)
    {
        base.OnNavigatedTo(parameter);
        if (parameter is GeocacheCode code)
        {
            _session.Open(code);
        }
    }

    public override void OnNavigatedFrom()
    {
        base.OnNavigatedFrom();
        Scratchpad.OnNavigatedFrom();
    }

    [RelayCommand(CanExecute = nameof(CanOpenCache))]
    private void OpenCache()
    {
        if (GeocacheCode.TryParse(NewCacheCode, out var code))
        {
            NewCacheCode = string.Empty;
            _session.Open(code);
        }
    }

    private bool CanOpenCache() => GeocacheCode.TryParse(NewCacheCode, out _);

    public void OpenSuggestedCache(CacheSuggestion suggestion)
    {
        NewCacheCode = string.Empty;
        _session.Open(suggestion.Code);
    }

    partial void OnNewCacheCodeChanged(string value) => _ = RefreshCacheSuggestionsAsync(value);

    private async Task RefreshCacheSuggestionsAsync(string text)
    {
        var version = ++_suggestionVersion;
        IReadOnlyList<GeocacheSummary> found = [];
        if (!string.IsNullOrWhiteSpace(text))
        {
            try
            {
                found = (await _store.QueryAsync(new() { Text = text.Trim(), Sort = GeocacheSort.Name, Take = 8 })).Items;
            }
            catch (Exception)
            {
                // No suggestions; a typed code still opens.
            }
        }

        // A slower, older query must not overwrite the newer one's results.
        if (version != _suggestionVersion)
        {
            return;
        }

        CacheSuggestions.Clear();
        foreach (var cache in found)
        {
            CacheSuggestions.Add(new CacheSuggestion(cache.Code, cache.Name));
        }
    }

    [RelayCommand]
    private void ToggleToolPane() => IsToolPaneOpen = !IsToolPaneOpen;

    /// <summary>
    /// F6 / Shift+F6: the pane focus should move to from <paramref name="from"/>. On phones it steps
    /// through the sections instead; on medium windows it opens the overlay when focus goes into it.
    /// </summary>
    public WorkbenchPane MoveFocus(WorkbenchPane from, bool backwards = false)
    {
        var step = backwards ? -1 : 1;
        if (IsCompact)
        {
            var count = Enum.GetValues<WorkbenchPhoneSection>().Length;
            PhoneSection = (WorkbenchPhoneSection)(((int)PhoneSection + step + count) % count);
            return PhoneSection switch
            {
                WorkbenchPhoneSection.Listing => WorkbenchPane.Listing,
                WorkbenchPhoneSection.Tool => WorkbenchPane.Tools,
                _ => WorkbenchPane.Dock,
            };
        }

        var panes = Enum.GetValues<WorkbenchPane>().Length;
        var target = (WorkbenchPane)(((int)from + step + panes) % panes);
        if (target != WorkbenchPane.Listing)
        {
            IsToolPaneOpen = true;
        }

        return target;
    }

    private void RestoreLayout()
    {
        _requestedToolPaneWidth = _preferences.Get(ToolPaneWidthKey, WorkbenchLayout.DefaultToolPaneWidth);
        DockHeight = _preferences.Get(DockHeightKey, WorkbenchLayout.DefaultDockHeight);
        IsToolPanePinned = _preferences.Get(PinnedKey, false);
        _isOverlayOpen = IsToolPanePinned;
    }

    [RelayCommand]
    private void TogglePin()
    {
        IsToolPanePinned = !IsToolPanePinned;
        _preferences.Set(PinnedKey, IsToolPanePinned);
        if (IsToolPanePinned)
        {
            _isOverlayOpen = true;
        }

        RaiseLayoutChanged();
    }

    partial void OnActiveCacheChanged(WorkbenchCacheTab? value)
    {
        if (_syncing)
        {
            return;
        }

        if (value is null)
        {
            // The tab strip clears its selection while it rebuilds; the session still has an active cache.
            if (_session.ActiveCache is { } active && _tabs.TryGetValue(active, out var activeTab))
            {
                ActiveCache = activeTab;
            }

            return;
        }

        _session.Activate(value.Code);
        LoadCache(value.Code);
    }

    partial void OnPhoneSectionChanged(WorkbenchPhoneSection value) => RaiseLayoutChanged();

    partial void OnDockSectionChanged(WorkbenchDockSection value) => RaiseLayoutChanged();

    partial void OnToolSearchQueryChanged(string value) => RefreshToolPicker();

    /// <summary>Called by the view as the picker opens, so favourites and recents are current.</summary>
    public void RefreshToolPicker()
    {
        IEnumerable<ToolDescriptor> tools = _catalog.Search(ToolSearchQuery);
        if (string.IsNullOrWhiteSpace(ToolSearchQuery))
        {
            // Favourites, then recents, then everything else in catalog order.
            var pinned = _favorites.GetFavoriteToolIds().Concat(_recents.GetRecentToolIds()).Distinct().ToList();
            tools = tools.OrderBy(t => pinned.IndexOf(t.Id) is var rank and >= 0 ? rank : int.MaxValue);
        }

        ToolPickerResults.Clear();
        foreach (var tool in tools)
        {
            ToolPickerResults.Add(new ToolPickerItem(tool.Id, _localizer[tool.NameKey].Value, tool.IconKey, tool.ViewModelType));
        }
    }

    private WorkbenchCacheTab CreateCacheTab(GeocacheCode code)
    {
        WorkbenchCacheTab cache = new(code);
        if (_toolStore.Load(code) is { } saved)
        {
            for (var i = 0; i < saved.Tools.Count; i++)
            {
                var tool = saved.Tools[i];
                if (_catalog.FindTool(tool.ToolId) is not { } descriptor)
                {
                    continue;
                }

                WorkbenchToolTab tab = new(
                    descriptor.Id,
                    _localizer[descriptor.NameKey].Value,
                    descriptor.IconKey,
                    descriptor.ViewModelType,
                    tool.Inputs is null ? null : new ToolInputState(tool.Inputs));
                cache.Tools.Add(tab);
                if (i <= saved.SelectedIndex || cache.SelectedTool is null)
                {
                    cache.SelectedTool = tab;
                }
            }
        }

        cache.ToolsChanged += (_, _) =>
        {
            SaveTools(cache);
            _progress.RecordWork(cache.Code, openTools: cache.Tools.Count);
        };
        return cache;
    }

    private void SaveTools(WorkbenchCacheTab cache)
        => _toolStore.Save(cache.Code, new WorkbenchToolsState(
            [.. cache.Tools.Select(t => new WorkbenchToolState(t.ToolId, t.State is { } state ? new(state.Values) : null))],
            cache.SelectedTool is { } selected ? cache.Tools.IndexOf(selected) : -1));

    private void SyncFromSession()
    {
        var open = _session.OpenCaches;
        _syncing = true;
        try
        {
            for (var i = OpenCaches.Count - 1; i >= 0; i--)
            {
                if (!open.Contains(OpenCaches[i].Code))
                {
                    _tabs.Remove(OpenCaches[i].Code);
                    OpenCaches.RemoveAt(i);
                }
            }

            for (var i = 0; i < open.Count; i++)
            {
                if (!_tabs.TryGetValue(open[i], out var tab))
                {
                    tab = CreateCacheTab(open[i]);
                    _tabs[open[i]] = tab;
                }

                var index = OpenCaches.IndexOf(tab);
                if (index < 0)
                {
                    OpenCaches.Insert(i, tab);
                }
                else if (index != i)
                {
                    OpenCaches.Move(index, i);
                }
            }

            ActiveCache = _session.ActiveCache is { } active ? _tabs[active] : null;
        }
        finally
        {
            _syncing = false;
        }

        OnPropertyChanged(nameof(HasOpenCaches));
        if (ActiveCache is { } current)
        {
            LoadCache(current.Code);
        }
        else
        {
            WatchActiveCache();
        }
    }

    private void OnOpenCachesChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        // A drag reorder in the tab strip arrives as a remove followed by an insert; the insert carries the new index.
        if (_syncing || e.Action != NotifyCollectionChangedAction.Add || e.NewItems?[0] is not WorkbenchCacheTab tab)
        {
            return;
        }

        _session.Move(tab.Code, e.NewStartingIndex);
    }

    private async void LoadCache(GeocacheCode code)
    {
        WatchActiveCache();
        if (_loadedCode == code)
        {
            return;
        }

        _loadedCode = code;
        ForgetLastSend();
        SelectedText = string.Empty;
        LoadPastedListing(code);
        Variables.ShowSheetOf(code);
        RefreshResultTarget();
        var listing = Listing.LoadAsync(code);
        try
        {
            await Scratchpad.InitializeAsync(code);
            _scratchpadCode = code;
        }
        catch (Exception)
        {
            // The scratchpad stays empty; the rest of the workbench keeps working.
        }

        try
        {
            await listing;
        }
        catch (Exception)
        {
            // An unreadable store leaves the listing empty, as on the cache page.
        }

        if (Listing.IsLoaded && Listing.Code == code.Value && _tabs.TryGetValue(code, out var tab))
        {
            tab.Name = Listing.Name;
            _progress.RecordDetails(code, Listing.Name, Listing.IsSolved, Listing.IsFound);
            if (string.IsNullOrWhiteSpace(Variables.PostedCoordinate))
            {
                Variables.PostedCoordinate = Listing.PostedCoordinate;
            }
        }
    }

    // Through the field, not the setter: reading the saved text back is not an edit to save.
    private void LoadPastedListing(GeocacheCode code)
        => SetProperty(ref _pastedListing, _preferences.Get(PastedListingKeyPrefix + code.Value, string.Empty), nameof(PastedListing));

    private void RaiseLayoutChanged()
    {
        var previous = _layoutSnapshot;
        _layoutSnapshot = [.. _layoutProperties.Select(p => p.Get(this))];
        for (var i = 0; i < _layoutProperties.Length; i++)
        {
            if (previous.Length == 0 || !Equals(previous[i], _layoutSnapshot[i]))
            {
                OnPropertyChanged(_layoutProperties[i].Name);
            }
        }
    }
}
