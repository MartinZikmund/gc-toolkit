using GcToolkit.Core.Coordinates;

namespace GcToolkit.Core.Services.Settings;

public interface IAppPreferences
{
    int DataVersion { get; set; }

    bool FirstStart { get; set; }

    int LaunchCount { get; set; }

    bool OfferUserRating { get; set; }

    ElementTheme Theme { get; set; }

    /// <summary>The notation coordinates are shown in across the app.</summary>
    CoordinateFormat CoordinateFormat { get; set; }

    /// <summary>Clears all persisted preference values (used by the "reset settings" action).</summary>
    void Clear();
}
