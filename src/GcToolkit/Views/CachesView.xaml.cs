using System.ComponentModel;
using System.Globalization;
using GcToolkit.Core.Geocaches;
using GcToolkit.Core.Navigation;
using GcToolkit.Core.ViewModels;
using GcToolkit.Services.Localization;
using Microsoft.UI.Xaml.Media;

namespace GcToolkit.Views;

[NavigationInfo(NavigationSection.Caches)]
public partial class CachesViewBase : ViewBase<CachesViewModel> { }

public sealed partial class CachesView : CachesViewBase
{
    // Page width where the detail pane sits beside the list instead of under it.
    private const double WideBreakpoint = 760;
    private const double LoadMoreDistance = 800;
    private const int MaxIssuesShown = 50;

    private ScrollViewer? _listScroller;
    private bool _isWide = true;

    public CachesView()
    {
        InitializeComponent();
        Unloaded += OnViewUnloaded;
    }

    #region Layout

    private void OnPageSizeChanged(object sender, SizeChangedEventArgs e) => ApplyLayout(e.NewSize.Width);

    private void ApplyLayout(double width)
    {
        _isWide = width >= WideBreakpoint;

        DetailColumn.Width = _isWide ? new GridLength(360) : new GridLength(0);
        DetailRow.Height = GridLength.Auto;
        Grid.SetColumn(DetailPanel, _isWide ? 1 : 0);
        Grid.SetRow(DetailPanel, _isWide ? 0 : 1);
        ContentGrid.ColumnSpacing = _isWide ? 16 : 0;

        // Narrow: the list picker drops under the search box so nothing is squeezed.
        Grid.SetRow(SearchBox, 0);
        Grid.SetColumnSpan(SearchBox, _isWide ? 1 : 5);
        Grid.SetRow(ListCombo, _isWide ? 0 : 1);
        Grid.SetColumn(ListCombo, _isWide ? 1 : 0);
        ListCombo.HorizontalAlignment = _isWide ? HorizontalAlignment.Left : HorizontalAlignment.Stretch;
        Grid.SetRow(SortCombo, _isWide ? 0 : 1);
        Grid.SetRow(SortToggle, _isWide ? 0 : 1);
        Grid.SetRow(FiltersButton, _isWide ? 0 : 1);

        UpdateDetailVisibility();
    }

    private void UpdateDetailVisibility()
        => DetailPanel.Visibility = _isWide || ViewModel?.Detail is not null ? Visibility.Visible : Visibility.Collapsed;

    #endregion

    #region List behaviour

    private void OnListLoaded(object sender, RoutedEventArgs e)
    {
        if (ViewModel is null)
        {
            return;
        }

        // The page is cached, so Loaded runs again on every return.
        ViewModel.PropertyChanged -= OnViewModelPropertyChanged;
        ViewModel.PropertyChanged += OnViewModelPropertyChanged;

        if (_listScroller is null)
        {
            _listScroller = FindDescendant<ScrollViewer>(CacheList);
            if (_listScroller is not null)
            {
                _listScroller.ViewChanged += OnListViewChanged;
            }
        }

        ApplyLayout(ActualWidth);
    }

