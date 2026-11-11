using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using AngleSharp.Dom;
using AngleSharp.Html.Parser;

namespace GcToolkit.Core.Listings;

/// <summary>
/// Looks through a listing's description HTML for things the rendered page does not show:
/// comments, hidden text, tooltips, link targets, invisible characters and encoded blobs.
/// Works on the HTML string alone (no network), so it is fine for cached listings.
/// </summary>
/// <remarks>
/// Only inline styles, the <c>hidden</c> attribute and the old <c>bgcolor</c>/<c>&lt;font color&gt;</c> attributes
/// are understood; rules in <c>&lt;style&gt;</c> blocks are not applied. Image metadata (EXIF) is not read here.
/// </remarks>
public static partial class HiddenContentInspector
{
    private const double ColorMatchDistance = 24;

    private static readonly HashSet<string> _blockTags = new(StringComparer.Ordinal)
    {
        "address", "article", "aside", "blockquote", "br", "dd", "div", "dl", "dt", "fieldset", "figcaption", "figure",
        "footer", "form", "h1", "h2", "h3", "h4", "h5", "h6", "header", "hr", "li", "main", "nav", "ol", "p", "pre",
        "section", "table", "tr", "ul",
    };

    private static readonly HashSet<string> _nonRenderedTags = new(StringComparer.Ordinal)
    {
        "head", "title", "script", "style", "noscript", "template", "meta", "link",
    };

    private static readonly HashSet<string> _skipBlobScanAttributes = new(StringComparer.Ordinal)
    {
        "href", "src", "srcset", "style", "class", "id", "action", "data-src",
    };

    public static HiddenContentReport Analyze(string html)
    {
        ArgumentNullException.ThrowIfNull(html);

        if (string.IsNullOrWhiteSpace(html))
        {
            return new HiddenContentReport([], string.Empty);
        }

        var document = new HtmlParser().ParseDocument(html);
        Walker walker = new();
        walker.Walk(document, new Context(null, null, null, false, false));
        return walker.Finish();
    }

    private readonly record struct Context(CssColor? TextColor, CssColor? Background, string? HiddenReason, bool NotRendered, bool InPre);

    private sealed class Walker
    {
        private readonly List<HiddenContentFinding> _findings = [];
        private readonly StringBuilder _allText = new();
        private readonly StringBuilder _visible = new();

        public HiddenContentReport Finish()
        {
            var allText = _allText.ToString();
            _findings.AddRange(TextAnomalyScanner.FindCharacterRuns(allText));
            if (TextAnomalyScanner.FindTrailingWhitespace(allText) is { } trailing)
            {
                _findings.Add(trailing);
            }

            return new HiddenContentReport(_findings, CleanVisibleText(_visible.ToString()));
        }

        public void Walk(INode node, Context context)
        {
            switch (node)
            {
                case IComment comment:
                    HandleComment(comment);
                    break;
                case IText text:
                    HandleText(text, context);
                    break;
                case IElement element:
                    HandleElement(element, context);
                    break;
                default:
                    WalkChildren(node, context);
                    break;
            }
        }

        private void WalkChildren(INode node, Context context)
        {
            foreach (var child in node.ChildNodes.ToList())
            {
                Walk(child, context);
            }
        }

        private void HandleComment(IComment comment)
        {
            var value = comment.Data.Trim();
            if (value.Length == 0)
            {
                return;
            }

            _findings.Add(new HiddenContentFinding(HiddenContentKind.HtmlComment, value, "<!-- comment -->"));
            _findings.AddRange(TextAnomalyScanner.FindBlobs(value, "<!-- comment -->"));
        }

        private void HandleText(IText text, Context context)
        {
            var data = text.Data;
            if (context.NotRendered || data.Length == 0)
            {
                return;
            }

            _allText.Append(data);
            _findings.AddRange(TextAnomalyScanner.FindBlobs(data, "text"));

            var hiddenReason = context.HiddenReason;
            if (hiddenReason is null && !string.IsNullOrWhiteSpace(data))
            {
                hiddenReason = ColorHidingReason(context);
                if (hiddenReason is not null)
                {
                    _findings.Add(new HiddenContentFinding(
                        HiddenContentKind.HiddenText, TextAnomalyScanner.Normalize(data), "text", hiddenReason));
                }
            }

            if (hiddenReason is null)
            {
                AppendVisible(data, context.InPre);
            }
        }

        private static string? ColorHidingReason(Context context)
        {
            if (context.TextColor is not { } text)
            {
                return null;
            }

            if (text.IsTransparent)
            {
                return "transparent text colour";
            }

            var background = context.Background ?? CssColor.White;
            return text.DistanceTo(background) <= ColorMatchDistance
                ? $"text colour {text.ToHex()} matches background {background.ToHex()}"
                : null;
        }

        private void HandleElement(IElement element, Context context)
        {
            var tag = element.LocalName;
            var style = ParseStyle(element.GetAttribute("style"));
            ReportAttributes(element, tag);

            var notRendered = context.NotRendered || _nonRenderedTags.Contains(tag);
            var hiddenReason = context.HiddenReason;
            if (hiddenReason is null && !notRendered && FindHidingReason(element, style) is { } reason)
            {
                hiddenReason = reason;
                var hiddenText = TextAnomalyScanner.Normalize(element.TextContent);
                if (hiddenText.Length > 0)
                {
                    _findings.Add(new HiddenContentFinding(HiddenContentKind.HiddenText, hiddenText, $"<{tag}>", reason));
                }
            }

            var isBlock = _blockTags.Contains(tag);
            if (isBlock && hiddenReason is null && !notRendered)
            {
                BreakLine();
            }

            var textColor = CssColor.Parse(style.GetValueOrDefault("color")) ?? CssColor.Parse(element.GetAttribute("color")) ?? context.TextColor;
            var background = FindBackground(element, style);
            if (background is { IsTransparent: true })
            {
                background = null;
            }

            WalkChildren(element, new Context(textColor, background ?? context.Background, hiddenReason, notRendered, context.InPre || tag == "pre"));

            if (isBlock && hiddenReason is null && !notRendered)
            {
                BreakLine();
            }
        }

