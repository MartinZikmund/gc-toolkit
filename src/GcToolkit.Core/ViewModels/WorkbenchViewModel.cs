using System.Collections.ObjectModel;
using System.Collections.Specialized;
using CommunityToolkit.Mvvm.Messaging;
using GcToolkit.Core.Catalog;
using GcToolkit.Core.Geocaches;
using GcToolkit.Core.Messages;
using GcToolkit.Core.Workbench;

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
        (nameof(IsPhoneScratchpadSelected), vm => vm.IsPhoneScratchpadSelected),
        (nameof(IsDockVariablesSelected), vm => vm.IsDockVariablesSelected),
        (nameof(IsDockScratchpadSelected), vm => vm.IsDockScratchpadSelected),
    ];

    private object[] _layoutSnapshot = [];

    private readonly IWorkbenchSessionService _session;
    private readonly ICatalogService _catalog;
    private readonly IStringLocalizer _localizer;
    private readonly Dictionary<GeocacheCode, WorkbenchCacheTab> _tabs = [];
    private double _availableWidth;
    private double _requestedToolPaneWidth = WorkbenchLayout.DefaultToolPaneWidth;
    private bool _isOverlayOpen;
    private bool _syncing;
    private GeocacheCode? _scratchpadCode;

    public WorkbenchViewModel(
        IWorkbenchSessionService session,
        ICatalogService catalog,
        IStringLocalizer localizer,
        ScratchpadViewModel scratchpad,
        IMessenger? messenger = null)
    {
        _session = session;
        _catalog = catalog;
        _localizer = localizer;
        Scratchpad = scratchpad;

        OpenCaches.CollectionChanged += OnOpenCachesChanged;
        (messenger ?? WeakReferenceMessenger.Default)
            .Register<WorkbenchViewModel, WorkbenchSessionChangedMessage>(this, (vm, _) => vm.SyncFromSession());

        RefreshToolPicker();
        _layoutSnapshot = [.. _layoutProperties.Select(p => p.Get(this))];
    }

    public ScratchpadViewModel Scratchpad { get; }

    // Caches

    public ObservableCollection<WorkbenchCacheTab> OpenCaches { get; } = [];

    public bool HasOpenCaches => OpenCaches.Count > 0;

    [ObservableProperty]
    public partial WorkbenchCacheTab? ActiveCache { get; set; }

    /// <summary>Typed cache code; a stand-in for a picker over the offline store (#408).</summary>
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
        set => SetProperty(ref field, Math.Max(WorkbenchLayout.MinDockHeight, value));
    } = WorkbenchLayout.DefaultDockHeight;

    public double PaneLength => LayoutMode switch
    {
        WorkbenchLayoutMode.Wide => ToolPaneWidth,
        WorkbenchLayoutMode.Medium => WorkbenchLayout.OverlayPaneWidth,
        _ => _availableWidth,
    };

    public bool IsListingVisible => !IsCompact || PhoneSection == WorkbenchPhoneSection.Listing;

    public bool IsToolTabsVisible => !IsCompact || PhoneSection == WorkbenchPhoneSection.Tool;

    public bool IsDockVisible => !IsCompact || PhoneSection == WorkbenchPhoneSection.Scratchpad;

    /// <summary>Phones have no Variables·Scratchpad switch: the scratchpad is its own section there.</summary>
    public bool IsDockSelectorVisible => !IsCompact;

    public bool IsVariablesVisible => !IsCompact && DockSection == WorkbenchDockSection.Variables;

    public bool IsScratchpadVisible => IsCompact
        ? PhoneSection == WorkbenchPhoneSection.Scratchpad
        : DockSection == WorkbenchDockSection.Scratchpad;

    public bool IsPhoneListingSelected => PhoneSection == WorkbenchPhoneSection.Listing;

    public bool IsPhoneToolSelected => PhoneSection == WorkbenchPhoneSection.Tool;

    public bool IsPhoneScratchpadSelected => PhoneSection == WorkbenchPhoneSection.Scratchpad;

    public bool IsDockVariablesSelected => DockSection == WorkbenchDockSection.Variables;

    public bool IsDockScratchpadSelected => DockSection == WorkbenchDockSection.Scratchpad;

    // Tools

    [ObservableProperty]
    public partial string ToolSearchQuery { get; set; } = string.Empty;

    public ObservableCollection<ToolPickerItem> ToolPickerResults { get; } = [];

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

    [RelayCommand]
    private void ToggleToolPane() => IsToolPaneOpen = !IsToolPaneOpen;

    [RelayCommand]
    private void TogglePin()
    {
        IsToolPanePinned = !IsToolPanePinned;
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
        LoadScratchpad(value.Code);
    }

    partial void OnPhoneSectionChanged(WorkbenchPhoneSection value) => RaiseLayoutChanged();

    partial void OnDockSectionChanged(WorkbenchDockSection value) => RaiseLayoutChanged();

    partial void OnToolSearchQueryChanged(string value) => RefreshToolPicker();

    private void RefreshToolPicker()
    {
        ToolPickerResults.Clear();
        foreach (var tool in _catalog.Search(ToolSearchQuery))
        {
            ToolPickerResults.Add(new ToolPickerItem(tool.Id, _localizer[tool.NameKey].Value, tool.IconKey, tool.ViewModelType));
        }
    }

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
                    tab = new WorkbenchCacheTab(open[i]);
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
            LoadScratchpad(current.Code);
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

    private async void LoadScratchpad(GeocacheCode code)
    {
        if (_scratchpadCode == code)
        {
            return;
        }

        _scratchpadCode = code;
        try
        {
            await Scratchpad.InitializeAsync(code);
        }
        catch (Exception)
        {
            // The scratchpad stays empty; the rest of the workbench keeps working.
        }
    }

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
