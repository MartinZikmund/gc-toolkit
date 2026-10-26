using GcToolkit.Core.Isbn;

namespace GcToolkit.Core.Tests.Isbn;

[TestClass]
public class CodeAnalyzerTests
{
    [TestMethod]
    [DataRow("")]
    [DataRow("   ")]
    [DataRow(" - ")]
    public void Analyze_BlankInput_IsEmpty(string input)
        => Assert.AreEqual(CodeStatus.Empty, CodeAnalyzer.Analyze(input, CodeStandard.Isbn).Status);

    [TestMethod]
    [DataRow("978-0-13-468599-1")]
    [DataRow("978 0 13 468599 1")]
    [DataRow("9780134685991")]
    [DataRow("ISBN-13: 978-0-13-468599-1")]
    [DataRow("isbn 978–0–13–468599–1")]
    public void Analyze_ValidIsbn13InAnyNotation_IsValid(string input)
    {
        var analysis = CodeAnalyzer.Analyze(input, CodeStandard.Isbn);

        Assert.AreEqual(CodeStatus.Valid, analysis.Status);
        Assert.AreEqual(CodeFormat.Isbn13, analysis.Format);
        Assert.AreEqual("9780134685991", analysis.Code);
    }

    [TestMethod]
    public void Analyze_ValidIsbn13With978_ConvertsToIsbn10()
    {
        var analysis = CodeAnalyzer.Analyze("9780134685991", CodeStandard.Isbn);

        Assert.AreEqual(1, analysis.Conversions.Count);
        Assert.AreEqual(new CodeConversion(CodeFormat.Isbn10, "0134685997"), analysis.Conversions[0]);
        Assert.AreEqual(ConversionNote.None, analysis.Note);
    }

    [TestMethod]
    [DataRow("0-8044-2957-X", "9780804429573")]
    [DataRow("0-8044-2957-x", "9780804429573")]
    [DataRow("0306406152", "9780306406157")]
    public void Analyze_ValidIsbn10_ConvertsToIsbn13(string input, string expected)
    {
        var analysis = CodeAnalyzer.Analyze(input, CodeStandard.Isbn);

        Assert.AreEqual(CodeStatus.Valid, analysis.Status);
        Assert.AreEqual(CodeFormat.Isbn10, analysis.Format);
        Assert.AreEqual(new CodeConversion(CodeFormat.Isbn13, expected), analysis.Conversions.Single());
    }

    [TestMethod]
    public void Analyze_Isbn13With979_HasNoIsbn10AndExplainsWhy()
    {
        var analysis = CodeAnalyzer.Analyze("979-10-90636-07-1", CodeStandard.Isbn);

        Assert.AreEqual(CodeStatus.Valid, analysis.Status);
        Assert.AreEqual(0, analysis.Conversions.Count);
        Assert.AreEqual(ConversionNote.NoIsbn10For979, analysis.Note);
    }

    [TestMethod]
    public void Analyze_ThirteenDigitsWithoutBooklandPrefix_FlagsItAsNotAnIsbn()
    {
        var analysis = CodeAnalyzer.Analyze("4006381333931", CodeStandard.Isbn);

        Assert.AreEqual(CodeStatus.Valid, analysis.Status);
        Assert.AreEqual(ConversionNote.NotBookland, analysis.Note);
        Assert.AreEqual(0, analysis.LookupLinks.Count);
    }

    [TestMethod]
    public void Analyze_WrongCheckDigit_ReportsEnteredAndExpected()
    {
        var analysis = CodeAnalyzer.Analyze("978-0-13-468599-2", CodeStandard.Isbn);

        Assert.AreEqual(CodeStatus.InvalidCheckDigit, analysis.Status);
        Assert.AreEqual('2', analysis.EnteredCheck);
        Assert.AreEqual('1', analysis.ExpectedCheck);
        Assert.IsNull(analysis.Code);
        Assert.AreEqual(0, analysis.Conversions.Count);
    }

