using System.Globalization;
using System.Text.Json;
using GcToolkit.Core.Catalog;
using GcToolkit.Core.FavoriteTools;
using GcToolkit.Core.Geocaches;
using GcToolkit.Core.Geocaches.Gpx;
using GcToolkit.Core.Geocaches.Storage;
using GcToolkit.Core.Infrastructure;
using GcToolkit.Core.Localization;
using GcToolkit.Core.Recents;
using GcToolkit.Core.Search;
using GcToolkit.Core.Serialization;
using GcToolkit.Core.Services;
using GcToolkit.Core.Services.Devices;
using GcToolkit.Core.Services.Files;
using GcToolkit.Core.Services.Dialogs;
using GcToolkit.Core.Services.Settings;
using GcToolkit.Core.Services.Theming;
using GcToolkit.Core.ViewModels;
using GcToolkit.Core.Workbench;
using GcToolkit.Services.Devices;
using GcToolkit.Services.Dialogs;
using GcToolkit.Services.Files;
using GcToolkit.Services.Navigation;
using GcToolkit.Services.Rating;
using GcToolkit.Services.Settings;
using GcToolkit.Services.Storage;
using GcToolkit.Services.Theming;
using Uno.Resizetizer;
using IPreferences = MZikmund.Toolkit.WinUI.Services.IPreferences;
using Preferences = MZikmund.Toolkit.WinUI.Services.Preferences;

namespace GcToolkit;

public partial class App : Application, IApplication
{
    public static new App Current => (App)Application.Current;

    public IServiceProvider Services => Host!.Services;

    public string AppVersion
    {
        get
        {
            var version = Windows.ApplicationModel.Package.Current.Id.Version;
            return $"{version.Major}.{version.Minor}.{version.Build}";
        }
    }

    public App()
    {
        this.InitializeComponent();
    }

    protected Window? MainWindow { get; private set; }
    protected IHost? Host { get; private set; }

    protected override async void OnLaunched(LaunchActivatedEventArgs args)
    {
        var builder = this.CreateBuilder(args)
            .Configure(host => host
#if DEBUG
                .UseEnvironment(Environments.Development)
#endif
                .UseLogging(ConfigureLogging, enableUnoLogging: true)
                .UseConfiguration(configure: configBuilder =>
                    configBuilder
                        .EmbeddedSource<App>()
                        .Section<AppConfig>()
                )
                .UseLocalization()
                .UseDefaultServiceProvider((context, options) =>
                {
                    options.ValidateScopes = true;
                    options.ValidateOnBuild = true;
                })
                .UseHttp((context, services) =>
                {
#if DEBUG
                    services.AddTransient<DelegatingHandler, DebugHttpHandler>();
#endif
                })
                .ConfigureServices(RegisterServices)
            );

        MainWindow = builder.Window;

#if DEBUG
        MainWindow.UseStudio();
#endif
        MainWindow.SetWindowIcon();

        Host = builder.Build();
        IoC.SetProvider(Host.Services);

        // Apply the persisted/resolved language before building the shell so the selection takes
        // effect on this launch. Resolving ILanguageService here (before the override) lets it
        // capture the real system language for first-run default resolution (FR-009).
        ApplyLanguage(Host.Services.GetRequiredService<ILanguageService>().Current.Code);

        // Run app lifecycle updates
        var appPreferences = Host.Services.GetRequiredService<IAppPreferences>();
        var appUpdater = Host.Services.GetRequiredService<IAppUpdater>();
        await appUpdater.EnsureAppUpToDateAsync();
        appPreferences.LaunchCount++;

        // Create WindowShell as root content
        if (MainWindow.Content is not WindowShell)
        {
            var shell = new WindowShell(Host.Services, MainWindow);
            MainWindow.Content = shell;
        }

        MainWindow.Activate();
    }

