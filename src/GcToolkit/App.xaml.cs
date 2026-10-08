using System.Diagnostics;
using System.Globalization;
using GcToolkit.Core.Catalog;
using GcToolkit.Core.FavoriteTools;
using GcToolkit.Core.Infrastructure;
using GcToolkit.Core.Localization;
using GcToolkit.Core.Recents;
using GcToolkit.Core.Search;
using GcToolkit.Core.Services;
using GcToolkit.Core.Services.Dialogs;
using GcToolkit.Core.Services.Settings;
using GcToolkit.Core.Services.Theming;
using GcToolkit.Core.ViewModels;
using GcToolkit.Services.Dialogs;
using GcToolkit.Services.Navigation;
using GcToolkit.Services.Rating;
using GcToolkit.Services.Settings;
using GcToolkit.Services.Theming;
using Microsoft.UI.Text;
using Uno.Resizetizer;
using IPreferences = MZikmund.Toolkit.WinUI.Services.IPreferences;
using Preferences = MZikmund.Toolkit.WinUI.Services.Preferences;

namespace GcToolkit;

public partial class App : Application, IApplication
{
    private bool _isShowingErrorDialog;

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

        // Subscribed here so even a failure during launch is logged and shown instead of taking the
        // process down silently.
        UnhandledException += OnUnhandledException;
        AppDomain.CurrentDomain.UnhandledException += OnDomainUnhandledException;
        TaskScheduler.UnobservedTaskException += OnUnobservedTaskException;
    }

    protected Window? MainWindow { get; private set; }
    protected IHost? Host { get; private set; }

    protected override async void OnLaunched(LaunchActivatedEventArgs args)
    {
        try
        {
            await LaunchAsync(args);
        }
        catch (Exception exception)
        {
            // OnLaunched is `async void`: without this the failure is rethrown on the dispatcher after
            // it returns, and the app vanishes with no error UI and nothing to send back.
            LogCritical("Application launch failed.", exception);
            ShowStartupFailure(exception);
        }
    }

    private async Task LaunchAsync(LaunchActivatedEventArgs args)
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

    #region Failure handling

    private void OnUnhandledException(object sender, Microsoft.UI.Xaml.UnhandledExceptionEventArgs e)
    {
        LogCritical("Unhandled exception on the UI thread.", e.Exception);

        if (MainWindow?.Content is WindowShell shell)
        {
            // The shell is up, so the app stays usable: report the failure instead of dying on it.
            e.Handled = true;
            ShowUnexpectedErrorDialog(shell, e.Exception);
        }
        else if (MainWindow is not null)
        {
            // Nothing can host a dialog yet — the window itself becomes the diagnostic.
            e.Handled = true;
            ShowStartupFailure(e.Exception);
        }
    }

    private void OnDomainUnhandledException(object sender, System.UnhandledExceptionEventArgs e)
        => LogCritical(
            e.IsTerminating ? "Unhandled exception; the process is terminating." : "Unhandled exception.",
            e.ExceptionObject as Exception);

    private void OnUnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e)
    {
        LogCritical("Unobserved task exception.", e.Exception);

        // Logged, so don't let a forgotten task tear the process down.
        e.SetObserved();
    }

    private void ShowUnexpectedErrorDialog(WindowShell shell, Exception? exception)
    {
        if (_isShowingErrorDialog)
        {
            return;
        }

        try
        {
            if (shell.ServiceProvider.GetService<IDialogService>() is not { } dialogService)
            {
                return;
            }

            _isShowingErrorDialog = true;
            _ = ShowUnexpectedErrorDialogAsync(dialogService, exception);
        }
        catch (Exception dialogFailure)
        {
            _isShowingErrorDialog = false;
            LogCritical("Could not report the unhandled exception to the user.", dialogFailure);
        }
    }

    private async Task ShowUnexpectedErrorDialogAsync(IDialogService dialogService, Exception? exception)
    {
        try
        {
            var message = Localize("UnexpectedErrorMessage", "Something went wrong, but the app is still running.");
            var detail = exception?.Message;
            var content = string.IsNullOrWhiteSpace(detail)
                ? message
                : $"{message}{Environment.NewLine}{Environment.NewLine}{detail}";

            await dialogService.ShowAsync(Localize("UnexpectedErrorTitle", "Unexpected error"), content);
        }
        catch (Exception dialogFailure)
        {
            LogCritical("Could not report the unhandled exception to the user.", dialogFailure);
        }
        finally
        {
            _isShowingErrorDialog = false;
        }
    }

    /// <summary>Replaces the (empty) window content with a diagnostic when the app cannot start.</summary>
    private void ShowStartupFailure(Exception exception)
    {
        try
        {
            MainWindow ??= new Window();
            MainWindow.Content = BuildStartupFailureView(exception);
            MainWindow.Activate();
        }
        catch (Exception displayFailure)
        {
            LogCritical("Could not show the startup failure view.", displayFailure);
        }
    }

    private UIElement BuildStartupFailureView(Exception exception)
    {
        StackPanel panel = new()
        {
            MaxWidth = 720,
            Margin = new Thickness(32),
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            Spacing = 12,
        };

        panel.Children.Add(new TextBlock
        {
            Text = Localize("StartupErrorTitle", "The app could not start"),
            FontSize = 24,
            FontWeight = FontWeights.SemiBold,
            TextWrapping = TextWrapping.Wrap,
        });

        panel.Children.Add(new TextBlock
        {
            Text = Localize("StartupErrorMessage", "An unexpected error stopped the app from starting."),
            TextWrapping = TextWrapping.Wrap,
        });

        // The exception text is the only diagnostic a user can send back, so keep it selectable.
        panel.Children.Add(new TextBox
        {
            Text = exception.ToString(),
            IsReadOnly = true,
            AcceptsReturn = true,
            IsSpellCheckEnabled = false,
            TextWrapping = TextWrapping.Wrap,
            MaxHeight = 280,
        });

        Grid root = new();
        if (Application.Current.Resources.TryGetValue("ApplicationPageBackgroundThemeBrush", out var background)
            && background is Brush backgroundBrush)
        {
            root.Background = backgroundBrush;
        }

        root.Children.Add(new ScrollViewer { Content = panel });
        return root;
    }

    /// <summary>
    /// Localized text with an English fallback: a failure early in launch can happen before the host —
    /// and with it <see cref="IStringLocalizer"/> — exists.
    /// </summary>
    private string Localize(string key, string fallback)
    {
        try
        {
            if (Host?.Services.GetService<IStringLocalizer>() is { } localizer)
            {
                var localized = localizer[key];
                return localized.ResourceNotFound ? fallback : localized.Value;
            }
        }
        catch (Exception)
        {
            // Falls through to the built-in text below; this path only runs while already failing.
        }

        return fallback;
    }

    /// <summary>Best-effort logging: a crash handler that throws is worse than no crash handler.</summary>
    private void LogCritical(string message, Exception? exception)
    {
        try
        {
            if (Host?.Services.GetService<ILogger<App>>() is { } logger)
            {
                logger.LogCritical(exception, "{Message}", message);
                return;
            }
        }
        catch (Exception loggingFailure)
        {
            Trace.WriteLine($"{message} Logging that failure also failed: {loggingFailure}");
        }

        // No host (or it is already gone): tracing is the only sink that cannot itself throw.
        Trace.WriteLine($"{message}{Environment.NewLine}{exception}");
    }

    #endregion

    private static void RegisterServices(HostBuilderContext context, IServiceCollection services)
    {
        // Singleton services
        services.AddSingleton<IApplication>(sp => Current);
        services.AddSingleton<IPreferences, Preferences>();
        services.AddSingleton<IAppPreferences, AppPreferences>();
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
        services.AddScoped<IConfirmationDialogService, ConfirmationDialogService>();
        services.AddScoped<ILauncherService, LauncherService>();
        services.AddScoped<IAppRestartService, AppRestartService>();
        services.AddScoped<IShareService, ShareService>();
        services.AddScoped<IClipboardService, ClipboardService>();
        services.AddScoped<IMorseAudioService, MorseAudioService>();
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

        // Scoped ViewModels
        services.AddScoped<WindowShellViewModel>();
        services.AddScoped<SearchViewModel>();

        // Transient ViewModels (new instance per navigation). Discovered tool ViewModels are
        // registered by AddDiscoveredTools() above.
        services.AddTransient<HomeViewModel>();
        services.AddTransient<CatalogViewModel>();
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
