using System.Diagnostics.CodeAnalysis;

namespace GcToolkit.Core.Geocaches;

/// <summary>
/// A cache's code (<c>GC8KX2Q</c>, <c>OC1A2B</c>, …), normalized to upper case so two spellings of
/// the same code are equal.
/// </summary>
public readonly record struct GeocacheCode
{
    private GeocacheCode(string value) => Value = value;

    public string Value { get; }

    public bool IsGeocachingCom => Value.StartsWith("GC", StringComparison.Ordinal);

    public static bool TryParse([NotNullWhen(true)] string? input, out GeocacheCode code)
    {
        code = default;
        var trimmed = input?.Trim();
        if (trimmed is not { Length: >= 3 } || !trimmed.All(char.IsAsciiLetterOrDigit))
        {
            return false;
        }

        code = new GeocacheCode(trimmed.ToUpperInvariant());
        return true;
    }

    public static GeocacheCode Parse(string input) =>
        TryParse(input, out var code) ? code : throw new FormatException($"'{input}' is not a valid geocache code.");

    public override string ToString() => Value;
}
