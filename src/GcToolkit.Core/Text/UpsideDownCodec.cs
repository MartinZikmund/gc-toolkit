using System.Globalization;
using System.Text;

namespace GcToolkit.Core.Text;

/// <summary>Which way to run the transform. <see cref="Auto"/> picks one from the input.</summary>
public enum UpsideDownDirection
{
    Auto,
    Flip,
    Unflip,
}

/// <summary>Glyph set used when flipping. Unflipping always understands the full set.</summary>
public enum UpsideDownVariant
{
    /// <summary>Distinct upper- and lower-case glyphs plus extended punctuation.</summary>
    Full,

    /// <summary>Widely supported subset: upper-case folds onto lower-case glyphs; basic punctuation only.</summary>
    Compatible,
}

/// <summary>What to do with characters that have no rotated form.</summary>
public enum UpsideDownUnmapped
{
    Keep,
    Placeholder,
    Remove,
}

public enum UpsideDownCase
{
    None,
    Upper,
    Lower,
    FirstLetter,
}

/// <param name="Direction">Flip, unflip, or auto-detect.</param>
/// <param name="Reverse">Reverse the character order (the "mirror" half of the trick).</param>
/// <param name="Rotate">Substitute rotated glyphs (the "rotate" half of the trick).</param>
/// <param name="PerLine">Transform each line on its own, keeping line order.</param>
/// <param name="Case">Case applied to the readable side (input when flipping, output when unflipping).</param>
/// <param name="GroupSize">Insert a space every N characters of the result; 0 disables.</param>
public sealed record UpsideDownOptions(
    UpsideDownDirection Direction = UpsideDownDirection.Flip,
    bool Reverse = true,
    bool Rotate = true,
    UpsideDownVariant Variant = UpsideDownVariant.Full,
    UpsideDownUnmapped Unmapped = UpsideDownUnmapped.Keep,
    bool PerLine = false,
    UpsideDownCase Case = UpsideDownCase.None,
    bool RemoveSpaces = false,
    int GroupSize = 0);

/// <param name="Text">The transformed text.</param>
/// <param name="ResolvedDirection">The direction actually used (never <see cref="UpsideDownDirection.Auto"/>).</param>
/// <param name="UnmappedCharacters">Distinct characters with no rotated form, in order of appearance.</param>
/// <param name="MappedCount">Non-whitespace characters that rotated (or are symmetric).</param>
/// <param name="TotalCount">Non-whitespace characters in the input.</param>
/// <param name="RoundTrips"><see langword="true"/> when running the opposite direction restores the input exactly.</param>
public sealed record UpsideDownResult(
    string Text,
    UpsideDownDirection ResolvedDirection,
    IReadOnlyList<string> UnmappedCharacters,
    int MappedCount,
    int TotalCount,
    bool RoundTrips);

/// <summary>
/// Upside-down text: swaps characters for their 180°-rotated Unicode look-alikes and reverses the
/// order. The substitution is an involution, so flip and unflip are the same operation (flipping
/// flipped text restores it); the direction only matters for labelling, case handling and auto-detect.
/// </summary>
public static class UpsideDownCodec
{
    private const string Placeholder = "▯";

    // Pairs rotate into each other in both directions.
    private static readonly (string A, string B)[] _lowerPairs =
    [
        ("a", "ɐ"), ("b", "q"), ("c", "ɔ"), ("d", "p"), ("e", "ǝ"), ("f", "ɟ"), ("g", "ƃ"), ("h", "ɥ"),
        ("i", "ᴉ"), ("j", "ɾ"), ("k", "ʞ"), ("m", "ɯ"), ("n", "u"), ("r", "ɹ"), ("t", "ʇ"), ("v", "ʌ"),
        ("w", "ʍ"), ("y", "ʎ"),
    ];

    private static readonly (string A, string B)[] _digitPairs =
    [
        ("1", "Ɩ"), ("2", "ᄅ"), ("3", "Ɛ"), ("4", "ㄣ"), ("5", "ϛ"), ("6", "9"), ("7", "ㄥ"),
    ];

