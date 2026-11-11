namespace GcToolkit.Core.Services.Files;

/// <summary>A file the user picked. The stream is opened on demand and owned by the caller.</summary>
public sealed record PickedFile(string Name, Func<CancellationToken, Task<Stream>> OpenReadAsync);

public interface IFilePickerService
{
    /// <summary>Lets the user pick any number of files. Returns an empty list when they cancel.</summary>
    /// <param name="extensions">Allowed extensions with the dot, such as <c>.gpx</c>.</param>
    Task<IReadOnlyList<PickedFile>> PickFilesAsync(IReadOnlyList<string> extensions, CancellationToken cancellationToken = default);
}
