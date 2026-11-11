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
        if (!_subscribed)
        {
            _progress.ProgressChanged += (_, _) => Refresh();
            _subscribed = true;
            RestoredCount = _session.RestoredCount;
            IsRestoredNoticeVisible = RestoredCount > 0;
        }

        Refresh();
    }

    public override void OnNavigatedTo(object? parameter)
    {
        base.OnNavigatedTo(parameter);
        Refresh();
    }

    [RelayCommand]
    private void Resume(ContinueSolvingItem item)
    {
        _session.Open(item.Code);
        _navigation.Navigate<WorkbenchViewModel>();
    }

    [RelayCommand]
    private void DismissRestoredNotice() => IsRestoredNoticeVisible = false;

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
                string.Format(CultureInfo.CurrentCulture, _localizer["Workbench_ScratchpadLines"].Value, p.ScratchpadLineCount),
                FormatEdited(now - p.LastEdited),
                p.IsSolved));
        }

        HasItems = Items.Count > 0;
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
