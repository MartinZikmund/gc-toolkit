#if __WASM__
using System.Runtime.InteropServices.JavaScript;
using GcToolkit.Core.Infrastructure;

namespace GcToolkit.Services.Storage;

/// <summary>Flushes the browser file system to IndexedDB via emscripten's <c>FS.syncfs</c> (see StorageFlusher.js).</summary>
internal sealed partial class WasmStorageFlusher : IStorageFlusher
{
    public async Task FlushAsync() => await SyncFs();

    [JSImport("globalThis.gcToolkitSyncFs")]
    private static partial Task<bool> SyncFs();
}
#endif
