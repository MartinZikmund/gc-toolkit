using System.Globalization;
using GcToolkit.Core.Coordinates;
using GcToolkit.Core.Geocaches;
using GcToolkit.Core.ViewModels.Tools;

namespace GcToolkit.Core.ViewModels;

/// <summary>
/// The workbench's "Save as corrected coordinates" for the active cache. Saving marks the cache solved;
/// once solved, a changed final never overwrites the saved coordinates on its own: the user updates
/// or keeps them (#465).
/// </summary>
public partial class CorrectedCoordinatesViewModel(IGeocacheStore store, IStringLocalizer localizer) : ViewModelBase
{
    private const double Tolerance = 1e-9;

    private GeocacheCode? _code;
    private VariableCoordinateCalculatorViewModel? _sheet;
    private GeoCoordinate? _kept;

    /// <summary>Raised when the cache becomes solved or unsolved.</summary>
    public event EventHandler? SolvedChanged;

    /// <summary>Only caches in the offline store can keep corrected coordinates.</summary>
    [ObservableProperty]
    public partial bool IsStored { get; private set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasSaved), nameof(SavedText))]
    public partial GeoCoordinate? Saved { get; private set; }

    [ObservableProperty]
    public partial bool IsSolved { get; private set; }

    /// <summary>The last complete stage's coordinate.</summary>
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SaveCommand))]
    public partial GeoCoordinate? Final { get; private set; }

    /// <summary>A solved cache's final differs from the saved coordinates and the user has not chosen to keep them.</summary>
    [ObservableProperty]
    public partial bool NeedsUpdate { get; private set; }

    public bool HasSaved => Saved is not null;

    public string SavedText => Saved is { } saved
        ? string.Format(CultureInfo.CurrentCulture, localizer["Workbench_CorrectedSaved"].Value, CoordinateFormatter.Format(saved, CoordinateFormat.DegreesDecimalMinutes))
        : string.Empty;

    public async Task LoadAsync(GeocacheCode code, VariableCoordinateCalculatorViewModel sheet)
    {
        if (!ReferenceEquals(sheet, _sheet))
        {
            if (_sheet is not null)
            {
                _sheet.Edited -= OnSheetEdited;
            }

            _sheet = sheet;
            _sheet.Edited += OnSheetEdited;
        }

        _code = code;
        _kept = null;
        Geocache? cache = null;
        try
        {
            cache = await store.GetAsync(code);
        }
        catch (Exception)
        {
            // Treated as not stored: nothing can be saved.
        }

        if (_code != code)
        {
            return;
        }

        IsStored = cache is not null;
        Saved = cache?.UserData.CorrectedCoordinate;
        IsSolved = cache?.UserData.IsSolved ?? false;
        Refresh();
    }

    private void OnSheetEdited(object? sender, EventArgs e) => Refresh();

    private void Refresh()
    {
        Final = _sheet?.Stages.LastOrDefault(s => s.IsComplete)?.Coordinate;
        NeedsUpdate = IsSolved && Final is { } final && Saved is { } saved && !Same(final, saved)
            && !(_kept is { } kept && Same(kept, final));
        SaveCommand.NotifyCanExecuteChanged();
    }

    private static bool Same(GeoCoordinate a, GeoCoordinate b)
        => Math.Abs(a.Latitude - b.Latitude) < Tolerance && Math.Abs(a.Longitude - b.Longitude) < Tolerance;

    private bool CanSave() => IsStored && Final is { } final && !(Saved is { } saved && Same(saved, final));

    /// <summary>Saves the final as the corrected coordinates, or updates them after the user was asked.</summary>
    [RelayCommand(CanExecute = nameof(CanSave))]
    private async Task SaveAsync()
    {
        if (Final is { } final)
        {
            await WriteAsync(final);
        }
    }

    [RelayCommand]
    private void Keep()
    {
        _kept = Final;
        Refresh();
    }

    [RelayCommand]
    private Task ClearAsync() => WriteAsync(null);

    private async Task WriteAsync(GeoCoordinate? corrected)
    {
        if (_code is not { } code)
        {
            return;
        }

        try
        {
            if (await store.GetAsync(code) is not { } cache
                || !await store.UpdateUserDataAsync(code, cache.UserData with { CorrectedCoordinate = corrected, IsSolved = corrected is not null }))
            {
                return;
            }
        }
        catch (Exception)
        {
            // Nothing changed; the buttons stay as they were.
            return;
        }

        var wasSolved = IsSolved;
        Saved = corrected;
        IsSolved = corrected is not null;
        _kept = null;
        Refresh();
        if (wasSolved != IsSolved)
        {
            SolvedChanged?.Invoke(this, EventArgs.Empty);
        }
    }
}
