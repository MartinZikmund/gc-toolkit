using System.Text.Json.Serialization;

namespace GcToolkit.Core.Geocaches.Storage;

/// <summary>Source-generated JSON for the nested columns: no reflection, so it survives the WASM trim.</summary>
[JsonSerializable(typeof(GeocacheAttribute[]))]
[JsonSerializable(typeof(Waypoint[]))]
[JsonSerializable(typeof(GeocacheLog[]))]
[JsonSerializable(typeof(GeocacheImage[]))]
internal sealed partial class GeocacheJsonContext : JsonSerializerContext
{
}
