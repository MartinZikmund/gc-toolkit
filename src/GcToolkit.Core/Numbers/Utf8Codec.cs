using System.Globalization;
using System.Text;

namespace GcToolkit.Core.Numbers;

public enum Utf8Representation { Hex, Decimal, Binary, Octal, Percent, CodePoints, Utf16BE, Utf16LE, Utf32BE, Utf32LE }

public enum Utf8Separator { Space, None, Comma, ZeroX, EscapeX }

public enum Utf8Error { None, OddLength, InvalidHex, InvalidSequence, InvalidCodePoint, LoneSurrogate }

public readonly record struct Utf8Options(
    Utf8Representation Representation, Utf8Separator Separator, bool Uppercase, bool EmitBom);

public readonly record struct Utf8Result(string Output, Utf8Error Error);

/// <summary>One character's row in the breakdown table.</summary>
public sealed record Utf8CharInfo(
    string Glyph, string CodePoint, int ByteCount, string Hex, string Decimal, string Binary, string Octal);

/// <summary>Text ⇄ UTF-8 (and sibling encodings) converter with a forgiving, strict-validating decoder.</summary>
public static class Utf8Codec
{
    private static readonly char[] _separators = [' ', '\t', '\r', '\n', ',', ';', ':', '-', '_', '|'];

    public static Utf8Result Encode(string text, Utf8Options options)
    {
        if (string.IsNullOrEmpty(text))
        {
            return new(string.Empty, Utf8Error.None);
        }

        if (!TryRunes(text, out var runes))
        {
            return new(string.Empty, Utf8Error.LoneSurrogate);
        }

        if (options.Representation == Utf8Representation.CodePoints)
        {
            return new(string.Join(' ', runes.Select(r => FormatCodePoint(r, options.Uppercase))), Utf8Error.None);
        }

        var body = GetEncoding(options.Representation).GetBytes(text);
        var bytes = options.EmitBom ? [.. Bom(options.Representation), .. body] : body;
        return new(Format(bytes, options), Utf8Error.None);
    }

