using GcToolkit.Core.Numbers;

namespace GcToolkit.Core.Tests.Numbers;

[TestClass]
public class CrossSumCalculatorTests
{
    // ---- Plain cross sum ----

    [TestMethod]
    public void Calculate_DecimalNumber_SumsTheDigits()
    {
        var result = CrossSumCalculator.Calculate("12345");

        Assert.IsTrue(result.IsValid);
        Assert.AreEqual(15, result.CrossSum);
        CollectionAssert.AreEqual(new[] { 1, 2, 3, 4, 5 }, result.Digits.ToArray());
        CollectionAssert.AreEqual(new long[] { 15, 6 }, result.Reduction.ToArray());
    }

    [TestMethod]
    public void Calculate_WhitespaceBetweenDigits_IsIgnored()
        => Assert.AreEqual(28, CrossSumCalculator.Calculate(" 1 234 567 ").CrossSum);

    [TestMethod]
    public void Calculate_LeadingZeros_ContributeNothing()
    {
        var result = CrossSumCalculator.Calculate("00120");

        Assert.AreEqual(3, result.CrossSum);
        Assert.AreEqual(-1, result.AlternatingSum);   // from the right: 0 - 2 + 1
    }

    // ---- Alternating sum: the rightmost digit is +, like the divisibility-by-11 rule ----

    [TestMethod]
    [DataRow("5", 5L)]
    [DataRow("12", 1L)]          // 2 - 1
    [DataRow("21", -1L)]         // 1 - 2
    [DataRow("12345", 3L)]       // 5 - 4 + 3 - 2 + 1
    [DataRow("121", 0L)]         // a multiple of 11
    [DataRow("918082", -22L)]    // 2 - 8 + 0 - 8 + 1 - 9
    public void Calculate_AlternatingSum_StartsPositiveAtTheRightmostDigit(string input, long expected)
        => Assert.AreEqual(expected, CrossSumCalculator.Calculate(input).AlternatingSum);

    // ---- Sign ----

    [TestMethod]
    public void Calculate_NegativeNumber_IgnoresTheSignButRemembersIt()
    {
        var negative = CrossSumCalculator.Calculate("-12345");

        Assert.IsTrue(negative.IsValid);
        Assert.IsTrue(negative.IsNegative);
        Assert.AreEqual(15, negative.CrossSum);
        Assert.AreEqual(3, negative.AlternatingSum);
    }

    [TestMethod]
    public void Calculate_ExplicitPlus_IsAccepted()
    {
        var result = CrossSumCalculator.Calculate("+42");

        Assert.IsTrue(result.IsValid);
        Assert.IsFalse(result.IsNegative);
        Assert.AreEqual(6, result.CrossSum);
    }

    // ---- Modulo ----

    [TestMethod]
    [DataRow(1L, 0L)]
    [DataRow(4L, 3L)]
    [DataRow(7L, 1L)]
    [DataRow(15L, 0L)]
    [DataRow(100L, 15L)]
    public void Calculate_Modulus_ReducesTheCrossSum(long modulus, long expected)
    {
        var result = CrossSumCalculator.Calculate("12345", modulus: modulus);

        Assert.AreEqual(modulus, result.Modulus);
        Assert.AreEqual(expected, result.CrossSumModulo);
    }

    [TestMethod]
    public void Calculate_NoModulus_LeavesItOut()
    {
        var result = CrossSumCalculator.Calculate("12345");

        Assert.IsNull(result.Modulus);
        Assert.IsNull(result.CrossSumModulo);
    }

    [TestMethod]
    [DataRow(0L)]
    [DataRow(-1L)]
    [DataRow(-7L)]
    public void Calculate_NonPositiveModulus_IsRejected(long modulus)
        => Assert.AreEqual(CrossSumError.InvalidModulus, CrossSumCalculator.Calculate("123", modulus: modulus).Error);

    // ---- Number bases ----

    [TestMethod]
    [DataRow("101", 2, 2L)]
    [DataRow("777", 8, 21L)]
    [DataRow("FF", 16, 30L)]
    [DataRow("ff", 16, 30L)]
    [DataRow("zz", 36, 70L)]
    [DataRow("Z", 36, 35L)]
    public void Calculate_InputInOtherBase_SumsDigitValuesInThatBase(string input, int numberBase, long expected)
    {
        var result = CrossSumCalculator.Calculate(input, numberBase, numberBase);

        Assert.IsTrue(result.IsValid);
        Assert.AreEqual(expected, result.CrossSum);
    }

    [TestMethod]
    public void Calculate_EveryBaseFrom2To36_AcceptsItsHighestDigit()
    {
        for (var numberBase = 2; numberBase <= 36; numberBase++)
        {
            var highest = "0123456789abcdefghijklmnopqrstuvwxyz"[numberBase - 1].ToString();

            var result = CrossSumCalculator.Calculate(highest, numberBase, numberBase);

            Assert.IsTrue(result.IsValid, $"base {numberBase}");
            Assert.AreEqual(numberBase - 1, result.CrossSum, $"base {numberBase}");
        }
    }

    [TestMethod]
    public void Calculate_LettersAreDigitsAboveBase10()
        => CollectionAssert.AreEqual(new[] { 10, 11, 12 }, CrossSumCalculator.Calculate("aBc", 16, 16).Digits.ToArray());