    private static readonly (string A, string B)[] _basicPunctuation =
    [
        (".", "˙"), (",", "'"), ("?", "¿"), ("!", "¡"), ("(", ")"), ("[", "]"), ("{", "}"), ("<", ">"),
    ];

    private static readonly (string A, string B)[] _upperPairs =
    [
        ("A", "∀"), ("B", "ᗺ"), ("C", "Ɔ"), ("D", "ᗡ"), ("E", "Ǝ"), ("F", "Ⅎ"), ("G", "⅁"), ("J", "ſ"),
        ("K", "ꓘ"), ("L", "⅂"), ("M", "W"), ("P", "Ԁ"), ("Q", "Ό"), ("R", "ᴚ"), ("T", "⊥"), ("U", "∩"),
        ("V", "Λ"), ("Y", "⅄"),
    ];

    private static readonly (string A, string B)[] _extendedPunctuation =
    [
        ("&", "⅋"), ("_", "‾"), (";", "؛"), ("\"", "„"),
    ];

    // Look the same rotated, so they map to themselves.
    private static readonly string[] _symmetric =
        ["l", "o", "s", "x", "z", "0", "8", "H", "I", "N", "O", "S", "X", "Z"];

    private static readonly Dictionary<string, string> _fullMap = BuildMap(compatible: false);
    private static readonly Dictionary<string, string> _compatibleMap = BuildMap(compatible: true);

    public static UpsideDownResult Transform(string text, UpsideDownOptions options)
    {
        text = (text ?? string.Empty).Replace("\r\n", "\n").Replace('\r', '\n');
        if (options.RemoveSpaces)
        {
            text = string.Concat(text.Where(c => c == '\n' || !char.IsWhiteSpace(c)));
        }

        var direction = options.Direction == UpsideDownDirection.Auto
            ? (LooksFlipped(text) ? UpsideDownDirection.Unflip : UpsideDownDirection.Flip)
            : options.Direction;
        var opposite = direction == UpsideDownDirection.Flip ? UpsideDownDirection.Unflip : UpsideDownDirection.Flip;

        var source = direction == UpsideDownDirection.Flip ? ApplyCase(text, options.Case) : text;
        var core = Run(source, direction, options, options.Unmapped);

        var (mapped, total, unmapped) = Coverage(source, direction, options);

        // Compare against the un-cased source: case is a presentation choice, not a loss.
        var back = Run(Run(text, direction, options, UpsideDownUnmapped.Keep), opposite, options, UpsideDownUnmapped.Keep);
        var roundTrips = string.Equals(back, text, StringComparison.Ordinal);

        if (direction == UpsideDownDirection.Unflip)
        {
            core = ApplyCase(core, options.Case);
        }

        return new UpsideDownResult(Group(core, options.GroupSize), direction, unmapped, mapped, total, roundTrips);
    }

    private static string Run(string text, UpsideDownDirection direction, UpsideDownOptions options, UpsideDownUnmapped unmapped)
    {
        if (text.Length == 0)
        {
            return text;
        }

        return options.PerLine
            ? string.Join('\n', text.Split('\n').Select(line => RunLine(line, direction, options, unmapped)))
            : RunLine(text, direction, options, unmapped);
    }

    private static string RunLine(string text, UpsideDownDirection direction, UpsideDownOptions options, UpsideDownUnmapped unmapped)
    {
        var map = MapFor(direction, options);
        var result = new List<string>();
        foreach (var element in Graphemes(text))
        {
            if (!options.Rotate || element == "\n")
            {
                result.Add(element);
            }
            else if (map.TryGetValue(element, out var rotated))
            {
                result.Add(rotated);
            }
            else if (char.IsWhiteSpace(element[0]))
            {
                result.Add(element);
            }
            else if (unmapped == UpsideDownUnmapped.Keep)
            {
                result.Add(element);
            }
            else if (unmapped == UpsideDownUnmapped.Placeholder)
            {
                result.Add(Placeholder);
            }
        }

        if (options.Reverse)
        {
            result.Reverse();
        }

        return string.Concat(result);
    }