    [TestMethod]
    public void Analyze_AdjacentTransposition_SuggestsTheSwapBeforeAnySubstitution()
    {
        // 9780134685991 with the 6 and 8 swapped.
        var analysis = CodeAnalyzer.Analyze("9780134865991", CodeStandard.Isbn);

        Assert.AreEqual(CodeStatus.InvalidCheckDigit, analysis.Status);
        Assert.IsTrue(analysis.Corrections.Any(c =>
            c is { Kind: CorrectionKind.Transposition, Code: "9780134685991", Position: 8 }));
        var kinds = analysis.Corrections.Select(c => c.Kind).ToList();
        Assert.IsTrue(kinds.LastIndexOf(CorrectionKind.Transposition) < kinds.IndexOf(CorrectionKind.Substitution));
    }

    [TestMethod]
    public void Analyze_SingleDigitTypo_SuggestsTheOriginalAmongSubstitutions()
    {
        var analysis = CodeAnalyzer.Analyze("0306406162", CodeStandard.Isbn);

        Assert.AreEqual(CodeStatus.InvalidCheckDigit, analysis.Status);
        Assert.IsTrue(analysis.Corrections.Any(c =>
            c is { Kind: CorrectionKind.Substitution, Code: "0306406152", Position: 9 }));
        Assert.IsTrue(analysis.Corrections.All(c =>
            CodeAnalyzer.Analyze(c.Code, CodeStandard.Isbn).Status == CodeStatus.Valid));
    }

    [TestMethod]
    [DataRow("978013468599", "9780134685991")]
    [DataRow("978-0-13-468599-?", "9780134685991")]
    [DataRow("080442957", "080442957X")]
    [DataRow("0-8044-2957-?", "080442957X")]
    public void Analyze_MissingCheckDigit_ComputesIt(string input, string expected)
    {
        var analysis = CodeAnalyzer.Analyze(input, CodeStandard.Isbn);

        Assert.AreEqual(CodeStatus.CheckDigitComputed, analysis.Status);
        Assert.AreEqual(expected, analysis.Code);
        char? computedCheck = analysis.ExpectedCheck;
        Assert.AreEqual(expected[^1], computedCheck);
    }

    [TestMethod]
    public void Analyze_ComputedIsbn10Check_ShowsTheWorking()
    {
        var working = CodeAnalyzer.Analyze("080442957", CodeStandard.Isbn).Working!;

        Assert.AreEqual(199, working.Sum);
        Assert.AreEqual(1, working.Remainder);
        Assert.AreEqual(10, working.CheckValue);
        Assert.AreEqual('X', working.CheckSymbol);
        Assert.AreEqual("0×10 + 8×9 + 0×8 + 4×7 + 4×6 + 2×5 + 9×4 + 5×3 + 7×2 = 199", working.TermsLine);
        Assert.AreEqual("199 mod 11 = 1", working.RemainderLine);
        Assert.AreEqual("(11 − 1) mod 11 = 10 = X", working.CheckLine);
    }

    [TestMethod]
    public void Analyze_ComputedIsbn13Check_ShowsTheWorking()
    {
        var working = CodeAnalyzer.Analyze("978013468599", CodeStandard.Isbn).Working!;

        Assert.AreEqual(129, working.Sum);
        Assert.AreEqual("129 mod 10 = 9", working.RemainderLine);
        Assert.AreEqual("(10 − 9) mod 10 = 1", working.CheckLine);
        Assert.IsTrue(working.TermsLine.StartsWith("9×1 + 7×3 + 8×1", StringComparison.Ordinal));
    }

    [TestMethod]
    public void Analyze_OneUnknownBodyDigitWithKnownCheck_FindsTheSingleSolution()
    {
        var analysis = CodeAnalyzer.Analyze("978-0-306-4061?-7", CodeStandard.Isbn);

        Assert.AreEqual(CodeStatus.Solved, analysis.Status);
        CollectionAssert.AreEqual(new[] { "9780306406157" }, analysis.Solutions.ToArray());
        Assert.AreEqual(1, analysis.SolutionCount);
    }

