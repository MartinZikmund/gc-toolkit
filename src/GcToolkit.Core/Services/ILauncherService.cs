namespace GcToolkit.Core.Services;

/// <summary>Opens a URI with the platform's default handler (browser, store, mail client, …).</summary>
public interface ILauncherService
{
    Task<bool> LaunchUriAsync(Uri uri);
}
