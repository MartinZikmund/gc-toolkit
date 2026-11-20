using GcToolkit.Core.Navigation;
using GcToolkit.Core.ViewModels;
using System.Globalization;
using GcToolkit.Controls;
using GcToolkit.Core.Workbench;
using GcToolkit.Helpers;
using GcToolkit.Services.Localization;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Windows.System;

namespace GcToolkit.Views;

[NavigationInfo(NavigationSection.Workbench)]
public partial class WorkbenchViewBase : ViewBase<WorkbenchViewModel> { }

public sealed partial class WorkbenchView : WorkbenchViewBase
{
#if __ANDROID__
    private readonly HingeWatcher _hinge;
#endif

    public WorkbenchView()
    {
        this.InitializeComponent();
#if HAS_UNO
        Split.RegisterPropertyChangedCallback(SplitView.OpenPaneLengthProperty, (_, _) => RefreshInlinePane());
#endif
#if __ANDROID__
        _hinge = new(this, span => ViewModel?.UpdateSpan(span));
        Loaded += (_, _) => _hinge.Start();
        Unloaded += (_, _) => _hinge.Stop();
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

    // Keeps the listing off the hinge when the window spans one.
    public static Thickness HingeGap(double hingeWidth) => new(0, 0, hingeWidth, 0);

    public static Visibility ShowWhenText(string text) => string.IsNullOrEmpty(text) ? Visibility.Collapsed : Visibility.Visible;

    public static Visibility ShowResultBar(bool toolTabsVisible, bool hasResult)
        => toolTabsVisible && hasResult ? Visibility.Visible : Visibility.Collapsed;

    public static string ToVariableLabel(string? target)
        => target is null
            ? Localizer.Instance["Workbench_ToVariables"]
            : string.Format(CultureInfo.CurrentCulture, Localizer.Instance["Workbench_ToVariableFormat"], target);

    public static Visibility ShowSelectionBar(bool compact, bool hasSelection, bool listingVisible)
        => compact && hasSelection && listingVisible ? Visibility.Visible : Visibility.Collapsed;

    private CommandBarFlyout? _selectionFlyout;

    private void OnListingSelectionChanged(object? sender, ListingSelectionEventArgs e) => OnSelection(e.Text, ListingPanel, e.Anchor);

    private void OnPastedSelectionChanged(object sender, RoutedEventArgs e)
    {
        if (sender is TextBox box)
        {
            OnSelection(box.SelectedText, box, new(box.ActualWidth / 2, 0));
        }
    }

    // Wide and medium windows get a flyout above the selection; phones use the bottom bar in the XAML.
    private void OnSelection(string text, FrameworkElement target, Windows.Foundation.Point anchor)
    {
        if (ViewModel is not { } viewModel)
        {
            return;
        }

        viewModel.SelectedText = text;
        _selectionFlyout?.Hide();
        if (!viewModel.HasSelection || viewModel.IsCompact)
        {
            return;
        }

        _selectionFlyout = BuildSelectionFlyout(viewModel);
        _selectionFlyout.ShowAt(target, new FlyoutShowOptions
        {
            Position = anchor,
            Placement = FlyoutPlacementMode.Top,
            ShowMode = FlyoutShowMode.Transient,
        });
    }

    private CommandBarFlyout BuildSelectionFlyout(WorkbenchViewModel viewModel)
    {
        CommandBarFlyout flyout = new();
        if (viewModel.SuggestedTool is { } suggested)
        {
            flyout.PrimaryCommands.Add(SendButton(viewModel, suggested, ToolIcons.CreateIcon(suggested.IconKey)));
        }

        AppBarButton scratchpad = new()
        {
            Label = Localizer.Instance["Workbench_AddToScratchpad"],
            Icon = new FontIcon { Glyph = "\uE70B" },
            Command = viewModel.AddSelectionToScratchpadCommand,
        };
        ToolTipService.SetToolTip(scratchpad, scratchpad.Label);
        flyout.PrimaryCommands.Add(scratchpad);

        foreach (var tool in viewModel.SelectionSuggestions)
        {
            flyout.SecondaryCommands.Add(SendButton(viewModel, tool, ToolIcons.NavIcon(tool.IconKey)));
        }

        AppBarButton other = new() { Label = Localizer.Instance["Workbench_SendToOther"] };
        other.Click += (_, _) => SendToOtherTool(ListingPanel);
        flyout.SecondaryCommands.Add(other);
        return flyout;
    }

    private AppBarButton SendButton(WorkbenchViewModel viewModel, ToolPickerItem tool, IconElement? icon)
    {
        AppBarButton button = new()
        {
            Label = string.Format(CultureInfo.CurrentCulture, Localizer.Instance["Workbench_SendToFormat"], tool.Name),
            Icon = icon,
        };
        ToolTipService.SetToolTip(button, button.Label);
        button.Click += (_, _) =>
        {
            _selectionFlyout?.Hide();
            viewModel.SendSelectionTo(tool);
        };
        return button;
    }

    private void OnSendToOtherClick(object sender, RoutedEventArgs e) => SendToOtherTool(SendToOtherButton);

    private void SendToOtherTool(FrameworkElement target)
    {
        _selectionFlyout?.Hide();
        ViewModel?.BeginSendToOtherTool();
        ViewModel?.RefreshToolPicker();
        ToolPickerFlyout.ShowAt(target);
    }

    // The only layout input the view supplies; the view model decides the mode from it.
    private void OnSizeChanged(object sender, SizeChangedEventArgs e)
    {
        ViewModel?.UpdateAvailableWidth(e.NewSize.Width);
#if __ANDROID__
        _hinge.Refresh();
#endif
    }

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

    private void OnOpenCacheQuerySubmitted(AutoSuggestBox sender, AutoSuggestBoxQuerySubmittedEventArgs args)
    {
        if (ViewModel is not { } vm)
        {
            return;
        }

        if (args.ChosenSuggestion is CacheSuggestion suggestion)
        {
            vm.OpenSuggestedCache(suggestion);
        }
        else if (vm.OpenCacheCommand.CanExecute(null))
        {
            vm.OpenCacheCommand.Execute(null);
        }
        else
        {
            return;
        }

        OpenCacheFlyout.Hide();
    }

    private void OnPopOutClick(object sender, RoutedEventArgs e)
    {
        if (ViewModel?.ActiveCache?.SelectedTool is { } tool)
        {
            ViewModel.PopOutCommand.Execute(tool);
        }
    }

    private void OnBringBackClick(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { Tag: WorkbenchToolTab tool })
        {
            ViewModel?.BringBackCommand.Execute(tool);
        }
    }