    [TestMethod]
    public void Analyze_UnknownBodyAndCheckDigits_EnumeratesEveryFill()
    {
        var analysis = CodeAnalyzer.Analyze("030640615?", CodeStandard.Isbn);
        Assert.AreEqual(CodeStatus.CheckDigitComputed, analysis.Status);

        analysis = CodeAnalyzer.Analyze("03064061??", CodeStandard.Isbn);

        Assert.AreEqual(CodeStatus.Solved, analysis.Status);
        Assert.AreEqual(10, analysis.SolutionCount);
        Assert.IsTrue(analysis.Solutions.Contains("0306406152"));
        Assert.IsTrue(analysis.Solutions.All(s =>
            CodeAnalyzer.Analyze(s, CodeStandard.Isbn).Status == CodeStatus.Valid));
    }

    [TestMethod]
    [DataRow("978-0-13-46?599-1")]
    [DataRow("978-0-13-46_599-1")]
    [DataRow("978-0-13-46*599-1")]
    public void Analyze_AnyWildcardSymbol_IsTreatedAsUnknown(string input)
    {
        var analysis = CodeAnalyzer.Analyze(input, CodeStandard.Isbn);

        Assert.AreEqual(CodeStatus.Solved, analysis.Status);
        Assert.IsTrue(analysis.Solutions.Contains("9780134685991"));
    }

    [TestMethod]
    public void Analyze_NoFillSatisfiesTheCheck_ReportsZeroSolutions()
    {
        // ISBN-10 weights are coprime with 11, so a single unknown has exactly one residue —
        // and here it needs a 10 in a body position, which no digit can supply.
        var analysis = CodeAnalyzer.Analyze("?00000000X", CodeStandard.Isbn);

        Assert.AreEqual(CodeStatus.Solved, analysis.Status);
        Assert.AreEqual(0, analysis.SolutionCount);
    }

    [TestMethod]
    public void Analyze_ManySolutions_CapsTheListButCountsThemAll()
    {
        var analysis = CodeAnalyzer.Analyze("978-0-13-4???9?-1", CodeStandard.Isbn);

        Assert.AreEqual(CodeStatus.Solved, analysis.Status);
        Assert.AreEqual(1000, analysis.SolutionCount);
        Assert.AreEqual(CodeAnalyzer.MaxSolutions, analysis.Solutions.Count);
        Assert.IsTrue(analysis.SolutionsTruncated);
    }

    [TestMethod]
    public void Analyze_TooManyWildcards_IsRejected()
    {
        var analysis = CodeAnalyzer.Analyze("978-???????-91", CodeStandard.Isbn);

        Assert.AreEqual(CodeStatus.TooManyWildcards, analysis.Status);
        Assert.AreEqual(7, analysis.WildcardCount);
    }

    [TestMethod]
    public void Analyze_UnexpectedCharacter_ReportsItAndItsPosition()
    {
        var analysis = CodeAnalyzer.Analyze("978-0-1A-468599-1", CodeStandard.Isbn);

        Assert.AreEqual(CodeStatus.InvalidCharacter, analysis.Status);
        Assert.AreEqual(new InputError('A', 8), analysis.Error);
    }

    [TestMethod]
    [DataRow("97801346859X1")]
    [DataRow("978013468599X")]
    [DataRow("08044X957X")]
    public void Analyze_XOutsideAMod11CheckPosition_IsMisplaced(string input)
        => Assert.AreEqual(CodeStatus.MisplacedX, CodeAnalyzer.Analyze(input, CodeStandard.Isbn).Status);

