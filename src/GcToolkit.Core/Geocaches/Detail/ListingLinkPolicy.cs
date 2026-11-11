namespace GcToolkit.Core.Geocaches.Detail;

public enum ListingLinkAction
{
    /// <summary>The listing document itself (the string we navigated to).</summary>
    Allow,

    /// <summary>A web or mail link: cancel and hand it to the system.</summary>
    OpenExternally,

    /// <summary>Anything else (file:, app protocols, javascript:) never runs.</summary>
    Block,
}

/// <summary>What the listing WebView does with a navigation. Listing HTML is written by cache owners, so only known-safe schemes leave the app.</summary>
public static class ListingLinkPolicy
{
    public static ListingLinkAction Decide(string? uri)
    {
        if (!Uri.TryCreate(uri, UriKind.Absolute, out var parsed))
        {
            return ListingLinkAction.Block;
        }

        return parsed.Scheme switch
        {
            "http" or "https" or "mailto" => ListingLinkAction.OpenExternally,
            "data" or "about" => ListingLinkAction.Allow,
            _ => ListingLinkAction.Block,
        };
    }
}
