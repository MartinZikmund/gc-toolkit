using GcToolkit.Core.Coordinates;

namespace GcToolkit.Core.Tests.Coordinates;

// Reference areas/perimeters come from GeographicLib's PolygonArea on WGS84 (Karney 2013,
// geographiclib 2.x for Python), i.e. true geodesic edges on the ellipsoid.
[TestClass]
public class PolygonGeometryTests
{
    private static GeoCoordinate[] Pts(params (double Lat, double Lon)[] p)
        => [.. p.Select(x => new GeoCoordinate(x.Lat, x.Lon))];

    private static void AssertRelative(double expected, double actual, double relative)
        => Assert.IsTrue(
            Math.Abs(actual - expected) <= Math.Abs(expected) * relative,
            $"expected {expected}, got {actual}");

    [TestMethod]
    [DataRow(0, 0, 0, 1, 1, 1, 1, 0, 12308778361.469452, 443770.917248)] // 1 deg box at the equator
    [DataRow(50.0, 14.0, 50.0, 14.01, 50.005, 14.01, 50.005, 14.0, 398712.073225, 2546.131834)] // Prague field
    [DataRow(10, 179, 10, -179, 11, -179, 11, 179, 24218606783.655426, 659075.476105)] // across the date line
    public void Compute_Quadrilateral_MatchesGeographicLib(
        double lat1, double lon1, double lat2, double lon2, double lat3, double lon3, double lat4, double lon4,
        double area, double perimeter)
    {
        var result = PolygonGeometry.Compute(Pts((lat1, lon1), (lat2, lon2), (lat3, lon3), (lat4, lon4)));

        AssertRelative(area, result!.AreaSquareMeters, 1e-9);
        AssertRelative(perimeter, result.PerimeterMeters, 1e-8);
    }

    [TestMethod]
    public void Compute_Triangle_MatchesGeographicLib()
    {
        var result = PolygonGeometry.Compute(Pts((0, 0), (0, 1), (1, 0)));

        AssertRelative(6154854786.721430, result!.AreaSquareMeters, 1e-9);
        AssertRelative(378793.447642, result.PerimeterMeters, 1e-8);
    }

    [TestMethod]
    public void Compute_LargeTriangle_MatchesGeographicLib()
    {
        var result = PolygonGeometry.Compute(Pts((50, 14), (51, 16), (49, 16.5)));

        AssertRelative(17940187034.324524, result!.AreaSquareMeters, 1e-9);
    }

    [TestMethod]
    public void Compute_PolygonAroundThePole_MatchesGeographicLib()
    {
        var result = PolygonGeometry.Compute(Pts((80, 0), (80, 90), (80, 180), (80, -90)));

        AssertRelative(2507270031169.875, result!.AreaSquareMeters, 1e-9);
    }

    [TestMethod]
    public void Compute_HemisphereSizedTriangle_MatchesGeographicLib()
    {
        var result = PolygonGeometry.Compute(Pts((45, 0), (45, 120), (45, -120)));

        AssertRelative(39433884866571.437, result!.AreaSquareMeters, 1e-9);
    }

    [TestMethod]
    public void Compute_ReversedVertexOrder_GivesSameArea()
    {
        var forward = PolygonGeometry.Compute(Pts((0, 0), (0, 1), (1, 1), (1, 0)));
        var reversed = PolygonGeometry.Compute(Pts((1, 0), (1, 1), (0, 1), (0, 0)));

        AssertRelative(forward!.AreaSquareMeters, reversed!.AreaSquareMeters, 1e-12);
    }

    [TestMethod]
    public void Compute_RepeatedFirstVertex_IsDroppedAndFlagged()
    {
        var open = PolygonGeometry.Compute(Pts((0, 0), (0, 1), (1, 1), (1, 0)));
        var closed = PolygonGeometry.Compute(Pts((0, 0), (0, 1), (1, 1), (1, 0), (0, 0)));

        Assert.IsFalse(open!.WasClosed);
        Assert.IsTrue(closed!.WasClosed);
        Assert.AreEqual(4, closed.Edges.Count);
        Assert.AreEqual(open.AreaSquareMeters, closed.AreaSquareMeters, 1e-6);
        Assert.AreEqual(open.PerimeterMeters, closed.PerimeterMeters, 1e-6);
    }

    [TestMethod]
    public void Compute_Edges_AreClosedLoopWithLengthsSummingToPerimeter()
    {
        var result = PolygonGeometry.Compute(Pts((0, 0), (0, 1), (1, 1), (1, 0)))!;

        Assert.AreEqual(4, result.Edges.Count);
        Assert.AreEqual(3, result.Edges[3].FromIndex);
        Assert.AreEqual(0, result.Edges[3].ToIndex);
        Assert.AreEqual(result.PerimeterMeters, result.Edges.Sum(e => e.LengthMeters), 1e-6);
        Assert.AreEqual(90.0, result.Edges[0].InitialBearingDegrees, 1e-6);
    }

