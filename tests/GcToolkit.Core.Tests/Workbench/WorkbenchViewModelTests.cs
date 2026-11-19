using CommunityToolkit.Mvvm.Messaging;
using GcToolkit.Core.Catalog;
using GcToolkit.Core.Geocaches;
using GcToolkit.Core.Navigation;
using GcToolkit.Core.Tests.Fakes;
using GcToolkit.Core.ViewModels;
using GcToolkit.Core.ViewModels.Tools;
using GcToolkit.Core.Workbench;

namespace GcToolkit.Core.Tests.Workbench;

[TestClass]
public class WorkbenchViewModelTests
{
    private static readonly GeocacheCode A = GeocacheCode.Parse("GC1AAA");
    private static readonly GeocacheCode B = GeocacheCode.Parse("GC2BBB");

    private readonly WeakReferenceMessenger _messenger = new();
    private readonly InMemoryScratchpadRepository _scratchpads = new();
    private readonly FakeTimeProvider _time = new();
    private readonly PreferencesWorkbenchToolStore _toolStore = new(InMemoryPreferences.TrimmedHead());
    private readonly FakeFavoriteToolsService _favorites = new();
    private readonly FakeRecentsService _recents = new();
    private readonly InMemoryPreferences _layoutPreferences = InMemoryPreferences.TrimmedHead();
    private readonly InMemoryGeocacheStore _store = new();
    private readonly FakeToolWindowService _toolWindows = new();
    private WorkbenchProgressStore _progress = null!;
    private WorkbenchSessionService _session = null!;

    [TestInitialize]
    public void Setup()
    {
        _session = new WorkbenchSessionService(
            InMemoryPreferences.TrimmedHead(),
            _messenger);
        _progress = new WorkbenchProgressStore(InMemoryPreferences.TrimmedHead(), _time);
    }

    private WorkbenchViewModel Create(double width = 1200, ICatalogService? catalog = null)
    {
        var localizer = new FakeStringLocalizer(new Dictionary<string, string>
        {
            ["Name_Caesar"] = "Caesar cipher",
            ["Name_Morse"] = "Morse code",
            ["Name_Roman"] = "Roman numerals",
        });
        var vm = new WorkbenchViewModel(
            _session,
            catalog ?? new FilteringCatalog("Caesar", "Morse", "Roman"),
            localizer,
            _toolStore,
            _favorites,
            _recents,
            new ScratchpadViewModel(_scratchpads, localizer, _time),
            new GeocacheDetailViewModel(_store, new FakeClipboardService(), new FakeNavigationService(), localizer, new FakeAppPreferences()),
            CreateSheet(),
            new CorrectedCoordinatesViewModel(_store, localizer),
            _progress,
            _store,
            _toolWindows,
            _layoutPreferences,
            _messenger);
        vm.ViewCreated();
        vm.UpdateAvailableWidth(width);
        return vm;
    }

    private VariableCoordinateCalculatorViewModel CreateSheet() => new(
        new StubCatalogService("VariableCoordinateCalculator"),
        new FakeRecentsService(),
        new FakeFavoriteToolsService(),
        new FakeStringLocalizer(),
        new FakeClipboardService(),
        new FakeShareService(),
        _layoutPreferences);

    private static ToolPickerItem Pick(WorkbenchViewModel vm, string toolId)
        => vm.ToolPickerResults.Single(t => t.ToolId == toolId);

    // Layout

    [TestMethod]
    public void UpdateAvailableWidth_Wide_ToolPaneOpenInlineAtItsWidth()
    {
        var vm = Create(1200);

        Assert.IsTrue(vm.IsWide);
        Assert.IsTrue(vm.IsToolPaneOpen);
        Assert.IsTrue(vm.IsToolPaneInline);
        Assert.IsTrue(vm.IsListingVisible);
        Assert.AreEqual(WorkbenchLayout.DefaultToolPaneWidth, vm.PaneLength);
    }

    [TestMethod]
    public void UpdateAvailableWidth_Medium_ToolPaneClosedOverlay()
    {
        var vm = Create(700);

        Assert.IsTrue(vm.IsMedium);
        Assert.IsFalse(vm.IsToolPaneOpen);
        Assert.IsFalse(vm.IsToolPaneInline);
        Assert.AreEqual(WorkbenchLayout.OverlayPaneWidth, vm.PaneLength);
    }

    [TestMethod]
    public void ToggleToolPane_Medium_OpensAndCloses()
    {
        var vm = Create(700);

        vm.ToggleToolPaneCommand.Execute(null);
        Assert.IsTrue(vm.IsToolPaneOpen);

        vm.ToggleToolPaneCommand.Execute(null);
        Assert.IsFalse(vm.IsToolPaneOpen);
    }

    [TestMethod]
    public void IsToolPaneOpen_LightDismissInMedium_Closes()
    {
        var vm = Create(700);
        vm.ToggleToolPaneCommand.Execute(null);

        vm.IsToolPaneOpen = false;

        Assert.IsFalse(vm.IsToolPaneOpen);
    }

    [TestMethod]
    public void IsToolPaneOpen_SetFalseInWide_StaysOpen()
    {
        var vm = Create(1200);

        vm.IsToolPaneOpen = false;

        Assert.IsTrue(vm.IsToolPaneOpen);
    }

    [TestMethod]
    public void TogglePin_Medium_PinnedPaneIsInlineAndOpen()
    {
        var vm = Create(700);

        vm.TogglePinCommand.Execute(null);

        Assert.IsTrue(vm.IsToolPanePinned);
        Assert.IsTrue(vm.IsToolPaneInline);
        Assert.IsTrue(vm.IsToolPaneOpen);
    }

    [TestMethod]
    public void TogglePin_Unpin_BecomesOverlayAgain()
    {
        var vm = Create(700);
        vm.TogglePinCommand.Execute(null);

        vm.TogglePinCommand.Execute(null);

        Assert.IsFalse(vm.IsToolPanePinned);
        Assert.IsFalse(vm.IsToolPaneInline);
    }

    [TestMethod]
    public void UpdateAvailableWidth_Compact_StartsOnListing()
    {
        var vm = Create(400);

        Assert.IsTrue(vm.IsCompact);
        Assert.AreEqual(WorkbenchPhoneSection.Listing, vm.PhoneSection);
        Assert.IsTrue(vm.IsListingVisible);
        Assert.IsFalse(vm.IsToolPaneOpen);
    }

    [TestMethod]
    public void PhoneSection_Tool_PaneFillsTheWidthAndHidesListing()
    {
        var vm = Create(400);

        vm.PhoneSection = WorkbenchPhoneSection.Tool;

        Assert.IsTrue(vm.IsToolPaneOpen);
        Assert.IsTrue(vm.IsToolPaneInline);
        Assert.AreEqual(400d, vm.PaneLength);
        Assert.IsFalse(vm.IsListingVisible);
        Assert.IsTrue(vm.IsToolTabsVisible);
        Assert.IsFalse(vm.IsDockVisible);
    }

