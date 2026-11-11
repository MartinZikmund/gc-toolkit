using System.Collections.ObjectModel;
using System.Globalization;
using GcToolkit.Core.Geocaches;
using GcToolkit.Core.Geocaches.Gpx;
using GcToolkit.Core.Services;
using GcToolkit.Core.Services.Dialogs;
using GcToolkit.Core.Services.Files;
using Microsoft.Extensions.Localization;

namespace GcToolkit.Core.ViewModels;

/// <summary>One choice in a filter or sort drop-down. A null <paramref name="Value"/> means "any".</summary>
public sealed record FilterOption<T>(string Label, T Value);

public enum CachesNoticeSeverity
{
    Success,
    Warning,
    Error,
}

/// <summary>What an import did, for the message bar above the list.</summary>
public sealed record ImportSummary(
    int Added,
    int Updated,
    int PlainWaypoints,
    IReadOnlyList<GpxImportIssue> Issues,
    IReadOnlyList<GpxImportIssue> Errors);

/// <param name="Summary">Set after an import; the view lists its issues.</param>
public sealed record CachesNotice(CachesNoticeSeverity Severity, string Message, ImportSummary? Summary = null);

/// <summary>
/// Browses the stored caches: search, filters, sort and paging run in the store, so the list stays
/// light however many caches there are. Also owns lists, multi-select actions and GPX import.
/// </summary>
public partial class CachesViewModel : ViewModelBase
{
    private const string GpxExtension = ".gpx";
    private const string ZipExtension = ".zip";

    private static readonly double[] _ratings = [1, 1.5, 2, 2.5, 3, 3.5, 4, 4.5, 5];

    private readonly IGeocacheStore _store;
    private readonly IFilePickerService _filePicker;
    private readonly IConfirmationDialogService _confirmation;
    private readonly ITextPromptService _prompt;
    private readonly IStringLocalizer _localizer;
    private readonly GpxImporter _importer;
    private readonly INavigationService _navigation;

    private CancellationTokenSource? _loadCts;
    private Task _loadTask = Task.CompletedTask;
    private int _generation;
    private bool _suppressReload;
    private bool _hasLoaded;
    private bool _isLoadingMore;

    public CachesViewModel(
        IGeocacheStore store,
        IFilePickerService filePicker,
        IConfirmationDialogService confirmation,
        ITextPromptService prompt,
        IStringLocalizer localizer,
        GpxImporter importer,
        INavigationService navigation)
    {
        _store = store;
        _filePicker = filePicker;
        _confirmation = confirmation;
        _prompt = prompt;
        _localizer = localizer;
        _importer = importer;
        _navigation = navigation;

        PageTitle = _localizer["Caches"];

        TypeOptions = Options<GeocacheType?>(Enum.GetValues<GeocacheType>().Where(t => t != GeocacheType.Unknown).Select(t => (Label(nameof(GeocacheType), t), (GeocacheType?)t)));
        SizeOptions = Options<GeocacheSize?>(Enum.GetValues<GeocacheSize>().Where(s => s != GeocacheSize.Unknown).Select(s => (Label(nameof(GeocacheSize), s), (GeocacheSize?)s)));
        StatusOptions = Options<GeocacheStatus?>(Enum.GetValues<GeocacheStatus>().Select(s => (Label(nameof(GeocacheStatus), s), (GeocacheStatus?)s)));
        RatingOptions = Options<double?>(_ratings.Select(r => (r.ToString("0.#", CultureInfo.CurrentCulture), (double?)r)));
        YesNoOptions = Options<bool?>([(_localizer["CachesYes"], true), (_localizer["CachesNo"], false)]);
        SortOptions =
        [
            new(_localizer["CachesSort_Name"], GeocacheSort.Name),
            new(_localizer["CachesSort_Code"], GeocacheSort.Code),
            new(_localizer["CachesSort_Difficulty"], GeocacheSort.Difficulty),
            new(_localizer["CachesSort_Terrain"], GeocacheSort.Terrain),
            new(_localizer["CachesSort_PlacedOn"], GeocacheSort.PlacedOn),
            new(_localizer["CachesSort_FavoritePoints"], GeocacheSort.FavoritePoints),
        ];
        ListOptions = [AllLists()];

        _suppressReload = true;
        SelectedTypeOption = TypeOptions[0];
        SelectedSizeOption = SizeOptions[0];
        SelectedStatusOption = StatusOptions[0];
        MinDifficultyOption = RatingOptions[0];
        MaxDifficultyOption = RatingOptions[0];
        MinTerrainOption = RatingOptions[0];
        MaxTerrainOption = RatingOptions[0];
        SolvedOption = YesNoOptions[0];
        FoundOption = YesNoOptions[0];
        CorrectedOption = YesNoOptions[0];
        SelectedListOption = ListOptions[0];
        SelectedSortOption = SortOptions[0];
        _suppressReload = false;
    }

