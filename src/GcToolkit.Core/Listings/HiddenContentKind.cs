namespace GcToolkit.Core.Listings;

/// <summary>What sort of hidden or easily-missed content a finding is.</summary>
public enum HiddenContentKind
{
    /// <summary>An HTML comment (<c>&lt;!-- ... --&gt;</c>).</summary>
    HtmlComment,

    /// <summary>Text the reader cannot see: hidden, tiny, transparent or the same colour as its background.</summary>
    HiddenText,

    /// <summary>The <c>alt</c> text of an image.</summary>
    ImageAlt,

    /// <summary>The <c>title</c> (tooltip) of an image.</summary>
    ImageTitle,

    /// <summary>The <c>title</c> (tooltip) of any other element.</summary>
    ElementTitle,

    /// <summary>The target of a link (<c>href</c>).</summary>
    LinkTarget,

    /// <summary>The address of an image (<c>src</c>).</summary>
    ImageUrl,

    /// <summary>A run of zero-width or other invisible characters.</summary>
    InvisibleCharacters,

    /// <summary>Trailing space/tab patterns or unusual Unicode spaces.</summary>
    OddWhitespace,

    /// <summary>A long string that looks like Base64.</summary>
    Base64Blob,

    /// <summary>A long string that looks like hexadecimal bytes.</summary>
    HexBlob,
}
