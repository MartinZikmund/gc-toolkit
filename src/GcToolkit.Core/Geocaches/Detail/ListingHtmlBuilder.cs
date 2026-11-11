using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace GcToolkit.Core.Geocaches.Detail;

public sealed record ListingChip(string Text, bool IsNegated);

public sealed record ListingImage(string Url, string? Caption);

public sealed record ListingHtmlLabels(string Attributes, string Note, string Images);

/// <summary>Everything the listing document shows. The description fields are raw source text.</summary>
public sealed record ListingHtmlModel
{
    public string? ShortDescription { get; init; }

    public bool ShortDescriptionIsHtml { get; init; }

    public string? LongDescription { get; init; }

    public bool LongDescriptionIsHtml { get; init; }

    public IReadOnlyList<ListingChip> Attributes { get; init; } = [];

    public IReadOnlyList<ListingImage> Images { get; init; } = [];

    public string? Note { get; init; }

    public bool IsDark { get; init; }

    public required ListingHtmlLabels Labels { get; init; }

    public string FontFamily { get; init; } = "'Segoe UI Variable Text', 'Segoe UI', system-ui, -apple-system, Roboto, sans-serif";
}

/// <summary>
/// Builds the page shown in the listing WebView. The listing's own scripts never run: they are stripped
/// and a Content-Security-Policy only allows the one script of ours, identified by a nonce.
/// </summary>
public static partial class ListingHtmlBuilder
{
    /// <summary>Reads the current text selection. Posts it to the host where the platform allows (see #416).</summary>
    public const string SelectionScript =
        "(function(){function post(m){try{if(window.chrome&&chrome.webview){chrome.webview.postMessage(m);}" +
        "else if(window.unoWebView){unoWebView.postMessage(JSON.stringify(m));}" +
        "else if(window.webkit&&webkit.messageHandlers&&webkit.messageHandlers.unoWebView)" +
        "{webkit.messageHandlers.unoWebView.postMessage(JSON.stringify(m));}}catch(e){}}" +
        "window.gcGetSelection=function(){return String(window.getSelection());};" +
        "document.addEventListener('selectionchange',function(){post({type:'selection',text:String(window.getSelection())});});})();";

    public static string Build(ListingHtmlModel model, string? nonce = null)
    {
        nonce ??= Convert.ToBase64String(RandomNumberGenerator.GetBytes(16));

        StringBuilder body = new();
        AppendNote(body, model);
        AppendAttributes(body, model);
        AppendDescription(body, model.ShortDescription, model.ShortDescriptionIsHtml, model.IsDark, "short");
        AppendDescription(body, model.LongDescription, model.LongDescriptionIsHtml, model.IsDark, "long");
        AppendImages(body, model);

        return $$"""
            <!DOCTYPE html>
            <html>
            <head>
            <meta charset="utf-8">
            <meta name="viewport" content="width=device-width, initial-scale=1">
            <meta http-equiv="Content-Security-Policy" content="default-src 'none'; img-src https: http: data:; media-src https: http:; font-src https: data:; style-src 'unsafe-inline'; script-src 'nonce-{{nonce}}'">
            <base target="_blank">
            <style>{{Css(model)}}</style>
            </head>
            <body>
            <main>
            {{body}}
            </main>
            <script nonce="{{nonce}}">{{SelectionScript}}</script>
            </body>
            </html>
            """;
    }

    /// <summary>Removes scripts, embedded content, event handlers and base/meta tags from listing HTML.</summary>
    public static string Sanitize(string html, bool stripColors)
    {
        var result = ScriptRegex().Replace(html, string.Empty);
        result = BlockedTagRegex().Replace(result, string.Empty);
        result = EventHandlerRegex().Replace(result, string.Empty);
        result = JavascriptUrlRegex().Replace(result, "$1=\"#\"");
        if (stripColors)
        {
            result = ColorAttributeRegex().Replace(result, string.Empty);
            result = InlineColorRegex().Replace(result, string.Empty);
        }

        return result;
    }

    private static void AppendNote(StringBuilder sb, ListingHtmlModel model)
    {
        if (string.IsNullOrWhiteSpace(model.Note))
        {
            return;
        }

        sb.Append("<section class=\"note\"><h2>").Append(Encode(model.Labels.Note)).Append("</h2><p>")
            .Append(Encode(model.Note).Replace("\n", "<br>")).AppendLine("</p></section>");
    }

    private static void AppendAttributes(StringBuilder sb, ListingHtmlModel model)
    {
        if (model.Attributes.Count == 0)
        {
            return;
        }

        sb.Append("<section><h2>").Append(Encode(model.Labels.Attributes)).AppendLine("</h2><ul class=\"chips\">");
        foreach (var chip in model.Attributes)
        {
            sb.Append("<li").Append(chip.IsNegated ? " class=\"no\"" : string.Empty).Append('>')
                .Append(Encode(chip.Text)).AppendLine("</li>");
        }

        sb.AppendLine("</ul></section>");
    }

