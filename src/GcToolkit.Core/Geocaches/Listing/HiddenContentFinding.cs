namespace GcToolkit.Core.Geocaches.Listing;

/// <summary>One thing the inspector found in a listing.</summary>
/// <param name="Kind">What sort of finding it is.</param>
/// <param name="Value">The text or value found (for invisible characters, their code points).</param>
/// <param name="Source">Where it came from, e.g. <c>&lt;img alt&gt;</c>, <c>&lt;!-- comment --&gt;</c> or <c>text</c>.</param>
/// <param name="Detail">Why it is suspicious, e.g. <c>display:none</c>; <c>null</c> when self-explanatory.</param>
/// <param name="Count">How many characters/lines the finding covers (1 for most kinds).</param>
/// <param name="Position">Offset into the listing's text content for characters and whitespace; otherwise <c>null</c>.</param>
public sealed record HiddenContentFinding(
    HiddenContentKind Kind,
    string Value,
    string Source,
    string? Detail = null,
    int Count = 1,
    int? Position = null);
