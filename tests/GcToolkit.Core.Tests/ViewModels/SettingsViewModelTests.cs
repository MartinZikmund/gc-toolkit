using GcToolkit.Core.Infrastructure;
using GcToolkit.Core.Localization;
using GcToolkit.Core.Recents;
using GcToolkit.Core.Services;
using GcToolkit.Core.Services.Dialogs;
using GcToolkit.Core.Services.Settings;
using GcToolkit.Core.Services.Theming;
using GcToolkit.Core.Tests.Fakes;
using GcToolkit.Core.ViewModels;
using Microsoft.UI.Xaml;

namespace GcToolkit.Core.Tests.ViewModels;

/// <summary>
/// Exercises <see cref="SettingsViewModel"/> with hand-written fakes — proof that relocating it
/// (and its service interfaces) into Core made it unit-testable without a UI head.
/// </summary>
[TestClass]
public sealed class SettingsViewModelTests
{
    [TestMethod]
    public void OnThemeChanged_AfterInit_WritesThroughToThemeManagerAndPreferences()
    {
        var preferences = new FakeAppPreferences();
        var themeManager = new FakeThemeManager();
        var sut = CreateSut(preferences: preferences, themeManager: themeManager);
        sut.OnNavigatedTo(null);

        sut.Theme = ElementTheme.Dark;

        Assert.AreEqual(ElementTheme.Dark, themeManager.LastSetTheme);
        Assert.AreEqual(ElementTheme.Dark, preferences.Theme);
    }

    [TestMethod]
    public void OnNavigatedTo_DoesNotWriteBackTheStoredThemeWhileInitializing()
    {
        var themeManager = new FakeThemeManager();
        var preferences = new FakeAppPreferences { Theme = ElementTheme.Light };
        var sut = CreateSut(preferences: preferences, themeManager: themeManager);

        sut.OnNavigatedTo(null);

        Assert.AreEqual(ElementTheme.Light, sut.Theme);
        Assert.IsNull(themeManager.LastSetTheme, "loading the stored value must not trigger a write-back");
    }

    [TestMethod]
    public async Task ClearPreferencesCommand_WhenConfirmed_ClearsPreferences()
    {
        var preferences = new FakeAppPreferences();
        var sut = CreateSut(preferences: preferences, dialog: new FakeConfirmationDialogService(ConfirmationResult.Confirmed));

        await sut.ClearPreferencesCommand.ExecuteAsync(null);

        Assert.AreEqual(1, preferences.ClearCallCount);
    }

    [TestMethod]
    public async Task ClearPreferencesCommand_WhenDenied_DoesNotClearPreferences()
    {
        var preferences = new FakeAppPreferences();
        var sut = CreateSut(preferences: preferences, dialog: new FakeConfirmationDialogService(ConfirmationResult.Denied));

        await sut.ClearPreferencesCommand.ExecuteAsync(null);

        Assert.AreEqual(0, preferences.ClearCallCount);
    }

    [TestMethod]
    public void AppVersion_ReturnsValueFromApplication()
    {
        var sut = CreateSut(application: new FakeApplication { AppVersion = "1.2.3" });

        Assert.AreEqual("1.2.3", sut.AppVersion);
    }

    [TestMethod]
    public async Task ClearRecentsCommand_WhenConfirmed_ClearsRecents()
    {
        var recents = new FakeRecentsService();
        var sut = CreateSut(recents: recents, dialog: new FakeConfirmationDialogService(ConfirmationResult.Confirmed));

        await sut.ClearRecentsCommand.ExecuteAsync(null);

        Assert.AreEqual(1, recents.ClearCallCount);
    }

    [TestMethod]
    public async Task ClearRecentsCommand_WhenDenied_DoesNotClearRecents()
    {
        var recents = new FakeRecentsService();
        var sut = CreateSut(recents: recents, dialog: new FakeConfirmationDialogService(ConfirmationResult.Denied));

        await sut.ClearRecentsCommand.ExecuteAsync(null);

        Assert.AreEqual(0, recents.ClearCallCount);
    }

