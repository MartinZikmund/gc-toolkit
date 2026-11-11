using System.Globalization;
using GcToolkit.Core.Catalog;
using GcToolkit.Core.Discovery;
using GcToolkit.Core.FavoriteTools;
using GcToolkit.Core.Recents;
using GcToolkit.Core.Services;
using GcToolkit.Core.Services.Devices;

namespace GcToolkit.Core.ViewModels.Tools;

/// <summary>
/// A magnetic bearing readout (issue #369). Shows the smoothed heading in degrees, its 16-point
/// cardinal name and a needle on a north-up rose; true north appears as a second line only where the
/// platform actually resolves it.
/// </summary>
[Tool("Compass", ToolCategory.Field,
      Introduced = "2026-05-18", Updated = "2026-09-03",
      Keywords = ["compass", "kompas", "heading", "směr", "north", "bearing", "azimut"])]
[RequiresDeviceCapability(DeviceCapability.Compass)]
public sealed partial class CompassViewModel : ToolViewModelBase
{
    /// <summary>10 Hz: fast enough to feel attached to the hand, slow enough not to cook the battery.</summary>
    private static readonly TimeSpan _reportInterval = TimeSpan.FromMilliseconds(100);

    /// <summary>
    /// Circular EMA weight for one raw reading. 0.15 at 10 Hz settles in roughly a third of a second
    /// while swallowing the several degrees of frame-to-frame noise every magnetometer emits — an
    /// unsmoothed needle reads as a broken one.
    /// </summary>
    private const double SmoothingAlpha = 0.15;

    /// <summary>Below this the smoothed vector has no direction left, so the last heading stands.</summary>
    private const double MinimumVectorLength = 1e-6;

    /// <summary>16 points, N first, one every 22.5°. Index order is what <see cref="ResolveCardinalKey"/> relies on.</summary>
    internal static readonly string[] _cardinalKeys =
    [
        "Compass_North", "Compass_NorthNorthEast", "Compass_NorthEast", "Compass_EastNorthEast",
        "Compass_East", "Compass_EastSouthEast", "Compass_SouthEast", "Compass_SouthSouthEast",
        "Compass_South", "Compass_SouthSouthWest", "Compass_SouthWest", "Compass_WestSouthWest",
        "Compass_West", "Compass_WestNorthWest", "Compass_NorthWest", "Compass_NorthNorthWest",
    ];

    private readonly ICompassService _compass;
    private readonly IDisplayRequestManager _display;
    private readonly IClipboardService _clipboard;
    private readonly IStringLocalizer _localizer;

    private IDisposable? _displayToken;
    private bool _isRunning;
    private bool _startAttempted;

    // Smoothed unit vector (x = east, y = north). Averaging the vector rather than the angle is what
    // carries the needle across 359°->0° the short way instead of spinning it all the way round.
    private double _smoothedEast;
    private double _smoothedNorth;
    private bool _hasSmoothedHeading;

    /// <summary>
    /// Last known true-north offset. Held for the session rather than read per reading: it changes with
    /// position, not with pointing, so re-deriving it from the smoothed heading keeps the second line
    /// locked to the needle, and keeping the last value stops the line blinking out whenever a single
    /// reading arrives without a location fix.
    /// </summary>
    private double? _declinationDegrees;

    public CompassViewModel(
        ICatalogService catalog,
        IRecentsService recents,
        IFavoriteToolsService favorites,
        IStringLocalizer localizer,
        ICompassService compass,
        IDisplayRequestManager display,
        IClipboardService clipboard)
        : base("Compass", catalog, recents, favorites, localizer)
    {
        _localizer = localizer;
        _compass = compass;
        _display = display;
        _clipboard = clipboard;
    }

    /// <summary>Smoothed magnetic heading, 0..360.</summary>
    [ObservableProperty]
    public partial double HeadingDegrees { get; set; }

    /// <summary>The same heading relative to true north, or <see langword="null"/> where unavailable.</summary>
    [ObservableProperty]
    public partial double? TrueNorthDegrees { get; set; }

    [ObservableProperty]
    public partial bool HasTrueNorth { get; set; }

    /// <summary>Zero-padded to three digits, as bearings are always written and read.</summary>
    [ObservableProperty]
    public partial string HeadingText { get; set; } = "000°";

    [ObservableProperty]
    public partial string TrueNorthText { get; set; } = string.Empty;

    /// <summary>Localized 16-point name of the current heading (N, NNE, NE, …).</summary>
    [ObservableProperty]
    public partial string CardinalPoint { get; set; } = string.Empty;

    [ObservableProperty]
    public partial SensorStatus Status { get; set; } = SensorStatus.Unavailable;

    /// <summary>Started and permitted, but nothing has arrived yet — a cold or uncalibrated magnetometer.</summary>
    [ObservableProperty]
    public partial bool IsWarmingUp { get; set; }

    /// <summary>Readings are flowing; the readout and the copy action are meaningful.</summary>
    [ObservableProperty]
    public partial bool IsLive { get; set; }

    [ObservableProperty]
    public partial bool HasError { get; set; }

    [ObservableProperty]
    public partial string ErrorMessage { get; set; } = string.Empty;

    /// <summary>On by default: a compass is read while walking, and a screen that sleeps mid-leg is useless.</summary>
    [ObservableProperty]
    public partial bool KeepScreenAwake { get; set; } = true;

    private bool CanCopyHeading => IsLive;

