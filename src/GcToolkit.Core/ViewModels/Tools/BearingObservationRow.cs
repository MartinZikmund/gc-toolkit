namespace GcToolkit.Core.ViewModels.Tools;

/// <summary>One editable point + bearing row of a bearing-fix tool, plus its readouts once a fix exists.</summary>
public sealed partial class BearingObservationRow : ObservableObject
{
    private readonly Action<BearingObservationRow> _remove;

    public BearingObservationRow(Action<BearingObservationRow> remove) => _remove = remove;

    /// <summary>A, B, C... — reassigned whenever a row is removed.</summary>
    [ObservableProperty]
    public partial string Label { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string CoordinateHeader { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string BearingHeader { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string Coordinate { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string Bearing { get; set; } = string.Empty;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(RemoveCommand))]
    public partial bool CanRemove { get; set; }

    /// <summary>The declination applied and the resulting true bearing, when bearings are magnetic.</summary>
    [ObservableProperty]
    public partial string DeclinationText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial bool HasDeclination { get; set; }

    [ObservableProperty]
    public partial string DistanceText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string BearingToFixText { get; set; } = string.Empty;

    /// <summary>How far this row's line misses the fix (3+ rows), and by how many degrees.</summary>
    [ObservableProperty]
    public partial string OffsetText { get; set; } = string.Empty;

    public bool IsBlank => string.IsNullOrWhiteSpace(Coordinate) && string.IsNullOrWhiteSpace(Bearing);

    [RelayCommand(CanExecute = nameof(CanRemove))]
    private void Remove() => _remove(this);

    internal void ClearReadouts()
    {
        DistanceText = string.Empty;
        BearingToFixText = string.Empty;
        OffsetText = string.Empty;
    }
}