    [TestMethod]
    public void SelectedLanguageChanged_AfterInit_PersistsAndShowsRestartNotice()
    {
        var language = new FakeLanguageService();
        var sut = CreateSut(language: language);
        sut.OnNavigatedTo(null);

        sut.SelectedLanguage = sut.Languages.First(l => l.Code == "cs");

        Assert.AreEqual("cs", language.LastSetCode);
        Assert.IsTrue(sut.ShowLanguageRestartNotice);
    }

    private static SettingsViewModel CreateSut(
        FakeAppPreferences? preferences = null,
        FakeThemeManager? themeManager = null,
        FakeLanguageService? language = null,
        FakeRecentsService? recents = null,
        FakeConfirmationDialogService? dialog = null,
        FakeApplication? application = null,
        FakeLauncherService? launcher = null,
        FakeAppRestartService? appRestart = null)
        => new(
            new FakeStringLocalizer(),
            preferences ?? new FakeAppPreferences(),
            themeManager ?? new FakeThemeManager(),
            language ?? new FakeLanguageService(),
            recents ?? new FakeRecentsService(),
            dialog ?? new FakeConfirmationDialogService(ConfirmationResult.Denied),
            application ?? new FakeApplication(),
            launcher ?? new FakeLauncherService(),
            appRestart ?? new FakeAppRestartService());

    private sealed class FakeAppPreferences : IAppPreferences
    {
        public int DataVersion { get; set; }
        public bool FirstStart { get; set; }
        public int LaunchCount { get; set; }
        public bool OfferUserRating { get; set; }
        public ElementTheme Theme { get; set; } = ElementTheme.Default;
        public int ClearCallCount { get; private set; }

        public void Clear() => ClearCallCount++;
    }

    private sealed class FakeThemeManager : IThemeManager
    {
        public ElementTheme? LastSetTheme { get; private set; }
        public ElementTheme CurrentTheme => LastSetTheme ?? ElementTheme.Default;
        public ApplicationTheme ActualTheme => ApplicationTheme.Light;

        public void SetTheme(ElementTheme theme) => LastSetTheme = theme;

        public void Dispose() { }
    }

    private sealed class FakeLanguageService : ILanguageService
    {
        public AppLanguage Current { get; private set; } = new("en", "English");
        public IReadOnlyList<AppLanguage> Available { get; } = [new("en", "English"), new("cs", "Czech")];
        public string? LastSetCode { get; private set; }

        public event EventHandler? LanguageChanged;

        public Task SetAsync(string code)
        {
            LastSetCode = code;
            Current = Available.First(l => l.Code == code);
            LanguageChanged?.Invoke(this, EventArgs.Empty);
            return Task.CompletedTask;
        }
    }

    private sealed class FakeRecentsService : IRecentsService
    {
        public int ClearCallCount { get; private set; }

        public event EventHandler? RecentsChanged;

        public Task RecordOpenedAsync(string toolId) => Task.CompletedTask;

        public IReadOnlyList<string> GetRecentToolIds() => [];

        public Task ClearAsync()
        {
            ClearCallCount++;
            RecentsChanged?.Invoke(this, EventArgs.Empty);
            return Task.CompletedTask;
        }
    }

    private sealed class FakeConfirmationDialogService(ConfirmationResult result) : IConfirmationDialogService
    {
        public Task<ConfirmationResult> ShowAsync(string title, string text) => Task.FromResult(result);
    }

    private sealed class FakeLauncherService : ILauncherService
    {
        public Uri? LastUri { get; private set; }

        public Task<bool> LaunchUriAsync(Uri uri)
        {
            LastUri = uri;
            return Task.FromResult(true);
        }
    }

    private sealed class FakeAppRestartService : IAppRestartService
    {
        public bool CanRestart { get; set; } = true;
        public int RestartCallCount { get; private set; }

        public void Restart() => RestartCallCount++;
    }

    private sealed class FakeApplication : IApplication
    {
        public ApplicationTheme RequestedTheme => ApplicationTheme.Light;
        public ResourceDictionary Resources => null!;
        public string AppVersion { get; set; } = "0.0.0";

        public void Exit() { }
    }
}
