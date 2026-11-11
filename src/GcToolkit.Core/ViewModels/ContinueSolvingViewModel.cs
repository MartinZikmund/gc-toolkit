using System.Collections.ObjectModel;
using System.Globalization;
using GcToolkit.Core.Geocaches;
using GcToolkit.Core.Services;
using GcToolkit.Core.Workbench;

namespace GcToolkit.Core.ViewModels;

public sealed record ContinueSolvingItem(
    GeocacheCode Code,
    string Name,
    string VariablesText,
    string ScratchpadText,
    string EditedText,
    bool IsSolved);

/// <summary>Home's "Continue solving" list: caches with work in progress, newest edit first.</summary>
public partial class ContinueSolvingViewModel : ViewModelBase
{
    private readonly IWorkbenchProgressProvider _progress;
    private readonly IWorkbenchSessionService _session;
    private readonly INavigationService _navigation;
    private readonly IStringLocalizer _localizer;
    private readonly TimeProvider _time;
    private readonly SynchronizationContext? _uiContext = SynchronizationContext.Current;
    private bool _subscribed;

    public ContinueSolvingViewModel(
        IWorkbenchProgressProvider progress,
        IWorkbenchSessionService session,
        INavigationService navigation,
        IStringLocalizer localizer,
        TimeProvider time)
    {
        _progress = progress;
        _session = session;
        _navigation = navigation;
        _localizer = localizer;
        _time = time;
    }

    public ObservableCollection<ContinueSolvingItem> Items { get; } = [];

    [ObservableProperty]
    public partial bool HasItems { get; set; }

    [ObservableProperty]
    public partial int RestoredCount { get; set; }

    [ObservableProperty]
    public partial bool IsRestoredNoticeVisible { get; set; }

    public override void ViewCreated()
    {
        RestoredCount = _session.RestoredCount;
        IsRestoredNoticeVisible = RestoredCount > 0;
        Refresh();
    }

    // The provider is a singleton, so hold its event only while the view is on screen.
    public override void ViewLoaded()
    {
        if (!_subscribed)
        {
            _progress.ProgressChanged += OnProgressChanged;
            _subscribed = true;
        }

        Refresh();
    }

    public override void ViewUnloaded()
    {
        _progress.ProgressChanged -= OnProgressChanged;
        _subscribed = false;
    }

    [RelayCommand]
    private void Resume(ContinueSolvingItem item)
    {
        _session.Open(item.Code);
        _navigation.Navigate<WorkbenchViewModel>();
    }

    [RelayCommand]
    private void DismissRestoredNotice() => IsRestoredNoticeVisible = false;

    private void OnProgressChanged(object? sender, EventArgs e)
    {
        if (_uiContext is null || SynchronizationContext.Current == _uiContext)
        {
            Refresh();
            return;
        }

        _uiContext.Post(_ => Refresh(), null);
    }

    private void Refresh()
    {
        var now = _time.GetUtcNow();
        Items.Clear();
        foreach (var p in _progress.GetProgress().Where(p => p.IsInProgress).OrderByDescending(p => p.LastEdited))
        {
            Items.Add(new ContinueSolvingItem(
                p.Code,
                p.Name,
                string.Format(CultureInfo.CurrentCulture, _localizer["Workbench_VariablesProgress"].Value, p.VariablesFilled, p.VariablesTotal),
                FormatScratchpadLines(p.ScratchpadLineCount),
                FormatEdited(now - p.LastEdited),
                p.IsSolved));
        }

        HasItems = Items.Count > 0;
    }

    // 1 / 2–4 / 5+ covers both English and Czech.
    private string FormatScratchpadLines(int count)
    {
        var key = count switch
        {
            1 => "Workbench_ScratchpadLinesOne",
            >= 2 and <= 4 => "Workbench_ScratchpadLinesFew",
            _ => "Workbench_ScratchpadLinesMany",
        };

        return Format(key, count);
    }

    private string FormatEdited(TimeSpan age)
    {
        if (age < TimeSpan.FromMinutes(1))
        {
            return _localizer["Workbench_EditedJustNow"].Value;
        }

        if (age < TimeSpan.FromHours(1))
        {
            return Format("Workbench_EditedMinutesAgo", (int)age.TotalMinutes);
        }

        if (age < TimeSpan.FromDays(1))
        {
            return Format("Workbench_EditedHoursAgo", (int)age.TotalHours);
        }

        return age < TimeSpan.FromDays(2)
            ? _localizer["Workbench_EditedYesterday"].Value
            : Format("Workbench_EditedDaysAgo", (int)age.TotalDays);
    }

    private string Format(string key, int value)
        => string.Format(CultureInfo.CurrentCulture, _localizer[key].Value, value);
}