    [TestMethod]
    public void PhoneSection_Notes_ShowsOnlyTheScratchpad()
    {
        var vm = Create(400);

        vm.PhoneSection = WorkbenchPhoneSection.Notes;

        Assert.IsTrue(vm.IsToolPaneOpen);
        Assert.IsFalse(vm.IsToolTabsVisible);
        Assert.IsTrue(vm.IsDockVisible);
        Assert.IsTrue(vm.IsScratchpadVisible);
        Assert.IsFalse(vm.IsVariablesVisible);
        Assert.IsFalse(vm.IsDockSelectorVisible);
    }

    [TestMethod]
    public void PhoneSection_Variables_ShowsOnlyTheVariables()
    {
        var vm = Create(400);

        vm.PhoneSection = WorkbenchPhoneSection.Variables;

        Assert.IsTrue(vm.IsToolPaneOpen);
        Assert.IsFalse(vm.IsListingVisible);
        Assert.IsFalse(vm.IsToolTabsVisible);
        Assert.IsTrue(vm.IsDockVisible);
        Assert.IsTrue(vm.IsVariablesVisible);
        Assert.IsFalse(vm.IsScratchpadVisible);
        Assert.IsFalse(vm.IsDockSelectorVisible);
    }

    [TestMethod]
    public void Relaunch_RestoresPaneWidthDockHeightAndPin()
    {
        var first = Create(1400);
        first.ToolPaneWidth = 520;
        first.DockHeight = 300;
        first.TogglePinCommand.Execute(null);

        var second = Create(1400);

        Assert.AreEqual(520d, second.ToolPaneWidth);
        Assert.AreEqual(300d, second.DockHeight);
        Assert.IsTrue(second.IsToolPanePinned);
    }

    [TestMethod]
    [DataRow(WorkbenchPane.Listing, false, WorkbenchPane.Tools)]
    [DataRow(WorkbenchPane.Tools, false, WorkbenchPane.Dock)]
    [DataRow(WorkbenchPane.Dock, false, WorkbenchPane.Listing)]
    [DataRow(WorkbenchPane.Listing, true, WorkbenchPane.Dock)]
    [DataRow(WorkbenchPane.Tools, true, WorkbenchPane.Listing)]
    public void MoveFocus_Wide_CyclesThePanes(WorkbenchPane from, bool backwards, WorkbenchPane expected)
    {
        var vm = Create(1400);

        Assert.AreEqual(expected, vm.MoveFocus(from, backwards));
    }

    [TestMethod]
    public void MoveFocus_MediumToTheTools_OpensTheOverlay()
    {
        var vm = Create(700);

        var target = vm.MoveFocus(WorkbenchPane.Listing);

        Assert.AreEqual(WorkbenchPane.Tools, target);
        Assert.IsTrue(vm.IsToolPaneOpen);
    }

    [TestMethod]
    [DataRow(WorkbenchPhoneSection.Listing, false, WorkbenchPhoneSection.Tool, WorkbenchPane.Tools)]
    [DataRow(WorkbenchPhoneSection.Tool, false, WorkbenchPhoneSection.Variables, WorkbenchPane.Dock)]
    [DataRow(WorkbenchPhoneSection.Notes, false, WorkbenchPhoneSection.Listing, WorkbenchPane.Listing)]
    [DataRow(WorkbenchPhoneSection.Listing, true, WorkbenchPhoneSection.Notes, WorkbenchPane.Dock)]
    public void MoveFocus_Compact_StepsThroughTheSections(WorkbenchPhoneSection from, bool backwards, WorkbenchPhoneSection expectedSection, WorkbenchPane expectedPane)
    {
        var vm = Create(400);
        vm.PhoneSection = from;

        var target = vm.MoveFocus(WorkbenchPane.Listing, backwards);

        Assert.AreEqual(expectedSection, vm.PhoneSection);
        Assert.AreEqual(expectedPane, target);
    }

    [TestMethod]
    public void DockSection_Wide_SwitchesBetweenVariablesAndScratchpad()
    {
        var vm = Create(1200);
        Assert.IsTrue(vm.IsVariablesVisible);
        Assert.IsFalse(vm.IsScratchpadVisible);

        vm.DockSection = WorkbenchDockSection.Scratchpad;

        Assert.IsFalse(vm.IsVariablesVisible);
        Assert.IsTrue(vm.IsScratchpadVisible);
        Assert.IsTrue(vm.IsDockSelectorVisible);
    }

    [TestMethod]
    public void UpdateAvailableWidth_ModeChanges_RaisesDerivedProperties()
    {
        var vm = Create(1200);
        List<string?> changed = [];
        vm.PropertyChanged += (_, e) => changed.Add(e.PropertyName);

        vm.UpdateAvailableWidth(360);

        CollectionAssert.Contains(changed, nameof(WorkbenchViewModel.IsCompact));
        CollectionAssert.Contains(changed, nameof(WorkbenchViewModel.IsToolPaneOpen));
        CollectionAssert.Contains(changed, nameof(WorkbenchViewModel.PaneLength));
    }

    [TestMethod]
    public void UpdateAvailableWidth_SameMode_DoesNotRaiseUnchangedLayout()
    {
        var vm = Create(1200);
        List<string?> changed = [];
        vm.PropertyChanged += (_, e) => changed.Add(e.PropertyName);

        vm.UpdateAvailableWidth(1300);

        CollectionAssert.DoesNotContain(changed, nameof(WorkbenchViewModel.IsWide));
        CollectionAssert.DoesNotContain(changed, nameof(WorkbenchViewModel.IsDockVisible));
    }

    [TestMethod]
    public void UpdateAvailableWidth_ToCompactAfterDrag_RaisesPaneLength()
    {
        var vm = Create(1200);
        vm.ToolPaneWidth = 500;
        List<string?> changed = [];
        vm.PropertyChanged += (_, e) => changed.Add(e.PropertyName);

        vm.UpdateAvailableWidth(400);

        CollectionAssert.Contains(changed, nameof(WorkbenchViewModel.PaneLength));
        Assert.AreEqual(400d, vm.PaneLength);
    }

    [TestMethod]
    public void UpdateAvailableWidth_ResizeAcrossModes_KeepsTabsAndSections()
    {
        _session.Open(A);
        var vm = Create(400);
        vm.AddTool(Pick(vm, "Morse"));
        vm.DockSection = WorkbenchDockSection.Scratchpad;

        vm.UpdateAvailableWidth(1200);
        vm.UpdateAvailableWidth(700);
        vm.UpdateAvailableWidth(400);

        Assert.AreEqual(1, vm.ActiveCache!.Tools.Count);
        Assert.AreEqual(WorkbenchPhoneSection.Tool, vm.PhoneSection);
        Assert.AreEqual(WorkbenchDockSection.Scratchpad, vm.DockSection);
    }

