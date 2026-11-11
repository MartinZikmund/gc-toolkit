namespace GcToolkit.Core.Coordinates;

/// <summary>Converts a distance expressed in a <see cref="DistanceUnit"/> to metres, the unit the
/// geodesy routines (e.g. <see cref="Geodesy.Destination"/>) operate in.</summary>
public static class DistanceUnits
{
    private const double MetersPerFoot = 0.3048;     // international foot
    private const double MetersPerYard = 0.9144;     // international yard (3 ft)
    private const double MetersPerMile = 1609.344;   // international mile
    private const double MetersPerNauticalMile = 1852.0;

    /// <summary>Converts <paramref name="meters"/> to <paramref name="unit"/>.</summary>
    public static double FromMeters(double meters, DistanceUnit unit) => meters / ToMeters(1.0, unit);

    /// <summary>Converts <paramref name="value"/> in <paramref name="unit"/> to metres.</summary>
    public static double ToMeters(double value, DistanceUnit unit) => unit switch
    {
        DistanceUnit.Kilometer => value * 1000.0,
        DistanceUnit.Feet => value * MetersPerFoot,
        DistanceUnit.Yard => value * MetersPerYard,
        DistanceUnit.Mile => value * MetersPerMile,
        DistanceUnit.NauticalMile => value * MetersPerNauticalMile,
        _ => value,
    };
}
