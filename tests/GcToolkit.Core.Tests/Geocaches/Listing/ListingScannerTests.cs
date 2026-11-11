using GcToolkit.Core.Coordinates;
using GcToolkit.Core.Geocaches.Listing;

namespace GcToolkit.Core.Tests.Geocaches.Listing;

[TestClass]
public class ListingScannerTests
{
    private const double Tol = 1e-5;

    private static CoordinateSpan SingleCoordinate(string text)
    {
        var spans = ListingScanner.Scan(text);
        Assert.HasCount(1, spans, text);
        var span = spans[0] as CoordinateSpan;
        Assert.IsNotNull(span, text);
        return span;
    }

    [TestMethod]
    public void Scan_EmptyOrNull_ReturnsNothing()
    {
        Assert.IsEmpty(ListingScanner.Scan(string.Empty));
        Assert.IsEmpty(ListingScanner.Scan(null!));
    }

    [TestMethod]
    [DataRow("N 49° 12.345 E 016° 34.567", CoordinateFormat.DegreesDecimalMinutes, 49.20575, 16.576117)]
    [DataRow("N49°12.345' E016°34.567'", CoordinateFormat.DegreesDecimalMinutes, 49.20575, 16.576117)]
    [DataRow("S 12 34.567 W 012 56.789", CoordinateFormat.DegreesDecimalMinutes, -12.576117, -12.946483)]
    [DataRow("N 50° 04,321' E 014° 25,678'", CoordinateFormat.DegreesDecimalMinutes, 50.072017, 14.427967)]
    [DataRow("N 49.205750° E 016.576117°", CoordinateFormat.DecimalDegrees, 49.20575, 16.576117)]
    [DataRow("N49.20575 E16.576117", CoordinateFormat.DecimalDegrees, 49.20575, 16.576117)]
    [DataRow("N 49° 12' 20.70\" E 016° 34' 34.02\"", CoordinateFormat.DegreesMinutesSeconds, 49.205750, 16.576117)]
    [DataRow("49.205750, 16.576117", CoordinateFormat.DecimalDegrees, 49.20575, 16.576117)]
    [DataRow("-33.8568 151.2153", CoordinateFormat.DecimalDegrees, -33.8568, 151.2153)]
    public void Scan_AngularCoordinate_DetectedWithParsedValue(string text, CoordinateFormat format, double lat, double lon)
    {
        var span = SingleCoordinate($"See {text}.");

        Assert.AreEqual(format, span.Format);
        Assert.AreEqual(lat, span.Coordinate.Latitude, Tol);
        Assert.AreEqual(lon, span.Coordinate.Longitude, Tol);
        Assert.AreEqual(4, span.Start);
        Assert.AreEqual(text.Length, span.Length);
        Assert.AreEqual(text, span.Text);
    }

    [TestMethod]
    public void Scan_UtmAndMgrs_BothDetected()
    {
        var spans = ListingScanner.Scan(ListingFixtures.UtmAndMgrs);

        Assert.HasCount(2, spans);
        Assert.AreEqual(CoordinateFormat.Utm, ((CoordinateSpan)spans[0]).Format);
        Assert.AreEqual("33U 705083 5644673", spans[0].Text);
        Assert.AreEqual(CoordinateFormat.Mgrs, ((CoordinateSpan)spans[1]).Format);
        Assert.AreEqual("33U VR 05083 44673", spans[1].Text);
    }

    [TestMethod]
    public void Scan_BritishGrid_Detected()
    {
        var span = SingleCoordinate(ListingFixtures.BritishGridRef);

        Assert.AreEqual(CoordinateFormat.BritishGrid, span.Format);
        Assert.AreEqual("TG 51409 13177", span.Text);
    }

    [TestMethod]
    public void Scan_EnglishListing_FindsOnlyTheCoordinate()
    {
        var span = SingleCoordinate(ListingFixtures.EnglishMultiIntro);

        Assert.AreEqual("N 49° 12.345 E 016° 34.567", span.Text);
    }

    [TestMethod]
    public void Scan_CzechDecimalComma_Detected()
    {
        var span = SingleCoordinate(ListingFixtures.CzechDecimalComma);

        Assert.AreEqual("N 50° 04,321' E 014° 25,678'", span.Text);
    }

    [TestMethod]
    public void Scan_DmsWaypoint_Detected()
    {
        var span = SingleCoordinate(ListingFixtures.DmsWaypoint);

        Assert.AreEqual(CoordinateFormat.DegreesMinutesSeconds, span.Format);
    }