    /// <summary>How many caches one query returns. The list asks for the next page when it scrolls near the end.</summary>
    public int PageSize { get; init; } = 100;

    public ObservableCollection<GeocacheSummary> Items { get; } = [];

    public ObservableCollection<GeocacheList> Lists { get; } = [];

    public IReadOnlyList<FilterOption<GeocacheType?>> TypeOptions { get; }

    public IReadOnlyList<FilterOption<GeocacheSize?>> SizeOptions { get; }

    public IReadOnlyList<FilterOption<GeocacheStatus?>> StatusOptions { get; }

    /// <summary>1 to 5 in half steps, shared by the four difficulty and terrain bounds.</summary>
    public IReadOnlyList<FilterOption<double?>> RatingOptions { get; }

    public IReadOnlyList<FilterOption<bool?>> YesNoOptions { get; }

    public IReadOnlyList<FilterOption<GeocacheSort>> SortOptions { get; }

    [ObservableProperty]
    public partial IReadOnlyList<FilterOption<GeocacheList?>> ListOptions { get; set; }

    [ObservableProperty]
    public partial string SearchText { get; set; } = "";

    [ObservableProperty]
    public partial FilterOption<GeocacheType?> SelectedTypeOption { get; set; }

    [ObservableProperty]
    public partial FilterOption<GeocacheSize?> SelectedSizeOption { get; set; }

    [ObservableProperty]
    public partial FilterOption<GeocacheStatus?> SelectedStatusOption { get; set; }

    [ObservableProperty]
    public partial FilterOption<double?> MinDifficultyOption { get; set; }

    [ObservableProperty]
    public partial FilterOption<double?> MaxDifficultyOption { get; set; }

    [ObservableProperty]
    public partial FilterOption<double?> MinTerrainOption { get; set; }

    [ObservableProperty]
    public partial FilterOption<double?> MaxTerrainOption { get; set; }

    [ObservableProperty]
    public partial FilterOption<bool?> SolvedOption { get; set; }

    [ObservableProperty]
    public partial FilterOption<bool?> FoundOption { get; set; }

    [ObservableProperty]
    public partial FilterOption<bool?> CorrectedOption { get; set; }

    [ObservableProperty]
    public partial FilterOption<GeocacheList?> SelectedListOption { get; set; }

    [ObservableProperty]
    public partial FilterOption<GeocacheSort> SelectedSortOption { get; set; }

    [ObservableProperty]
    public partial bool IsDescending { get; set; }

    [ObservableProperty]
    public partial int TotalCount { get; set; }

    [ObservableProperty]
    public partial bool HasMore { get; set; }

    [ObservableProperty]
    public partial bool IsImporting { get; set; }

    [ObservableProperty]
    public partial CachesNotice? Notice { get; set; }

    [ObservableProperty]
    public partial IReadOnlyList<GeocacheSummary> SelectedItems { get; set; } = [];

    /// <summary>The cache shown in the detail pane: set only while exactly one is selected.</summary>
    [ObservableProperty]
    public partial GeocacheSummary? Detail { get; set; }

    public int SelectedCount => SelectedItems.Count;

    public bool HasSelection => SelectedItems.Count > 0;

    public bool HasSelectedList => SelectedListOption?.Value is not null;

