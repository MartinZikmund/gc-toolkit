using System.ComponentModel;
using System.Diagnostics;
using System.Windows.Input;
using GcToolkit.Services.Localization;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace GcToolkit.Controls;

/// <summary>
/// The shared scaffold for every tool page: scroller, width cap, identity header and the pinned
/// action bar. Lives as the single child of a <c>ViewBase&lt;T&gt;</c>-derived page — Uno's
/// <c>UserControl</c> (hence <c>Page</c>) parents Content via AddChild and returns a null default
/// style key, so a Page ControlTemplate is not an option on any head.
/// </summary>
/// <remarks>
/// DefaultStyleKey is deliberately not set. ToolPage inherits ContentControl's key, so if the
/// implicit style ever fails to resolve the page degrades to a bare content presenter (tool
/// content visible, chrome missing) instead of rendering blank.
/// </remarks>
public sealed partial class ToolPage : ContentControl
{
    private const double StandardWidth = 760;
    private const double WideWidth = 1024;

    // Below this the page drops to compact padding and a smaller header (a phone, or the workbench tool pane).
    private const double CompactWidth = 640;

    private FrameworkElement? _header;

    public ToolPage()
    {
        IsTabStop = false;
        HorizontalContentAlignment = HorizontalAlignment.Stretch;
        VerticalContentAlignment = VerticalAlignment.Stretch;
        Loaded += OnLoaded;
        SizeChanged += (_, e) => UpdateWidthState(e.NewSize.Width);
    }

    // ---- Tool -----------------------------------------------------------------

    public static readonly DependencyProperty ToolProperty = DependencyProperty.Register(
        nameof(Tool), typeof(ToolViewModelBase), typeof(ToolPage),
        new PropertyMetadata(null, OnToolChanged));

    /// <summary>The tool view model. Always set as <c>Tool="{x:Bind ViewModel}"</c>.</summary>
    public ToolViewModelBase? Tool
    {
        get => (ToolViewModelBase?)GetValue(ToolProperty);
        set => SetValue(ToolProperty, value);
    }

    private static void OnToolChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var page = (ToolPage)d;

        if (e.OldValue is ToolViewModelBase old)
        {
            old.PropertyChanged -= page.OnToolPropertyChanged;
        }

        if (e.NewValue is ToolViewModelBase added)
        {
            added.PropertyChanged += page.OnToolPropertyChanged;
        }

