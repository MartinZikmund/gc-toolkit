using GcToolkit.Core.Coordinates;

namespace GcToolkit.Core.Geocaches.Listing;

public enum ListingSpanKind
{
    /// <summary>A complete coordinate that parsed to a real position.</summary>
    Coordinate,

    /// <summary>A coordinate-shaped template with letters standing in for digits.</summary>
    Formula,
}

/// <summary>Something worth highlighting in listing text: where it is and what it was recognised as.</summary>
public abstract record ListingSpan(int Start, int Length, string Text)
{
    public int End => Start + Length;

    public abstract ListingSpanKind Kind { get; }
}

/// <summary>A full coordinate, with the notation it was written in and its parsed position.</summary>
public sealed record CoordinateSpan(
    int Start,
    int Length,
    string Text,
    GeoCoordinate Coordinate,
    CoordinateFormat Format) : ListingSpan(Start, Length, Text)
{
    public override ListingSpanKind Kind => ListingSpanKind.Coordinate;
}

/// <summary>A coordinate formula. It is only detected here; evaluating it is someone else's job.</summary>
public sealed record FormulaSpan(
    int Start,
    int Length,
    string Text,
    CoordinateFormula Formula) : ListingSpan(Start, Length, Text)
{
    public override ListingSpanKind Kind => ListingSpanKind.Formula;
}

/// <summary>The formula as written, plus the variable letters it uses (distinct, in order of appearance).</summary>
public sealed record CoordinateFormula(string Raw, IReadOnlyList<string> Variables);