    private static void AppendDescription(StringBuilder sb, string? text, bool isHtml, bool isDark, string cssClass)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return;
        }

        var content = isHtml ? Sanitize(text, stripColors: isDark) : Encode(text).Replace("\n", "<br>");
        sb.Append("<section class=\"description ").Append(cssClass).Append("\">").Append(content).AppendLine("</section>");
    }

    private static void AppendImages(StringBuilder sb, ListingHtmlModel model)
    {
        if (model.Images.Count == 0)
        {
            return;
        }

        sb.Append("<section><h2>").Append(Encode(model.Labels.Images)).AppendLine("</h2><div class=\"gallery\">");
        foreach (var image in model.Images)
        {
            sb.Append("<figure><a href=\"").Append(Encode(image.Url)).Append("\"><img src=\"").Append(Encode(image.Url))
                .Append("\" alt=\"").Append(Encode(image.Caption ?? string.Empty)).Append("\" loading=\"lazy\"></a>");
            if (!string.IsNullOrEmpty(image.Caption))
            {
                sb.Append("<figcaption>").Append(Encode(image.Caption)).Append("</figcaption>");
            }

            sb.AppendLine("</figure>");
        }

        sb.AppendLine("</div></section>");
    }

    private static string Encode(string value) => WebUtility.HtmlEncode(value);

    private static string Css(ListingHtmlModel model)
    {
        var (ground, text, muted, link, stroke, soft, onSoft) = model.IsDark
            ? ("#111511", "#E8ECE8", "#A3ACA3", "#5FBF8D", "#272E27", "#1C3327", "#8FD9B2")
            : ("#F4F2ED", "#1C1F1C", "#5C635C", "#1B5E3F", "#DEDBD1", "#DFEDE4", "#134B32");

        var scheme = model.IsDark ? "dark" : "light";

        return $$"""
            :root{color-scheme:{{scheme}}}
            *{box-sizing:border-box}
            html,body{margin:0;background:{{ground}};color:{{text}};font:15px/1.6 {{model.FontFamily}};-webkit-text-size-adjust:100%}
            main{max-width:72ch;margin:0 auto;padding:16px 20px 40px;overflow-wrap:anywhere}
            h1,h2,h3{line-height:1.25}
            h2{font-size:12px;font-weight:600;letter-spacing:.06em;text-transform:uppercase;color:{{muted}};margin:24px 0 8px}
            a{color:{{link}}}
            img{max-width:100%;height:auto;border-radius:4px}
            table{border-collapse:collapse;max-width:100%}
            td,th{border:1px solid {{stroke}};padding:4px 8px}
            pre,code{font-family:'Cascadia Mono',Consolas,monospace;font-size:13px;white-space:pre-wrap}
            .note{border-left:3px solid {{link}};background:{{soft}};color:{{onSoft}};padding:4px 12px;border-radius:4px}
            .note h2{color:inherit;margin-top:8px}
            .chips{list-style:none;margin:0;padding:0;display:flex;flex-wrap:wrap;gap:8px}
            .chips li{border:1px solid {{stroke}};border-radius:12px;padding:2px 10px;font-size:13px}
            .chips li.no{color:{{muted}};text-decoration:line-through}
            .chips li.no::before{content:"\00d7\00a0";display:inline-block;text-decoration:none}
            .description{margin-top:16px}
            .gallery{display:grid;grid-template-columns:repeat(auto-fill,minmax(160px,1fr));gap:12px}
            figure{margin:0}
            figcaption{font-size:12px;color:{{muted}};margin-top:4px}
            """;
    }

    [GeneratedRegex(@"<script\b.*?</script\s*>|<script\b[^>]*/?>", RegexOptions.IgnoreCase | RegexOptions.Singleline)]
    private static partial Regex ScriptRegex();

    [GeneratedRegex(@"<(/?)(iframe|object|embed|form|base|meta|link)\b[^>]*>", RegexOptions.IgnoreCase)]
    private static partial Regex BlockedTagRegex();

    [GeneratedRegex(@"\son\w+\s*=\s*(""[^""]*""|'[^']*'|[^\s>]+)", RegexOptions.IgnoreCase)]
    private static partial Regex EventHandlerRegex();

    [GeneratedRegex(@"(href|src)\s*=\s*[""']?\s*javascript:[^""'>\s]*[""']?", RegexOptions.IgnoreCase)]
    private static partial Regex JavascriptUrlRegex();

    [GeneratedRegex(@"\s(color|bgcolor|text|link)\s*=\s*(""[^""]*""|'[^']*'|[^\s>]+)", RegexOptions.IgnoreCase)]
    private static partial Regex ColorAttributeRegex();

    [GeneratedRegex(@"(?<![\w-])(background-color|background|color)\s*:\s*[^;""']+;?", RegexOptions.IgnoreCase)]
    private static partial Regex InlineColorRegex();
}
