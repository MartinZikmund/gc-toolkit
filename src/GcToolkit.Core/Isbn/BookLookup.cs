namespace GcToolkit.Core.Isbn;

/// <summary>
/// Deep links to public book catalogues. Opt-in and online-only: the tool itself never fetches
/// anything, the OS browser does when the user follows a link.
/// </summary>
public static class BookLookup
{
    public static IReadOnlyList<BookLookupLink> LinksFor(string isbn13) =>
    [
        new("Open Library", new Uri($"https://openlibrary.org/isbn/{isbn13}")),
        new("Google Books", new Uri($"https://books.google.com/books?vid=ISBN{isbn13}")),
        new("WorldCat", new Uri($"https://search.worldcat.org/search?q=bn:{isbn13}")),
        new("Amazon", new Uri($"https://www.amazon.com/s?k={isbn13}&i=stripbooks")),
    ];
}
