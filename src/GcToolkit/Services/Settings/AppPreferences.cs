using GcToolkit.Core.Services.Settings;
using MZikmund.Toolkit.WinUI.Services;

namespace GcToolkit.Services.Settings;

/// <summary>
/// Persisted placement of the main window, in physical screen pixels. The maximized state is kept
/// separately so a maximized window restores maximized without losing its restored bounds.
/// </summary>
public sealed record WindowPlacement(int Left, int Top, int Width, int Height, bool IsMaximized);

public sealed class AppPreferences : IAppPreferences
{
    private readonly IPreferences _preferences;

    public AppPreferences(IPreferences preferences)
    {
        _preferences = preferences ?? throw new ArgumentNullException(nameof(preferences));
    }

    private const string DataVersionKey = "AppDataVersion";
    public int DataVersion
    {
        get => _preferences.Get(DataVersionKey, 0);
        set => _preferences.Set(DataVersionKey, value);
    }

    private const string FirstStartKey = "AppFirstStart";
    public bool FirstStart
    {
        get => _preferences.Get(FirstStartKey, true);
        set => _preferences.Set(FirstStartKey, value);
    }

    private const string LaunchCountKey = "AppLaunchCount";
    public int LaunchCount
    {
        get => _preferences.Get(LaunchCountKey, 0);
        set => _preferences.Set(LaunchCountKey, value);
    }

    private const string OfferUserRatingKey = "OfferUserRating";
    public bool OfferUserRating
    {
        get => _preferences.Get(OfferUserRatingKey, true);
        set => _preferences.Set(OfferUserRatingKey, value);
    }

    private const string ThemeKey = "AppTheme";
    public ElementTheme Theme
    {
        get => _preferences.GetComplex(ThemeKey, ElementTheme.Default);
        set => _preferences.SetComplex(ThemeKey, value);
    }

    public void Clear() => Windows.Storage.ApplicationData.Current.LocalSettings.Values.Clear();
}
