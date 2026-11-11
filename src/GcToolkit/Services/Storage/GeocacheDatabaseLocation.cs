using Windows.Storage;

namespace GcToolkit.Services.Storage;

internal static class GeocacheDatabaseLocation
{
    /// <summary>
    /// Creating the folder through the storage API (rather than building a path) is what makes
    /// WebAssembly load its IndexedDB-backed file system before SQLite opens the file.
    /// </summary>
    public static async Task<string> GetPathAsync()
    {
        var folder = await ApplicationData.Current.LocalFolder.CreateFolderAsync("db", CreationCollisionOption.OpenIfExists);
        return Path.Combine(folder.Path, "geocaches.db3");
    }
}
