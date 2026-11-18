using Microsoft.UI.Xaml.Markup;
using Microsoft.UI.Xaml.Media;

namespace GcToolkit.Helpers;

/// <summary>Turns path mini-language from Core (e.g. <c>GeocacheIcons</c>) into a fresh <see cref="Geometry"/> for x:Bind.</summary>
public static class PathData
{
    public static Geometry? ToGeometry(string? path)
        => string.IsNullOrEmpty(path) ? null : (Geometry)XamlBindingHelper.ConvertValue(typeof(Geometry), path);
}