        private void ReportAttributes(IElement element, string tag)
        {
            if (tag == "img")
            {
                AddAttribute(element, "alt", HiddenContentKind.ImageAlt);
                AddAttribute(element, "title", HiddenContentKind.ImageTitle);
                AddAttribute(element, "src", HiddenContentKind.ImageUrl);
            }
            else
            {
                AddAttribute(element, "title", HiddenContentKind.ElementTitle);
                if (tag is "a" or "area" && element.GetAttribute("href")?.Trim() is { Length: > 0 } href && href != "#")
                {
                    _findings.Add(new HiddenContentFinding(HiddenContentKind.LinkTarget, href, $"<{tag} href>"));
                }
            }

            foreach (var attribute in element.Attributes)
            {
                if (!_skipBlobScanAttributes.Contains(attribute.Name))
                {
                    _findings.AddRange(TextAnomalyScanner.FindBlobs(attribute.Value, $"<{tag} {attribute.Name}>"));
                }
            }
        }

        private void AddAttribute(IElement element, string name, HiddenContentKind kind)
        {
            if (element.GetAttribute(name)?.Trim() is { Length: > 0 } value)
            {
                _findings.Add(new HiddenContentFinding(kind, value, $"<{element.LocalName} {name}>"));
            }
        }

        private static string? FindHidingReason(IElement element, Dictionary<string, string> style)
        {
            if (element.HasAttribute("hidden"))
            {
                return "hidden attribute";
            }

            if (style.GetValueOrDefault("display") == "none")
            {
                return "display:none";
            }

            if (style.GetValueOrDefault("visibility") is "hidden" or "collapse")
            {
                return "visibility:hidden";
            }

            if (style.GetValueOrDefault("font-size") is { } size && IsTinyFontSize(size))
            {
                return $"tiny font-size ({size})";
            }

            if (style.GetValueOrDefault("opacity") is { } opacity
                && double.TryParse(opacity, NumberStyles.Float, CultureInfo.InvariantCulture, out var value)
                && value <= 0.02)
            {
                return "opacity:0";
            }

            return null;
        }

        private static CssColor? FindBackground(IElement element, Dictionary<string, string> style) =>
            CssColor.Parse(style.GetValueOrDefault("background-color"))
            ?? CssColor.ParseFirstIn(style.GetValueOrDefault("background"))
            ?? CssColor.Parse(element.GetAttribute("bgcolor"));

        private static bool IsTinyFontSize(string size)
        {
            var match = FontSizeRegex().Match(size);
            if (!match.Success || !double.TryParse(match.Groups[1].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var number))
            {
                return false;
            }

            if (number == 0)
            {
                return true;
            }

            return match.Groups[2].Value switch
            {
                "px" => number < 2,
                "pt" => number < 1.5,
                "em" or "rem" or "ex" or "ch" => number < 0.15,
                "%" => number < 15,
                _ => false,
            };
        }

        private static Dictionary<string, string> ParseStyle(string? style)
        {
            Dictionary<string, string> result = new(StringComparer.Ordinal);
            if (string.IsNullOrWhiteSpace(style))
            {
                return result;
            }

            foreach (var declaration in style.Split(';'))
            {
                var colon = declaration.IndexOf(':');
                if (colon <= 0)
                {
                    continue;
                }

                var value = declaration[(colon + 1)..].Replace("!important", string.Empty, StringComparison.OrdinalIgnoreCase).Trim();
                result[declaration[..colon].Trim().ToLowerInvariant()] = value.ToLowerInvariant();
            }

            return result;
        }

        private void AppendVisible(string data, bool inPre)
        {
            if (inPre)
            {
                _visible.Append(data);
                return;
            }

            foreach (var c in data)
            {
                if (char.IsWhiteSpace(c) && c != '\u00A0')
                {
                    if (_visible.Length > 0 && _visible[^1] is not (' ' or '\n'))
                    {
                        _visible.Append(' ');
                    }
                }
                else
                {
                    _visible.Append(c);
                }
            }
        }

        private void BreakLine()
        {
            if (_visible.Length > 0 && _visible[^1] != '\n')
            {
                _visible.Append('\n');
            }
        }

        private static string CleanVisibleText(string text)
        {
            List<string> lines = [];
            foreach (var line in text.Split('\n'))
            {
                var trimmed = line.TrimEnd().TrimStart(' ');
                if (trimmed.Length == 0 && (lines.Count == 0 || lines[^1].Length == 0))
                {
                    continue;
                }

                lines.Add(trimmed);
            }

            return string.Join('\n', lines).TrimEnd();
        }
    }

    [GeneratedRegex(@"^\s*([0-9]*\.?[0-9]+)\s*(px|pt|em|rem|ex|ch|%)?\s*$")]
    private static partial Regex FontSizeRegex();
}