    public static Utf8Result Decode(string input)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            return new(string.Empty, Utf8Error.None);
        }

        return input.Contains("U+", StringComparison.OrdinalIgnoreCase)
            ? DecodeCodePoints(input)
            : DecodeBytes(input);
    }

    public static IReadOnlyList<Utf8CharInfo> Breakdown(string text, int max = 500)
    {
        var rows = new List<Utf8CharInfo>();
        if (!TryRunes(text, out var runes))
        {
            return rows;
        }

        foreach (var rune in runes.Take(max))
        {
            var bytes = Encoding.UTF8.GetBytes(rune.ToString());
            rows.Add(new(
                Glyph(rune),
                FormatCodePoint(rune, true),
                bytes.Length,
                string.Join(' ', bytes.Select(b => b.ToString("X2", CultureInfo.InvariantCulture))),
                string.Join(' ', bytes.Select(b => b.ToString(CultureInfo.InvariantCulture))),
                string.Join(' ', bytes.Select(b => Convert.ToString(b, 2).PadLeft(8, '0'))),
                string.Join(' ', bytes.Select(b => Convert.ToString(b, 8).PadLeft(3, '0')))));
        }

        return rows;
    }

    private static Utf8Result DecodeCodePoints(string input)
    {
        var sb = new StringBuilder();
        foreach (var token in input.Split(_separators, StringSplitOptions.RemoveEmptyEntries))
        {
            var hex = token.StartsWith("U+", StringComparison.OrdinalIgnoreCase) ? token[2..] : token;
            if (!int.TryParse(hex, NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out var value))
            {
                return new(string.Empty, Utf8Error.InvalidHex);
            }

            if (!Rune.IsValid(value))
            {
                return new(string.Empty, Utf8Error.InvalidCodePoint);
            }

            sb.Append(new Rune(value).ToString());
        }

        return new(sb.ToString(), Utf8Error.None);
    }

    private static Utf8Result DecodeBytes(string input)
    {
        // Break prefixed forms apart first, then strip a leading 0x from each token.
        var tokens = input.Replace("\\x", " ", StringComparison.OrdinalIgnoreCase).Replace('%', ' ')
            .Split(_separators, StringSplitOptions.RemoveEmptyEntries)
            .Select(t => t.StartsWith("0x", StringComparison.OrdinalIgnoreCase) ? t[2..] : t);
        var hex = string.Concat(tokens);

        if (!hex.All(Uri.IsHexDigit))
        {
            return new(string.Empty, Utf8Error.InvalidHex);
        }

        if (hex.Length % 2 != 0)
        {
            return new(string.Empty, Utf8Error.OddLength);
        }

        var bytes = Convert.FromHexString(hex);
        if (bytes is [0xEF, 0xBB, 0xBF, ..])
        {
            bytes = bytes[3..];
        }

        try
        {
            return new(new UTF8Encoding(false, true).GetString(bytes), Utf8Error.None);
        }
        catch (DecoderFallbackException)
        {
            return new(string.Empty, Utf8Error.InvalidSequence);
        }
    }

    private static string Format(byte[] bytes, Utf8Options options)
    {
        var items = bytes.Select(b => Item(b, options)).ToArray();

        if (options.Representation == Utf8Representation.Percent)
        {
            return string.Concat(items.Select(i => "%" + i));
        }

        return options.Separator switch
        {
            Utf8Separator.None => string.Concat(items),
            Utf8Separator.Comma => string.Join(',', items),
            Utf8Separator.ZeroX => string.Join(' ', items.Select(i => "0x" + i)),
            Utf8Separator.EscapeX => string.Concat(items.Select(i => "\\x" + i)),
            _ => string.Join(' ', items),
        };
    }

    private static string Item(byte b, Utf8Options options) => options.Representation switch
    {
        Utf8Representation.Decimal => b.ToString(CultureInfo.InvariantCulture),
        Utf8Representation.Binary => Convert.ToString(b, 2).PadLeft(8, '0'),
        Utf8Representation.Octal => Convert.ToString(b, 8).PadLeft(3, '0'),
        _ => b.ToString(options.Uppercase ? "X2" : "x2", CultureInfo.InvariantCulture),
    };

    private static Encoding GetEncoding(Utf8Representation rep) => rep switch
    {
        Utf8Representation.Utf16BE => new UnicodeEncoding(true, false, true),
        Utf8Representation.Utf16LE => new UnicodeEncoding(false, false, true),
        Utf8Representation.Utf32BE => new UTF32Encoding(true, false, true),
        Utf8Representation.Utf32LE => new UTF32Encoding(false, false, true),
        _ => new UTF8Encoding(false, true),
    };

    private static byte[] Bom(Utf8Representation rep) => rep switch
    {
        Utf8Representation.Utf16BE => [0xFE, 0xFF],
        Utf8Representation.Utf16LE => [0xFF, 0xFE],
        Utf8Representation.Utf32BE => [0x00, 0x00, 0xFE, 0xFF],
        Utf8Representation.Utf32LE => [0xFF, 0xFE, 0x00, 0x00],
        _ => [0xEF, 0xBB, 0xBF],
    };

    // EnumerateRunes silently substitutes U+FFFD for lone surrogates, so validate the source too.
    private static bool TryRunes(string text, out List<Rune> runes)
    {
        runes = [.. text.EnumerateRunes()];
        for (var i = 0; i < text.Length; i++)
        {
            if (char.IsHighSurrogate(text[i]) && i + 1 < text.Length && char.IsLowSurrogate(text[i + 1]))
            {
                i++;
            }
            else if (char.IsSurrogate(text[i]))
            {
                return false;
            }
        }

        return true;
    }

    private static string FormatCodePoint(Rune rune, bool upper)
        => "U+" + rune.Value.ToString(upper ? "X4" : "x4", CultureInfo.InvariantCulture);

    private static string Glyph(Rune rune) => rune.Value switch
    {
        0x20 => "␠",
        < 0x20 => ((char)(0x2400 + rune.Value)).ToString(),
        0x7F => "␡",
        _ => rune.ToString(),
    };
}
