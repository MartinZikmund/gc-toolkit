using System.Globalization;
using GcToolkit.Core.Ciphers;
using GcToolkit.Core.Coordinates;
using GcToolkit.Core.Geocaches;
using GcToolkit.Core.Geocaches.Detail;
using GcToolkit.Core.Services;
using GcToolkit.Core.Services.Settings;
using GcToolkit.Core.ViewModels.Tools;
using Microsoft.Extensions.Localization;

namespace GcToolkit.Core.ViewModels;

public enum GeocacheDetailSection
{
    Listing,
    Hint,
    Waypoints,
    Logs,
}

public sealed partial record AttributeItem(int Id, string Text, bool IsNegated);

public sealed record WaypointItem(string Prefix, string Name, string TypeLabel, string? CoordinateText, string? Note)
{
    public bool IsHidden => CoordinateText is null;
}

public sealed record LogItem(GeocacheLogType Type, string TypeLabel, string Author, string DateText, string Text);

/// <summary>A log filter choice; <see cref="Type"/> is null for "all".</summary>
public sealed record LogFilterOption(GeocacheLogType? Type, string Label, int Count)
{
    public string Display => $"{Label} ({Count})";
}

/// <summary>The full listing of one cache, opened with a <see cref="GeocacheCode"/> (or its text) as the navigation parameter.</summary>
public sealed partial class GeocacheDetailViewModel : ViewModelBase
{
    private const int HintRotation = 13;

    private readonly IGeocacheStore _store;
    private readonly IClipboardService _clipboard;
    private readonly INavigationService _navigation;
    private readonly IStringLocalizer _localizer;
    private readonly IAppPreferences _preferences;
    private readonly CaesarCipher _caesar = new();

    private Geocache? _cache;
    private IReadOnlyList<LogItem> _allLogs = [];

    public GeocacheDetailViewModel(
        IGeocacheStore store,
        IClipboardService clipboard,
        INavigationService navigation,
        IStringLocalizer localizer,
        IAppPreferences preferences)
    {
        _store = store;
        _clipboard = clipboard;
        _navigation = navigation;
        _localizer = localizer;
        _preferences = preferences;
    }

    [ObservableProperty]
    public partial bool IsLoaded { get; set; }

    [ObservableProperty]
    public partial bool NotFound { get; set; }

