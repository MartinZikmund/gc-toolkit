using System.Globalization;
using GcToolkit.Core.Coordinates;

namespace GcToolkit.Core.Tests.Coordinates;

/// <summary>
/// Covers decimal-comma input ("N 49 12,345") — how half of this app's audience writes numbers — and pins
/// the parser to the invariant number format no matter what the ambient culture is.
/// </summary>
[TestClass]
public class CoordinateParserCultureTests
{
    private const double Tol = 1e-5;

    // ---- A decimal comma reads exactly like a decimal dot ----

    [DataTestMethod]
    [DataRow("N 49 12,345 E 016 34,567", "N 49 12.345 E 016 34.567")]
    [DataRow("N 49 12,3 E 016 34,5", "N 49 12.3 E 016 34.5")]
    [DataRow("N 49,205 E 016,576", "N 49.205 E 016.576")]
    [DataRow("N49°12,345 E16°34,567", "N49°12.345 E16°34.567")]
    [DataRow("49,20575 16,576117", "49.20575 16.576117")]
    [DataRow("-33,8568 151,2153", "-33.8568 151.2153")]
    [DataRow("S 12 34,567 W 012 56,789", "S 12 34.567 W 012 56.789")]
    [DataRow("N 49 12 20,7 E 016 34 34,02", "N 49 12 20.7 E 016 34 34.02")]
    [DataRow("N 49° 12' 20,70\" E 016° 34' 34,02\"", "N 49° 12' 20.70\" E 016° 34' 34.02\"")]
    [DataRow("N 49 12.345 E 016 34,567", "N 49 12.345 E 016 34.567")] // mixed dot and comma
    [DataRow("N 49 12,345, E 016 34,567", "N 49 12.345 E 016 34.567")] // stray comma before the hemisphere
    public void TryParse_DecimalComma_MatchesDotEquivalent(string commaText, string dotText)
    {
        Assert.IsTrue(CoordinateParser.TryParse(dotText, out var expected, out var expectedFormat), dotText);

        Assert.IsTrue(CoordinateParser.TryParse(commaText, out var actual, out var actualFormat), commaText);

        Assert.AreEqual(expectedFormat, actualFormat, commaText);
        Assert.AreEqual(expected.Latitude, actual.Latitude, Tol, commaText);
        Assert.AreEqual(expected.Longitude, actual.Longitude, Tol, commaText);
    }

    // ---- The component count (and hence the detected format) is derived after the comma is folded ----

    [DataTestMethod]
    [DataRow("N 49 12,345 E 016 34,567", CoordinateFormat.DegreesDecimalMinutes, 49.205750, 16.576117)]
    [DataRow("N 49 12,3 E 016 34,5", CoordinateFormat.DegreesDecimalMinutes, 49.205, 16.575)]
    [DataRow("N 49,205 E 016,576", CoordinateFormat.DecimalDegrees, 49.205, 16.576)]
    [DataRow("49,20575 16,576117", CoordinateFormat.DecimalDegrees, 49.20575, 16.576117)]
    [DataRow("N 49 12 20,7 E 016 34 34,02", CoordinateFormat.DegreesMinutesSeconds, 49.205750, 16.576117)]
    [DataRow("S 12 34,567 W 012 56,789", CoordinateFormat.DegreesDecimalMinutes, -12.576117, -12.946483)]
    public void TryParse_DecimalComma_DetectsFormatAndValue(string text, CoordinateFormat expectedFormat, double lat, double lon)
    {
        Assert.IsTrue(CoordinateParser.TryParse(text, out var c, out var format), text);

        Assert.AreEqual(expectedFormat, format, text);
        Assert.AreEqual(lat, c.Latitude, Tol, "lat");
        Assert.AreEqual(lon, c.Longitude, Tol, "lon");
    }

    // ---- Comma as the axis divider still works, alone or alongside decimal commas ----

