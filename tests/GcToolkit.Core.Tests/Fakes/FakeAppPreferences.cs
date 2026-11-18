using GcToolkit.Core.Coordinates;
using GcToolkit.Core.Services.Settings;
using Microsoft.UI.Xaml;

namespace GcToolkit.Core.Tests.Fakes;

public sealed class FakeAppPreferences : IAppPreferences
{
    public int DataVersion { get; set; }

    public bool FirstStart { get; set; }

    public int LaunchCount { get; set; }

    public bool OfferUserRating { get; set; }

    public ElementTheme Theme { get; set; } = ElementTheme.Default;

    public CoordinateFormat CoordinateFormat { get; set; } = CoordinateFormat.DegreesDecimalMinutes;

    public void Clear()
    {
    }
}
