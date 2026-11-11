using System.Net;
using System.Text.RegularExpressions;

namespace GcToolkit.Core.Geocaches.Detail;

/// <summary>Turns HTML from a listing or a log into plain text for native lists.</summary>
public static partial class HtmlText
{
    public static string ToPlainText(string? html)
    {
        if (string.IsNullOrEmpty(html))
        {
            return string.Empty;
        }

        var text = ScriptOrStyleRegex().Replace(html, string.Empty);
        text = LineBreakRegex().Replace(text, "\n");
        text = TagRegex().Replace(text, string.Empty);
        text = WebUtility.HtmlDecode(text);
        return BlankLinesRegex().Replace(text, "\n\n").Trim();
    }

    [GeneratedRegex(@"<(script|style)\b.*?</\1\s*>", RegexOptions.IgnoreCase | RegexOptions.Singleline)]
    private static partial Regex ScriptOrStyleRegex();

    [GeneratedRegex(@"<br\s*/?>|</p\s*>|</div\s*>|</li\s*>", RegexOptions.IgnoreCase)]
    private static partial Regex LineBreakRegex();

    [GeneratedRegex(@"<[^>]*>")]
    private static partial Regex TagRegex();

    [GeneratedRegex(@"\n[ \t]*(\n[ \t]*){2,}")]
    private static partial Regex BlankLinesRegex();
}
