namespace GcToolkit.Core.Tests.Fakes;

/// <summary>Queues posted callbacks until <see cref="RunPosted"/>, standing in for the UI thread.</summary>
internal sealed class RecordingSynchronizationContext : SynchronizationContext
{
    private readonly Queue<(SendOrPostCallback Callback, object? State)> _posted = new();

    public override void Post(SendOrPostCallback d, object? state) => _posted.Enqueue((d, state));

    public void RunPosted()
    {
        while (_posted.TryDequeue(out var item))
        {
            item.Callback(item.State);
        }
    }
}