    [TestMethod]
    public void Compute_SymmetricBox_CentroidIsItsMiddle()
    {
        var result = PolygonGeometry.Compute(Pts((0, 0), (0, 1), (1, 1), (1, 0)))!;

        Assert.AreEqual(0.5, result.Centroid.Latitude, 1e-4);
        Assert.AreEqual(0.5, result.Centroid.Longitude, 1e-6);
    }

    [TestMethod]
    public void Compute_Triangle_CentroidIsTheVertexAverage()
    {
        var result = PolygonGeometry.Compute(Pts((50.0, 14.0), (50.0, 14.03), (50.03, 14.0)))!;

        Assert.AreEqual(50.01, result.Centroid.Latitude, 1e-4);
        Assert.AreEqual(14.01, result.Centroid.Longitude, 1e-4);
    }

    [TestMethod]
    public void Compute_LShape_AreaCentroidDiffersFromVertexMean()
    {
        // Six vertices bunched on one side: the vertex mean is dragged there, the area centroid is not.
        var result = PolygonGeometry.Compute(Pts((0, 0), (0, 0.04), (0.01, 0.04), (0.01, 0.01), (0.04, 0.01), (0.04, 0)))!;

        Assert.IsTrue(Geodesy.DistanceMeters(result.Centroid, result.VertexMean) > 100.0);
        Assert.IsTrue(result.Centroid.Latitude is > 0.0 and < 0.04);
    }

    [TestMethod]
    public void Compute_AcrossDateLine_CentroidStaysOnTheDateLine()
    {
        var result = PolygonGeometry.Compute(Pts((10, 179), (10, -179), (11, -179), (11, 179)))!;

        Assert.AreEqual(10.5, result.Centroid.Latitude, 1e-2);
        Assert.AreEqual(180.0, Math.Abs(result.Centroid.Longitude), 1e-6);
    }

    [TestMethod]
    public void Compute_BowTie_IsFlaggedSelfIntersecting()
    {
        var result = PolygonGeometry.Compute(Pts((0, 0), (1, 1), (1, 0), (0, 1)))!;

        Assert.IsTrue(result.IsSelfIntersecting);
    }

    [TestMethod]
    public void Compute_SimplePolygon_IsNotFlagged()
    {
        var result = PolygonGeometry.Compute(Pts((0, 0), (0, 1), (1, 1), (1, 0)))!;

        Assert.IsFalse(result.IsSelfIntersecting);
    }

    [TestMethod]
    public void Compute_CollinearPoints_HaveZeroAreaAndFallBackToVertexMean()
    {
        var result = PolygonGeometry.Compute(Pts((0, 0), (0, 1), (0, 2)))!;

        Assert.AreEqual(0.0, result.AreaSquareMeters, 1.0);
        Assert.AreEqual(result.VertexMean.Longitude, result.Centroid.Longitude, 1e-9);
    }

    [TestMethod]
    public void Compute_FewerThanThreeVertices_ReturnsNull()
    {
        Assert.IsNull(PolygonGeometry.Compute(Pts((0, 0), (1, 1))));
        Assert.IsNull(PolygonGeometry.Compute([]));
        Assert.IsNull(PolygonGeometry.Compute(Pts((0, 0), (0, 1), (0, 0))));
    }

    [TestMethod]
    public void Compute_NullPoints_Throws()
        => Assert.ThrowsExactly<ArgumentNullException>(() => PolygonGeometry.Compute(null!));
}

[TestClass]
public class AreaUnitsTests
{
    [TestMethod]
    [DataRow(PolygonAreaUnit.SquareMeter, 1.0)]
    [DataRow(PolygonAreaUnit.SquareKilometer, 1e-6)]
    [DataRow(PolygonAreaUnit.Hectare, 1e-4)]
    [DataRow(PolygonAreaUnit.Acre, 1.0 / 4046.8564224)]
    [DataRow(PolygonAreaUnit.SquareFoot, 1.0 / 0.09290304)]
    [DataRow(PolygonAreaUnit.SquareMile, 1.0 / 2589988.110336)]
    public void FromSquareMeters_ConvertsOneSquareMeter(PolygonAreaUnit unit, double expected)
        => Assert.AreEqual(expected, AreaUnits.FromSquareMeters(1.0, unit), expected * 1e-12);

    [TestMethod]
    [DataRow(DistanceUnit.Kilometer, 0.001)]
    [DataRow(DistanceUnit.Feet, 1.0 / 0.3048)]
    [DataRow(DistanceUnit.Mile, 1.0 / 1609.344)]
    public void FromMeters_InvertsToMeters(DistanceUnit unit, double expected)
    {
        Assert.AreEqual(expected, DistanceUnits.FromMeters(1.0, unit), expected * 1e-12);
        Assert.AreEqual(1.0, DistanceUnits.ToMeters(DistanceUnits.FromMeters(1.0, unit), unit), 1e-12);
    }
}
