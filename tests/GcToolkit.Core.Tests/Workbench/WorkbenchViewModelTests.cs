using CommunityToolkit.Mvvm.Messaging;
using GcToolkit.Core.Catalog;
using GcToolkit.Core.Geocaches;
using GcToolkit.Core.Navigation;
using GcToolkit.Core.Tests.Fakes;
using GcToolkit.Core.ViewModels;
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
    private WorkbenchSessionService _session = null!;

    [TestInitialize]
    public void Setup()
        => _session = new WorkbenchSessionService(
            InMemoryPreferences.TrimmedHead(),
            _messenger);

    private WorkbenchViewModel Create(double width = 1200)
    {
        var localizer = new FakeStringLocalizer(new Dictionary<string, string>
        {
            ["Name_Caesar"] = "Caesar cipher",
            ["Name_Morse"] = "Morse code",
            ["Name_Roman"] = "Roman numerals",
        });
        var vm = new WorkbenchViewModel(
            _session,
            new FilteringCatalog("Caesar", "Morse", "Roman"),
            localizer,
            new ScratchpadViewModel(_scratchpads, localizer, _time),
            _messenger);
        vm.ViewCreated();
        vm.UpdateAvailableWidth(width);
        return vm;
    }

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
    public void PhoneSection_Scratchpad_ShowsOnlyTheScratchpad()
    {
        var vm = Create(400);

        vm.PhoneSection = WorkbenchPhoneSection.Scratchpad;

        Assert.IsTrue(vm.IsToolPaneOpen);
        Assert.IsFalse(vm.IsToolTabsVisible);
        Assert.IsTrue(vm.IsDockVisible);
        Assert.IsTrue(vm.IsScratchpadVisible);
        Assert.IsFalse(vm.IsVariablesVisible);
        Assert.IsFalse(vm.IsDockSelectorVisible);
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
        Assert.AreEqual(typeof(object), vm.ActiveCache.SelectedTool.ViewModelType);
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

    // Scratchpad

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

    private sealed class FilteringCatalog(params string[] toolIds) : ICatalogService
    {
        private readonly IReadOnlyList<ToolDescriptor> _tools =
            [.. toolIds.Select(id => new ToolDescriptor(id, $"Name_{id}", "cat", [], id, typeof(object)))];

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
