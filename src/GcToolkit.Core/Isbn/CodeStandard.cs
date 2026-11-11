namespace GcToolkit.Core.Isbn;

/// <summary>The family the user picks; the concrete <see cref="CodeFormat"/> is detected from the length.</summary>
public enum CodeStandard
{
    /// <summary>ISBN-10 or ISBN-13, told apart by length.</summary>
    Isbn,
    Issn,

    /// <summary>Any EAN-13 barcode, including ISMN (979-0) and the ISBN/ISSN "Bookland" ranges.</summary>
    Ean13,
    UpcA,
}

/// <summary>A concrete code layout with its own length and check-digit rule.</summary>
public enum CodeFormat
{
    Isbn10,
    Isbn13,
    Issn,
    Ean13,
    UpcA,
}