    [TestMethod]
    [DataRow("12", 2, '2')]
    [DataRow("9", 8, '9')]
    [DataRow("G", 16, 'G')]
    [DataRow("1.5", 10, '.')]
    [DataRow("12a", 10, 'a')]
    [DataRow("1-2", 10, '-')]
    public void Calculate_DigitNotInBase_IsRejectedAndNamed(string input, int numberBase, char culprit)
    {
        var result = CrossSumCalculator.Calculate(input, numberBase, numberBase);

        Assert.AreEqual(CrossSumError.InvalidDigit, result.Error);
        Assert.AreEqual(culprit, result.InvalidCharacter);
    }

    [TestMethod]
    [DataRow(1, 10)]
    [DataRow(0, 10)]
    [DataRow(37, 10)]
    [DataRow(10, 1)]
    [DataRow(10, 37)]
    public void Calculate_BaseOutsideTwoToThirtySix_IsRejected(int inputBase, int digitBase)
        => Assert.AreEqual(CrossSumError.InvalidBase, CrossSumCalculator.Calculate("11", inputBase, digitBase).Error);

    [TestMethod]
    public void Calculate_DecimalNumberInHex_SumsTheHexDigits()
    {
        var result = CrossSumCalculator.Calculate("255", inputBase: 10, digitBase: 16);

        CollectionAssert.AreEqual(new[] { 15, 15 }, result.Digits.ToArray());
        Assert.AreEqual(30, result.CrossSum);
    }

    [TestMethod]
    public void Calculate_HexNumberInDecimal_SumsTheDecimalDigits()
    {
        var result = CrossSumCalculator.Calculate("FF", inputBase: 16, digitBase: 10);

        CollectionAssert.AreEqual(new[] { 2, 5, 5 }, result.Digits.ToArray());
        Assert.AreEqual(12, result.CrossSum);
    }

    [TestMethod]
    public void Calculate_BinaryToDecimal_ConvertsTheValue()
        => Assert.AreEqual(1, CrossSumCalculator.Calculate("1010", 2, 10).CrossSum);   // 10 -> 1 + 0

    [TestMethod]
    public void Calculate_ConvertedNumber_AlternatesOverTheConvertedDigits()
        => Assert.AreEqual(2, CrossSumCalculator.Calculate("FF", 16, 10).AlternatingSum);   // 255: 5 - 5 + 2

    [TestMethod]
    public void Calculate_ConvertedZero_IsASingleZeroDigit()
    {
        var result = CrossSumCalculator.Calculate("0", 10, 2);

        CollectionAssert.AreEqual(new[] { 0 }, result.Digits.ToArray());
        Assert.AreEqual(0, result.CrossSum);
    }

    // ---- Reduction in the digit base ----

    [TestMethod]
    public void Calculate_DecimalReduction_ShowsEveryStep()
        => CollectionAssert.AreEqual(new long[] { 18, 9 }, CrossSumCalculator.Calculate("99").Reduction.ToArray());

    [TestMethod]
    public void Calculate_HexReduction_StopsAtASingleHexDigit()
        // FF -> 30 (0x1E) -> 1 + 14 = 15 (F)
        => CollectionAssert.AreEqual(new long[] { 30, 15 }, CrossSumCalculator.Calculate("FF", 16, 16).Reduction.ToArray());

    [TestMethod]
    public void Calculate_SingleDigit_ReducesToItself()
        => CollectionAssert.AreEqual(new long[] { 7 }, CrossSumCalculator.Calculate("7").Reduction.ToArray());

    // ---- Nothing to sum ----

    [TestMethod]
    [DataRow(null)]
    [DataRow("")]
    [DataRow("   ")]
    [DataRow("-")]
    [DataRow("+ ")]
    public void Calculate_NoDigits_ReportsNoDigits(string? input)
        => Assert.AreEqual(CrossSumError.NoDigits, CrossSumCalculator.Calculate(input).Error);

    // ---- Very long input ----

    [TestMethod]
    public void Calculate_VeryLongNumberInOneBase_IsSummedDigitWise()
    {
        var even = CrossSumCalculator.Calculate(new string('9', 100_000));
        var odd = CrossSumCalculator.Calculate(new string('9', 99_999));

        Assert.AreEqual(900_000, even.CrossSum);
        Assert.AreEqual(0, even.AlternatingSum);     // 50k pluses cancel 50k minuses
        Assert.AreEqual(9, odd.AlternatingSum);
    }

    [TestMethod]
    public void Calculate_LongNumberAcrossBases_StillConverts()
    {
        var result = CrossSumCalculator.Calculate(new string('9', 2_000), 10, 16);

        Assert.IsTrue(result.IsValid);
        Assert.IsTrue(result.CrossSum > 0);
    }

    [TestMethod]
    public void Calculate_TooLongToConvert_IsRejectedInsteadOfFreezing()
    {
        var input = new string('9', CrossSumCalculator.MaxConversionDigits + 1);

        Assert.AreEqual(CrossSumError.TooLong, CrossSumCalculator.Calculate(input, 10, 16).Error);
    }
}