    public override void ViewLoaded()
    {
        base.ViewLoaded();

        if (_isRunning)
        {
            return;
        }

        // Subscribe before Start so a status the service settles synchronously is not missed.
        _compass.HeadingChanged += OnHeadingChanged;
        _compass.StatusChanged += OnStatusChanged;
        _startAttempted = true;

        // Only "running" if the sensor actually started: the wake lock keys off this, and holding a
        // screen awake for a compass that refused to start is the worst outcome here.
        _isRunning = _compass.Start(_reportInterval);

        // Start can refuse without raising StatusChanged (absent sensor), so read the status directly.
        ApplyStatus();
        ApplyKeepScreenAwake();
    }

    /// <summary>
    /// Non-negotiable teardown: a magnetometer left streaming is a battery bug, and a held display
    /// request keeps the user's screen on long after they left the tool.
    /// </summary>
    public override void ViewUnloaded()
    {
        base.ViewUnloaded();

        // Unconditional: ViewLoaded subscribes before Start, so a refused start leaves the handlers
        // attached with _isRunning false. Detaching only when running leaked them and let the next
        // ViewLoaded subscribe a second time. -= is safe when nothing is attached.
        _compass.HeadingChanged -= OnHeadingChanged;
        _compass.StatusChanged -= OnStatusChanged;
        _isRunning = false;

        _compass.Stop();

        _displayToken?.Dispose();
        _displayToken = null;

        _startAttempted = false;
        _hasSmoothedHeading = false;
        _declinationDegrees = null;

        // The next visit starts from whatever the device can resolve then, not from a stale fix.
        HasTrueNorth = false;
        TrueNorthDegrees = null;
        TrueNorthText = string.Empty;

        ApplyStatus();
    }

    [RelayCommand(CanExecute = nameof(CanCopyHeading))]
    private void CopyHeading() => _clipboard.SetText($"{HeadingText} {CardinalPoint}");

    partial void OnKeepScreenAwakeChanged(bool value) => ApplyKeepScreenAwake();

    partial void OnIsLiveChanged(bool value) => CopyHeadingCommand.NotifyCanExecuteChanged();

    private void OnStatusChanged(object? sender, EventArgs e) => ApplyStatus();

    private void OnHeadingChanged(object? sender, CompassHeading heading)
    {
        if (!double.IsFinite(heading.MagneticNorthDegrees))
        {
            return;
        }

        if (heading.TrueNorthDegrees is { } trueNorth)
        {
            _declinationDegrees = SignedDifference(trueNorth - heading.MagneticNorthDegrees);
        }

        if (!TrySmooth(heading.MagneticNorthDegrees, out var smoothed))
        {
            return;
        }

        HeadingDegrees = smoothed;
        HeadingText = FormatDegrees(smoothed);
        CardinalPoint = _localizer[ResolveCardinalKey(smoothed)].Value;

        if (_declinationDegrees is { } declination)
        {
            var trueHeading = Normalize(smoothed + declination);
            TrueNorthDegrees = trueHeading;
            TrueNorthText = FormatDegrees(trueHeading);
            HasTrueNorth = true;
        }
    }

    /// <summary>Folds one raw reading into the smoothed unit vector and returns the resulting bearing.</summary>
    private bool TrySmooth(double degrees, out double smoothedDegrees)
    {
        var radians = degrees * Math.PI / 180d;
        var east = Math.Sin(radians);
        var north = Math.Cos(radians);

        if (_hasSmoothedHeading)
        {
            _smoothedEast += SmoothingAlpha * (east - _smoothedEast);
            _smoothedNorth += SmoothingAlpha * (north - _smoothedNorth);
        }
        else
        {
            // The first reading seeds the filter outright; easing in from an arbitrary zero would
            // sweep the needle across the rose on every entry into the tool.
            _smoothedEast = east;
            _smoothedNorth = north;
            _hasSmoothedHeading = true;
        }

        if (Math.Abs(_smoothedEast) + Math.Abs(_smoothedNorth) < MinimumVectorLength)
        {
            smoothedDegrees = HeadingDegrees;
            return false;
        }

        smoothedDegrees = Normalize(Math.Atan2(_smoothedEast, _smoothedNorth) * 180d / Math.PI);
        return true;
    }

    private void ApplyStatus()
    {
        Status = _compass.Status;
        IsWarmingUp = Status == SensorStatus.NoData;
        IsLive = Status == SensorStatus.Ready;
        HasError = _startAttempted && Status is SensorStatus.Unavailable or SensorStatus.PermissionDenied;
        ErrorMessage = HasError
            ? _localizer[Status == SensorStatus.PermissionDenied ? "Compass_PermissionDenied" : "Compass_Unavailable"].Value
            : string.Empty;
    }

    private void ApplyKeepScreenAwake()
    {
        if (KeepScreenAwake && _isRunning)
        {
            _displayToken ??= _display.RequestActive();
            return;
        }

        _displayToken?.Dispose();
        _displayToken = null;
    }

    private static string ResolveCardinalKey(double degrees)
        => _cardinalKeys[(int)Math.Round(Normalize(degrees) / 22.5d, MidpointRounding.AwayFromZero) % _cardinalKeys.Length];

    private static string FormatDegrees(double degrees)
        => ((int)Math.Round(Normalize(degrees)) % 360).ToString("000", CultureInfo.InvariantCulture) + "°";

    private static double Normalize(double degrees) => ((degrees % 360d) + 360d) % 360d;

    /// <summary>Maps a raw angular difference onto -180..180 so a declination never arrives as 350°.</summary>
    private static double SignedDifference(double degrees) => (((degrees % 360d) + 540d) % 360d) - 180d;
}