    [TestMethod]
    public void ToolPaneWidth_SetBeyondRange_Clamped()
    {
        var vm = Create(1000);

        vm.ToolPaneWidth = 900;

        Assert.AreEqual(600d, vm.ToolPaneWidth);
        Assert.AreEqual(600d, vm.PaneLength);
    }

    [TestMethod]
    public void UpdateAvailableWidth_Shrinks_ReclampsToolPaneWidth()
    {
        var vm = Create(1400);
        vm.ToolPaneWidth = 800;

        vm.UpdateAvailableWidth(900);

        Assert.AreEqual(500d, vm.ToolPaneWidth);
    }

    [TestMethod]
    public void DockHeight_Default_IsTheDefaultHeight()
        => Assert.AreEqual(WorkbenchLayout.DefaultDockHeight, Create().DockHeight);

    [TestMethod]
    [DataRow(50d, 120d)]
    [DataRow(300d, 300d)]
    public void DockHeight_Set_KeepsAMinimum(double requested, double expected)
    {
        var vm = Create();

        vm.DockHeight = requested;

        Assert.AreEqual(expected, vm.DockHeight);
    }

    // Caches

    [TestMethod]
    public void ViewCreated_SessionHasCaches_ShowsThemAsTabs()
    {
        _session.Open(A);
        _session.Open(B);

        var vm = Create();

        CollectionAssert.AreEqual(new[] { A, B }, vm.OpenCaches.Select(t => t.Code).ToArray());
        Assert.AreEqual(B, vm.ActiveCache!.Code);
        Assert.IsTrue(vm.HasOpenCaches);
    }

    [TestMethod]
    public void ViewCreated_NoCaches_HasNoOpenCaches()
    {
        var vm = Create();

        Assert.IsFalse(vm.HasOpenCaches);
        Assert.IsNull(vm.ActiveCache);
    }

    [TestMethod]
    public void OnNavigatedTo_GeocacheCode_OpensIt()
    {
        var vm = Create();

        vm.OnNavigatedTo(A);

        Assert.AreEqual(A, _session.ActiveCache);
        Assert.AreEqual(A, vm.ActiveCache!.Code);
    }

    [TestMethod]
    public void OpenCache_ValidCode_OpensAndClearsInput()
    {
        var vm = Create();
        vm.NewCacheCode = " gc2bbb ";

        vm.OpenCacheCommand.Execute(null);

        Assert.AreEqual(B, vm.ActiveCache!.Code);
        Assert.AreEqual(string.Empty, vm.NewCacheCode);
    }

    [TestMethod]
    [DataRow("")]
    [DataRow("G")]
    [DataRow("GC 12")]
    public void OpenCache_InvalidCode_CannotExecute(string code)
    {
        var vm = Create();

        vm.NewCacheCode = code;

        Assert.IsFalse(vm.OpenCacheCommand.CanExecute(null));
    }

    [TestMethod]
    public void ActiveCache_SelectedTab_ActivatesItInTheSession()
    {
        _session.Open(A);
        _session.Open(B);
        var vm = Create();

        vm.ActiveCache = vm.OpenCaches[0];

        Assert.AreEqual(A, _session.ActiveCache);
    }

    [TestMethod]
    public void ActiveCache_SetToNull_KeepsTheSessionActiveCache()
    {
        _session.Open(A);
        var vm = Create();

        vm.ActiveCache = null;

        Assert.AreEqual(A, _session.ActiveCache);
        Assert.AreEqual(A, vm.ActiveCache!.Code);
    }

    [TestMethod]
    public void CloseCache_Tab_ClosesItInTheSession()
    {
        _session.Open(A);
        _session.Open(B);
        var vm = Create();

        vm.CloseCache(vm.OpenCaches[1]);

        CollectionAssert.AreEqual(new[] { A }, _session.OpenCaches.ToArray());
        CollectionAssert.AreEqual(new[] { A }, vm.OpenCaches.Select(t => t.Code).ToArray());
        Assert.AreEqual(A, vm.ActiveCache!.Code);
    }

    [TestMethod]
    public void CloseCache_LastTab_NoActiveCache()
    {
        _session.Open(A);
        var vm = Create();

        vm.CloseCache(vm.OpenCaches[0]);

        Assert.IsFalse(vm.HasOpenCaches);
        Assert.IsNull(vm.ActiveCache);
    }

    [TestMethod]
    public void OpenCaches_ReorderedByTheTabStrip_MovesInTheSession()
    {
        _session.Open(A);
        _session.Open(B);
        var vm = Create();

        // A drag reorder arrives as remove + insert.
        var tab = vm.OpenCaches[1];
        vm.OpenCaches.RemoveAt(1);
        vm.OpenCaches.Insert(0, tab);

        CollectionAssert.AreEqual(new[] { B, A }, _session.OpenCaches.ToArray());
        Assert.AreEqual(2, vm.OpenCaches.Count);
    }

    [TestMethod]
    public void SessionChangedElsewhere_TabsFollow()
    {
        var vm = Create();

        _session.Open(A);

        Assert.AreEqual(1, vm.OpenCaches.Count);
        Assert.AreEqual(A, vm.ActiveCache!.Code);
    }

    [TestMethod]
    public void SessionChanged_ExistingTab_KeepsItsTools()
    {
        _session.Open(A);
        var vm = Create();
        var tab = vm.ActiveCache!;
        vm.AddTool(Pick(vm, "Morse"));

        _session.Open(B);
        _session.Activate(A);

        Assert.AreSame(tab, vm.ActiveCache);
        Assert.AreEqual(1, vm.ActiveCache!.Tools.Count);
    }

    // Tools

    [TestMethod]
    public void ToolSearchQuery_Filters_PickerResults()
    {
        var vm = Create();
        Assert.AreEqual(3, vm.ToolPickerResults.Count);

        vm.ToolSearchQuery = "mor";

        Assert.AreEqual("Morse", vm.ToolPickerResults.Single().ToolId);
        Assert.AreEqual("Morse code", vm.ToolPickerResults.Single().Name);
    }

    [TestMethod]
    public void AddTool_ActiveCache_AddsAndSelectsTab()
    {
        _session.Open(A);
        var vm = Create();

        vm.AddTool(Pick(vm, "Caesar"));
        vm.AddTool(Pick(vm, "Morse"));

        Assert.AreEqual(2, vm.ActiveCache!.Tools.Count);
        Assert.AreEqual("Morse", vm.ActiveCache.SelectedTool!.ToolId);
        Assert.AreEqual(typeof(FakeStatefulTool), vm.ActiveCache.SelectedTool.ViewModelType);
        Assert.IsTrue(vm.ActiveCache.HasTools);
    }