    [DataTestMethod]
    [DataRow("49.20575, 16.576117")]   // dot decimals, comma divides the axes
    [DataRow("49,20575, 16,576117")]   // comma decimals plus a comma divider — the space divides the axes
    public void TryParse_CommaSeparatedAxes_SplitsIntoLatitudeAndLongitude(string text)
    {
        Assert.IsTrue(CoordinateParser.TryParse(text, out var c, out var format), text);

        Assert.AreEqual(CoordinateFormat.DecimalDegrees, format, text);
        Assert.AreEqual(49.20575, c.Latitude, Tol, "lat");
        Assert.AreEqual(16.576117, c.Longitude, Tol, "lon");
    }

    // ---- Ambiguous or malformed commas are rejected rather than silently mis-parsed ----

    [DataTestMethod]
    [DataRow("N 1,234.5 E 12.5")]      // thousands-style group: the number already has a dot
    [DataRow("N 12,345.678 E 1.0")]
    [DataRow("49.205,16.576,5")]       // a stray third value
    // Every comma sits between digits and nothing carries a dot, so which one divides the axes is
    // undecidable. Rejecting beats guessing: a wrong guess here is a silently wrong coordinate.
    [DataRow("49,20575,16,576117")]
    public void TryParse_AmbiguousComma_ReturnsFalse(string text)
        => Assert.IsFalse(CoordinateParser.TryParse(text, out _, out _), text);

    // ---- Ambient culture must not change anything ----

    [DataTestMethod]
    [DataRow("cs-CZ")]
    [DataRow("en-US")]
    [DataRow("")] // invariant
    public void TryParse_AnyAmbientCulture_ProducesTheSameCoordinate(string cultureName)
    {
        RunInCulture(cultureName, static () =>
        {
            Assert.IsTrue(CoordinateParser.TryParse("N 49 12.345 E 016 34.567", out var dot, out var dotFormat));
            Assert.IsTrue(CoordinateParser.TryParse("N 49 12,345 E 016 34,567", out var comma, out var commaFormat));

            Assert.AreEqual(CoordinateFormat.DegreesDecimalMinutes, dotFormat);
            Assert.AreEqual(CoordinateFormat.DegreesDecimalMinutes, commaFormat);
            Assert.AreEqual(49.205750, dot.Latitude, Tol, "dot lat");
            Assert.AreEqual(16.576117, dot.Longitude, Tol, "dot lon");
            Assert.AreEqual(49.205750, comma.Latitude, Tol, "comma lat");
            Assert.AreEqual(16.576117, comma.Longitude, Tol, "comma lon");
        });
    }

    [TestMethod]
    public void ParseFormat_UnderCzechCulture_RoundTrips()
    {
        RunInCulture("cs-CZ", static () =>
        {
            var original = new GeoCoordinate(49.205750, 16.576117);
            foreach (var format in new[]
            {
                CoordinateFormat.DecimalDegrees,
                CoordinateFormat.DegreesDecimalMinutes,
                CoordinateFormat.DegreesMinutesSeconds,
            })
            {
                var text = CoordinateFormatter.Format(original, format);
                Assert.IsTrue(CoordinateParser.TryParse(text, format, out var parsed), $"{format}: {text}");
                Assert.AreEqual(original.Latitude, parsed.Latitude, 1e-3, $"{format} lat '{text}'");
                Assert.AreEqual(original.Longitude, parsed.Longitude, 1e-3, $"{format} lon '{text}'");
            }
        });
    }

    /// <summary>Runs <paramref name="assertions"/> with the ambient culture switched, then restores it so
    /// the neighbouring (parallel) tests keep the culture they started with.</summary>
    private static void RunInCulture(string cultureName, Action assertions)
    {
        var culture = CultureInfo.GetCultureInfo(cultureName);
        var previousCulture = CultureInfo.CurrentCulture;
        var previousUiCulture = CultureInfo.CurrentUICulture;
        try
        {
            CultureInfo.CurrentCulture = culture;
            CultureInfo.CurrentUICulture = culture;
            assertions();
        }
        finally
        {
            CultureInfo.CurrentCulture = previousCulture;
            CultureInfo.CurrentUICulture = previousUiCulture;
        }
    }
}
