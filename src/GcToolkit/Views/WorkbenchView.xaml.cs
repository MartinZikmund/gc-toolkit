using GcToolkit.Core.Navigation;
using GcToolkit.Core.ViewModels;
using GcToolkit.Core.Workbench;

namespace GcToolkit.Views;

[NavigationInfo(NavigationSection.Workbench)]
public partial class WorkbenchViewBase : ViewBase<WorkbenchViewModel> { }

public sealed partial class WorkbenchView : WorkbenchViewBase
{
    public WorkbenchView()
    {
        this.InitializeComponent();
#if HAS_UNO
        Split.RegisterPropertyChangedCallback(SplitView.OpenPaneLengthProperty, (_, _) => RefreshInlinePane());
#endif
    }

#if HAS_UNO
    // Uno does not re-run the SplitView display state when OpenPaneLength changes (WinUI does), so an
    // open inline pane keeps its old column width while dragged. unoplatform/uno#22037
    private void RefreshInlinePane()
    {
        // Bounce through the overlay state: it keeps the pane (and the focused resizer) visible.
        if (Split.IsPaneOpen && Split.DisplayMode == SplitViewDisplayMode.Inline && Split.PanePlacement == SplitViewPanePlacement.Right)
        {
            VisualStateManager.GoToState(Split, "OpenOverlayRight", false);
            VisualStateManager.GoToState(Split, "OpenInlineRight", false);
        }
    }
#endif

    public static SplitViewDisplayMode PaneMode(bool inline) => inline ? SplitViewDisplayMode.Inline : SplitViewDisplayMode.Overlay;

    public static GridLength ToolRowHeight(bool visible) => visible ? new(1, GridUnitType.Star) : new(0);

    // Phones show the scratchpad as a whole section; elsewhere it is a resizable panel under the tools.
    public static GridLength DockRowHeight(bool visible, bool compact, double height)
        => !visible ? new(0) : compact ? new(1, GridUnitType.Star) : new(height);

    public static Visibility ShowEmptyTools(bool toolTabsVisible, bool hasTools)
        => toolTabsVisible && !hasTools ? Visibility.Visible : Visibility.Collapsed;

    public static Visibility Not(bool value) => value ? Visibility.Collapsed : Visibility.Visible;

    // The only layout input the view supplies; the view model decides the mode from it.
    private void OnSizeChanged(object sender, SizeChangedEventArgs e) => ViewModel?.UpdateAvailableWidth(e.NewSize.Width);

    // The pane sits on the right and the panel at the bottom, so dragging left/up makes them bigger.
    private void OnPaneResized(object? sender, double delta)
    {
        if (ViewModel is { } viewModel)
        {
            viewModel.ToolPaneWidth -= delta;
        }
    }

    private void OnDockResized(object? sender, double delta)
    {
        if (ViewModel is { } viewModel)
        {
            viewModel.DockHeight -= delta;
        }
    }

    private void OnCacheTabCloseRequested(TabView sender, TabViewTabCloseRequestedEventArgs args)
    {
        if (args.Item is WorkbenchCacheTab cache)
        {
            ViewModel?.CloseCache(cache);
        }
    }

    private void OnToolTabCloseRequested(TabView sender, TabViewTabCloseRequestedEventArgs args)
    {
        if (args.Item is WorkbenchToolTab tool)
        {
            ViewModel?.CloseTool(tool);
        }
    }

    private void OnToolPickerOpening(object? sender, object e) => ViewModel?.RefreshToolPicker();

    private void OnToolPicked(object sender, ItemClickEventArgs e)
    {
        if (e.ClickedItem is ToolPickerItem item)
        {
            ToolPickerFlyout.Hide();
            ViewModel?.AddTool(item);
        }
    }

    private void OnOpenCacheClick(object sender, RoutedEventArgs e)
    {
        OpenCacheFlyout.Hide();
        CacheSwitcherFlyout.Hide();
    }

    private void OnCacheSwitcherItemClick(object sender, ItemClickEventArgs e)
    {
        if (e.ClickedItem is WorkbenchCacheTab cache && ViewModel is { } viewModel)
        {
            viewModel.ActiveCache = cache;
            CacheSwitcherFlyout.Hide();
        }
    }

    private void OnPhoneSelectorChanged(SelectorBar sender, SelectorBarSelectionChangedEventArgs args)
    {
        if (ViewModel is { } viewModel && sender.SelectedItem is { } item)
        {
            viewModel.PhoneSection = (WorkbenchPhoneSection)sender.Items.IndexOf(item);
        }
    }

    private void OnDockSelectorChanged(SelectorBar sender, SelectorBarSelectionChangedEventArgs args)
    {
        if (ViewModel is { } viewModel && sender.SelectedItem is { } item)
        {
            viewModel.DockSection = (WorkbenchDockSection)sender.Items.IndexOf(item);
        }
    }
}
