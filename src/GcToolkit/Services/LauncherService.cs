using GcToolkit.Core.Services;

namespace GcToolkit.Services;

public sealed class LauncherService : ILauncherService
{
    public async Task<bool> LaunchUriAsync(Uri uri)
        => await Windows.System.Launcher.LaunchUriAsync(uri);
}