    private static (int Mapped, int Total, List<string> Unmapped) Coverage(string text, UpsideDownDirection direction, UpsideDownOptions options)
    {
        var map = MapFor(direction, options);
        var mapped = 0;
        var total = 0;
        var unmapped = new List<string>();
        foreach (var element in Graphemes(text))
        {
            if (char.IsWhiteSpace(element[0]))
            {
                continue;
            }

            total++;
            if (!options.Rotate || map.ContainsKey(element))
            {
                mapped++;
            }
            else if (!unmapped.Contains(element))
            {
                unmapped.Add(element);
            }
        }

        return (mapped, total, unmapped);
    }

    // Unflipping is lenient: it reads any glyph from the full set whatever variant flipped it.
    private static Dictionary<string, string> MapFor(UpsideDownDirection direction, UpsideDownOptions options) =>
        direction == UpsideDownDirection.Flip && options.Variant == UpsideDownVariant.Compatible
            ? _compatibleMap
            : _fullMap;

    // Rotated glyphs live outside ASCII; seeing any of them means the text is already flipped.
    private static bool LooksFlipped(string text) =>
        Graphemes(text).Any(e => e[0] > 0x7F && _fullMap.ContainsKey(e));

    private static IEnumerable<string> Graphemes(string text)
    {
        var enumerator = StringInfo.GetTextElementEnumerator(text);
        while (enumerator.MoveNext())
        {
            yield return (string)enumerator.Current;
        }
    }

    private static string ApplyCase(string text, UpsideDownCase mode) => mode switch
    {
        UpsideDownCase.Upper => text.ToUpperInvariant(),
        UpsideDownCase.Lower => text.ToLowerInvariant(),
        UpsideDownCase.FirstLetter => FirstLetters(text),
        _ => text,
    };

    private static string FirstLetters(string text)
    {
        var builder = new StringBuilder(text.Length);
        var startOfWord = true;
        foreach (var c in text)
        {
            builder.Append(startOfWord ? char.ToUpperInvariant(c) : char.ToLowerInvariant(c));
            startOfWord = char.IsWhiteSpace(c);
        }

        return builder.ToString();
    }

    private static string Group(string text, int size)
    {
        if (size <= 0 || text.Length == 0)
        {
            return text;
        }

        return string.Join('\n', text.Split('\n').Select(line =>
        {
            var elements = Graphemes(line).ToList();
            var builder = new StringBuilder();
            for (var i = 0; i < elements.Count; i++)
            {
                if (i > 0 && i % size == 0)
                {
                    builder.Append(' ');
                }

                builder.Append(elements[i]);
            }

            return builder.ToString();
        }));
    }

    private static Dictionary<string, string> BuildMap(bool compatible)
    {
        var map = new Dictionary<string, string>(StringComparer.Ordinal);

        void AddPairs(IEnumerable<(string A, string B)> pairs)
        {
            foreach (var (a, b) in pairs)
            {
                map.Add(a, b);
                map.Add(b, a);
            }
        }

        AddPairs(_lowerPairs);
        AddPairs(_digitPairs);
        AddPairs(_basicPunctuation);

        foreach (var s in _symmetric.Where(s => !compatible || char.IsLower(s[0]) || char.IsDigit(s[0])))
        {
            map.Add(s, s);
        }

        if (!compatible)
        {
            AddPairs(_upperPairs);
            AddPairs(_extendedPunctuation);
            return map;
        }

        // Compatible: upper-case letters fold onto their lower-case glyph (one-way, so it is lossy).
        foreach (var upper in _upperPairs.Select(p => p.A).Concat(_symmetric.Where(s => char.IsUpper(s[0]))))
        {
            if (map.TryGetValue(upper.ToLowerInvariant(), out var glyph))
            {
                map[upper] = glyph;
            }
        }

        return map;
    }
}
