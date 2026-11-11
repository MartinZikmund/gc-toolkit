namespace GcToolkit.Core.Infrastructure;

/// <summary>
/// Pushes file writes to durable storage. Only WebAssembly needs it: its file system lives in memory
/// and is copied to IndexedDB later, so a write followed by a quick reload can be lost.
/// </summary>
public interface IStorageFlusher
{
    Task FlushAsync();
}

public sealed class NoOpStorageFlusher : IStorageFlusher
{
    public Task FlushAsync() => Task.CompletedTask;
}
