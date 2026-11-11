using System.Globalization;

namespace GcToolkit.Core.Coordinates.Magnetic;

/// <summary>The geomagnetic field at one place and time. Field components in nanotesla, angles in degrees.</summary>
/// <param name="X">North component.</param>
/// <param name="Y">East component.</param>
/// <param name="Z">Down component.</param>
/// <param name="H">Horizontal intensity.</param>
/// <param name="F">Total intensity.</param>
/// <param name="Inclination">Dip angle, positive downward.</param>
/// <param name="Declination">Angle from true North to magnetic North, positive East.</param>
public sealed record MagneticFieldElements(
    double X, double Y, double Z, double H, double F, double Inclination, double Declination);

/// <summary>
/// The World Magnetic Model evaluated offline. Coefficients are NOAA NCEI's WMM2025 (WMM.COF from
/// WMM2025COF.zip, https://www.ncei.noaa.gov/products/world-magnetic-model, US public domain), shipped as
/// an embedded resource. The evaluation follows "The US/UK World Magnetic Model for 2025-2030: Technical
/// Report" (NOAA NCEI, 2024), section 1.2, and mirrors NOAA's reference C library (GeomagnetismLibrary.c).
/// </summary>
public sealed class WorldMagneticModel
{
    private const string Wmm2025ResourceName = "GcToolkit.Core.Coordinates.Magnetic.WMM2025.COF";

    // WGS84 ellipsoid and the geomagnetic reference radius, in km (Technical Report, table 1).
    private const double SemiMajorAxisKm = 6378.137;
    private const double Flattening = 1.0 / 298.257223563;
    private const double ReferenceRadiusKm = 6371.2;

    private const double ValidityYears = 5.0;

    private static readonly Lazy<WorldMagneticModel> _wmm2025 = new(() => LoadEmbedded(Wmm2025ResourceName));

    private readonly double[] _g;
    private readonly double[] _h;
    private readonly double[] _gDot;
    private readonly double[] _hDot;

    private WorldMagneticModel(string name, double epoch, int maxDegree, double[] g, double[] h, double[] gDot, double[] hDot)
    {
        Name = name;
        Epoch = epoch;
        MaxDegree = maxDegree;
        _g = g;
        _h = h;
        _gDot = gDot;
        _hDot = hDot;
    }

    /// <summary>WMM2025, valid 2025.0 to 2030.0.</summary>
    public static WorldMagneticModel Wmm2025 => _wmm2025.Value;

    /// <summary>The model's base epoch, as a decimal year.</summary>
    public double Epoch { get; }

    public string Name { get; }

    public int MaxDegree { get; }

    /// <summary>Whether <paramref name="decimalYear"/> falls inside the model's five-year validity window.</summary>
    public bool IsValidFor(double decimalYear) => decimalYear >= Epoch && decimalYear < Epoch + ValidityYears;

    /// <summary>The magnetic declination (East positive) at a point on the ellipsoid.</summary>
    public double DeclinationDegrees(double latitude, double longitude, double decimalYear)
        => Calculate(latitude, longitude, 0.0, decimalYear).Declination;

    /// <summary>A date as a decimal year (1 January 00:00 = whole year).</summary>
    public static double DecimalYear(DateOnly date)
        => date.Year + (date.DayOfYear - 1) / (DateTime.IsLeapYear(date.Year) ? 366.0 : 365.0);

    /// <summary>Evaluates the main field plus secular variation at a geodetic position and height above the ellipsoid.</summary>
    public MagneticFieldElements Calculate(double latitude, double longitude, double heightKm, double decimalYear)
    {
        // Pole: the east component is undefined at exactly 90 deg, so step a hair off it, as the report advises.
        latitude = Math.Clamp(latitude, -90.0 + 1e-7, 90.0 - 1e-7);

        var phi = latitude * Math.PI / 180.0;
        var lambda = longitude * Math.PI / 180.0;

        // Geodetic -> geocentric spherical (report eq. 7).
        var e2 = Flattening * (2.0 - Flattening);
        var sinPhi = Math.Sin(phi);
        var cosPhi = Math.Cos(phi);
        var rc = SemiMajorAxisKm / Math.Sqrt(1.0 - e2 * sinPhi * sinPhi);
        var p = (rc + heightKm) * cosPhi;
        var z = (rc * (1.0 - e2) + heightKm) * sinPhi;
        var r = Math.Sqrt(p * p + z * z);
        var phiPrime = Math.Asin(z / r);

        var n = MaxDegree;
        var dt = decimalYear - Epoch;
        var (pnm, dpnm) = SchmidtLegendre(Math.Sin(phiPrime), n);

        // Field in spherical coordinates (report eq. 10-12).
        double bx = 0, by = 0, bz = 0;
        for (var deg = 1; deg <= n; deg++)
        {
            var radiusPower = Math.Pow(ReferenceRadiusKm / r, deg + 2);
            for (var m = 0; m <= deg; m++)
            {
                var i = Index(deg, m);
                var g = _g[i] + dt * _gDot[i];
                var h = _h[i] + dt * _hDot[i];
                var cosM = Math.Cos(m * lambda);
                var sinM = Math.Sin(m * lambda);

                bx -= radiusPower * (g * cosM + h * sinM) * dpnm[i];
                by += radiusPower * (g * sinM - h * cosM) * m * pnm[i];
                bz -= radiusPower * (g * cosM + h * sinM) * (deg + 1) * pnm[i];
            }
        }

        by /= Math.Cos(phiPrime);

        // Rotate back into the geodetic frame (report eq. 17).
        var psi = phiPrime - phi;
        var x = bx * Math.Cos(psi) - bz * Math.Sin(psi);
        var zDown = bx * Math.Sin(psi) + bz * Math.Cos(psi);

        var horizontal = Math.Sqrt(x * x + by * by);
        var total = Math.Sqrt(horizontal * horizontal + zDown * zDown);

        return new MagneticFieldElements(
            x,
            by,
            zDown,
            horizontal,
            total,
            Math.Atan2(zDown, horizontal) * 180.0 / Math.PI,
            Math.Atan2(by, x) * 180.0 / Math.PI);
    }

