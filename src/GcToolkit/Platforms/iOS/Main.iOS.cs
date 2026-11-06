using UIKit;
using Uno.UI.Hosting;

namespace GcToolkit.iOS;

public class EntryPoint
{
    // This is the main entry point of the application.
    public static void Main(string[] args)
    {
        // Uno 7 keeps ApplicationData settings in a dedicated NSUserDefaults suite; move pre-7.0
        // preferences there before anything reads them. Idempotent, so safe on every launch.
        Uno.Storage.ApplicationDataMigrator.MigrateSettings();

        var host = UnoPlatformHostBuilder.Create()
            .App(() => new App())
            .UseAppleUIKit()
            .Build();

        host.Run();
    }
}
