using System.Globalization;
using GcToolkit.Core.Coordinates.Magnetic;

namespace GcToolkit.Core.Tests.Coordinates;

/// <summary>
/// Checks the WMM2025 evaluation against NOAA NCEI's published test values
/// (https://www.ncei.noaa.gov/products/world-magnetic-model). Both tables are US public domain.
/// </summary>
[TestClass]
public class WorldMagneticModelTests
{
    // Official WMM2025 test-value table (WMM2025_TEST_VALUES.txt / WMM2025testvalues.pdf, Feb 2025):
    // year, height km, lat, lon, X, Y, Z, H, F, I, D. Field values are rounded to 0.1 nT, angles to 0.01 deg.
    [TestMethod]
    [DataRow(2025.0, 0.0, 80.0, 0.0, 6521.6, 145.9, 54791.5, 6523.2, 55178.5, 83.21, 1.28)]
    [DataRow(2025.0, 0.0, 0.0, 120.0, 39677.8, -109.6, -10580.2, 39677.9, 41064.3, -14.93, -0.16)]
    [DataRow(2025.0, 0.0, -80.0, 240.0, 6117.5, 15751.9, -52022.5, 16898.1, 54698.2, -72.00, 68.78)]
    [DataRow(2025.0, 100.0, 80.0, 0.0, 6216.0, 92.4, 52598.8, 6216.7, 52964.9, 83.26, 0.85)]
    [DataRow(2025.0, 100.0, 0.0, 120.0, 37688.6, -96.2, -10152.1, 37688.7, 39032.1, -15.08, -0.15)]
    [DataRow(2025.0, 100.0, -80.0, 240.0, 5907.6, 14780.3, -49540.7, 15917.1, 52035.0, -72.19, 68.21)]
    [DataRow(2027.5, 0.0, 80.0, 0.0, 6500.8, 294.5, 54869.4, 6507.5, 55253.9, 83.24, 2.59)]
    [DataRow(2027.5, 0.0, 0.0, 120.0, 39701.6, -167.4, -10381.8, 39702.0, 41036.9, -14.65, -0.24)]
    [DataRow(2027.5, 0.0, -80.0, 240.0, 6200.7, 15730.3, -51783.7, 16908.3, 54474.2, -71.92, 68.49)]
    [DataRow(2027.5, 100.0, 80.0, 0.0, 6196.7, 233.8, 52670.5, 6201.1, 53034.3, 83.29, 2.16)]
    [DataRow(2027.5, 100.0, 0.0, 120.0, 37711.5, -148.7, -9969.8, 37711.8, 39007.4, -14.81, -0.23)]
    [DataRow(2027.5, 100.0, -80.0, 240.0, 5984.0, 14760.1, -49317.7, 15927.0, 51825.7, -72.10, 67.93)]
    public void Calculate_OfficialTestTable_MatchesPublishedValues(
        double year, double heightKm, double lat, double lon,
        double x, double y, double z, double h, double f, double inclination, double declination)
    {
        var field = WorldMagneticModel.Wmm2025.Calculate(lat, lon, heightKm, year);

        // Half a unit of the published rounding, plus a hair for binary floating point.
        const double nT = 0.05 + 1e-6;
        const double deg = 0.005 + 1e-6;
        Assert.AreEqual(x, field.X, nT, "X");
        Assert.AreEqual(y, field.Y, nT, "Y");
        Assert.AreEqual(z, field.Z, nT, "Z");
        Assert.AreEqual(h, field.H, nT, "H");
        Assert.AreEqual(f, field.F, nT, "F");
        Assert.AreEqual(inclination, field.Inclination, deg, "I");
        Assert.AreEqual(declination, field.Declination, deg, "D");
    }

    // The 100-point table shipped inside WMM2025COF.zip (WMM2025_TestValues.txt), with fields to 1e-6 nT.
    [TestMethod]
    public void Calculate_FullPrecisionTestValues_MatchAllHundredPoints()
    {
        var rows = LoadFullPrecisionTable();
        Assert.AreEqual(100, rows.Count);

        foreach (var r in rows)
        {
            var field = WorldMagneticModel.Wmm2025.Calculate(r[2], r[3], r[1], r[0]);
            var where = string.Create(CultureInfo.InvariantCulture, $"{r[0]} {r[1]}km {r[2]},{r[3]}");

            Assert.AreEqual(r[4], field.Declination, 0.005 + 1e-6, "D " + where);
            Assert.AreEqual(r[5], field.Inclination, 0.005 + 1e-6, "I " + where);
            Assert.AreEqual(r[6], field.H, 1e-3, "H " + where);
            Assert.AreEqual(r[7], field.X, 1e-3, "X " + where);
            Assert.AreEqual(r[8], field.Y, 1e-3, "Y " + where);
            Assert.AreEqual(r[9], field.Z, 1e-3, "Z " + where);
            Assert.AreEqual(r[10], field.F, 1e-3, "F " + where);
        }
    }

    [TestMethod]
    public void Wmm2025_Metadata_MatchesCoefficientHeader()
    {
        var model = WorldMagneticModel.Wmm2025;

        Assert.AreEqual(2025.0, model.Epoch);
        Assert.AreEqual("WMM-2025", model.Name);
        Assert.AreEqual(12, model.MaxDegree);
        Assert.IsTrue(model.IsValidFor(2029.9));
        Assert.IsFalse(model.IsValidFor(2030.0));
        Assert.IsFalse(model.IsValidFor(2024.9));
    }

    [TestMethod]
    [DataRow(2025, 1, 1, 2025.0)]
    [DataRow(2027, 7, 2, 2027.5)] // 2 July 00:00 is day 182 of 365: 182/365 = 0.4986
    [DataRow(2028, 12, 31, 2028.99727)] // leap year: 365/366
    public void DecimalYear_Date_ReturnsFractionOfYear(int year, int month, int day, double expected)
    {
        var result = WorldMagneticModel.DecimalYear(new DateOnly(year, month, day));

        Assert.AreEqual(expected, result, 2e-3);
    }

    [TestMethod]
    public void DeclinationDegrees_Prague2026_IsAboutFiveDegreesEast()
    {
        // Sanity check only: central Europe sits at roughly +5 deg (East) in the mid-2020s.
        var declination = WorldMagneticModel.Wmm2025.DeclinationDegrees(50.0875, 14.4214, 2026.0);

        Assert.IsTrue(declination is > 4.5 and < 6.0, declination.ToString(CultureInfo.InvariantCulture));
    }

    [TestMethod]
    public void Calculate_AtGeographicPole_ReturnsFiniteField()
    {
        var field = WorldMagneticModel.Wmm2025.Calculate(90.0, 0.0, 0.0, 2026.0);

        Assert.IsTrue(double.IsFinite(field.Declination));
        Assert.IsTrue(double.IsFinite(field.F));
    }

    private static List<double[]> LoadFullPrecisionTable()
    {
        using var stream = typeof(WorldMagneticModelTests).Assembly
            .GetManifestResourceStream("GcToolkit.Core.Tests.Coordinates.Data.WMM2025_TestValues.txt")!;
        using StreamReader reader = new(stream);

        List<double[]> rows = new();
        while (reader.ReadLine() is { } line)
        {
            if (line.StartsWith('#') || string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            rows.Add(line
                .Split(' ', StringSplitOptions.RemoveEmptyEntries)
                .Select(t => double.Parse(t, CultureInfo.InvariantCulture))
                .ToArray());
        }

        return rows;
    }
}
