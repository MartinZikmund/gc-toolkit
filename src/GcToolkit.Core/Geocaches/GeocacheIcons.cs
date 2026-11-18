using System.Globalization;
using System.Text;

namespace GcToolkit.Core.Geocaches;

/// <summary>
/// Our own cache type, size and attribute icons as path data on a 24×24 box (even-odd fill), so XAML,
/// the listing HTML and map markers all draw the same shapes. No Groundspeak artwork or colours.
/// </summary>
public static partial class GeocacheIcons
{
    /// <summary>The diagonal bar drawn over a negated attribute.</summary>
    public const string NegationStrike = "M 3.7 2.3 L 21.7 20.3 L 20.3 21.7 L 2.3 3.7 z";

    public static string Type(GeocacheType type) => _types.GetValueOrDefault(type, Unknown);

    public static string Size(GeocacheSize size) => size switch
    {
        GeocacheSize.NotChosen => SizeMeter(0),
        GeocacheSize.Micro => SizeMeter(1),
        GeocacheSize.Small => SizeMeter(2),
        GeocacheSize.Regular => SizeMeter(3),
        GeocacheSize.Large => SizeMeter(4),
        GeocacheSize.Other => SizeOther,
        GeocacheSize.Virtual => SizeVirtual,
        _ => Unknown,
    };

    public static string Attribute(int id) => _attributes.GetValueOrDefault(id, Unknown);

    /// <summary>A standalone SVG for HTML or map markers. It takes the colour of the surrounding text.</summary>
    public static string ToSvg(string path, bool isNegated = false)
    {
        StringBuilder sb = new("<svg xmlns=\"http://www.w3.org/2000/svg\" viewBox=\"0 0 24 24\" width=\"16\" height=\"16\" fill=\"currentColor\" fill-rule=\"evenodd\" aria-hidden=\"true\">");
        if (isNegated)
        {
            // Knock a gap around the strike so it reads as "no" rather than as part of the icon.
            sb.Append("<mask id=\"gc-no\"><rect width=\"24\" height=\"24\" fill=\"#fff\"/>")
                .Append("<path d=\"M 3 3 L 21 21\" stroke=\"#000\" stroke-width=\"5\"/></mask>")
                .Append("<path mask=\"url(#gc-no)\" d=\"").Append(path).Append("\"/>")
                .Append("<path d=\"").Append(NegationStrike).Append("\"/>");
        }
        else
        {
            sb.Append("<path d=\"").Append(path).Append("\"/>");
        }

        return sb.Append("</svg>").ToString();
    }

    /// <summary>Four rising bars, the first <paramref name="filled"/> of them solid and the rest outlined.</summary>
    private static string SizeMeter(int filled)
    {
        StringBuilder sb = new();
        for (var i = 0; i < 4; i++)
        {
            double left = 1.5 + (i * 5.5);
            double top = 16 - (i * 4);
            Append(sb, left, top, left + 4, 21);
            if (i >= filled)
            {
                Append(sb, left + 1.25, top + 1.25, left + 2.75, 19.75);
            }
        }

        return sb.ToString().TrimEnd();

        static void Append(StringBuilder sb, double l, double t, double r, double b)
            => sb.Append(CultureInfo.InvariantCulture, $"M {l} {t} L {r} {t} L {r} {b} L {l} {b} z ");
    }
}
