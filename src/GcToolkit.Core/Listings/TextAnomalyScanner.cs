using System.Text;
using System.Text.RegularExpressions;

namespace GcToolkit.Core.Listings;

/// <summary>Finds invisible characters, odd whitespace and Base64/hex blobs in plain strings.</summary>
internal static partial class TextAnomalyScanner
{

    public static bool IsInvisible(char c) =>
        c is '\u00AD' or '\u034F' or '\u180E' or '\uFEFF'
        || c is >= '\u200B' and <= '\u200F'
        || c is >= '\u202A' and <= '\u202E'
        || c is >= '\u2060' and <= '\u2064'
        || c is >= '\u2066' and <= '\u2069';

    // Plain NBSP is everywhere in real listings, so it is left out on purpose.
    public static bool IsOddSpace(char c) =>
        c is '\u1680' or '\u202F' or '\u205F' or '\u3000'
        || c is >= '\u2000' and <= '\u200A';

    /// <summary>Runs of invisible characters and unusual spaces in <paramref name="text"/>.</summary>
    public static IEnumerable<HiddenContentFinding> FindCharacterRuns(string text)
    {
        var i = 0;
        while (i < text.Length)
        {
            var invisible = IsInvisible(text[i]);
            if (!invisible && !IsOddSpace(text[i]))
            {
                i++;
                continue;
            }

            var start = i;
            while (i < text.Length && (invisible ? IsInvisible(text[i]) : IsOddSpace(text[i])))
            {
                i++;
            }

            var codePoints = string.Join(' ', text[start..i].Select(c => $"U+{(int)c:X4}"));
            yield return new HiddenContentFinding(
                invisible ? HiddenContentKind.InvisibleCharacters : HiddenContentKind.OddWhitespace,
                codePoints,
                "text",
                Count: i - start,
                Position: start);
        }
    }

    /// <summary>
    /// Lines ending in tabs or several spaces (the usual whitespace-steganography pattern), as one summary finding.
    /// </summary>
    public static HiddenContentFinding? FindTrailingWhitespace(string text)
    {
        var lineCount = 0;
        int? firstPosition = null;
        List<string> samples = [];

        var offset = 0;
        foreach (var rawLine in text.Split('\n'))
        {
            var line = rawLine.TrimEnd('\r');
            var trimmed = line.TrimEnd(' ', '\t');
            var run = line[trimmed.Length..];
            if (trimmed.Length > 0 && (run.Contains('\t') || run.Length >= 3))
            {
                lineCount++;
                firstPosition ??= offset + trimmed.Length;
                if (samples.Count < 3)
                {
                    samples.Add(run.Replace(' ', '\u00B7').Replace("\t", "\u2192"));
                }
            }

            offset += rawLine.Length + 1;
        }

        return lineCount == 0
            ? null
            : new HiddenContentFinding(
                HiddenContentKind.OddWhitespace,
                string.Join(" | ", samples),
                "text",
                "trailing space/tab pattern (\u00B7 = space, \u2192 = tab)",
                lineCount,
                firstPosition);
    }

    /// <summary>Hex and Base64 blobs in <paramref name="text"/>, labelled with where they were found.</summary>
    public static IEnumerable<HiddenContentFinding> FindBlobs(string text, string source)
    {
        HashSet<int> hexStarts = [];

        foreach (Match match in SpacedHexRegex().Matches(text))
        {
            if (HasHexLetter(match.Value))
            {
                hexStarts.Add(match.Index);
                yield return new HiddenContentFinding(HiddenContentKind.HexBlob, match.Value, source, Count: match.Length, Position: null);
            }
        }

        foreach (Match match in HexRegex().Matches(text))
        {
            if (HasHexLetter(match.Value) && hexStarts.Add(match.Index))
            {
                yield return new HiddenContentFinding(HiddenContentKind.HexBlob, match.Value, source, Count: match.Length);
            }
        }

        foreach (Match match in Base64Regex().Matches(text))
        {
            if (!hexStarts.Contains(match.Index) && LooksLikeBase64(match.Value))
            {
                yield return new HiddenContentFinding(HiddenContentKind.Base64Blob, match.Value, source, Count: match.Length);
            }
        }
    }

    private static bool HasHexLetter(string value) => value.Any(c => c is >= 'a' and <= 'f' or >= 'A' and <= 'F');

    private static bool LooksLikeBase64(string value)
    {
        var body = value.TrimEnd('=');
        if (value.Length % 4 != 0 || body.All(Uri.IsHexDigit))
        {
            return false;
        }

        var hasUpper = body.Any(char.IsUpper);
        var hasLower = body.Any(char.IsLower);
        var hasOther = body.Any(c => char.IsDigit(c) || c is '+' or '/') || value.Length != body.Length;
        return hasUpper && hasLower && hasOther && Convert.TryFromBase64String(value, new byte[value.Length], out _);
    }

    /// <summary>Collapses runs of whitespace to single spaces and trims.</summary>
    public static string Normalize(string text)
    {
        var builder = new StringBuilder(text.Length);
        var lastWasSpace = true;
        foreach (var c in text)
        {
            if (char.IsWhiteSpace(c))
            {
                if (!lastWasSpace)
                {
                    builder.Append(' ');
                }

                lastWasSpace = true;
            }
            else
            {
                builder.Append(c);
                lastWasSpace = false;
            }
        }

        return builder.ToString().TrimEnd();
    }

    [GeneratedRegex(@"(?<![0-9A-Za-z])(?:[0-9A-Fa-f]{2}){8,}(?![0-9A-Za-z])")]
    private static partial Regex HexRegex();

    [GeneratedRegex(@"(?<![0-9A-Za-z])(?:[0-9A-Fa-f]{2}[ :-]){7,}[0-9A-Fa-f]{2}(?![0-9A-Za-z])")]
    private static partial Regex SpacedHexRegex();

    [GeneratedRegex(@"(?<![A-Za-z0-9+/=_\-])[A-Za-z0-9+/]{24,}={0,2}(?![A-Za-z0-9+/=_\-])")]
    private static partial Regex Base64Regex();
}
