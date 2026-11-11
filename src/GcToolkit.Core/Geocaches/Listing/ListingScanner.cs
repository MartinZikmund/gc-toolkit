using System.Globalization;
using System.Text.RegularExpressions;
using GcToolkit.Core.Coordinates;

namespace GcToolkit.Core.Geocaches.Listing;

/// <summary>
/// Finds coordinates and coordinate formulas in plain listing text (HTML must already be stripped).
/// Candidates are located with strict patterns and then confirmed by <see cref="CoordinateParser"/>,
/// so look-alikes such as dates, phone numbers and D/T ratings are left alone.
/// </summary>
public static partial class ListingScanner
{
    private const int TimeoutMs = 1000;

    private const string Degree = "[°º˚]";
    private const string Minute = "['’´′]";
    private const string Second = "[\"”″]";

    // Hemisphere, degrees, optional minutes, optional seconds. Latitude is N/S, longitude is E/W.
    private const string AngularAxis1 =
        $@"(?<h1>[NS])\s*(?<d1>\d{{1,2}}(?:[.,]\d+)?)(?:(?:\s*{Degree}\s*|\s+)(?<m1>\d{{1,2}}(?:[.,]\d+)?)(?:(?:\s*{Minute}\s*|\s+)(?<s1>\d{{1,2}}(?:[.,]\d+)?)\s*(?:{Second}|{Minute}{{2}})?|\s*{Minute})?|\s*{Degree})?";

    private const string AngularAxis2 =
        $@"(?<h2>[EW])\s*(?<d2>\d{{1,3}}(?:[.,]\d+)?)(?:(?:\s*{Degree}\s*|\s+)(?<m2>\d{{1,2}}(?:[.,]\d+)?)(?:(?:\s*{Minute}\s*|\s+)(?<s2>\d{{1,2}}(?:[.,]\d+)?)\s*(?:{Second}|{Minute}{{2}})?|\s*{Minute})?|\s*{Degree})?";

    // Letters, digits and a decimal point, with parenthesised arithmetic allowed.
    private const string Expr =
        @"(?:[0-9A-Za-z]|[.,](?=[0-9A-Za-z(])|\((?:[0-9A-Za-z.,+\-*/%^\s]|\([0-9A-Za-z.,+\-*/%^\s]*\))*\))+";

    /// <summary>Returns the detected spans ordered by position, never overlapping.</summary>
    public static IReadOnlyList<ListingSpan> Scan(string? text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return [];
        }

        List<ListingSpan> found = [];
        Collect(() => FindAngular(text, found));
        Collect(() => FindFormulas(text, found));
        Collect(() => FindParsed(SignedPair(), text, found));
        Collect(() => FindParsed(Utm(), text, found));
        Collect(() => FindParsed(MgrsRegex(), text, found));
        Collect(() => FindParsed(British(), text, found));

        // Earliest start wins; at the same start the longer match wins.
        List<ListingSpan> result = [];
        var end = 0;
        foreach (var span in found.OrderBy(s => s.Start).ThenByDescending(s => s.Length))
        {
            if (span.Start >= end)
            {
                result.Add(span);
                end = span.End;
            }
        }

