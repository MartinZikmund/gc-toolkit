using GcToolkit.Core.Geocaches;

namespace GcToolkit.Core.Tests.Fakes;

public sealed class InMemoryScratchpadRepository : IScratchpadRepository
{
    private readonly Dictionary<GeocacheCode, string> _texts = [];

    /// <summary>Number of upcoming saves that throw before saving works again.</summary>
    public int FailNextSaves { get; set; }

    public List<(GeocacheCode Code, string Text)> Saves { get; } = [];

    /// <summary>When set, saves wait for it so tests can type during a save.</summary>
    public TaskCompletionSource? SaveGate { get; set; }

    public void Seed(GeocacheCode code, string text) => _texts[code] = text;

    public string? Get(GeocacheCode code) => _texts.GetValueOrDefault(code);

    public Task<string?> LoadAsync(GeocacheCode code, CancellationToken cancellationToken = default) =>
        Task.FromResult(_texts.GetValueOrDefault(code));

    public async Task SaveAsync(GeocacheCode code, string text, CancellationToken cancellationToken = default)
    {
        if (SaveGate is { } gate)
        {
            await gate.Task;
        }

        if (FailNextSaves > 0)
        {
            FailNextSaves--;
            throw new IOException("disk on fire");
        }

        _texts[code] = text;
        Saves.Add((code, text));
    }
}