    /// <summary>True when nothing is stored at all, so the page should point at importing.</summary>
    public bool IsEmpty => _hasLoaded && TotalCount == 0 && !HasActiveFilters;

    /// <summary>True when caches exist but the search and filters hide all of them.</summary>
    public bool HasNoMatches => _hasLoaded && TotalCount == 0 && HasActiveFilters;

    public bool HasActiveFilters
        => !string.IsNullOrWhiteSpace(SearchText)
            || SelectedTypeOption.Value is not null
            || SelectedSizeOption.Value is not null
            || SelectedStatusOption.Value is not null
            || MinDifficultyOption.Value is not null
            || MaxDifficultyOption.Value is not null
            || MinTerrainOption.Value is not null
            || MaxTerrainOption.Value is not null
            || SolvedOption.Value is not null
            || FoundOption.Value is not null
            || CorrectedOption.Value is not null
            || SelectedListOption?.Value is not null;

    public string CountText => string.Format(CultureInfo.CurrentCulture, _localizer["CachesCount"].Value, TotalCount);

    public override void ViewLoaded() => _ = RefreshAsync();

    /// <summary>Completes when the reload started by the last filter change has finished.</summary>
    public Task WhenIdleAsync() => _loadTask;

    /// <summary>Reloads the lists and the first page, keeping the current search and filters.</summary>
    public async Task RefreshAsync()
    {
        await LoadListsAsync();
        QueueReload();
        await _loadTask;
    }

    public async Task LoadMoreAsync()
    {
        if (!HasMore || _loadCts is null || _isLoadingMore)
        {
            return;
        }

        _isLoadingMore = true;
        var generation = _generation;
        var token = _loadCts.Token;
        try
        {
            var page = await _store.QueryAsync(BuildQuery(Items.Count), token);
            if (generation != _generation)
            {
                return;
            }

            foreach (var item in page.Items)
            {
                Items.Add(item);
            }

            HasMore = Items.Count < page.TotalCount;
        }
        catch (OperationCanceledException)
        {
        }
        finally
        {
            _isLoadingMore = false;
        }
    }

    [RelayCommand(CanExecute = nameof(CanOpen))]
    private void Open()
    {
        if (Detail is { } cache)
        {
            _navigation.Navigate<GeocacheDetailViewModel>(cache.Code);
        }
    }

    private bool CanOpen() => Detail is not null;

    public void SetSelection(IReadOnlyList<GeocacheSummary> selected)
    {
        SelectedItems = selected;
        Detail = selected.Count == 1 ? selected[0] : null;
        OpenCommand.NotifyCanExecuteChanged();
        OnPropertyChanged(nameof(SelectedCount));
        OnPropertyChanged(nameof(HasSelection));
        DeleteSelectedCommand.NotifyCanExecuteChanged();
        MoveSelectedToListCommand.NotifyCanExecuteChanged();
    }

    [RelayCommand]
    private void ClearFilters()
    {
        _suppressReload = true;
        SearchText = "";
        SelectedTypeOption = TypeOptions[0];
        SelectedSizeOption = SizeOptions[0];
        SelectedStatusOption = StatusOptions[0];
        MinDifficultyOption = RatingOptions[0];
        MaxDifficultyOption = RatingOptions[0];
        MinTerrainOption = RatingOptions[0];
        MaxTerrainOption = RatingOptions[0];
        SolvedOption = YesNoOptions[0];
        FoundOption = YesNoOptions[0];
        CorrectedOption = YesNoOptions[0];
        SelectedListOption = ListOptions[0];
        _suppressReload = false;
        QueueReload();
    }

    [RelayCommand]
    private void DismissNotice() => Notice = null;

    [RelayCommand(CanExecute = nameof(HasSelection))]
    private async Task DeleteSelectedAsync()
    {
        var codes = SelectedItems.Select(i => i.Code).ToList();
        var text = string.Format(CultureInfo.CurrentCulture, _localizer["CachesDeleteText"].Value, codes.Count);
        if (await _confirmation.ShowAsync(_localizer["CachesDeleteTitle"], text) != ConfirmationResult.Confirmed)
        {
            return;
        }

        await _store.DeleteAsync(codes);
        await RefreshAsync();
    }