    private static void RegisterServices(HostBuilderContext context, IServiceCollection services)
    {
        // Singleton services
        services.AddSingleton<IApplication>(sp => Current);
        // Complex preferences resolve through the source-generated contracts: trimming (the WASM
        // release publish) turns reflection-based System.Text.Json off, and Preferences would then
        // throw on every stored value. Any new persisted type must be added to the context.
        services.AddSingleton<IPreferences>(_ => new Preferences(
            new JsonSerializerOptions { TypeInfoResolver = PreferencesJsonContext.Default }));
        services.AddSingleton<IAppPreferences, AppPreferences>();

        // Offline geocache store. Only WebAssembly needs the flusher: its file system is copied to
        // IndexedDB asynchronously, so every committed write is flushed explicitly.
#if __WASM__
        services.AddSingleton<IStorageFlusher, WasmStorageFlusher>();
#else
        services.AddSingleton<IStorageFlusher, NoOpStorageFlusher>();
#endif
        services.AddSingleton<IGeocacheStore>(sp => new SqliteGeocacheStore(
            GeocacheDatabaseLocation.GetPathAsync,
            sp.GetRequiredService<IStorageFlusher>()));
        services.AddSingleton<GpxImporter>();
        services.AddSingleton<IDisplayRequestManager, DisplayRequestManager>();
        services.AddSingleton<IAppUpdater, Infrastructure.AppUpdater>();
        services.AddScoped<IAppRatingService, AppRatingService>();

        // Per-window scoped services
        services.AddScoped<IThemeManager, ThemeManager>();
        services.AddScoped<WindowShellProvider>();
        services.AddScoped<IWindowShellProvider>(sp => sp.GetRequiredService<WindowShellProvider>());
        services.AddScoped<IXamlRootProvider>(sp => sp.GetRequiredService<WindowShellProvider>());
        services.AddScoped<IDialogCoordinator, DialogCoordinator>();
        services.AddScoped<IDialogService, DialogService>();
        services.AddScoped<ITextPromptService, TextPromptService>();
        services.AddScoped<IFilePickerService, FilePickerService>();
        services.AddScoped<IConfirmationDialogService, ConfirmationDialogService>();
        services.AddScoped<ILauncherService, LauncherService>();
        services.AddScoped<IShareService, ShareService>();
        services.AddScoped<IClipboardService, ClipboardService>();
        services.AddScoped<IMorseAudioService, MorseAudioService>();
        // Device sensors. Scoped: each captures its window's dispatcher and owns the hardware while
        // the tool is open. The compass is only ever a reader; the torch owns the lamp while lit.
        services.AddScoped<ICompassService, CompassService>();
        services.AddScoped<ITorchService, TorchService>();
        services.AddScoped<INavigationService>(sp =>
        {
            var service = new NavigationService(sp.GetRequiredService<IWindowShellProvider>());
            // Generated (ViewRegistrations.g.cs): every view↔ViewModel pair plus each discovered
            // tool ViewModel → the shared ToolHostView. Replaces the hand-written RegisterView block.
            service.RegisterDiscoveredViews();
            return service;
        });

        // Catalog domain (FR-010/FR-015): the matcher is a pure singleton; the catalog is per-window.
        services.AddSingleton<IToolMatcher, ToolMatcher>();
        // Hardware presence is a process-wide fact, so it is probed once and cached: singleton.
        // The policy that reads it is stateless and ANDed into every window's scoped catalog.
        services.AddSingleton<IDeviceCapabilityService, DeviceCapabilityService>();
        services.AddSingleton<IToolAvailabilityPolicy, DeviceCapabilityToolPolicy>();
        // Generated (ToolDiscoveryServiceCollectionExtensions.g.cs): registers the discovered tool
        // contributor (IToolContributor + ICategoryContributor) and each tool ViewModel (transient).
        services.AddDiscoveredTools();
        services.AddScoped<ICatalogService, CatalogService>();

        // Favorite tools, recents, and language selection.
        services.AddScoped<IFavoriteToolsService, FavoriteToolsService>();
        services.AddScoped<IRecentsService, RecentsService>();
        services.AddSingleton<ILanguageService>(sp => new LanguageService(
            sp.GetRequiredService<IPreferences>(),
            CultureInfo.CurrentUICulture.TwoLetterISOLanguageName));

        // Workbench session is app-wide; the progress source is a placeholder until the store lands.
        services.AddSingleton(TimeProvider.System);
        services.AddSingleton<IWorkbenchProgressProvider, EmptyWorkbenchProgressProvider>();
        services.AddSingleton<IWorkbenchSessionService, WorkbenchSessionService>();

        // Scoped ViewModels
        services.AddScoped<WindowShellViewModel>();
        services.AddScoped<SearchViewModel>();

        // Transient ViewModels (new instance per navigation). Discovered tool ViewModels are
        // registered by AddDiscoveredTools() above.
        services.AddTransient<HomeViewModel>();
        services.AddTransient<ContinueSolvingViewModel>();
        services.AddTransient<WorkbenchViewModel>();
        services.AddTransient<CatalogViewModel>();
        services.AddTransient<CachesViewModel>();
        services.AddTransient<SettingsViewModel>();
    }

    /// <summary>
    /// Applies <paramref name="languageCode"/> via the WinRT language override and the current
    /// thread culture. The stock one-shot LocalizeExtension reads these when the shell loads, so a
    /// language change takes effect on the next launch (restart accepted, FR-008/009).
    /// </summary>
    private static void ApplyLanguage(string languageCode)
    {
        Windows.Globalization.ApplicationLanguages.PrimaryLanguageOverride = languageCode;

        var culture = new CultureInfo(languageCode);
        CultureInfo.CurrentCulture = culture;
        CultureInfo.CurrentUICulture = culture;
        CultureInfo.DefaultThreadCurrentCulture = culture;
        CultureInfo.DefaultThreadCurrentUICulture = culture;
    }

    private static void ConfigureLogging(HostBuilderContext context, ILoggingBuilder logBuilder)
    {
        logBuilder
            .SetMinimumLevel(
                context.HostingEnvironment.IsDevelopment() ?
                    LogLevel.Information :
                    LogLevel.Warning)
            .CoreLogLevel(LogLevel.Warning);
    }
}