    [TestMethod]
    public void AddTool_NoCacheOpen_DoesNothing()
    {
        var vm = Create();

        vm.AddTool(Pick(vm, "Caesar"));

        Assert.IsNull(vm.ActiveCache);
    }

    [TestMethod]
    public void AddTool_Compact_SwitchesToTheToolSection()
    {
        _session.Open(A);
        var vm = Create(400);

        vm.AddTool(Pick(vm, "Caesar"));

        Assert.AreEqual(WorkbenchPhoneSection.Tool, vm.PhoneSection);
    }

    [TestMethod]
    public void AddTool_Medium_OpensThePane()
    {
        _session.Open(A);
        var vm = Create(700);

        vm.AddTool(Pick(vm, "Caesar"));

        Assert.IsTrue(vm.IsToolPaneOpen);
    }

    [TestMethod]
    public void AddTool_ClearsTheSearch()
    {
        _session.Open(A);
        var vm = Create();
        vm.ToolSearchQuery = "mor";

        vm.AddTool(Pick(vm, "Morse"));

        Assert.AreEqual(string.Empty, vm.ToolSearchQuery);
        Assert.AreEqual(3, vm.ToolPickerResults.Count);
    }

    [TestMethod]
    public void CloseTool_SelectedTab_SelectsTheNeighbour()
    {
        _session.Open(A);
        var vm = Create();
        vm.AddTool(Pick(vm, "Caesar"));
        vm.AddTool(Pick(vm, "Morse"));
        vm.AddTool(Pick(vm, "Roman"));
        var cache = vm.ActiveCache!;
        cache.SelectedTool = cache.Tools[1];

        vm.CloseTool(cache.Tools[1]);

        CollectionAssert.AreEqual(new[] { "Caesar", "Roman" }, cache.Tools.Select(t => t.ToolId).ToArray());
        Assert.AreEqual("Roman", cache.SelectedTool!.ToolId);
    }

    [TestMethod]
    public void CloseTool_LastTab_NothingSelected()
    {
        _session.Open(A);
        var vm = Create();
        vm.AddTool(Pick(vm, "Caesar"));

        vm.CloseTool(vm.ActiveCache!.Tools[0]);

        Assert.IsNull(vm.ActiveCache!.SelectedTool);
        Assert.IsFalse(vm.ActiveCache.HasTools);
    }

    [TestMethod]
    public void Tools_ArePerCache()
    {
        _session.Open(A);
        _session.Open(B);
        var vm = Create();
        vm.AddTool(Pick(vm, "Morse"));

        vm.ActiveCache = vm.OpenCaches[0];

        Assert.AreEqual(A, vm.ActiveCache!.Code);
        Assert.IsFalse(vm.ActiveCache.HasTools);
    }

    // Picker

    [TestMethod]
    public void ToolPickerResults_NoQuery_FavouritesThenRecentsFirst()
    {
        _ = _favorites.ToggleAsync("Roman");
        _ = _recents.RecordOpenedAsync("Morse");

        var vm = Create();

        CollectionAssert.AreEqual(
            new[] { "Roman", "Morse", "Caesar" },
            vm.ToolPickerResults.Select(t => t.ToolId).ToArray());
    }

    [TestMethod]
    public void ToolPickerResults_Query_KeepsTheSearchOrder()
    {
        _ = _favorites.ToggleAsync("Roman");
        var vm = Create();

        vm.ToolSearchQuery = "r";

        CollectionAssert.AreEqual(
            new[] { "Caesar", "Morse", "Roman" },
            vm.ToolPickerResults.Select(t => t.ToolId).ToArray());
    }

    // Per-cache tool state

    [TestMethod]
    public void AddTool_SavesTheCachesTools()
    {
        _session.Open(A);
        var vm = Create();

        vm.AddTool(Pick(vm, "Caesar"));
        vm.AddTool(Pick(vm, "Morse"));

        var saved = _toolStore.Load(A)!;
        CollectionAssert.AreEqual(new[] { "Caesar", "Morse" }, saved.Tools.Select(t => t.ToolId).ToArray());
        Assert.AreEqual(1, saved.SelectedIndex);
    }

    [TestMethod]
    public void SelectedTool_Changed_SavesTheSelection()
    {
        _session.Open(A);
        var vm = Create();
        vm.AddTool(Pick(vm, "Caesar"));
        vm.AddTool(Pick(vm, "Morse"));

        vm.ActiveCache!.SelectedTool = vm.ActiveCache.Tools[0];

        Assert.AreEqual(0, _toolStore.Load(A)!.SelectedIndex);
    }

    [TestMethod]
    public void SelectedTool_ClearedWhileToolsRemain_KeepsTheSavedSelection()
    {
        _session.Open(A);
        var vm = Create();
        vm.AddTool(Pick(vm, "Caesar"));
        vm.AddTool(Pick(vm, "Morse"));

        // A tab strip rebuilding its items clears its selection for a moment.
        vm.ActiveCache!.SelectedTool = null;

        Assert.AreEqual(1, _toolStore.Load(A)!.SelectedIndex);
    }

    [TestMethod]
    public void CloseTool_SavesTheRemainingTools()
    {
        _session.Open(A);
        var vm = Create();
        vm.AddTool(Pick(vm, "Caesar"));
        vm.AddTool(Pick(vm, "Morse"));

        vm.CloseTool(vm.ActiveCache!.Tools[0]);

        CollectionAssert.AreEqual(new[] { "Morse" }, _toolStore.Load(A)!.Tools.Select(t => t.ToolId).ToArray());
    }

    [TestMethod]
    public void CloseTool_LastOne_ForgetsTheCache()
    {
        _session.Open(A);
        var vm = Create();
        vm.AddTool(Pick(vm, "Caesar"));

        vm.CloseTool(vm.ActiveCache!.Tools[0]);

        Assert.IsNull(_toolStore.Load(A));
    }

    [TestMethod]
    public void ToolInputEdited_SavesItsState()
    {
        _session.Open(A);
        var vm = Create();
        vm.AddTool(Pick(vm, "Caesar"));
        FakeStatefulTool tool = new();
        vm.ActiveCache!.SelectedTool!.Attach(tool);

        tool.Text = "Uryyb";

        Assert.AreEqual("Uryyb", _toolStore.Load(A)!.Tools[0].Inputs!["text"]);
    }

    [TestMethod]
    public void ToolInputEdited_AfterTheToolWasClosed_IsNotSaved()
    {
        _session.Open(A);
        var vm = Create();
        vm.AddTool(Pick(vm, "Caesar"));
        var tab = vm.ActiveCache!.SelectedTool!;
        FakeStatefulTool tool = new();
        tab.Attach(tool);
        vm.CloseTool(tab);

        tool.Text = "late";

        Assert.IsNull(_toolStore.Load(A));
    }

