using System.Globalization;

namespace GcToolkit.Core.Geocaches.Listing;

/// <summary>A parsed CSS colour; just enough of CSS to compare text and background colours.</summary>
internal readonly record struct CssColor(int R, int G, int B, double Alpha)
{
    public static CssColor White { get; } = new(255, 255, 255, 1);

    public bool IsTransparent => Alpha < 0.05;

    public string ToHex() => $"#{R:x2}{G:x2}{B:x2}";

    /// <summary>Straight-line distance in RGB space (0 to about 441).</summary>
    public double DistanceTo(CssColor other)
    {
        double dr = R - other.R;
        double dg = G - other.G;
        double db = B - other.B;
        return Math.Sqrt((dr * dr) + (dg * dg) + (db * db));
    }

    private static readonly Dictionary<string, CssColor> _named = new(StringComparer.OrdinalIgnoreCase)
    {
        ["white"] = new(255, 255, 255, 1),
        ["black"] = new(0, 0, 0, 1),
        ["red"] = new(255, 0, 0, 1),
        ["lime"] = new(0, 255, 0, 1),
        ["green"] = new(0, 128, 0, 1),
        ["blue"] = new(0, 0, 255, 1),
        ["yellow"] = new(255, 255, 0, 1),
        ["cyan"] = new(0, 255, 255, 1),
        ["aqua"] = new(0, 255, 255, 1),
        ["magenta"] = new(255, 0, 255, 1),
        ["fuchsia"] = new(255, 0, 255, 1),
        ["gray"] = new(128, 128, 128, 1),
        ["grey"] = new(128, 128, 128, 1),
        ["silver"] = new(192, 192, 192, 1),
        ["maroon"] = new(128, 0, 0, 1),
        ["olive"] = new(128, 128, 0, 1),
        ["navy"] = new(0, 0, 128, 1),
        ["purple"] = new(128, 0, 128, 1),
        ["teal"] = new(0, 128, 128, 1),
        ["orange"] = new(255, 165, 0, 1),
        ["whitesmoke"] = new(245, 245, 245, 1),
        ["ivory"] = new(255, 255, 240, 1),
        ["snow"] = new(255, 250, 250, 1),
        ["transparent"] = new(0, 0, 0, 0),
    };

    /// <summary>Parses a single colour value (<c>#rgb</c>, <c>#rrggbb</c>, <c>rgb()</c>, a common name); <c>null</c> if unrecognised.</summary>
    public static CssColor? Parse(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        value = value.Trim();
        if (value.StartsWith('#'))
        {
            return ParseHex(value[1..]);
        }

        if (value.StartsWith("rgb", StringComparison.OrdinalIgnoreCase))
        {
            return ParseRgbFunction(value);
        }

        return _named.TryGetValue(value, out var named) ? named : null;
    }

    /// <summary>Finds the first colour inside a <c>background</c> shorthand such as <c>#000 url(x.png) no-repeat</c>.</summary>
    public static CssColor? ParseFirstIn(string? shorthand)
    {
        if (string.IsNullOrWhiteSpace(shorthand))
        {
            return null;
        }

        var cleaned = System.Text.RegularExpressions.Regex.Replace(shorthand, @"url\([^)]*\)", " ", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        foreach (var token in TokenizeKeepingFunctions(cleaned))
        {
            if (Parse(token) is { } color)
            {
                return color;
            }
        }

        return null;
    }

    private static IEnumerable<string> TokenizeKeepingFunctions(string text)
    {
        var depth = 0;
        var start = -1;
        for (var i = 0; i < text.Length; i++)
        {
            var c = text[i];
            if (c == '(')
            {
                depth++;
            }
            else if (c == ')' && depth > 0)
            {
                depth--;
            }

            var isSeparator = char.IsWhiteSpace(c) && depth == 0;
            if (!isSeparator && start < 0)
            {
                start = i;
            }
            else if (isSeparator && start >= 0)
            {
                yield return text[start..i];
                start = -1;
            }
        }

        if (start >= 0)
        {
            yield return text[start..];
        }
    }

    private static CssColor? ParseHex(string hex)
    {
        if (hex.Length is 3 or 4)
        {
            hex = string.Concat(hex.Select(c => new string(c, 2)));
        }

        if (hex.Length is not (6 or 8) || !int.TryParse(hex[..6], NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var rgb))
        {
            return null;
        }

        var alpha = 1d;
        if (hex.Length == 8)
        {
            if (!int.TryParse(hex[6..], NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var a))
            {
                return null;
            }

            alpha = a / 255d;
        }

        return new CssColor((rgb >> 16) & 0xFF, (rgb >> 8) & 0xFF, rgb & 0xFF, alpha);
    }

    private static CssColor? ParseRgbFunction(string value)
    {
        var open = value.IndexOf('(');
        var close = value.LastIndexOf(')');
        if (open < 0 || close < open)
        {
            return null;
        }

        var parts = value[(open + 1)..close].Split([',', ' ', '/'], StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length is < 3 or > 4)
        {
            return null;
        }

        var channels = new int[3];
        for (var i = 0; i < 3; i++)
        {
            if (!TryParseChannel(parts[i], out channels[i]))
            {
                return null;
            }
        }

        var alpha = 1d;
        if (parts.Length == 4)
        {
            var text = parts[3];
            var isPercent = text.EndsWith('%');
            if (!double.TryParse(isPercent ? text[..^1] : text, NumberStyles.Float, CultureInfo.InvariantCulture, out alpha))
            {
                return null;
            }

            alpha = isPercent ? alpha / 100 : alpha;
        }

        return new CssColor(channels[0], channels[1], channels[2], alpha);
    }

    private static bool TryParseChannel(string text, out int channel)
    {
        channel = 0;
        var isPercent = text.EndsWith('%');
        if (!double.TryParse(isPercent ? text[..^1] : text, NumberStyles.Float, CultureInfo.InvariantCulture, out var number))
        {
            return false;
        }

        channel = (int)Math.Clamp(Math.Round(isPercent ? number * 2.55 : number), 0, 255);
        return true;
    }
}