    [TestMethod]
    [DataRow("12345")]
    [DataRow("12345678901")]
    [DataRow("12345678901234")]
    public void Analyze_WrongLength_ListsTheExpectedLengths(string input)
    {
        var analysis = CodeAnalyzer.Analyze(input, CodeStandard.Isbn);

        Assert.AreEqual(CodeStatus.WrongLength, analysis.Status);
        Assert.AreEqual(input.Length, analysis.Entered.Length);
        CollectionAssert.AreEqual(new[] { 10, 13 }, analysis.ExpectedLengths.ToArray());
    }

    [TestMethod]
    public void Analyze_ValidIsbn_OffersBookLookupLinks()
    {
        var links = CodeAnalyzer.Analyze("0-8044-2957-X", CodeStandard.Isbn).LookupLinks;

        Assert.AreEqual(4, links.Count);
        Assert.IsTrue(links.All(l => l.Uri.IsAbsoluteUri && l.Uri.Scheme == Uri.UriSchemeHttps));
        Assert.IsTrue(links.All(l => l.Uri.OriginalString.Contains("9780804429573", StringComparison.Ordinal)));
    }

    [TestMethod]
    public void Analyze_InvalidIsbn_OffersNoLookupLinks()
        => Assert.AreEqual(0, CodeAnalyzer.Analyze("9780134685992", CodeStandard.Isbn).LookupLinks.Count);

    [TestMethod]
    [DataRow("0317-8471")]
    [DataRow("ISSN 0317-8471")]
    public void Analyze_ValidIssn_ConvertsToEan13(string input)
    {
        var analysis = CodeAnalyzer.Analyze(input, CodeStandard.Issn);

        Assert.AreEqual(CodeStatus.Valid, analysis.Status);
        Assert.AreEqual(CodeFormat.Issn, analysis.Format);
        Assert.AreEqual(new CodeConversion(CodeFormat.Ean13, "9770317847001"), analysis.Conversions.Single());
    }

    [TestMethod]
    public void Analyze_IssnMissingCheck_ComputesIt()
    {
        var analysis = CodeAnalyzer.Analyze("0317847", CodeStandard.Issn);

        Assert.AreEqual(CodeStatus.CheckDigitComputed, analysis.Status);
        Assert.AreEqual("03178471", analysis.Code);
    }

    [TestMethod]
    public void Analyze_ValidUpcA_ConvertsToEan13()
    {
        var analysis = CodeAnalyzer.Analyze("0 36000 29145 2", CodeStandard.UpcA);

        Assert.AreEqual(CodeStatus.Valid, analysis.Status);
        Assert.AreEqual(new CodeConversion(CodeFormat.Ean13, "0036000291452"), analysis.Conversions.Single());
    }

    [TestMethod]
    public void Analyze_UpcAMissingCheck_ComputesIt()
        => Assert.AreEqual("036000291452", CodeAnalyzer.Analyze("03600029145", CodeStandard.UpcA).Code);

    [TestMethod]
    [DataRow("9770317847001", CodeFormat.Issn, "03178471")]
    [DataRow("9780134685991", CodeFormat.Isbn10, "0134685997")]
    [DataRow("0036000291452", CodeFormat.UpcA, "036000291452")]
    public void Analyze_Ean13WithAKnownPrefix_DerivesTheEmbeddedCode(string input, CodeFormat format, string expected)
    {
        var analysis = CodeAnalyzer.Analyze(input, CodeStandard.Ean13);

        Assert.AreEqual(CodeStatus.Valid, analysis.Status);
        Assert.AreEqual(CodeFormat.Ean13, analysis.Format);
        Assert.AreEqual(new CodeConversion(format, expected), analysis.Conversions.Single());
    }

    [TestMethod]
    public void Analyze_IsmnAsEan13_IsValid()
        => Assert.AreEqual(CodeStatus.Valid, CodeAnalyzer.Analyze("979-0-2600-0043-8", CodeStandard.Ean13).Status);

    [TestMethod]
    public void Analyze_XInAMod10Standard_IsMisplaced()
        => Assert.AreEqual(CodeStatus.MisplacedX, CodeAnalyzer.Analyze("03600029145X", CodeStandard.UpcA).Status);
}
