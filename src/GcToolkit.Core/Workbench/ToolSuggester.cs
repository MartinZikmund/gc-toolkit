using System.Text.RegularExpressions;

namespace GcToolkit.Core.Workbench;

/// <summary>Guesses which tools fit a piece of selected text, best first, from what the text looks like.</summary>
public static partial class ToolSuggester
{
    private const int MaxSuggestions = 4;

    public static IReadOnlyList<string> Suggest(string? text)
    {
        var value = text?.Trim() ?? string.Empty;
        if (value.Length == 0)
        {
            return [];
        }

        List<string> tools = [];
        if (CoordinateRegex().IsMatch(value) || DecimalCoordinateRegex().IsMatch(value))
        {
            tools.Add("CoordinateConversion");
        }

        if (GcCodeRegex().IsMatch(value))
        {
            tools.Add("GcCodeId");
        }

        if (MorseRegex().IsMatch(value))
        {
            tools.Add("MorseCode");
        }

        if (BinaryRegex().IsMatch(value))
        {
            tools.AddRange(["BaseConverter", "AsciiConverter"]);
        }
        else if (NumbersRegex().IsMatch(value))
        {
            tools.AddRange(["NumbersToLetters", "AsciiConverter", "BaseConverter"]);
        }

        if (RomanRegex().IsMatch(value))
        {
            tools.Add("RomanNumerals");
        }

        if (Base64Regex().IsMatch(value) && value.Length % 4 == 0 && value.Any(char.IsLower) && value.Any(char.IsUpper))
        {
            tools.Add("Base64");
        }

        if (value.Any(char.IsLetter))
        {
            tools.AddRange(["CaesarCipher", "VigenereCipher", "AtbashCipher", "WordValue"]);
        }

        tools.Add("WordValue");
        tools.Add("TextReverse");
        return [.. tools.Distinct().Take(MaxSuggestions)];
    }

    // N 50° 04.375 E 014° 26.120, also with ' and without the degree sign.
    [GeneratedRegex(@"^[NS]\s*\d{1,2}[°\s]+\d{1,2}[.,]\d+'?\s*[EW]\s*\d{1,3}[°\s]+\d{1,2}[.,]\d+'?$", RegexOptions.IgnoreCase)]
    private static partial Regex CoordinateRegex();

    [GeneratedRegex(@"^-?\d{1,2}\.\d{3,}\s*[,;\s]\s*-?\d{1,3}\.\d{3,}$")]
    private static partial Regex DecimalCoordinateRegex();

    [GeneratedRegex(@"^GC[0-9A-HJKMNPQRTV-Z]{1,6}$", RegexOptions.IgnoreCase)]
    private static partial Regex GcCodeRegex();

    [GeneratedRegex(@"^(?=.*[.\-])[.\-\s/|]+$")]
    private static partial Regex MorseRegex();

    [GeneratedRegex(@"^[01]{4,}(\s+[01]{4,})*$")]
    private static partial Regex BinaryRegex();

    [GeneratedRegex(@"^\d+([\s,;.\-]+\d+)*$")]
    private static partial Regex NumbersRegex();

    [GeneratedRegex(@"^[MDCLXVI]+$", RegexOptions.IgnoreCase)]
    private static partial Regex RomanRegex();

    [GeneratedRegex(@"^[A-Za-z0-9+/]{6,}={0,2}$")]
    private static partial Regex Base64Regex();
}
