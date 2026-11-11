using System.IO.Compression;
using System.Reflection;
using System.Threading.Tasks;

namespace GcToolkit.Core.Numbers;

/// <summary>
/// Provides the embedded 1,000,000-decimal expansion of e (gzip-compressed assembly resource),
/// decompressed lazily off the calling thread and cached for the lifetime of the app.
/// </summary>
public static class EulerNumberDigitSource
{
    private const string ResourceName = "GcToolkit.Core.Numbers.EulerNumberDecimals.gz";

    /// <summary>How many decimals the embedded expansion contains.</summary>
    public const int AvailableDecimals = 1_000_000;

    private static readonly Lazy<Task<string>> _decimals = new(() => Task.Run(LoadDecimals));

    /// <summary>The decimals of e as a digit string ("718281828…"), loaded once and cached.</summary>
    public static Task<string> GetDecimalsAsync() => _decimals.Value;

    private static string LoadDecimals()
    {
        using var stream = typeof(EulerNumberDigitSource).GetTypeInfo().Assembly.GetManifestResourceStream(ResourceName)
            ?? throw new InvalidOperationException($"Embedded resource '{ResourceName}' not found.");
        using GZipStream gzip = new(stream, CompressionMode.Decompress);
        using StreamReader reader = new(gzip);
        return reader.ReadToEnd();
    }
}