    private static int Index(int n, int m) => n * (n + 1) / 2 + m;

    // Schmidt semi-normalised associated Legendre functions and their derivative with respect to the
    // geocentric latitude, via the recursion in NOAA's MAG_PcupLow.
    private static (double[] P, double[] DP) SchmidtLegendre(double x, int maxDegree)
    {
        var count = Index(maxDegree, maxDegree) + 1;
        var p = new double[count];
        var dp = new double[count];
        var z = Math.Sqrt((1.0 - x) * (1.0 + x));

        p[0] = 1.0;
        for (var n = 1; n <= maxDegree; n++)
        {
            for (var m = 0; m <= n; m++)
            {
                var i = Index(n, m);
                if (n == m)
                {
                    var prev = Index(n - 1, m - 1);
                    p[i] = z * p[prev];
                    dp[i] = z * dp[prev] + x * p[prev];
                }
                else if (n == 1 && m == 0)
                {
                    var prev = Index(n - 1, m);
                    p[i] = x * p[prev];
                    dp[i] = x * dp[prev] - z * p[prev];
                }
                else
                {
                    var prev1 = Index(n - 1, m);
                    if (m > n - 2)
                    {
                        p[i] = x * p[prev1];
                        dp[i] = x * dp[prev1] - z * p[prev1];
                    }
                    else
                    {
                        var prev2 = Index(n - 2, m);
                        var k = ((double)((n - 1) * (n - 1)) - m * m) / ((2.0 * n - 1) * (2.0 * n - 3));
                        p[i] = x * p[prev1] - k * p[prev2];
                        dp[i] = x * dp[prev1] - z * p[prev1] - k * dp[prev2];
                    }
                }
            }
        }

        // Gauss -> Schmidt semi-normalisation; the derivative flips sign for latitude (not colatitude).
        var schmidt = new double[count];
        schmidt[0] = 1.0;
        for (var n = 1; n <= maxDegree; n++)
        {
            schmidt[Index(n, 0)] = schmidt[Index(n - 1, 0)] * (2 * n - 1) / n;
            for (var m = 1; m <= n; m++)
            {
                schmidt[Index(n, m)] = schmidt[Index(n, m - 1)] * Math.Sqrt((n - m + 1) * (m == 1 ? 2.0 : 1.0) / (n + m));
            }
        }

        for (var i = 0; i < count; i++)
        {
            p[i] *= schmidt[i];
            dp[i] *= -schmidt[i];
        }

        return (p, dp);
    }

    private static WorldMagneticModel LoadEmbedded(string resourceName)
    {
        using var stream = typeof(WorldMagneticModel).Assembly.GetManifestResourceStream(resourceName)
            ?? throw new InvalidOperationException($"Missing embedded magnetic model '{resourceName}'.");
        using StreamReader reader = new(stream);
        return Parse(reader);
    }

    // WMM.COF: a header "epoch name date", then "n m g h gDot hDot" rows, closed by a line of 9s.
    private static WorldMagneticModel Parse(TextReader reader)
    {
        var header = (reader.ReadLine() ?? string.Empty).Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var epoch = double.Parse(header[0], CultureInfo.InvariantCulture);
        var name = header.Length > 1 ? header[1] : string.Empty;

        List<double[]> rows = new();
        var maxDegree = 0;
        while (reader.ReadLine() is { } line)
        {
            if (line.StartsWith("9999", StringComparison.Ordinal))
            {
                break;
            }

            var parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length < 6)
            {
                continue;
            }

            var values = parts.Take(6).Select(v => double.Parse(v, CultureInfo.InvariantCulture)).ToArray();
            rows.Add(values);
            maxDegree = Math.Max(maxDegree, (int)values[0]);
        }

        var count = Index(maxDegree, maxDegree) + 1;
        var g = new double[count];
        var h = new double[count];
        var gDot = new double[count];
        var hDot = new double[count];
        foreach (var row in rows)
        {
            var i = Index((int)row[0], (int)row[1]);
            g[i] = row[2];
            h[i] = row[3];
            gDot[i] = row[4];
            hDot[i] = row[5];
        }

        return new WorldMagneticModel(name, epoch, maxDegree, g, h, gDot, hDot);
    }
}
