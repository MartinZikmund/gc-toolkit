using System.Xml.Linq;

namespace GcToolkit.Core.Geocaches.Gpx;

internal static class GpxNamespaces
{
    /// <summary>Written as the GPX <c>creator</c>; the importer then knows the header time is the export time, not a cache update time.</summary>
    public const string ExporterName = "GC Toolkit";

    public const string GroundspeakPrefix = "http://www.groundspeak.com/cache/";
    public const string GsakPrefix = "http://www.gsak.net/";

    public static readonly XNamespace Gpx = "http://www.topografix.com/GPX/1/0";
    public static readonly XNamespace Groundspeak = "http://www.groundspeak.com/cache/1/0/1";
    public static readonly XNamespace Gsak = "http://www.gsak.net/xmlv1/6";

    /// <summary>GC Toolkit's own extras (solved state, premium flag, images, the corrected-coordinates marker). Other apps ignore them.</summary>
    public static readonly XNamespace Gct = "urn:gc-toolkit:gpx:1";
}