    [TestMethod]
    public void Relaunch_RestoresToolsSelectionAndInputs()
    {
        _session.Open(A);
        var first = Create();
        first.AddTool(Pick(first, "Caesar"));
        first.AddTool(Pick(first, "Morse"));
        FakeStatefulTool typed = new();
        first.ActiveCache!.Tools[0].Attach(typed);
        typed.Text = "Uryyb";
        first.ActiveCache.SelectedTool = first.ActiveCache.Tools[0];

        var second = Create();

        var cache = second.ActiveCache!;
        CollectionAssert.AreEqual(new[] { "Caesar", "Morse" }, cache.Tools.Select(t => t.ToolId).ToArray());
        Assert.AreEqual("Caesar cipher", cache.Tools[0].Name);
        Assert.AreSame(cache.Tools[0], cache.SelectedTool);
        FakeStatefulTool restored = new();
        cache.Tools[0].Attach(restored);
        Assert.AreEqual("Uryyb", restored.Text);
    }

    [TestMethod]
    public void Restore_ToolNoLongerInTheCatalog_IsSkipped()
    {
        _toolStore.Save(A, new WorkbenchToolsState(
            [new WorkbenchToolState("Gone", null), new WorkbenchToolState("Morse", null)],
            SelectedIndex: 0));
        _session.Open(A);

        var vm = Create();

        CollectionAssert.AreEqual(new[] { "Morse" }, vm.ActiveCache!.Tools.Select(t => t.ToolId).ToArray());
        Assert.AreEqual("Morse", vm.ActiveCache.SelectedTool!.ToolId);
    }

    [TestMethod]
    public void Restore_IsPerCache()
    {
        _session.Open(A);
        _session.Open(B);
        var first = Create();
        first.AddTool(Pick(first, "Morse"));
        first.ActiveCache = first.OpenCaches[0];
        first.AddTool(Pick(first, "Roman"));

        var second = Create();

        Assert.AreEqual("Roman", second.OpenCaches[0].Tools.Single().ToolId);
        Assert.AreEqual("Morse", second.OpenCaches[1].Tools.Single().ToolId);
    }

    [TestMethod]
    public void CacheClosedAndReopened_RestoresItsTools()
    {
        _session.Open(A);
        var vm = Create();
        vm.AddTool(Pick(vm, "Morse"));

        vm.CloseCache(vm.ActiveCache!);
        _session.Open(A);

        Assert.AreEqual("Morse", vm.ActiveCache!.Tools.Single().ToolId);
    }

    // Scratchpad

    [TestMethod]
    public async Task ActiveCache_LoadsItsListing()
    {
        await _store.UpsertAsync([new Geocache { Code = A, Name = "Alpha", Coordinate = new(50, 14) }, new Geocache { Code = B, Name = "Bravo", Coordinate = new(50, 14) }]);
        _session.Open(A);
        var vm = Create();

        _session.Open(B);

        Assert.AreEqual("Bravo", vm.Listing.Name);
        Assert.IsTrue(vm.Listing.IsLoaded);
    }

    [TestMethod]
    public async Task ActiveCache_ListingLoaded_TabShowsTheCacheName()
    {
        await _store.UpsertAsync([new Geocache { Code = A, Name = "Alpha", Coordinate = new(50, 14) }]);
        _session.Open(A);

        var vm = Create();

        Assert.AreEqual("Alpha", vm.ActiveCache!.Title);
    }

    // Selection → tool

    private WorkbenchViewModel CreateWithTextTools(double width = 1200)
    {
        _session.Open(A);
        return Create(width, new FilteringCatalog("CaesarCipher", "VigenereCipher", "MorseCode", "WordValue"));
    }

    [TestMethod]
    public void SelectedText_Letters_SuggestsTheLetterToolsInTheCatalog()
    {
        var vm = CreateWithTextTools();

        vm.SelectedText = "WKUHH VHYHQ";

        Assert.IsTrue(vm.HasSelection);
        Assert.AreEqual("CaesarCipher", vm.SelectionSuggestions[0].ToolId);
        CollectionAssert.DoesNotContain(vm.SelectionSuggestions.Select(t => t.ToolId).ToList(), "MorseCode");
    }

    [TestMethod]
    public void SelectedText_Cleared_HasNoSelectionOrSuggestions()
    {
        var vm = CreateWithTextTools();
        vm.SelectedText = "WKUHH";

        vm.SelectedText = "  ";

        Assert.IsFalse(vm.HasSelection);
        Assert.AreEqual(0, vm.SelectionSuggestions.Count);
    }

    [TestMethod]
    public void SendSelectionTo_ToolNotOpen_OpensItWithTheText()
    {
        var vm = CreateWithTextTools();
        vm.SelectedText = "WKUHH";

        vm.SendSelectionTo(vm.SelectionSuggestions[0]);

        var tab = vm.ActiveCache!.SelectedTool!;
        Assert.AreEqual("CaesarCipher", tab.ToolId);
        FakeStatefulTool tool = new();
        tab.Attach(tool);
        Assert.AreEqual("WKUHH", tool.Text);
    }

    [TestMethod]
    public void SendSelectionTo_ToolAlreadyOpen_ReusesItsTab()
    {
        var vm = CreateWithTextTools();
        vm.SelectedText = "WKUHH";
        var caesar = vm.SelectionSuggestions[0];
        vm.SendSelectionTo(caesar);
        FakeStatefulTool tool = new();
        vm.ActiveCache!.SelectedTool!.Attach(tool);

        vm.SelectedText = "VHYHQ";
        vm.SendSelectionTo(caesar);

        Assert.AreEqual(1, vm.ActiveCache.Tools.Count);
        Assert.AreEqual("VHYHQ", tool.Text);
    }

    [TestMethod]
    public void SendSelectionTo_Compact_SwitchesToTheToolSection()
    {
        var vm = CreateWithTextTools(400);
        vm.SelectedText = "WKUHH";

        vm.SendSelectionTo(vm.SelectionSuggestions[0]);

        Assert.AreEqual(WorkbenchPhoneSection.Tool, vm.PhoneSection);
    }

    [TestMethod]
    public void SendSelectionToSuggested_SendsToTheFirstSuggestion()
    {
        var vm = CreateWithTextTools();
        vm.SelectedText = "WKUHH";

        Assert.AreEqual("CaesarCipher", vm.SuggestedTool!.ToolId);
        vm.SendSelectionToSuggestedCommand.Execute(null);

        Assert.AreEqual("CaesarCipher", vm.ActiveCache!.SelectedTool!.ToolId);
    }

    [TestMethod]
    public void PickTool_AfterSendToOther_SendsTheSelectionInsteadOfAddingAnEmptyTool()
    {
        var vm = CreateWithTextTools();
        vm.SelectedText = "... ---";
        vm.BeginSendToOtherTool();

        vm.PickTool(Pick(vm, "VigenereCipher"));

        var tab = vm.ActiveCache!.SelectedTool!;
        FakeStatefulTool tool = new();
        tab.Attach(tool);
        Assert.AreEqual("... ---", tool.Text);
        Assert.IsFalse(vm.IsPickingForSelection);
    }

