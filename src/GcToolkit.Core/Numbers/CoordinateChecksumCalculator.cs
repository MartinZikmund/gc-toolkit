using System.Globalization;
using System.Text.RegularExpressions;
using GcToolkit.Core.Coordinates;

namespace GcToolkit.Core.Numbers;

/// <summary>How the digits of a coordinate line are collected.</summary>
public enum CoordinateChecksumMode
{
    /// <summary>Parse the coordinate, write it as degrees decimal minutes, then sum — the figure a cache page lists.</summary>
    DegreesDecimalMinutes,

    /// <summary>Sum the digits exactly as typed (Plus Codes, UTM strings, Wherigo codes…).</summary>
    AsEntered,
}

/// <summary>One scored line. <see cref="Latitude"/>/<see cref="Longitude"/> are only known in DDM mode.</summary>
public sealed record CoordinateChecksumRow(
    string Input,
    bool IsValid,
    string Normalized,
    long? Latitude,
    long? Longitude,
    long Total,
    long Root,
    long? Expected)
{
    /// <summary><see langword="null"/> when no expected checksum was given or the line is invalid.</summary>
    public bool? Matches => IsValid && Expected is { } expected ? expected == Total : null;
}

/// <summary>
/// The coordinate checksum owners publish on cache pages: the sum of every digit of a coordinate written
/// as degrees decimal minutes. A trailing <c>= 62</c> on a line verifies it against the published value.
/// </summary>
public static partial class CoordinateChecksumCalculator
{
    /// <summary>Rows beyond this are ignored; a big paste stays instant.</summary>
    public const int MaxRows = 500;

    public static IReadOnlyList<CoordinateChecksumRow> Calculate(string? text, CoordinateChecksumMode mode)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return [];
        }

        List<CoordinateChecksumRow> rows = [];
        foreach (var line in text.Split('\n'))
        {
            if (rows.Count >= MaxRows)
            {
                break;
            }

            if (!string.IsNullOrWhiteSpace(line))
            {
                rows.Add(CalculateLine(line, mode));
            }
        }

        return rows;
    }

    public static CoordinateChecksumRow CalculateLine(string line, CoordinateChecksumMode mode)
    {
        var trimmed = line.Trim();
        long? expected = null;

        var match = ExpectedSuffix().Match(trimmed);
        if (match.Success)
        {
            trimmed = match.Groups["body"].Value.TrimEnd();
            if (long.TryParse(match.Groups["value"].Value, NumberStyles.None, CultureInfo.InvariantCulture, out var parsed))
            {
                expected = parsed;
            }
        }

        return mode == CoordinateChecksumMode.AsEntered
            ? AsEntered(trimmed, expected)
            : Ddm(trimmed, expected);
    }

    private static CoordinateChecksumRow AsEntered(string text, long? expected)
    {
        var (sum, count) = SumDigits(text);
        return count == 0
            ? Invalid(text, expected)
            : new CoordinateChecksumRow(text, true, text, null, null, sum, ChecksumCalculator.DigitalRoot(sum), expected);
    }

    private static CoordinateChecksumRow Ddm(string text, long? expected)
    {
        if (!CoordinateParser.TryParse(text, out var coordinate, out _) || !coordinate.IsValid)
        {
            return Invalid(text, expected);
        }

        var normalized = CoordinateFormatter.Format(coordinate, CoordinateFormat.DegreesDecimalMinutes);

        // Latitude is written first; the longitude starts at its E/W hemisphere letter.
        var split = normalized.IndexOfAny(['E', 'W']);
        var latitude = SumDigits(normalized[..split]).Sum;
        var longitude = SumDigits(normalized[split..]).Sum;
        var total = latitude + longitude;

        return new CoordinateChecksumRow(text, true, normalized, latitude, longitude, total, ChecksumCalculator.DigitalRoot(total), expected);
    }

    private static CoordinateChecksumRow Invalid(string text, long? expected)
        => new(text, false, string.Empty, null, null, 0, 0, expected);

    private static (long Sum, int Count) SumDigits(string text)
    {
        long sum = 0;
        var count = 0;
        foreach (var character in text)
        {
            if (char.IsAsciiDigit(character))
            {
                sum += character - '0';
                count++;
            }
        }

        return (sum, count);
    }

    [GeneratedRegex(@"^(?<body>.*?)\s*=\s*(?<value>\d+)$", RegexOptions.CultureInvariant)]
    private static partial Regex ExpectedSuffix();
}