    [ObservableProperty]
    public partial string Name { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string Code { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string TypeLabel { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string SizeLabel { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string StatusLabel { get; set; } = string.Empty;

    [ObservableProperty]
    public partial GeocacheStatus Status { get; set; }

    [ObservableProperty]
    public partial string DifficultyTerrain { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string Owner { get; set; } = string.Empty;

    /// <summary>Code, type, size and ratings on one line.</summary>
    [ObservableProperty]
    public partial string SummaryLine { get; set; } = string.Empty;

    /// <summary>Owner, placed date and location on one line.</summary>
    [ObservableProperty]
    public partial string DetailLine { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string OwnerLine { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string PlacedLine { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string LocationText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string PostedCoordinate { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string? CorrectedCoordinate { get; set; }

    [ObservableProperty]
    public partial bool IsFound { get; set; }

    [ObservableProperty]
    public partial bool IsSolved { get; set; }

    [ObservableProperty]
    public partial string? UserNote { get; set; }

    [ObservableProperty]
    public partial IReadOnlyList<AttributeItem> Attributes { get; set; } = [];

    [ObservableProperty]
    public partial IReadOnlyList<ListingImage> Images { get; set; } = [];

    [ObservableProperty]
    public partial IReadOnlyList<WaypointItem> Waypoints { get; set; } = [];

    [ObservableProperty]
    public partial IReadOnlyList<LogFilterOption> LogFilters { get; set; } = [];

    [ObservableProperty]
    public partial LogFilterOption? SelectedLogFilter { get; set; }

    [ObservableProperty]
    public partial IReadOnlyList<LogItem> Logs { get; set; } = [];

    [ObservableProperty]
    public partial GeocacheDetailSection SelectedSection { get; set; }

    [ObservableProperty]
    public partial bool HasHint { get; set; }

    [ObservableProperty]
    public partial bool IsHintRevealed { get; set; }

    [ObservableProperty]
    public partial string DisplayedHint { get; set; } = string.Empty;

    [ObservableProperty]
    public partial bool IsDarkTheme { get; set; }

    [ObservableProperty]
    public partial string ListingHtml { get; set; } = string.Empty;

    public bool HasCorrectedCoordinate => CorrectedCoordinate is not null;

    public bool HasUserNote => !string.IsNullOrWhiteSpace(UserNote);

    public bool HasWaypoints => Waypoints.Count > 0;

    public bool HasLogs => _allLogs.Count > 0;

    public bool IsListingSelected => SelectedSection == GeocacheDetailSection.Listing;

    public bool IsHintSelected => SelectedSection == GeocacheDetailSection.Hint;

    public bool IsWaypointsSelected => SelectedSection == GeocacheDetailSection.Waypoints;

    public bool IsLogsSelected => SelectedSection == GeocacheDetailSection.Logs;

    /// <summary>The coordinates the cache should be found at: corrected when the user has solved them, otherwise posted.</summary>
    public string EffectiveCoordinate => CorrectedCoordinate ?? PostedCoordinate;

    public string HintToggleText => _localizer[IsHintRevealed ? "GeocacheHintHide" : "GeocacheHintReveal"].Value;

    public override void OnNavigatedTo(object? parameter)
    {
        base.OnNavigatedTo(parameter);

        var code = parameter switch
        {
            GeocacheCode c => c,
            string s when GeocacheCode.TryParse(s, out var parsed) => parsed,
            _ => default,
        };

        if (code.Value is not null)
        {
            _ = LoadAsync(code);
        }
    }

    public async Task LoadAsync(GeocacheCode code)
    {
        IsLoading = true;
        try
        {
            _cache = await _store.GetAsync(code);
        }
        finally
        {
            IsLoading = false;
        }

        if (_cache is null)
        {
            NotFound = true;
            IsLoaded = false;
            return;
        }

        NotFound = false;
        Populate(_cache);
        IsLoaded = true;
    }

    partial void OnSelectedSectionChanged(GeocacheDetailSection value)
    {
        OnPropertyChanged(nameof(IsListingSelected));
        OnPropertyChanged(nameof(IsHintSelected));
        OnPropertyChanged(nameof(IsWaypointsSelected));
        OnPropertyChanged(nameof(IsLogsSelected));
    }

    partial void OnCorrectedCoordinateChanged(string? value)
    {
        OnPropertyChanged(nameof(HasCorrectedCoordinate));
        OnPropertyChanged(nameof(EffectiveCoordinate));
    }

    partial void OnPostedCoordinateChanged(string value) => OnPropertyChanged(nameof(EffectiveCoordinate));

    partial void OnUserNoteChanged(string? value) => OnPropertyChanged(nameof(HasUserNote));

    partial void OnWaypointsChanged(IReadOnlyList<WaypointItem> value) => OnPropertyChanged(nameof(HasWaypoints));

    partial void OnIsDarkThemeChanged(bool value) => RebuildListing();

    partial void OnSelectedLogFilterChanged(LogFilterOption? value)
        => Logs = value?.Type is { } type ? [.. _allLogs.Where(l => l.Type == type)] : _allLogs;

    partial void OnIsHintRevealedChanged(bool value)
    {
        UpdateDisplayedHint();
        OnPropertyChanged(nameof(HintToggleText));
    }

    [RelayCommand]
    private void ToggleHint() => IsHintRevealed = !IsHintRevealed;

    [RelayCommand]
    private void CopyCoordinates()
    {
        if (_cache is not null)
        {
            _clipboard.SetText(EffectiveCoordinate);
        }
    }

    [RelayCommand]
    private void CopyWaypointCoordinate(WaypointItem? waypoint)
    {
        if (waypoint?.CoordinateText is { } text)
        {
            _clipboard.SetText(text);
        }
    }

    [RelayCommand]
    private void OpenInConversionTool()
    {
        if (_cache is not null)
        {
            _navigation.Navigate<CoordinateConversionViewModel>(EffectiveCoordinate);
        }
    }

    // The solving workbench (#414) does not exist yet, so there is nothing to navigate to.
    [RelayCommand(CanExecute = nameof(CanSolve))]
    private void Solve()
    {
    }

    private static bool CanSolve() => false;

    private void Populate(Geocache cache)
    {
        var format = _preferences.CoordinateFormat;

        Name = cache.Name;
        PageTitle = cache.Name;
        Code = cache.Code.Value;
        TypeLabel = Label(cache.Type);
        SizeLabel = Label(cache.Size);
        Status = cache.Status;
        StatusLabel = Label(cache.Status);
        DifficultyTerrain = FormatRatings(cache.Difficulty, cache.Terrain);
        Owner = cache.Owner ?? string.Empty;
        OwnerLine = cache.Owner is { Length: > 0 } owner ? Format("GeocacheOwnerFormat", owner) : string.Empty;
        PlacedLine = cache.PlacedOn is { } placed
            ? Format("GeocachePlacedFormat", placed.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture))
            : string.Empty;
        LocationText = string.Join(", ", new[] { cache.State, cache.Country }.Where(s => !string.IsNullOrWhiteSpace(s)));
        SummaryLine = Join(Code, TypeLabel, SizeLabel, DifficultyTerrain);
        DetailLine = Join(OwnerLine, PlacedLine, LocationText);
        PostedCoordinate = CoordinateFormatter.Format(cache.Coordinate, format);
        CorrectedCoordinate = cache.UserData.CorrectedCoordinate is { } corrected
            ? CoordinateFormatter.Format(corrected, format)
            : null;
        IsFound = cache.UserData.IsFound;
        IsSolved = cache.UserData.IsSolved;
        UserNote = cache.UserData.Note;
        Attributes = [.. cache.Attributes.Select(ToAttributeItem)];
        Images = [.. cache.Images.Select(i => new ListingImage(i.Url, i.Name ?? i.Description))];
        Waypoints = [.. cache.Waypoints.Select(w => ToWaypointItem(w, format))];

        HasHint = !string.IsNullOrWhiteSpace(cache.Hint);
        IsHintRevealed = false;
        UpdateDisplayedHint();

        PopulateLogs(cache);
        RebuildListing();
    }

    private void PopulateLogs(Geocache cache)
    {
        _allLogs =
        [
            .. cache.Logs.OrderByDescending(l => l.Date).Select(l => new LogItem(
                l.Type,
                Label(l.Type),
                l.Author ?? string.Empty,
                l.Date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                l.TextIsHtml ? HtmlText.ToPlainText(l.Text) : l.Text ?? string.Empty)),
        ];

        List<LogFilterOption> filters = [new(null, _localizer["GeocacheLogsAll"].Value, _allLogs.Count)];
        filters.AddRange(_allLogs
            .GroupBy(l => l.Type)
            .OrderByDescending(g => g.Count())
            .Select(g => new LogFilterOption(g.Key, Label(g.Key), g.Count())));

        LogFilters = filters;
        SelectedLogFilter = filters[0];
        Logs = _allLogs;
        OnPropertyChanged(nameof(HasLogs));
    }

    private void UpdateDisplayedHint()
    {
        var hint = _cache?.Hint ?? string.Empty;
        DisplayedHint = IsHintRevealed ? hint : _caesar.Transform(hint, HintRotation);
    }

    private void RebuildListing()
    {
        if (_cache is null)
        {
            return;
        }

        ListingHtml = ListingHtmlBuilder.Build(new ListingHtmlModel
        {
            ShortDescription = _cache.ShortDescription,
            ShortDescriptionIsHtml = _cache.ShortDescriptionIsHtml,
            LongDescription = _cache.LongDescription,
            LongDescriptionIsHtml = _cache.LongDescriptionIsHtml,
            Attributes = [.. Attributes.Select(a => new ListingChip(a.Text, a.IsNegated))],
            Images = Images,
            Note = UserNote,
            IsDark = IsDarkTheme,
            Labels = new ListingHtmlLabels(
                _localizer["GeocacheAttributes"].Value,
                _localizer["GeocacheYourNote"].Value,
                _localizer["GeocacheImages"].Value),
        });
    }

    private AttributeItem ToAttributeItem(GeocacheAttribute attribute)
    {
        var name = GeocacheAttributeCatalog.IsKnown(attribute.Id)
            ? _localizer[GeocacheAttributeCatalog.ResourceKey(attribute.Id)].Value
            : Format("GeocacheAttributeUnknownFormat", attribute.Id);

        return new AttributeItem(attribute.Id, name, !attribute.IsPositive);
    }

    private WaypointItem ToWaypointItem(Waypoint waypoint, CoordinateFormat format) => new(
        waypoint.Prefix,
        waypoint.Name,
        Label(waypoint.Type),
        waypoint.Coordinate is { } coordinate ? CoordinateFormatter.Format(coordinate, format) : null,
        waypoint.Note);

    private static string Join(params string[] parts) => string.Join(" · ", parts.Where(p => !string.IsNullOrEmpty(p)));

    private string Format(string key, object arg) => string.Format(CultureInfo.CurrentCulture, _localizer[key].Value, arg);

    private string Label<TEnum>(TEnum value)
        where TEnum : struct, Enum
        => _localizer[$"{typeof(TEnum).Name}_{value}"].Value;

    private static string FormatRatings(double? difficulty, double? terrain)
        => difficulty is null && terrain is null
            ? string.Empty
            : $"D {Rating(difficulty)} / T {Rating(terrain)}";

    private static string Rating(double? value)
        => value?.ToString("0.#", CultureInfo.InvariantCulture) ?? "?";
}