    [TestMethod]
    public void PickTool_Normally_AddsAnEmptyTool()
    {
        var vm = CreateWithTextTools();
        vm.SelectedText = "WKUHH";

        vm.PickTool(Pick(vm, "VigenereCipher"));

        FakeStatefulTool tool = new();
        vm.ActiveCache!.SelectedTool!.Attach(tool);
        Assert.AreEqual(string.Empty, tool.Text);
    }

    [TestMethod]
    public async Task ShowPasteBox_CacheWithoutDescription_IsShown()
    {
        await _store.UpsertAsync([new Geocache { Code = A, Name = "Plain", Coordinate = new(50, 14) }]);
        _session.Open(A);

        var vm = Create();

        Assert.IsTrue(vm.ShowPasteBox);
    }

    [TestMethod]
    public async Task ShowPasteBox_CacheWithDescription_IsHidden()
    {
        await _store.UpsertAsync([new Geocache { Code = A, Name = "Full", Coordinate = new(50, 14), LongDescription = "Hello" }]);
        _session.Open(A);

        var vm = Create();

        Assert.IsFalse(vm.ShowPasteBox);
    }

    [TestMethod]
    public async Task PastedListing_IsKeptPerCacheAcrossRelaunch()
    {
        await _store.UpsertAsync([new Geocache { Code = A, Name = "Plain", Coordinate = new(50, 14) }]);
        _session.Open(A);
        var first = Create();
        first.PastedListing = "WKUHH VHYHQ";

        var second = Create();

        Assert.AreEqual("WKUHH VHYHQ", second.PastedListing);
    }

    [TestMethod]
    public void AddSelectionToScratchpad_AppendsALine()
    {
        var vm = CreateWithTextTools();
        vm.Scratchpad.Text = "first";
        vm.SelectedText = "WKUHH";

        vm.AddSelectionToScratchpadCommand.Execute(null);

        Assert.AreEqual("first\nWKUHH", vm.Scratchpad.Text.Replace("\r\n", "\n"));
    }

    [TestMethod]
    public void ActiveCache_Loads_ItsScratchpad()
    {
        _scratchpads.Seed(A, "Wall: Caesar +3");
        _session.Open(A);

        var vm = Create();

        Assert.AreEqual("Wall: Caesar +3", vm.Scratchpad.Text);
    }

    [TestMethod]
    public void ActiveCache_Switched_SavesThePreviousScratchpad()
    {
        _session.Open(A);
        _session.Open(B);
        var vm = Create();
        vm.Scratchpad.Text = "B notes";

        vm.ActiveCache = vm.OpenCaches[0];

        Assert.AreEqual("B notes", _scratchpads.Get(B));
        Assert.AreEqual(string.Empty, vm.Scratchpad.Text);
    }

    [TestMethod]
    public void OnNavigatedFrom_FlushesTheScratchpad()
    {
        _session.Open(A);
        var vm = Create();
        vm.Scratchpad.Text = "count the stones";

        vm.OnNavigatedFrom();

        Assert.AreEqual("count the stones", _scratchpads.Get(A));
    }

    // Variable sheet

    private const string SheetTemplate = "N 50° 0A.000 E 014° 25.000";

    [TestMethod]
    public void Variables_AreKeptPerCache()
    {
        _session.Open(A);
        _session.Open(B);
        var vm = Create();
        vm.Variables.Stages[0].Template = SheetTemplate;
        vm.Variables.Variables.Single(v => v.Name == "A").Value = "7";

        vm.ActiveCache = vm.OpenCaches[0];

        Assert.AreEqual(string.Empty, vm.Variables.Stages[0].Template);
        Assert.AreEqual(0, vm.Variables.Variables.Count);

        vm.ActiveCache = vm.OpenCaches[1];

        Assert.AreEqual(SheetTemplate, vm.Variables.Stages[0].Template);
        Assert.AreEqual("7", vm.Variables.Variables.Single(v => v.Name == "A").Value);
    }

    [TestMethod]
    public void Variables_SurviveRelaunch()
    {
        _session.Open(A);
        var first = Create();
        first.Variables.Stages[0].Template = SheetTemplate;
        first.Variables.Variables.Single(v => v.Name == "A").Value = "7";

        var second = Create();

        Assert.AreEqual(SheetTemplate, second.Variables.Stages[0].Template);
        Assert.AreEqual("7", second.Variables.Variables.Single(v => v.Name == "A").Value);
    }

    [TestMethod]
    public void Variables_DoNotTouchTheStandaloneCalculator()
    {
        _session.Open(A);
        var vm = Create();
        vm.Variables.Stages[0].Template = SheetTemplate;

        var calculator = CreateSheet();

        Assert.AreEqual(string.Empty, calculator.Stages[0].Template);
    }

    [TestMethod]
    public async Task Variables_PostedCoordinate_IsTakenFromTheListing()
    {
        await _store.UpsertAsync([new Geocache { Code = A, Name = "Alpha", Coordinate = new(50, 14) }]);
        _session.Open(A);

        var vm = Create();

        Assert.AreEqual(vm.Listing.PostedCoordinate, vm.Variables.PostedCoordinate);
        Assert.AreNotEqual(string.Empty, vm.Variables.PostedCoordinate);
    }

    [TestMethod]
    public async Task Variables_PostedCoordinateTypedByTheUser_IsKept()
    {
        await _store.UpsertAsync([new Geocache { Code = A, Name = "Alpha", Coordinate = new(50, 14) }]);
        _session.Open(A);
        var first = Create();
        first.Variables.PostedCoordinate = "N 49° 00.000 E 016° 00.000";

        var second = Create();

        Assert.AreEqual("N 49° 00.000 E 016° 00.000", second.Variables.PostedCoordinate);
    }

    // To variables

    private (WorkbenchViewModel Vm, FakeStatefulTool Tool) CreateWithResultTool(string output = "42")
    {
        _session.Open(A);
        var vm = Create();
        vm.AddTool(Pick(vm, "Caesar"));
        FakeStatefulTool tool = new() { Output = output };
        vm.ActiveCache!.SelectedTool!.Attach(tool);
        return (vm, tool);
    }

    [TestMethod]
    public void ToVariables_ToolWithoutResult_IsUnavailable()
    {
        var (vm, _) = CreateWithResultTool(output: "");

        Assert.IsFalse(vm.HasToolResult);
        Assert.IsFalse(vm.SendResultToVariablesCommand.CanExecute(null));
    }

