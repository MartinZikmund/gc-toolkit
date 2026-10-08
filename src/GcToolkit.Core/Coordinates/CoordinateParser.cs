using System.Globalization;
using System.Text.RegularExpressions;

namespace GcToolkit.Core.Coordinates;

/// <summary>
/// Parses the textual coordinate notations geocaching tools accept. Latitude/longitude formats take a
/// hemisphere letter (N/S/E/W) or a signed number, allow flexible whitespace, and treat the
/// <c>° ' "</c> symbols as optional. Numbers may use a dot or a comma as their decimal separator, whatever
/// the current culture is. UTM and MGRS grids are detected by their distinctive shape.
/// Every entry point returns <see langword="false"/> on unparseable input rather than throwing.
/// </summary>
public static partial class CoordinateParser
{
    /// <summary>Auto-detects the notation of <paramref name="text"/> and parses it.</summary>
    public static bool TryParse(string text, out GeoCoordinate coordinate, out CoordinateFormat detected)
    {
        coordinate = default;
        detected = CoordinateFormat.DecimalDegrees;

        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        var trimmed = text.Trim();

        // Grids first — their shape (digits + band/square letters) does not collide with angular input.
        if (Mgrs.TryParse(trimmed, out coordinate))
        {
            // USNG shares the MGRS notation; report MGRS (they are identical on WGS84).
            detected = CoordinateFormat.Mgrs;
            return true;
        }

        // British National Grid: two grid letters followed by an even run of digits.
        if (BritishGrid.TryParse(trimmed, out coordinate))
        {
            detected = CoordinateFormat.BritishGrid;
            return true;
        }

        if (Utm.TryParse(trimmed, out var utm))
        {
            coordinate = Utm.ToLatLon(utm);
            detected = CoordinateFormat.Utm;
            return true;
        }

        // Dutch RD: two large metric numbers inside the Netherlands extent (range-guarded so plain
        // lat/lon pairs are not mistaken for RD).
        if (DutchRd.TryParse(trimmed, out var rd))
        {
            coordinate = DutchRd.ToLatLon(rd);
            detected = CoordinateFormat.DutchRd;
            return true;
        }

        // British National Grid, all-numeric form (e.g. "651409 313177"). Tried after the Dutch RD so an
        // ambiguous metric pair inside the Netherlands extent still reads as RD, matching the reference site.
        if (BritishGrid.TryParseNumeric(trimmed, out coordinate))
        {
            detected = CoordinateFormat.BritishGrid;
            return true;
        }

        // Angular: the per-axis numeric component count selects DD (1), DDM (2) or DMS (3).
        if (TryParseAngular(trimmed, out coordinate, out detected))
        {
            return true;
        }

        return false;
    }

    /// <summary>Parses <paramref name="text"/> as a specific <paramref name="format"/>.</summary>
    public static bool TryParse(string text, CoordinateFormat format, out GeoCoordinate coordinate)
    {
        coordinate = default;
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        var trimmed = text.Trim();
        switch (format)
        {
            case CoordinateFormat.Utm:
                if (Utm.TryParse(trimmed, out var utm))
                {
                    coordinate = Utm.ToLatLon(utm);
                    return true;
                }

                return false;

            case CoordinateFormat.Mgrs:
                return Mgrs.TryParse(trimmed, out coordinate);

            case CoordinateFormat.Usng:
                return Usng.TryParse(trimmed, out coordinate);

            case CoordinateFormat.BritishGrid:
                return BritishGrid.TryParse(trimmed, out coordinate)
                    || BritishGrid.TryParseNumeric(trimmed, out coordinate);

            case CoordinateFormat.DutchRd:
                if (DutchRd.TryParse(trimmed, out var rd))
                {
                    coordinate = DutchRd.ToLatLon(rd);
                    return true;
                }

                return false;

            default:
                return TryParseAngular(trimmed, out coordinate, out var detected) && detected == format;
        }
    }

    // ---- Angular parsing ----

    private static bool TryParseAngular(string text, out GeoCoordinate coordinate, out CoordinateFormat detected)
    {
        coordinate = default;
        detected = CoordinateFormat.DecimalDegrees;

        // Czech (and most of Europe) writes "49 12,345". Fold the decimal comma into a dot before anything
        // counts numbers, so the component count — which selects DD/DDM/DMS below — sees the real values.
        text = NormalizeDecimalCommas(text);

        var (latText, lonText) = SplitAxes(text);
        if (latText is null || lonText is null)
        {
            return false;
        }

        if (!TryParseComponent(latText, isLatitude: true, out var lat, out var latComponents) ||
            !TryParseComponent(lonText, isLatitude: false, out var lon, out var lonComponents))
        {
            return false;
        }

        var coord = new GeoCoordinate(lat, lon);
        if (!coord.IsValid)
        {
            return false;
        }

        coordinate = coord;
        detected = Math.Max(latComponents, lonComponents) switch
        {
            1 => CoordinateFormat.DecimalDegrees,
            2 => CoordinateFormat.DegreesDecimalMinutes,
            _ => CoordinateFormat.DegreesMinutesSeconds,
        };
        return true;
    }