    [RelayCommand(CanExecute = nameof(HasSelection))]
    private async Task MoveSelectedToListAsync(GeocacheList? target)
    {
        if (target is null)
        {
            return;
        }

        var codes = SelectedItems.Select(i => i.Code).ToList();
        await _store.AddToListAsync(target.Id, codes);

        // Moving out of the list being viewed; from "All caches" it is just an add.
        if (SelectedListOption?.Value is { } current && current.Id != target.Id)
        {
            await _store.RemoveFromListAsync(current.Id, codes);
        }

        await RefreshAsync();
    }

    [RelayCommand]
    private async Task NewListAsync()
    {
        var name = await _prompt.PromptAsync(_localizer["CachesNewListTitle"], _localizer["CachesListNameLabel"]);
        if (string.IsNullOrWhiteSpace(name))
        {
            return;
        }

        try
        {
            var list = await _store.CreateListAsync(name);
            await LoadListsAsync();
            SelectedListOption = ListOptions.First(o => o.Value?.Id == list.Id);
            await _loadTask;
        }
        catch (InvalidOperationException)
        {
            ShowListNameTaken(name);
        }
    }

    [RelayCommand(CanExecute = nameof(HasSelectedList))]
    private async Task RenameListAsync()
    {
        if (SelectedListOption?.Value is not { } list)
        {
            return;
        }

        var name = await _prompt.PromptAsync(_localizer["CachesRenameListTitle"], _localizer["CachesListNameLabel"], list.Name);
        if (string.IsNullOrWhiteSpace(name) || name == list.Name)
        {
            return;
        }

        try
        {
            await _store.RenameListAsync(list.Id, name);
            await LoadListsAsync();
            SelectedListOption = ListOptions.First(o => o.Value?.Id == list.Id);
        }
        catch (InvalidOperationException)
        {
            ShowListNameTaken(name);
        }
    }

    [RelayCommand(CanExecute = nameof(HasSelectedList))]
    private async Task DeleteListAsync()
    {
        if (SelectedListOption?.Value is not { } list)
        {
            return;
        }

        var text = string.Format(CultureInfo.CurrentCulture, _localizer["CachesDeleteListText"].Value, list.Name);
        if (await _confirmation.ShowAsync(_localizer["CachesDeleteListTitle"], text) != ConfirmationResult.Confirmed)
        {
            return;
        }

        await _store.DeleteListAsync(list.Id);
        await LoadListsAsync();
        SelectedListOption = ListOptions[0];
        await _loadTask;
    }

    [RelayCommand]
    private async Task ImportGpxAsync()
    {
        var files = await _filePicker.PickFilesAsync([GpxExtension, ZipExtension]);
        if (files.Count == 0)
        {
            return;
        }

        IsImporting = true;
        try
        {
            Notice = await ImportAsync(files);
            await RefreshAsync();
        }
        finally
        {
            IsImporting = false;
        }
    }

