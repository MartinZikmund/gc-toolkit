using GcToolkit.Core.Services.Files;
using Windows.Storage.Pickers;

namespace GcToolkit.Services.Files;

public sealed class FilePickerService(IWindowShellProvider windowShellProvider) : IFilePickerService
{
    public async Task<IReadOnlyList<PickedFile>> PickFilesAsync(IReadOnlyList<string> extensions, CancellationToken cancellationToken = default)
    {
#if DEBUG
        // Lets UI automation import without driving the system file dialog.
        if (Environment.GetEnvironmentVariable("GCTOOLKIT_PICK_FILES") is { Length: > 0 } scripted)
        {
            return [.. scripted.Split(';', StringSplitOptions.RemoveEmptyEntries).Select(path =>
                new PickedFile(Path.GetFileName(path), _ => Task.FromResult<Stream>(File.OpenRead(path))))];
        }
#endif

        FileOpenPicker picker = new() { SuggestedStartLocation = PickerLocationId.Downloads };
        foreach (var extension in extensions)
        {
            picker.FileTypeFilter.Add(extension);
        }

#if !HAS_UNO
        WinRT.Interop.InitializeWithWindow.Initialize(picker, WinRT.Interop.WindowNative.GetWindowHandle(windowShellProvider.Window));
#endif

        var files = await picker.PickMultipleFilesAsync();
        return [.. files.Select(file => new PickedFile(file.Name, async _ => (await file.OpenReadAsync()).AsStreamForRead()))];
    }
}
