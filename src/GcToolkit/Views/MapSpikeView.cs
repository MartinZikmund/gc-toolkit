using Mapsui;
using Mapsui.Layers;
using Mapsui.Projections;
using Mapsui.Styles;
using Mapsui.Tiling;
using Mapsui.UI.WinUI;
using Microsoft.UI.Xaml.Controls;

namespace GcToolkit.Views;

// SPIKE #421: throwaway page hosting Mapsui. Delete after the spike.
public sealed class MapSpikeView : Page
{
    public MapSpikeView()
    {
        Mapsui.UI.WinUI.MapControl map = new();
        map.Map.Layers.Add(OpenStreetMap.CreateTileLayer());

        Random rnd = new(42);
        List<IFeature> features = new();
        for (var i = 0; i < 1000; i++)
        {
            var lat = 50.087 + (rnd.NextDouble() - 0.5) * 0.12;
            var lon = 14.421 + (rnd.NextDouble() - 0.5) * 0.24;
            var (x, y) = SphericalMercator.FromLonLat(lon, lat);
            PointFeature f = new(new MPoint(x, y));
            features.Add(f);
        }

        MemoryLayer markers = new("markers")
        {
            Features = features,
            Style = new SymbolStyle { SymbolScale = 0.4, Fill = new Mapsui.Styles.Brush(Mapsui.Styles.Color.Red) },
        };
        map.Map.Layers.Add(markers);

        var (cx, cy) = SphericalMercator.FromLonLat(14.421, 50.087);
        map.Loaded += (_, _) => map.Map.Navigator.CenterOnAndZoomTo(new MPoint(cx, cy), 20);

        Content = map;
    }
}
