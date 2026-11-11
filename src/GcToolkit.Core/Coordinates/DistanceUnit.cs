namespace GcToolkit.Core.Coordinates;

/// <summary>The length units a distance can be entered in. Metres is the canonical internal unit.</summary>
public enum DistanceUnit
{
    /// <summary>Metres — the unit the geodesy routines work in.</summary>
    Meter,

    /// <summary>Kilometres (1 km = 1000 m).</summary>
    Kilometer,

    /// <summary>International feet (1 ft = 0.3048 m).</summary>
    Feet,

    /// <summary>International yards (1 yd = 0.9144 m).</summary>
    Yard,

    /// <summary>International miles (1 mi = 1609.344 m).</summary>
    Mile,

    /// <summary>International nautical miles (1 nmi = 1852 m).</summary>
    NauticalMile,
}
