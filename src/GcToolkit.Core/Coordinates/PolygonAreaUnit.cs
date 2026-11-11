namespace GcToolkit.Core.Coordinates;

/// <summary>The area units a polygon's area can be shown in.</summary>
public enum PolygonAreaUnit
{
    SquareMeter,
    SquareKilometer,
    Hectare,
    Acre,
    SquareFoot,
    SquareMile,
}

/// <summary>Converts areas from square metres, the unit the geodesy routines work in.</summary>
public static class AreaUnits
{
    private const double SquareMetersPerAcre = 4046.8564224;      // international acre
    private const double SquareMetersPerSquareFoot = 0.09290304;  // 0.3048^2
    private const double SquareMetersPerSquareMile = 2589988.110336; // 1609.344^2

    public static double FromSquareMeters(double squareMeters, PolygonAreaUnit unit) => unit switch
    {
        PolygonAreaUnit.SquareKilometer => squareMeters / 1e6,
        PolygonAreaUnit.Hectare => squareMeters / 1e4,
        PolygonAreaUnit.Acre => squareMeters / SquareMetersPerAcre,
        PolygonAreaUnit.SquareFoot => squareMeters / SquareMetersPerSquareFoot,
        PolygonAreaUnit.SquareMile => squareMeters / SquareMetersPerSquareMile,
        _ => squareMeters,
    };
}
