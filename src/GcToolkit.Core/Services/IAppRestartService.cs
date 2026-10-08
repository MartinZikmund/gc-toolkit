namespace GcToolkit.Core.Services;

/// <summary>Relaunches the app so a pending change (e.g. a new UI language) takes effect.</summary>
public interface IAppRestartService
{
    /// <summary>True when this platform can relaunch the app itself; false hides the affordance.</summary>
    bool CanRestart { get; }

    /// <summary>Relaunches the app. No-op when <see cref="CanRestart"/> is false.</summary>
    void Restart();
}