    [TestMethod]
    public void ToVariables_ResultAppears_BecomesAvailable()
    {
        var (vm, tool) = CreateWithResultTool(output: "");

        tool.Output = "42";

        Assert.IsTrue(vm.HasToolResult);
        Assert.IsTrue(vm.SendResultToVariablesCommand.CanExecute(null));
    }

    [TestMethod]
    public void ToVariables_FillsTheNextEmptyLetter()
    {
        var (vm, _) = CreateWithResultTool();
        vm.Variables.SetVariable("A", "7");

        Assert.AreEqual("B", vm.ResultTarget);

        vm.SendResultToVariablesCommand.Execute(null);

        Assert.AreEqual("42", vm.Variables.ValueOf("B"));
        Assert.AreEqual("C", vm.ResultTarget);
        Assert.AreEqual("B = 42", vm.LastSentToVariables);
    }

    [TestMethod]
    public void ToVariables_PickedLetter_IsFilledInstead()
    {
        var (vm, _) = CreateWithResultTool();

        vm.SendResultToVariablesCommand.Execute("D");

        Assert.AreEqual("42", vm.Variables.ValueOf("D"));
        Assert.AreEqual(string.Empty, vm.Variables.ValueOf("A"));
    }

    [TestMethod]
    public void ToVariables_ResultIsTrimmed()
    {
        var (vm, _) = CreateWithResultTool(output: "  42\r\n");

        vm.SendResultToVariablesCommand.Execute(null);

        Assert.AreEqual("42", vm.Variables.ValueOf("A"));
    }

    [TestMethod]
    public void ToVariables_Undo_RestoresThePreviousValue()
    {
        var (vm, _) = CreateWithResultTool();
        vm.Variables.SetVariable("A", "7");
        vm.SendResultToVariablesCommand.Execute("A");

        vm.UndoSendToVariablesCommand.Execute(null);

        Assert.AreEqual("7", vm.Variables.ValueOf("A"));
        Assert.IsFalse(vm.UndoSendToVariablesCommand.CanExecute(null));
        Assert.AreEqual(string.Empty, vm.LastSentToVariables);
    }

    [TestMethod]
    public void ToVariables_UndoOfAnEmptyLetter_EmptiesItAgain()
    {
        var (vm, _) = CreateWithResultTool();
        vm.SendResultToVariablesCommand.Execute(null);

        vm.UndoSendToVariablesCommand.Execute(null);

        Assert.AreEqual(string.Empty, vm.Variables.ValueOf("A"));
        Assert.AreEqual("A", vm.ResultTarget);
    }

    [TestMethod]
    public void ToVariables_SwitchingToAToolWithoutResult_MakesItUnavailable()
    {
        var (vm, _) = CreateWithResultTool();
        vm.AddTool(Pick(vm, "Morse"));
        vm.ActiveCache!.SelectedTool!.Attach(new FakePlainTool());

        Assert.IsFalse(vm.HasToolResult);

        vm.ActiveCache.SelectedTool = vm.ActiveCache.Tools[0];

        Assert.IsTrue(vm.HasToolResult);
    }

    [TestMethod]
    public void ToVariables_LetterTypedInTheSheet_MovesTheTarget()
    {
        var (vm, _) = CreateWithResultTool();

        vm.Variables.SetVariable("A", "1");

        Assert.AreEqual("B", vm.ResultTarget);
    }

    [TestMethod]
    public void ToVariables_UndoIsForgottenWhenTheCacheChanges()
    {
        _session.Open(B);
        var (vm, _) = CreateWithResultTool();
        vm.SendResultToVariablesCommand.Execute(null);

        vm.ActiveCache = vm.OpenCaches.Single(c => c.Code == B);

        Assert.IsFalse(vm.UndoSendToVariablesCommand.CanExecute(null));
    }

    [TestMethod]
    public void ToVariables_LastCacheClosed_IsUnavailable()
    {
        var (vm, _) = CreateWithResultTool();

        vm.CloseCache(vm.ActiveCache!);

        Assert.IsFalse(vm.HasToolResult);
    }

    // Progress for Home's "Continue solving"

    private CacheProgress ProgressOf(GeocacheCode code) => _progress.GetProgress().Single(p => p.Code == code);

    [TestMethod]
    public void Progress_VariableFilled_IsRecorded()
    {
        _session.Open(A);
        var vm = Create();
        vm.Variables.Stages[0].Template = "N 50° 0A.B00 E 014° 25.000";

        vm.Variables.SetVariable("A", "1");

        Assert.AreEqual(1, ProgressOf(A).VariablesFilled);
        Assert.AreEqual(2, ProgressOf(A).VariablesTotal);
        Assert.IsTrue(ProgressOf(A).IsInProgress);
    }

    [TestMethod]
    public void Progress_ToolAdded_IsRecorded()
    {
        _session.Open(A);
        var vm = Create();

        vm.AddTool(Pick(vm, "Caesar"));

        Assert.AreEqual(1, ProgressOf(A).OpenToolCount);
    }

    [TestMethod]
    public async Task Progress_ScratchpadSaved_RecordsItsLines()
    {
        _session.Open(A);
        var vm = Create();
        vm.Scratchpad.Text = "Wall: Caesar +3\n\ncount the stones";

        await vm.Scratchpad.FlushAsync();

        Assert.AreEqual(2, ProgressOf(A).ScratchpadLineCount);
    }

    [TestMethod]
    public async Task Progress_ScratchpadWithTextBoxLineBreaks_CountsEveryLine()
    {
        _session.Open(A);
        var vm = Create();
        vm.Scratchpad.Text = "line one\rline two\rline three";

        await vm.Scratchpad.FlushAsync();

        Assert.AreEqual(3, ProgressOf(A).ScratchpadLineCount);
    }

    [TestMethod]
    public async Task Progress_ScratchpadOfThePreviousCache_IsRecordedForThatCache()
    {
        _session.Open(A);
        _session.Open(B);
        var vm = Create();
        vm.Scratchpad.Text = "B notes";

        vm.ActiveCache = vm.OpenCaches.Single(c => c.Code == A);
        await Task.Yield();

        Assert.AreEqual(1, ProgressOf(B).ScratchpadLineCount);
        Assert.IsFalse(_progress.GetProgress().Any(p => p.Code == A && p.ScratchpadLineCount > 0));
    }

    [TestMethod]
    public async Task Progress_ListingLoaded_RecordsNameAndFlags()
    {
        await _store.UpsertAsync([new Geocache { Code = A, Name = "Alpha", Coordinate = new(50, 14), UserData = new() { IsSolved = true } }]);
        _session.Open(A);

        Create();

        Assert.AreEqual("Alpha", ProgressOf(A).Name);
        Assert.IsTrue(ProgressOf(A).IsSolved);
    }

    [TestMethod]
    public void Progress_OpeningACache_IsNotWork()
    {
        _session.Open(A);

        Create();

        Assert.IsFalse(_progress.GetProgress().Any(p => p.IsInProgress));
    }

