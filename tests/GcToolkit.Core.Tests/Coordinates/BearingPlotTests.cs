using GcToolkit.Core.Coordinates;

namespace GcToolkit.Core.Tests.Coordinates;

[TestClass]
public class BearingPlotTests
{
    private static readonly GeoCoordinate _origin = new(50.0, 14.0);

    [TestMethod]
    public void Build_PointNorthEast_HasPositiveXAndY()
    {
        var east = Geodesy.Destination(_origin, 1000, 90);
        var north = Geodesy.Destination(_origin, 1000, 0);
        var plot = BearingPlot.Build([new(_origin, 0), new(east, 0), new(north, 90)], null);

        var o = plot.Markers[0].Position;
        var e = plot.Markers[1].Position;
        var n = plot.Markers[2].Position;
        Assert.AreEqual(1000, e.X - o.X, 5);
        Assert.AreEqual(0, e.Y - o.Y, 5);
        Assert.AreEqual(1000, n.Y - o.Y, 5);
        Assert.AreEqual(0, n.X - o.X, 5);
    }

    [TestMethod]
    public void Build_Markers_AreLabelledAlphabetically()
    {
        var plot = BearingPlot.Build([new(_origin, 0), new(new GeoCoordinate(50.01, 14), 0), new(new GeoCoordinate(50, 14.01), 0)], null);

        CollectionAssert.AreEqual(new[] { "A", "B", "C" }, plot.Markers.Select(m => m.Label).ToArray());
    }

    [TestMethod]
    [DataRow(0, "A")]
    [DataRow(25, "Z")]
    [DataRow(26, "AA")]
    [DataRow(27, "AB")]
    public void Label_Index_ReturnsSpreadsheetStyleLetters(int index, string expected)
        => Assert.AreEqual(expected, BearingPlot.Label(index));

    [TestMethod]
    public void Build_Ray_StartsAtPointAndFollowsBearing()
    {
        var plot = BearingPlot.Build([new(_origin, 90), new(new GeoCoordinate(50.01, 14), 180)], null);

        var ray = plot.Rays[0];
        Assert.AreEqual(plot.Markers[0].Position, ray.Start);
        Assert.IsTrue(ray.End.X > ray.Start.X + 100);
        Assert.AreEqual(ray.Start.Y, ray.End.Y, Math.Abs(ray.End.X - ray.Start.X) * 0.01);
    }

    [TestMethod]
    public void Build_WithFixAhead_RaysReachPastTheFix()
    {
        var a = new GeoCoordinate(50.0, 14.0);
        var b = new GeoCoordinate(50.0, 14.05);
        var fix = Triangulation.Solve([new(a, 45), new(b, 315)]);
        var plot = BearingPlot.Build([new(a, 45), new(b, 315)], fix.Point);

        var fixPosition = plot.Fix!.Value;
        foreach (var ray in plot.Rays)
        {
            var toFix = Distance(ray.Start, fixPosition);
            Assert.IsTrue(Distance(ray.Start, ray.End) > toFix);
        }
    }

    [TestMethod]
    public void Build_Bounds_ContainEveryMarkerRayAndTheFix()
    {
        var a = new GeoCoordinate(50.0, 14.0);
        var b = new GeoCoordinate(50.0, 14.05);
        var plot = BearingPlot.Build([new(a, 45), new(b, 315)], new GeoCoordinate(50.02, 14.025));

        IEnumerable<PlotPoint> all = plot.Markers.Select(m => m.Position)
            .Concat(plot.Rays.SelectMany(r => new[] { r.Start, r.End }))
            .Append(plot.Fix!.Value);
        foreach (var p in all)
        {
            Assert.IsTrue(p.X >= plot.MinX - 1e-6 && p.X <= plot.MaxX + 1e-6);
            Assert.IsTrue(p.Y >= plot.MinY - 1e-6 && p.Y <= plot.MaxY + 1e-6);
        }
    }

    [TestMethod]
    public void Build_AcrossAntimeridian_KeepsNeighboursClose()
    {
        var plot = BearingPlot.Build([new(new GeoCoordinate(0, 179.995), 90), new(new GeoCoordinate(0, -179.995), 270)], null);

        Assert.AreEqual(1113, Distance(plot.Markers[0].Position, plot.Markers[1].Position), 5);
    }

    [TestMethod]
    public void Fit_WideContent_ScalesToWidthAndCentresVertically()
    {
        var bounds = new BearingPlotGeometry([], [], null, 0, 0, 200, 0, 100);

        var t = bounds.Fit(400, 400, 20);

        Assert.AreEqual(1.8, t.Scale, 1e-9);
        Assert.AreEqual(new PlotPoint(20, 200 + 90), t.ToScreen(new PlotPoint(0, 0)));
        Assert.AreEqual(new PlotPoint(380, 200 - 90), t.ToScreen(new PlotPoint(200, 100)));
    }

    [TestMethod]
    public void Fit_North_IsUpOnScreen()
    {
        var bounds = new BearingPlotGeometry([], [], null, 0, 0, 100, 0, 100);

        var t = bounds.Fit(300, 300, 0);

        Assert.IsTrue(t.ToScreen(new PlotPoint(0, 100)).Y < t.ToScreen(new PlotPoint(0, 0)).Y);
    }

    [TestMethod]
    public void Fit_ZeroSizeBounds_DoesNotDivideByZero()
    {
        var bounds = new BearingPlotGeometry([], [], null, 0, 5, 5, 5, 5);

        var t = bounds.Fit(300, 200, 10);

        Assert.IsTrue(double.IsFinite(t.Scale));
        Assert.AreEqual(new PlotPoint(150, 100), t.ToScreen(new PlotPoint(5, 5)));
    }

    [TestMethod]
    [DataRow(1.0, 100.0, 100.0)] // 1 m/px, 100 px available -> 100 m
    [DataRow(3.0, 100.0, 200.0)] // 300 m fits, round down to 200 m
    [DataRow(13.0, 120.0, 1000.0)] // 1560 m fits -> 1 km
    [DataRow(0.04, 100.0, 2.0)] // 4 m fits -> 2 m
    public void ScaleBar_PicksLargestNiceLengthThatFits(double metersPerPixel, double maxPixels, double expectedMeters)
    {
        var bar = new PlotTransform(1.0 / metersPerPixel, 0, 0, 0, 0).ScaleBar(maxPixels);

        Assert.AreEqual(expectedMeters, bar.Meters, 1e-9);
        Assert.AreEqual(expectedMeters / metersPerPixel, bar.Pixels, 1e-9);
    }

    [TestMethod]
    [DataRow(2.0, "2 m")]
    [DataRow(500.0, "500 m")]
    [DataRow(2000.0, "2 km")]
    public void FormatScale_UsesMetresThenKilometres(double meters, string expected)
        => Assert.AreEqual(expected, BearingPlot.FormatScale(meters));

    private static double Distance(PlotPoint a, PlotPoint b) => Math.Sqrt((a.X - b.X) * (a.X - b.X) + (a.Y - b.Y) * (a.Y - b.Y));
}
