using GcToolkit.Core.Numbers;

namespace GcToolkit.Core.Tests.Numbers;

[TestClass]
public class CoordinateChecksumCalculatorTests
{
    private const string Ddm = "N 49° 12.345' E 016° 30.678'";

    [TestMethod]
    public void CalculateLine_DdmCoordinate_SumsLatitudeAndLongitudeSeparately()
    {
        var row = CoordinateChecksumCalculator.CalculateLine(Ddm, CoordinateChecksumMode.DegreesDecimalMinutes);

        Assert.IsTrue(row.IsValid);
        Assert.AreEqual(28, row.Latitude);
        Assert.AreEqual(31, row.Longitude);
        Assert.AreEqual(59, row.Total);
        Assert.AreEqual(5, row.Root);
    }

    [TestMethod]
    [DataRow("N 49 12.345 E 16 30.678")]
    [DataRow("49.20575 16.5113")]
    [DataRow("N49°12'20.7\" E16°30'40.68\"")]
    public void CalculateLine_OtherFormats_NormalizeToTheSameDdmChecksum(string input)
    {
        var row = CoordinateChecksumCalculator.CalculateLine(input, CoordinateChecksumMode.DegreesDecimalMinutes);

        Assert.IsTrue(row.IsValid);
        Assert.AreEqual(59, row.Total);
    }

    [TestMethod]
    public void CalculateLine_Ddm_ShowsTheNormalizedText()
    {
        var row = CoordinateChecksumCalculator.CalculateLine(
            "N 49 12.345 E 16 30.678", CoordinateChecksumMode.DegreesDecimalMinutes);

        Assert.AreEqual(Ddm, row.Normalized);
    }

    [TestMethod]
    public void CalculateLine_AsEntered_SumsEveryDigitTyped()
    {
        var row = CoordinateChecksumCalculator.CalculateLine("N 49 12.345 E 16 30.678", CoordinateChecksumMode.AsEntered);

        Assert.AreEqual(59, row.Total);
        Assert.IsNull(row.Latitude);
        Assert.IsNull(row.Longitude);
    }

    [TestMethod]
    public void CalculateLine_AsEntered_DoesNotNeedAValidCoordinate()
    {
        var row = CoordinateChecksumCalculator.CalculateLine("ABC123", CoordinateChecksumMode.AsEntered);

        Assert.IsTrue(row.IsValid);
        Assert.AreEqual(6, row.Total);
    }

    [TestMethod]
    public void CalculateLine_AsEnteredWithoutDigits_IsInvalid()
        => Assert.IsFalse(CoordinateChecksumCalculator.CalculateLine("hello", CoordinateChecksumMode.AsEntered).IsValid);

    [TestMethod]
    public void CalculateLine_Ddm_Garbage_IsInvalid()
        => Assert.IsFalse(CoordinateChecksumCalculator.CalculateLine("not a coordinate", CoordinateChecksumMode.DegreesDecimalMinutes).IsValid);

    [TestMethod]
    public void CalculateLine_ExpectedMatches_ReportsMatch()
    {
        var row = CoordinateChecksumCalculator.CalculateLine(Ddm + " = 59", CoordinateChecksumMode.DegreesDecimalMinutes);

        Assert.AreEqual(59, row.Expected);
        Assert.AreEqual(true, row.Matches);
    }

    [TestMethod]
    public void CalculateLine_ExpectedDiffers_ReportsMismatch()
    {
        var row = CoordinateChecksumCalculator.CalculateLine(Ddm + "=62", CoordinateChecksumMode.DegreesDecimalMinutes);

        Assert.AreEqual(62, row.Expected);
        Assert.AreEqual(false, row.Matches);
    }

    [TestMethod]
    public void CalculateLine_NoExpected_MatchesIsNull()
        => Assert.IsNull(CoordinateChecksumCalculator.CalculateLine(Ddm, CoordinateChecksumMode.DegreesDecimalMinutes).Matches);

    [TestMethod]
    public void CalculateLine_VeryLongExpected_DoesNotThrow()
    {
        var row = CoordinateChecksumCalculator.CalculateLine(Ddm + " = " + new string('9', 40), CoordinateChecksumMode.DegreesDecimalMinutes);

        Assert.IsNull(row.Expected);
    }

    [TestMethod]
    public void Calculate_Batch_ReturnsOneRowPerNonBlankLine()
    {
        var rows = CoordinateChecksumCalculator.Calculate(
            $"{Ddm}\r\n\r\n   \nbogus\n{Ddm} = 59", CoordinateChecksumMode.DegreesDecimalMinutes);

        Assert.AreEqual(3, rows.Count);
        Assert.IsTrue(rows[0].IsValid);
        Assert.IsFalse(rows[1].IsValid);
        Assert.AreEqual(true, rows[2].Matches);
    }

    [TestMethod]
    public void Calculate_BareCarriageReturns_SplitLikeNewlines()
    {
        // A WinUI TextBox gives back a bare '\r' for Enter.
        var rows = CoordinateChecksumCalculator.Calculate($"{Ddm}\r{Ddm} = 59", CoordinateChecksumMode.DegreesDecimalMinutes);

        Assert.AreEqual(2, rows.Count);
        Assert.IsTrue(rows[0].IsValid);
        Assert.AreEqual(true, rows[1].Matches);
    }

    [TestMethod]
    [DataRow(null)]
    [DataRow("")]
    [DataRow("  \n ")]
    public void Calculate_Empty_ReturnsNoRows(string? text)
        => Assert.AreEqual(0, CoordinateChecksumCalculator.Calculate(text, CoordinateChecksumMode.DegreesDecimalMinutes).Count);

    [TestMethod]
    public void Calculate_ManyLines_CapsTheRows()
    {
        var text = string.Join('\n', Enumerable.Repeat(Ddm, CoordinateChecksumCalculator.MaxRows + 50));

        var rows = CoordinateChecksumCalculator.Calculate(text, CoordinateChecksumMode.DegreesDecimalMinutes);

        Assert.AreEqual(CoordinateChecksumCalculator.MaxRows, rows.Count);
    }
}