    private void OnViewUnloaded(object sender, RoutedEventArgs e)
    {
        if (ViewModel is not null)
        {
            ViewModel.PropertyChanged -= OnViewModelPropertyChanged;
        }
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(CachesViewModel.Detail))
        {
            UpdateDetailVisibility();
        }
    }

    private void OnListViewChanged(object? sender, ScrollViewerViewChangedEventArgs e)
    {
        if (_listScroller is { } scroller
            && scroller.ExtentHeight - scroller.VerticalOffset - scroller.ViewportHeight < LoadMoreDistance)
        {
            _ = ViewModel?.LoadMoreAsync();
        }
    }

    private void OnSelectionChanged(object sender, SelectionChangedEventArgs e)
        => ViewModel?.SetSelection([.. CacheList.SelectedItems.OfType<GeocacheSummary>()]);

    private void OnMoveFlyoutOpening(object? sender, object e)
    {
        MoveFlyout.Items.Clear();
        if (ViewModel is null || ViewModel.Lists.Count == 0)
        {
            MoveFlyout.Items.Add(new MenuFlyoutItem { Text = Localizer.Instance["CachesNoListsYet"], IsEnabled = false });
            return;
        }

        foreach (var list in ViewModel.Lists)
        {
            MenuFlyoutItem item = new() { Text = list.Name };
            item.Click += (_, _) => ViewModel.MoveSelectedToListCommand.Execute(list);
            MoveFlyout.Items.Add(item);
        }
    }

    private static T? FindDescendant<T>(DependencyObject root)
        where T : DependencyObject
    {
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
        {
            var child = VisualTreeHelper.GetChild(root, i);
            if (child is T match)
            {
                return match;
            }

            if (FindDescendant<T>(child) is { } nested)
            {
                return nested;
            }
        }

        return null;
    }

    #endregion

    #region Binding helpers

    public static Visibility ToVisibility(bool value) => value ? Visibility.Visible : Visibility.Collapsed;

    public static Visibility VisibleWhenNull(object? value) => value is null ? Visibility.Visible : Visibility.Collapsed;

    public static Visibility VisibleWhenNotNull(object? value) => value is null ? Visibility.Collapsed : Visibility.Visible;

    public static bool IsNotNull(object? value) => value is not null;

    public static InfoBarSeverity ToSeverity(CachesNotice? notice) => notice?.Severity switch
    {
        CachesNoticeSeverity.Warning => InfoBarSeverity.Warning,
        CachesNoticeSeverity.Error => InfoBarSeverity.Error,
        _ => InfoBarSeverity.Success,
    };

    public static string NoticeTitle(CachesNotice? notice)
        => notice?.Summary is null ? "" : Localizer.Instance["CachesImportTitle"];

    public static IReadOnlyList<string> NoticeIssues(CachesNotice? notice)
        => notice?.Summary is { } summary
            ? [.. summary.Errors.Concat(summary.Issues).Take(MaxIssuesShown).Select(i => $"{i.Subject}: {i.Message}")]
            : [];

    // Placeholder glyphs from Segoe Fluent Icons until real cache-type icons land (#412).
    public static string TypeGlyph(GeocacheType type) => type switch
    {
        GeocacheType.Traditional => "",
        GeocacheType.Multi => "",
        GeocacheType.Mystery => "",
        GeocacheType.Letterbox => "",
        GeocacheType.Wherigo => "",
        GeocacheType.Earth => "",
        GeocacheType.Virtual => "",
        GeocacheType.Webcam => "",
        GeocacheType.Event or GeocacheType.MegaEvent or GeocacheType.GigaEvent or GeocacheType.CacheInTrashOutEvent
            or GeocacheType.CommunityCelebrationEvent or GeocacheType.GeocachingHqEvent => "",
        GeocacheType.AdventureLab => "",
        _ => "",
    };

    public static string Subtitle(GeocacheCode code, double? difficulty, double? terrain, GeocacheSize size)
        => string.Format(
            CultureInfo.CurrentCulture,
            Localizer.Instance["CachesRowSubtitle"],
            code,
            Rating(difficulty),
            Rating(terrain),
            Localizer.Instance[$"GeocacheSize_{size}"]);

    public static string DetailSubtitle(GeocacheSummary? cache)
        => cache is null
            ? ""
            : $"{Localizer.Instance[$"GeocacheType_{cache.Type}"]} · {Subtitle(cache.Code, cache.Difficulty, cache.Terrain, cache.Size)}";

    public static string DetailHint(int selectedCount)
        => selectedCount > 1
            ? string.Format(CultureInfo.CurrentCulture, Localizer.Instance["CachesDetailMany"], selectedCount)
            : Localizer.Instance["CachesDetailEmpty"];

    public static Visibility StatusBadgeVisibility(GeocacheStatus status)
        => status == GeocacheStatus.Available ? Visibility.Collapsed : Visibility.Visible;

    public static string StatusText(GeocacheStatus status) => Localizer.Instance[$"GeocacheStatus_{status}"];

    private static string Rating(double? value) => value?.ToString("0.#", CultureInfo.CurrentCulture) ?? "?";

    #endregion
}