    // A pocket query ships "Name.gpx" with "Name-wpts.gpx"; each group is imported together into one list.
    private async Task<CachesNotice> ImportAsync(IReadOnlyList<PickedFile> files)
    {
        int added = 0, updated = 0, plain = 0;
        List<GpxImportIssue> issues = [];
        List<GpxImportIssue> errors = [];
        FilterOption<GeocacheList?>? lastList = null;

        foreach (var group in files.GroupBy(f => ListNameFor(f.Name), StringComparer.OrdinalIgnoreCase))
        {
            List<Stream> streams = [];
            try
            {
                List<GpxInput> inputs = [];
                foreach (var file in group)
                {
                    var stream = await file.OpenReadAsync(CancellationToken.None);
                    streams.Add(stream);
                    inputs.Add(new(stream, file.Name));
                }

                var result = await _importer.ImportAsync(inputs);
                issues.AddRange(result.Warnings);
                errors.AddRange(result.Errors);
                plain += result.PlainWaypoints.Count;
                if (result.Caches.Count == 0)
                {
                    continue;
                }

                var upsert = await _store.UpsertAsync([.. result.Caches]);
                added += upsert.Added;
                updated += upsert.Updated;

                var list = await FindOrCreateListAsync(group.Key);
                await _store.AddToListAsync(list.Id, [.. result.Caches.Select(c => c.Code)]);
                await LoadListsAsync();
                lastList = ListOptions.FirstOrDefault(o => o.Value?.Id == list.Id);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                errors.Add(new(group.First().Name, GpxImportIssueKind.FileUnreadable, ex.Message));
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                // A command that throws takes the whole app down, so a failed save is reported like a bad file.
                errors.Add(new(group.First().Name, GpxImportIssueKind.SaveFailed, ex.Message));
            }
            finally
            {
                foreach (var stream in streams)
                {
                    await stream.DisposeAsync();
                }
            }
        }

        if (files.Count > 0 && lastList is not null && files.Select(f => ListNameFor(f.Name)).Distinct(StringComparer.OrdinalIgnoreCase).Count() == 1)
        {
            _suppressReload = true;
            SelectedListOption = lastList;
            _suppressReload = false;
        }

        ImportSummary summary = new(added, updated, plain, issues, errors);
        var severity = errors.Count > 0 ? CachesNoticeSeverity.Error
            : issues.Count > 0 ? CachesNoticeSeverity.Warning
            : CachesNoticeSeverity.Success;
        return new(severity, DescribeImport(summary), summary);
    }

    private string DescribeImport(ImportSummary summary)
    {
        var parts = new List<string>
        {
            string.Format(CultureInfo.CurrentCulture, _localizer["CachesImportAdded"].Value, summary.Added),
            string.Format(CultureInfo.CurrentCulture, _localizer["CachesImportUpdated"].Value, summary.Updated),
        };

        if (summary.Issues.Count > 0)
        {
            parts.Add(string.Format(CultureInfo.CurrentCulture, _localizer["CachesImportWarnings"].Value, summary.Issues.Count));
        }

        if (summary.Errors.Count > 0)
        {
            parts.Add(string.Format(CultureInfo.CurrentCulture, _localizer["CachesImportErrors"].Value, summary.Errors.Count));
        }

        var message = string.Join(", ", parts);
        return summary.PlainWaypoints > 0
            ? message + " " + string.Format(CultureInfo.CurrentCulture, _localizer["CachesImportWaypoints"].Value, summary.PlainWaypoints)
            : message;
    }

    private static string ListNameFor(string fileName)
    {
        var name = Path.GetFileNameWithoutExtension(fileName);
        return name.EndsWith("-wpts", StringComparison.OrdinalIgnoreCase) ? name[..^"-wpts".Length] : name;
    }

    private async Task<GeocacheList> FindOrCreateListAsync(string name)
    {
        var existing = (await _store.GetListsAsync()).FirstOrDefault(l => string.Equals(l.Name, name, StringComparison.OrdinalIgnoreCase));
        return existing ?? await _store.CreateListAsync(name);
    }

    private async Task LoadListsAsync()
    {
        var lists = await _store.GetListsAsync();
        var selectedId = SelectedListOption?.Value?.Id;

        Lists.Clear();
        foreach (var list in lists)
        {
            Lists.Add(list);
        }

        _suppressReload = true;

        // A fresh collection rather than editing in place: a ComboBox bound to an edited collection
        // keeps its selection but renders an empty box.
        List<FilterOption<GeocacheList?>> options = [AllLists()];
        options.AddRange(lists.Select(l => new FilterOption<GeocacheList?>($"{l.Name} ({l.Count})", l)));
        ListOptions = options;
        SelectedListOption = options.FirstOrDefault(o => o.Value?.Id == selectedId) ?? options[0];
        _suppressReload = false;
    }

    private void ShowListNameTaken(string name)
        => Notice = new(CachesNoticeSeverity.Error, string.Format(CultureInfo.CurrentCulture, _localizer["CachesListExists"].Value, name.Trim()));

    private FilterOption<GeocacheList?> AllLists() => new(_localizer["CachesAllLists"], null);

    private string Label(string enumName, Enum value) => _localizer[$"{enumName}_{value}"];

