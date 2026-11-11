using GcToolkit.Core.Services.Files;

namespace GcToolkit.Core.Tests.Fakes;

public sealed class FakeFilePickerService : IFilePickerService
{
    public List<PickedFile> Files { get; } = [];

    public void AddFixture(string fixtureName, string? asName = null)
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Geocaches", "Fixtures", fixtureName);
        Files.Add(new(asName ?? fixtureName, _ => Task.FromResult<Stream>(File.OpenRead(path))));
    }

    public void AddBytes(string name, byte[] bytes)
        => Files.Add(new(name, _ => Task.FromResult<Stream>(new MemoryStream(bytes))));

    public Task<IReadOnlyList<PickedFile>> PickFilesAsync(IReadOnlyList<string> extensions, CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<PickedFile>>([.. Files]);
}
