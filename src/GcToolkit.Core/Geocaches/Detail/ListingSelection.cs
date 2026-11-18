using System.Text.Json;

namespace GcToolkit.Core.Geocaches.Detail;

/// <summary>A text selection reported by the listing page's script, with its bounds in the page's viewport (DIPs).</summary>
public sealed record ListingSelection(string Text, double X, double Y, double Width, double Height)
{
    public static ListingSelection Empty { get; } = new(string.Empty, 0, 0, 0, 0);

    /// <summary>
    /// Reads <c>{"type":"selection","text":…,"x":…,"y":…,"w":…,"h":…}</c>. Some WebView hosts deliver the
    /// object itself, others the JSON text of it as a string; both are accepted.
    /// </summary>
    public static bool TryParse(string? json, out ListingSelection selection)
    {
        selection = Empty;
        try
        {
            using var document = JsonDocument.Parse(json ?? string.Empty);
            var root = document.RootElement;
            if (root.ValueKind == JsonValueKind.String)
            {
                return TryParse(root.GetString(), out selection);
            }

            if (root.ValueKind != JsonValueKind.Object
                || !root.TryGetProperty("type", out var type)
                || type.ValueKind != JsonValueKind.String
                || type.GetString() != "selection")
            {
                return false;
            }

            selection = new(
                root.TryGetProperty("text", out var text) && text.ValueKind == JsonValueKind.String ? text.GetString() ?? string.Empty : string.Empty,
                Number(root, "x"),
                Number(root, "y"),
                Number(root, "w"),
                Number(root, "h"));
            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private static double Number(JsonElement root, string name)
        => root.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.Number ? value.GetDouble() : 0;
}