        return result;
    }

    // A pathological input must not take the whole scan down with it.
    private static void Collect(Action find)
    {
        try
        {
            find();
        }
        catch (RegexMatchTimeoutException)
        {
        }
    }

    private static void FindAngular(string text, List<ListingSpan> found)
    {
        foreach (Match m in Angular().Matches(text))
        {
            var lat = CountParts(m, "1");
            var lon = CountParts(m, "2");
            if (lat == lon && AxisIsComplete(m, "1", lat) && AxisIsComplete(m, "2", lon))
            {
                AddIfParses(m, found);
            }
        }
    }

    // Degrees alone ("N 50 E 14") or whole minutes ("N 49 12 E 16 34") are too easy to hit by accident,
    // so the last component needs a fraction unless it is a full degrees-minutes-seconds axis.
    private static bool AxisIsComplete(Match m, string axis, int parts)
    {
        var last = m.Groups[parts switch { 1 => "d", 2 => "m", _ => "s" } + axis].Value;
        return parts == 3 || last.Contains('.') || last.Contains(',');
    }

    private static int CountParts(Match m, string axis) =>
        m.Groups["s" + axis].Success ? 3 : m.Groups["m" + axis].Success ? 2 : 1;

    private static void FindParsed(Regex regex, string text, List<ListingSpan> found)
    {
        foreach (Match m in regex.Matches(text))
        {
            AddIfParses(m, found);
        }
    }

    private static void AddIfParses(Match m, List<ListingSpan> found)
    {
        // The parser only knows '.' as the decimal separator; Czech listings often write ','.
        var normalized = DecimalComma().Replace(m.Value, ".");
        if (CoordinateParser.TryParse(normalized, out var coordinate, out var format))
        {
            found.Add(new CoordinateSpan(m.Index, m.Length, m.Value, coordinate, format));
        }
    }

    private static void FindFormulas(string text, List<ListingSpan> found)
    {
        foreach (Match m in Formula().Matches(text))
        {
            var lat = m.Groups["e1"].Value;
            var lon = m.Groups["e2"].Value;

            var inRange = int.Parse(m.Groups["d1"].Value, CultureInfo.InvariantCulture) <= 90
                && int.Parse(m.Groups["d2"].Value, CultureInfo.InvariantCulture) <= 180;
            if (!inRange || !HasDecimalPoint(lat) || !HasDecimalPoint(lon))
            {
                continue;
            }

            List<string> variables = [];
            foreach (var c in lat + lon)
            {
                var letter = c.ToString();
                if (char.IsAsciiLetter(c) && !variables.Contains(letter))
                {
                    variables.Add(letter);
                }
            }

            // Without a variable it is a plain coordinate, which the angular pass already handles.
            if (variables.Count > 0)
            {
                found.Add(new FormulaSpan(m.Index, m.Length, m.Value, new CoordinateFormula(m.Value, variables)));
            }
        }
    }

    private static bool HasDecimalPoint(string expression) => expression.Contains('.') || expression.Contains(',');

    [GeneratedRegex($@"(?<![\p{{L}}\d]){AngularAxis1}\s*[,;]?\s*{AngularAxis2}(?![\p{{L}}\d])", RegexOptions.None, TimeoutMs)]
    private static partial Regex Angular();

    [GeneratedRegex(
        $@"(?<![\p{{L}}\d])[NS]\s*(?<d1>\d{{1,2}})(?:\s*{Degree}\s*|\s+)(?<e1>{Expr})\s*{Minute}?\s*[,;]?\s*[EW]\s*(?<d2>\d{{1,3}})(?:\s*{Degree}\s*|\s+)(?<e2>{Expr})(?:\s*{Minute})?(?![\p{{L}}\d])",
        RegexOptions.None,
        TimeoutMs)]
    private static partial Regex Formula();

    // Two plain decimals with at least four places each, e.g. "49.205750, 16.576117".
    [GeneratedRegex($@"(?<![\p{{L}}\d.,])-?\d{{1,2}}\.\d{{4,}}\s*{Degree}?\s*[,;]?\s*-?\d{{1,3}}\.\d{{4,}}(?:\s*{Degree})?(?![\p{{L}}\d])", RegexOptions.None, TimeoutMs)]
    private static partial Regex SignedPair();

    [GeneratedRegex(@"(?<![\p{L}\d])\d{1,2}[C-HJ-NP-X]\s+\d{6,7}\s+\d{6,8}(?!\d)", RegexOptions.None, TimeoutMs)]
    private static partial Regex Utm();

    [GeneratedRegex(@"(?<![\p{L}\d])\d{1,2}[C-HJ-NP-X]\s?[A-HJ-NP-Z][A-HJ-NP-V]\s?(?:\d{1,5}\s\d{1,5}|\d{2,10})(?![\p{L}\d])", RegexOptions.None, TimeoutMs)]
    private static partial Regex MgrsRegex();

    [GeneratedRegex(@"(?<![\p{L}\d])[HJNOST][A-HJ-Z](?:\s\d{3,5}\s\d{3,5}|\s?\d{6,10})(?![\p{L}\d])", RegexOptions.None, TimeoutMs)]
    private static partial Regex British();

    [GeneratedRegex(@"(?<=\d),(?=\d)")]
    private static partial Regex DecimalComma();
}