    private List<FilterOption<T?>> Options<T>(IEnumerable<(string Label, T? Value)> choices)
    {
        List<FilterOption<T?>> options = [new(_localizer["CachesAny"], default)];
        options.AddRange(choices.Select(c => new FilterOption<T?>(c.Label, c.Value)));
        return options;
    }

    private GeocacheQuery BuildQuery(int skip) => new()
    {
        Text = SearchText,
        Types = SelectedTypeOption.Value is { } type ? [type] : null,
        Sizes = SelectedSizeOption.Value is { } size ? [size] : null,
        Statuses = SelectedStatusOption.Value is { } status ? [status] : null,
        MinDifficulty = MinDifficultyOption.Value,
        MaxDifficulty = MaxDifficultyOption.Value,
        MinTerrain = MinTerrainOption.Value,
        MaxTerrain = MaxTerrainOption.Value,
        IsSolved = SolvedOption.Value,
        IsFound = FoundOption.Value,
        HasCorrectedCoordinate = CorrectedOption.Value,
        ListId = SelectedListOption?.Value?.Id,
        Sort = SelectedSortOption.Value,
        Descending = IsDescending,
        Skip = skip,
        Take = PageSize,
    };

    private void QueueReload()
    {
        if (_suppressReload)
        {
            return;
        }

        _loadCts?.Cancel();
        _loadCts = new();
        _generation++;
        _loadTask = ReloadAsync(_loadCts.Token, _generation);
    }

    private async Task ReloadAsync(CancellationToken token, int generation)
    {
        try
        {
            var page = await _store.QueryAsync(BuildQuery(0), token);
            if (generation != _generation)
            {
                return;
            }

            Items.Clear();
            foreach (var item in page.Items)
            {
                Items.Add(item);
            }

            TotalCount = page.TotalCount;
            HasMore = Items.Count < page.TotalCount;
            _hasLoaded = true;
            SetSelection([]);
            NotifyStateChanged();
        }
        catch (OperationCanceledException)
        {
        }
    }

    private void NotifyStateChanged()
    {
        OnPropertyChanged(nameof(IsEmpty));
        OnPropertyChanged(nameof(HasNoMatches));
        OnPropertyChanged(nameof(HasActiveFilters));
        OnPropertyChanged(nameof(HasSelectedList));
        OnPropertyChanged(nameof(CountText));
        RenameListCommand.NotifyCanExecuteChanged();
        DeleteListCommand.NotifyCanExecuteChanged();
    }

    partial void OnSearchTextChanged(string value) => QueueReload();

    partial void OnSelectedTypeOptionChanged(FilterOption<GeocacheType?> value) => QueueReload();

    partial void OnSelectedSizeOptionChanged(FilterOption<GeocacheSize?> value) => QueueReload();

    partial void OnSelectedStatusOptionChanged(FilterOption<GeocacheStatus?> value) => QueueReload();

    partial void OnMinDifficultyOptionChanged(FilterOption<double?> value) => QueueReload();

    partial void OnMaxDifficultyOptionChanged(FilterOption<double?> value) => QueueReload();

    partial void OnMinTerrainOptionChanged(FilterOption<double?> value) => QueueReload();

    partial void OnMaxTerrainOptionChanged(FilterOption<double?> value) => QueueReload();

    partial void OnSolvedOptionChanged(FilterOption<bool?> value) => QueueReload();

    partial void OnFoundOptionChanged(FilterOption<bool?> value) => QueueReload();

    partial void OnCorrectedOptionChanged(FilterOption<bool?> value) => QueueReload();

    partial void OnSelectedSortOptionChanged(FilterOption<GeocacheSort> value) => QueueReload();

    partial void OnIsDescendingChanged(bool value) => QueueReload();

    // A two-way bound ComboBox briefly writes null while its options are rebuilt; that is not a choice.
    partial void OnSelectedListOptionChanged(FilterOption<GeocacheList?> value)
    {
        if (value is null)
        {
            return;
        }

        OnPropertyChanged(nameof(HasSelectedList));
        RenameListCommand.NotifyCanExecuteChanged();
        DeleteListCommand.NotifyCanExecuteChanged();
        QueueReload();
    }
}