    [TestMethod]
    public void Scan_DecimalDegreesFixture_FindsBothForms()
    {
        var spans = ListingScanner.Scan(ListingFixtures.DecimalDegreesPair);

        Assert.HasCount(2, spans);
        Assert.AreEqual("49.205750, 16.576117", spans[0].Text);
        Assert.AreEqual("N49.20575 E16.576117", spans[1].Text);
    }

    [TestMethod]
    public void Scan_Formula_ReturnsRawTextAndVariables()
    {
        var spans = ListingScanner.Scan("Final: N 50° 0A.BCD E 014° 2E.FGH, good luck");

        Assert.HasCount(1, spans);
        var span = spans[0] as FormulaSpan;
        Assert.IsNotNull(span);
        Assert.AreEqual("N 50° 0A.BCD E 014° 2E.FGH", span.Text);
        Assert.AreEqual("N 50° 0A.BCD E 014° 2E.FGH", span.Formula.Raw);
        CollectionAssert.AreEqual(new[] { "A", "B", "C", "D", "E", "F", "G", "H" }, span.Formula.Variables.ToArray());
        Assert.AreEqual(ListingSpanKind.Formula, span.Kind);
    }

    [TestMethod]
    public void Scan_FormulaWithExpressions_ListsEachVariableOnce()
    {
        var spans = ListingScanner.Scan(ListingFixtures.ExpressionFormula);

        Assert.HasCount(1, spans);
        var span = (FormulaSpan)spans[0];
        Assert.AreEqual("N 49° 1(A+B).C(D*2)E E 016° 3(F-1).GH(I*J)", span.Text);
        CollectionAssert.AreEqual(
            new[] { "A", "B", "C", "D", "E", "F", "G", "H", "I", "J" },
            span.Formula.Variables.ToArray());
    }

    [TestMethod]
    public void Scan_FormulaWithRepeatedVariable_ListsItOnce()
    {
        var span = (FormulaSpan)ListingScanner.Scan("N 49° 12.AAB E 016° 34.BC")[0];

        CollectionAssert.AreEqual(new[] { "A", "B", "C" }, span.Formula.Variables.ToArray());
    }

    [TestMethod]
    public void Scan_MixedFullLatitudeAndFormulaLongitude_IsFormula()
    {
        var spans = ListingScanner.Scan("N 49° 12.345 E 016° 3A.BCD");

        Assert.HasCount(1, spans);
        Assert.IsInstanceOfType<FormulaSpan>(spans[0]);
    }

    [TestMethod]
    public void Scan_CzechMystery_FindsFormula()
    {
        var spans = ListingScanner.Scan(ListingFixtures.CzechMystery);

        Assert.HasCount(1, spans);
        var span = (FormulaSpan)spans[0];
        Assert.AreEqual("N 49° 1A.BCD E 016° 3E.FGH", span.Text);
    }

    [TestMethod]
    public void Scan_MultipleSpans_AreOrderedByPosition()
    {
        var spans = ListingScanner.Scan("A: N 49° 12.345 E 016° 34.567 B: N 49° 1A.BCD E 016° 3E.FGH C: TG 51409 13177");

        Assert.HasCount(3, spans);
        Assert.IsTrue(spans[0].Start < spans[1].Start && spans[1].Start < spans[2].Start);
        Assert.IsInstanceOfType<CoordinateSpan>(spans[0]);
        Assert.IsInstanceOfType<FormulaSpan>(spans[1]);
        Assert.IsInstanceOfType<CoordinateSpan>(spans[2]);
    }

    [TestMethod]
    public void Scan_OrdinaryText_FindsNothing()
    {
        Assert.IsEmpty(ListingScanner.Scan(ListingFixtures.OrdinaryText));
    }

    [TestMethod]
    [DataRow("Stage 2")]
    [DataRow("D3.5/T2")]
    [DataRow("Difficulty 3.5, terrain 2.5")]
    [DataRow("12.11.2026")]
    [DataRow("1. 5. 2024 v 9:00")]
    [DataRow("Call +420 777 123 456")]
    [DataRow("Call 603-555-1234")]
    [DataRow("Cost 12.50 EUR")]
    [DataRow("v1.2.3")]
    [DataRow("N 5 E 3")]
    [DataRow("N 99° 12.345 E 016° 34.567")]
    [DataRow("N 49° 72.345 E 016° 34.567")]
    [DataRow("North 49 12.345 East 16")]
    [DataRow("GC12345 and GCABCDE")]
    [DataRow("12.5 7.25")]
    [DataRow("N 49° 12.AB")]
    public void Scan_LookAlikes_AreNotDetected(string text)
    {
        Assert.IsEmpty(ListingScanner.Scan(text), text);
    }
}