    /// <summary>Rewrites decimal commas ("49 12,345") as dots so the tokenizer and the culture-invariant
    /// <c>double.Parse</c> below see a single decimal separator. The rewrite preserves length, so the callers'
    /// index arithmetic still holds. A comma whose number already carries a dot ("49.205, 16.576") is left
    /// alone — there it divides the two axes rather than a number's integer and fractional parts.</summary>
    private static string NormalizeDecimalCommas(string text)
    {
        if (!text.Contains(','))
        {
            return text;
        }

        var chars = text.ToCharArray();
        for (var i = 1; i < chars.Length - 1; i++)
        {
            if (chars[i] is ',' &&
                char.IsAsciiDigit(chars[i - 1]) &&
                char.IsAsciiDigit(chars[i + 1]) &&
                !NumberRunHasDot(chars, i))
            {
                chars[i] = '.';
            }
        }

        return new string(chars);
    }

    /// <summary>Reports whether the uninterrupted number run around <paramref name="index"/> already
    /// contains a dot, which rules the comma out as a decimal separator.</summary>
    private static bool NumberRunHasDot(char[] chars, int index)
    {
        for (var i = index - 1; i >= 0 && IsNumberRunChar(chars[i]); i--)
        {
            if (chars[i] is '.')
            {
                return true;
            }
        }

        for (var i = index + 1; i < chars.Length && IsNumberRunChar(chars[i]); i++)
        {
            if (chars[i] is '.')
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsNumberRunChar(char c) => char.IsAsciiDigit(c) || c is '.' or ',';

    /// <summary>Splits a two-axis string into its latitude and longitude halves. The boundary is the
    /// N/S→E/W hemisphere change, an explicit comma, or (for signed input) the midpoint of the numbers.</summary>
    private static (string? Lat, string? Lon) SplitAxes(string text)
    {
        // Hemisphere-letter form: split right before the longitude's E/W letter.
        var ewMatch = LongitudeHemisphere().Match(text);
        if (NorthSouthLeading().IsMatch(text) && ewMatch.Success && ewMatch.Index > 0)
        {
            var lat = text[..ewMatch.Index].Trim();
            var lon = text[ewMatch.Index..].Trim();
            return (lat, lon);
        }

        // Explicit comma separator. Decimal commas are already folded to dots, so a lone remaining comma
        // can only be the axis divider.
        var comma = text.IndexOf(',');
        if (comma > 0 && text.IndexOf(',', comma + 1) < 0)
        {
            return (text[..comma].Trim(), text[(comma + 1)..].Trim());
        }

        // Signed/plain numeric form: tokenize numbers and split into two equal halves.
        var numbers = NumberToken().Matches(text);
        if (numbers.Count is 2 or 4 or 6 && numbers.Count % 2 == 0)
        {
            var half = numbers.Count / 2;
            var boundary = numbers[half].Index;
            return (text[..boundary].Trim(), text[boundary..].Trim());
        }

        return (null, null);
    }

    /// <summary>Parses one axis (latitude or longitude) and reports how many numeric components it had
    /// (1 = DD, 2 = DDM, 3 = DMS), so the caller can classify the overall format.</summary>
    private static bool TryParseComponent(string text, bool isLatitude, out double value, out int componentCount)
    {
        value = 0.0;
        componentCount = 0;

        var sign = 1;
        var working = text.Trim();

        var hemisphere = working.FirstOrDefault(char.IsLetter);
        if (hemisphere != default)
        {
            var upper = char.ToUpperInvariant(hemisphere);
            if (isLatitude && upper is not ('N' or 'S'))
            {
                return false;
            }

            if (!isLatitude && upper is not ('E' or 'W'))
            {
                return false;
            }

            if (upper is 'S' or 'W')
            {
                sign = -1;
            }

            working = working.Replace(hemisphere.ToString(), string.Empty, StringComparison.OrdinalIgnoreCase);
        }

        // Strip the angular symbols; what remains is a whitespace-separated number list.
        working = working.Replace('°', ' ').Replace('\'', ' ').Replace('"', ' ')
            .Replace('′', ' ').Replace('″', ' ').Trim();

        if (working.StartsWith('-'))
        {
            sign = -1;
            working = working[1..].Trim();
        }
        else if (working.StartsWith('+'))
        {
            working = working[1..].Trim();
        }

        var parts = NumberToken().Matches(working);
        if (parts.Count is < 1 or > 3)
        {
            return false;
        }

        componentCount = parts.Count;
        var degrees = double.Parse(parts[0].Value, CultureInfo.InvariantCulture);
        var minutes = parts.Count > 1 ? double.Parse(parts[1].Value, CultureInfo.InvariantCulture) : 0.0;
        var seconds = parts.Count > 2 ? double.Parse(parts[2].Value, CultureInfo.InvariantCulture) : 0.0;

        // Minutes/seconds must be sub-60; degrees alone may carry a fractional part (decimal degrees).
        if ((parts.Count > 1 && minutes >= 60.0) || (parts.Count > 2 && seconds >= 60.0))
        {
            return false;
        }

        value = sign * (degrees + minutes / 60.0 + seconds / 3600.0);
        return true;
    }

    [GeneratedRegex(@"[+-]?\d+(?:\.\d+)?")]
    private static partial Regex NumberToken();

    [GeneratedRegex(@"[EeWw]")]
    private static partial Regex LongitudeHemisphere();

    [GeneratedRegex(@"^\s*[NnSs]")]
    private static partial Regex NorthSouthLeading();
}
