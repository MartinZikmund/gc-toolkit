using CommunityToolkit.Mvvm.Messaging;
using GcToolkit.Core.Geocaches;
using GcToolkit.Core.Messages;
using MZikmund.Toolkit.WinUI.Services;

namespace GcToolkit.Core.Workbench;

/// <summary>
/// Keeps the open caches in memory and saves every change to preferences. On construction it
/// restores the previous run; a cache missing from the store keeps its tab, so the workbench can show it as not found.
/// </summary>
public sealed class WorkbenchSessionService : IWorkbenchSessionService
{
    private const string StorageKey = "workbench.session";

    private readonly IPreferences _preferences;
    private readonly IMessenger _messenger;
    private readonly List<GeocacheCode> _open = [];

    public WorkbenchSessionService(IPreferences preferences, IMessenger? messenger = null)
    {
        _preferences = preferences;
        _messenger = messenger ?? WeakReferenceMessenger.Default;
        Restore();
    }

    public IReadOnlyList<GeocacheCode> OpenCaches => _open;

    public GeocacheCode? ActiveCache { get; private set; }

    public int RestoredCount { get; private set; }

    public void Open(GeocacheCode code)
    {
        if (!_open.Contains(code))
        {
            _open.Add(code);
        }

        ActiveCache = code;
        Commit();
    }

    public void Close(GeocacheCode code)
    {
        var index = _open.IndexOf(code);
        if (index < 0)
        {
            return;
        }

        _open.RemoveAt(index);
        if (ActiveCache == code)
        {
            ActiveCache = _open.Count == 0 ? null : _open[Math.Min(index, _open.Count - 1)];
        }

        Commit();
    }

    public void Activate(GeocacheCode code)
    {
        if (!_open.Contains(code) || ActiveCache == code)
        {
            return;
        }

        ActiveCache = code;
        Commit();
    }

    public void Move(GeocacheCode code, int newIndex)
    {
        var index = _open.IndexOf(code);
        if (index < 0)
        {
            return;
        }

        _open.RemoveAt(index);
        _open.Insert(Math.Clamp(newIndex, 0, _open.Count), code);
        Commit();
    }

    private void Restore()
    {
        WorkbenchSessionState? state;
        try
        {
            state = _preferences.GetComplex<WorkbenchSessionState?>(StorageKey, null);
        }
        catch (Exception)
        {
            return;
        }

        if (state?.OpenCaches is null)
        {
            return;
        }

        foreach (var text in state.OpenCaches)
        {
            if (GeocacheCode.TryParse(text, out var code) && !_open.Contains(code))
            {
                _open.Add(code);
            }
        }

        RestoredCount = _open.Count;
        ActiveCache = GeocacheCode.TryParse(state.ActiveCache, out var active) && _open.Contains(active)
            ? active
            : _open.Count > 0 ? _open[0] : null;
    }

    private void Commit()
    {
        _preferences.SetComplex(
            StorageKey,
            new WorkbenchSessionState(_open.Select(c => c.Value).ToList(), ActiveCache?.Value));
        _messenger.Send(new WorkbenchSessionChangedMessage(_open.ToList(), ActiveCache));
    }
}