    private void OnVariablePicked(object sender, ItemClickEventArgs e)
    {
        if (e.ClickedItem is string name)
        {
            VariablePickerFlyout.Hide();
            ViewModel?.SendResultToVariablesCommand.Execute(name);
        }
    }

    private void OnToolPicked(object sender, ItemClickEventArgs e)
    {
        if (e.ClickedItem is ToolPickerItem item)
        {
            ToolPickerFlyout.Hide();
            ViewModel?.PickTool(item);
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

    private void OnMoveFocusInvoked(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
    {
        if (ViewModel is not { } viewModel)
        {
            return;
        }

        args.Handled = true;
        var target = viewModel.MoveFocus(FocusedPane(), sender.Modifiers.HasFlag(VirtualKeyModifiers.Shift));

        // The target may only become visible on the next layout pass (phone section, overlay pane).
        DispatcherQueue.TryEnqueue(() => FocusPane(target));
    }

    private WorkbenchPane FocusedPane()
    {
        var element = FocusManager.GetFocusedElement(XamlRoot) as DependencyObject;
        while (element is not null)
        {
            if (element == DockPanel)
            {
                return WorkbenchPane.Dock;
            }

            if (element == ToolPaneContent)
            {
                return WorkbenchPane.Tools;
            }

            element = VisualTreeHelper.GetParent(element);
        }

        return WorkbenchPane.Listing;
    }

    private void FocusPane(WorkbenchPane pane)
    {
        DependencyObject container = pane switch
        {
            WorkbenchPane.Tools => ToolTabs,
            WorkbenchPane.Dock => DockPanel,
            _ => ListingPanel,
        };

        if (FocusManager.FindFirstFocusableElement(container) is Control control)
        {
            control.Focus(FocusState.Keyboard);
        }
        else if (container is Control fallback)
        {
            fallback.Focus(FocusState.Keyboard);
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
