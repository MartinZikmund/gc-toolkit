using GcToolkit.Core.Services;

namespace GcToolkit.Services;

/// <summary>
/// Relaunches the app. Only the packaged WinAppSDK head can restart itself; the Uno heads report
/// <see cref="CanRestart"/> as false so the UI offers no button it cannot honour.
/// </summary>
public sealed class AppRestartService : IAppRestartService
{
#if HAS_UNO
    public bool CanRestart => false;

    public void Restart()
    {
    }
#else
    public bool CanRestart => true;

    public void Restart()
    {
        try
        {
            Microsoft.Windows.AppLifecycle.AppInstance.Restart(string.Empty);
        }
        catch (Exception)
        {
            // Restart needs package identity; without it the user relaunches manually.
        }
    }
#endif
}