    // Solved caches

    [TestMethod]
    public async Task SaveCorrected_MarksTheTabListingAndProgressSolved()
    {
        await _store.UpsertAsync([new Geocache { Code = A, Name = "Alpha", Coordinate = new(50, 14) }]);
        _session.Open(A);
        var vm = Create();
        await Task.Yield();
        vm.Variables.Stages[0].Template = "N 50° 01.000 E 014° 25.000";

        await vm.Corrected.SaveCommand.ExecuteAsync(null);

        Assert.IsTrue(vm.ActiveCache!.IsSolved);
        Assert.IsTrue(vm.Listing.IsSolved);
        Assert.IsTrue(ProgressOf(A).IsSolved);
    }

    [TestMethod]
    public async Task SolvedCache_TabShowsItWhenOpened()
    {
        await _store.UpsertAsync([new Geocache { Code = A, Name = "Alpha", Coordinate = new(50, 14), UserData = new() { IsSolved = true, CorrectedCoordinate = new(50.1, 14.1) } }]);
        _session.Open(A);

        var vm = Create();

        Assert.IsTrue(vm.ActiveCache!.IsSolved);
        Assert.IsTrue(vm.Corrected.HasSaved);
    }

    // Tool in its own window

    private (WorkbenchViewModel Vm, WorkbenchToolTab Tool) CreateWithOpenTool()
    {
        _session.Open(A);
        var vm = Create();
        vm.AddTool(Pick(vm, "Caesar"));
        return (vm, vm.ActiveCache!.SelectedTool!);
    }

    [TestMethod]
    public void PopOut_OpensTheToolInItsOwnWindow()
    {
        var (vm, tool) = CreateWithOpenTool();

        vm.PopOutCommand.Execute(tool);

        Assert.IsTrue(tool.IsPoppedOut);
        CollectionAssert.AreEqual(new[] { tool }, _toolWindows.Open.ToArray());
        Assert.AreEqual("Caesar cipher · GC1AAA", _toolWindows.LastTitle);
    }

    [TestMethod]
    public void PopOut_KeepsTheToolInTheCacheSoItsStateIsStillSaved()
    {
        var (vm, tool) = CreateWithOpenTool();
        vm.PopOutCommand.Execute(tool);
        FakeStatefulTool inWindow = new();

        tool.Attach(inWindow);
        inWindow.Text = "typed in the window";

        Assert.AreSame(tool, vm.ActiveCache!.Tools.Single());
        Assert.AreEqual("typed in the window", _toolStore.Load(A)!.Tools[0].Inputs!["text"]);
    }

    [TestMethod]
    public void ToolWindowClosed_BringsTheToolBack()
    {
        var (vm, tool) = CreateWithOpenTool();
        vm.PopOutCommand.Execute(tool);

        _toolWindows.UserCloses(tool);

        Assert.IsFalse(tool.IsPoppedOut);
    }

    [TestMethod]
    public void BringBack_ClosesTheToolWindow()
    {
        var (vm, tool) = CreateWithOpenTool();
        vm.PopOutCommand.Execute(tool);

        vm.BringBackCommand.Execute(tool);

        Assert.AreEqual(0, _toolWindows.Open.Count);
        Assert.IsFalse(tool.IsPoppedOut);
    }

    [TestMethod]
    public void CloseTool_PoppedOut_ClosesItsWindow()
    {
        var (vm, tool) = CreateWithOpenTool();
        vm.PopOutCommand.Execute(tool);

        vm.CloseTool(tool);

        Assert.AreEqual(0, _toolWindows.Open.Count);
    }

    [TestMethod]
    public void CloseCache_ClosesItsToolWindows()
    {
        var (vm, tool) = CreateWithOpenTool();
        vm.PopOutCommand.Execute(tool);

        vm.CloseCache(vm.ActiveCache!);

        Assert.AreEqual(0, _toolWindows.Open.Count);
    }

    [TestMethod]
    public void PopOut_WithoutWindows_IsUnavailable()
    {
        _toolWindows.IsSupported = false;
        var (vm, tool) = CreateWithOpenTool();

        Assert.IsFalse(vm.CanPopOut);
        Assert.IsFalse(vm.PopOutCommand.CanExecute(tool));
    }

    // Opening a stored cache

    [TestMethod]
    public async Task NewCacheCode_PartOfAName_SuggestsStoredCaches()
    {
        await _store.UpsertAsync(
        [
            new Geocache { Code = A, Name = "The Lost Legion", Coordinate = new(50, 14) },
            new Geocache { Code = B, Name = "Bridge of Sighs", Coordinate = new(50, 14) },
        ]);
        var vm = Create();

        vm.NewCacheCode = "legion";
        await Task.Yield();

        Assert.AreEqual(A, vm.CacheSuggestions.Single().Code);
        Assert.AreEqual("The Lost Legion (GC1AAA)", vm.CacheSuggestions.Single().ToString());
    }

    [TestMethod]
    public async Task OpenSuggestedCache_OpensItAndClearsTheBox()
    {
        await _store.UpsertAsync([new Geocache { Code = B, Name = "Bridge of Sighs", Coordinate = new(50, 14) }]);
        var vm = Create();
        vm.NewCacheCode = "bridge";
        await Task.Yield();

        vm.OpenSuggestedCache(vm.CacheSuggestions.Single());

        Assert.AreEqual(B, vm.ActiveCache!.Code);
        Assert.AreEqual(string.Empty, vm.NewCacheCode);
    }

    [TestMethod]
    public void NewCacheCode_NothingStored_SuggestsNothing()
    {
        var vm = Create();

        vm.NewCacheCode = "GC1";

        Assert.AreEqual(0, vm.CacheSuggestions.Count);
    }

    private sealed class FilteringCatalog(params string[] toolIds) : ICatalogService
    {
        private readonly IReadOnlyList<ToolDescriptor> _tools =
            [.. toolIds.Select(id => new ToolDescriptor(id, $"Name_{id}", "cat", [], id, typeof(FakeStatefulTool)))];

        public IReadOnlyList<Category> GetCategories() => [];

        public IReadOnlyList<ToolDescriptor> GetTools() => _tools;

        public IReadOnlyList<ToolDescriptor> GetToolsByCategory(string categoryId) => _tools;

        public IReadOnlyList<ToolDescriptor> Search(string query)
            => string.IsNullOrWhiteSpace(query)
                ? _tools
                : [.. _tools.Where(t => t.Id.Contains(query.Trim(), StringComparison.OrdinalIgnoreCase))];

        public IReadOnlyList<ToolDescriptor> GetAllTools() => _tools;

        public ToolDescriptor? FindTool(string toolId) => _tools.FirstOrDefault(t => t.Id == toolId);

        public NavigationTree GetNavigationTree() => new([]);
    }
}