        page.PushToolToHeader();
        page.UpdateFavoriteLabel();
    }

    private void OnToolPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is null or nameof(ToolViewModelBase.IsFavorite))
        {
            UpdateFavoriteLabel();
        }
    }

    // ---- ContentWidth / ContentMaxWidth ---------------------------------------

    public static readonly DependencyProperty ContentWidthProperty = DependencyProperty.Register(
        nameof(ContentWidth), typeof(ToolContentWidth), typeof(ToolPage),
        new PropertyMetadata(ToolContentWidth.Standard, OnContentWidthChanged));

    /// <summary>Picks the width cap: <c>Wide</c> for table/chart tools, <c>Standard</c> everywhere else.</summary>
    public ToolContentWidth ContentWidth
    {
        get => (ToolContentWidth)GetValue(ContentWidthProperty);
        set => SetValue(ContentWidthProperty, value);
    }

    private static void OnContentWidthChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        => ((ToolPage)d).ContentMaxWidth =
            (ToolContentWidth)e.NewValue == ToolContentWidth.Wide ? WideWidth : StandardWidth;

    public static readonly DependencyProperty ContentMaxWidthProperty = DependencyProperty.Register(
        nameof(ContentMaxWidth), typeof(double), typeof(ToolPage),
        new PropertyMetadata(StandardWidth));

    /// <summary>Written by <see cref="ContentWidth"/>. Set directly only for a width neither enum value covers.</summary>
    public double ContentMaxWidth
    {
        get => (double)GetValue(ContentMaxWidthProperty);
        set => SetValue(ContentMaxWidthProperty, value);
    }

    // ---- Action bar ------------------------------------------------------------

    public static readonly DependencyProperty CopyCommandProperty = DependencyProperty.Register(
        nameof(CopyCommand), typeof(ICommand), typeof(ToolPage),
        new PropertyMetadata(null, OnActionChanged));

    /// <summary>Copies the primary output. Null collapses the Copy button.</summary>
    public ICommand? CopyCommand
    {
        get => (ICommand?)GetValue(CopyCommandProperty);
        set => SetValue(CopyCommandProperty, value);
    }

    public static readonly DependencyProperty ShareCommandProperty = DependencyProperty.Register(
        nameof(ShareCommand), typeof(ICommand), typeof(ToolPage),
        new PropertyMetadata(null, OnActionChanged));

    /// <summary>Shares the primary output. Null collapses the Share button.</summary>
    public ICommand? ShareCommand
    {
        get => (ICommand?)GetValue(ShareCommandProperty);
        set => SetValue(ShareCommandProperty, value);
    }

    public static readonly DependencyProperty ActionsProperty = DependencyProperty.Register(
        nameof(Actions), typeof(object), typeof(ToolPage),
        new PropertyMetadata(null, OnActionChanged));

    /// <summary>Tool-specific buttons, rendered after Copy and Share so the order is identical on every screen.</summary>
    public object? Actions
    {
        get => GetValue(ActionsProperty);
        set => SetValue(ActionsProperty, value);
    }

    private static void OnActionChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        => ((ToolPage)d).UpdateActionBarVisibility();

    // ---- Template-only computed properties -------------------------------------
    // Typed Visibility, not bool, so the template needs only {TemplateBinding}: a bool would
    // force a RelativeSource binding to carry the converter — the least-travelled Uno path.

    public static readonly DependencyProperty FavoriteLabelProperty = DependencyProperty.Register(
        nameof(FavoriteLabel), typeof(string), typeof(ToolPage), new PropertyMetadata(string.Empty));

    /// <summary>Template-only, never set from a view. Flips between AddToFavorites and RemoveFromFavorites.</summary>
    public string FavoriteLabel
    {
        get => (string)GetValue(FavoriteLabelProperty);
        private set => SetValue(FavoriteLabelProperty, value);
    }

    public static readonly DependencyProperty CopyVisibilityProperty = DependencyProperty.Register(
        nameof(CopyVisibility), typeof(Visibility), typeof(ToolPage),
        new PropertyMetadata(Visibility.Collapsed));

    /// <summary>Template-only. Never set from a view.</summary>
    public Visibility CopyVisibility
    {
        get => (Visibility)GetValue(CopyVisibilityProperty);
        private set => SetValue(CopyVisibilityProperty, value);
    }

    public static readonly DependencyProperty ShareVisibilityProperty = DependencyProperty.Register(
        nameof(ShareVisibility), typeof(Visibility), typeof(ToolPage),
        new PropertyMetadata(Visibility.Collapsed));

    /// <summary>Template-only. Never set from a view.</summary>
    public Visibility ShareVisibility
    {
        get => (Visibility)GetValue(ShareVisibilityProperty);
        private set => SetValue(ShareVisibilityProperty, value);
    }

    public static readonly DependencyProperty ActionsVisibilityProperty = DependencyProperty.Register(
        nameof(ActionsVisibility), typeof(Visibility), typeof(ToolPage),
        new PropertyMetadata(Visibility.Collapsed));

    /// <summary>Template-only. Never set from a view.</summary>
    public Visibility ActionsVisibility
    {
        get => (Visibility)GetValue(ActionsVisibilityProperty);
        private set => SetValue(ActionsVisibilityProperty, value);
    }

    public static readonly DependencyProperty ActionBarVisibilityProperty = DependencyProperty.Register(
        nameof(ActionBarVisibility), typeof(Visibility), typeof(ToolPage),
        new PropertyMetadata(Visibility.Collapsed));

    /// <summary>Template-only. Never set from a view.</summary>
    public Visibility ActionBarVisibility
    {
        get => (Visibility)GetValue(ActionBarVisibilityProperty);
        private set => SetValue(ActionBarVisibilityProperty, value);
    }

    // ---- Template plumbing -----------------------------------------------------

    protected override void OnApplyTemplate()
    {
        base.OnApplyTemplate();
        _header = GetTemplateChild("PART_Header") as FrameworkElement;
        PushToolToHeader();
        UpdateFavoriteLabel();
        UpdateActionBarVisibility();
        UpdateWidthState(ActualWidth);
    }

    private void UpdateWidthState(double width)
        => VisualStateManager.GoToState(this, width > 0 && width < CompactWidth ? "Compact" : "Regular", false);

    // ToolHostView resolves its view model from the navigation parameter, which can land either
    // side of OnApplyTemplate — so push from both ends and keep the push idempotent.
    private void PushToolToHeader()
    {
        if (_header is not null && Tool is not null)
        {
            _header.DataContext = Tool;
        }
    }

    private void UpdateFavoriteLabel()
        => FavoriteLabel = Localizer.Instance[
            Tool?.IsFavorite == true ? "RemoveFromFavorites" : "AddToFavorites"];

    private void UpdateActionBarVisibility()
    {
        CopyVisibility = CopyCommand is null ? Visibility.Collapsed : Visibility.Visible;
        ShareVisibility = ShareCommand is null ? Visibility.Collapsed : Visibility.Visible;
        ActionsVisibility = Actions is null ? Visibility.Collapsed : Visibility.Visible;
        ActionBarVisibility =
            CopyVisibility == Visibility.Visible
            || ShareVisibility == Visibility.Visible
            || ActionsVisibility == Visibility.Visible
                ? Visibility.Visible
                : Visibility.Collapsed;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        // A view that forgot Tool="{x:Bind ViewModel}" renders a blank header with no compile error.
        Debug.Assert(Tool is not null, "ToolPage.Tool is null — the view is missing Tool=\"{x:Bind ViewModel}\".");
        PushToolToHeader();
        UpdateFavoriteLabel();
    }
}
