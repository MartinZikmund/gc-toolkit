using GcToolkit.Core.Text;

namespace GcToolkit.Core.Tests.Text;

[TestClass]
public class UpsideDownCodecTests
{
    private static UpsideDownResult Run(string text, UpsideDownOptions? options = null) =>
        UpsideDownCodec.Transform(text, options ?? new UpsideDownOptions());

    [TestMethod]
    public void Flip_Geocache_MatchesIssueExample()
    {
        Assert.AreEqual("ǝɥɔɐɔoǝ⅁", Run("Geocache").Text);
    }

    [TestMethod]
    public void Unflip_FlippedText_RestoresOriginal()
    {
        var result = Run("ǝɥɔɐɔoǝ⅁", new UpsideDownOptions(UpsideDownDirection.Unflip));
        Assert.AreEqual("Geocache", result.Text);
    }

    [TestMethod]
    public void Flip_ThenFlipAgain_IsIdentity()
    {
        const string text = "Hello, World! N 49° 12.345 E 016° 34.567?";
        var flipped = Run(text).Text;
        Assert.AreEqual(text, Run(flipped).Text);
    }

    [TestMethod]
    public void Auto_FlippedGlyphs_DetectsUnflip()
    {
        var result = Run("ǝɥɔɐɔoǝ⅁", new UpsideDownOptions(UpsideDownDirection.Auto));
        Assert.AreEqual(UpsideDownDirection.Unflip, result.ResolvedDirection);
        Assert.AreEqual("Geocache", result.Text);
    }

    [TestMethod]
    public void Auto_PlainText_DetectsFlip()
    {
        var result = Run("abc", new UpsideDownOptions(UpsideDownDirection.Auto));
        Assert.AreEqual(UpsideDownDirection.Flip, result.ResolvedDirection);
    }

    [TestMethod]
    public void ReverseOnly_MirrorsWithoutSubstitution()
    {
        Assert.AreEqual("cba", Run("abc", new UpsideDownOptions(Rotate: false)).Text);
    }

    [TestMethod]
    public void RotateOnly_SubstitutesWithoutReversing()
    {
        Assert.AreEqual("ɐqɔ", Run("abc", new UpsideDownOptions(Reverse: false)).Text);
    }

    [TestMethod]
    public void Digits_AreRotated()
    {
        Assert.AreEqual("0ϛ8ㄥ6", Run("9785" + "0").Text);
    }

    [TestMethod]
    public void WholeText_ReversesLineOrder()
    {
        Assert.AreEqual("q\np", Run("d\nb").Text);
    }

    [TestMethod]
    public void PerLine_KeepsLineOrder()
    {
        var result = Run("ab\ncd", new UpsideDownOptions(PerLine: true));
        Assert.AreEqual("qɐ\npɔ", result.Text);
    }

    [TestMethod]
    public void SymmetricCharacters_PassThroughAndCountAsCovered()
    {
        var result = Run("oxsz");
        Assert.AreEqual("zsxo", result.Text);
        Assert.AreEqual(0, result.UnmappedCharacters.Count);
        Assert.IsTrue(result.RoundTrips);
    }

    [TestMethod]
    public void UnmappedCharacter_IsReportedAndRoundTripIsLossyWhenRemoved()
    {
        var result = Run("aé", new UpsideDownOptions(Unmapped: UpsideDownUnmapped.Remove));
        Assert.AreEqual("ɐ", result.Text);
        CollectionAssert.AreEqual(new[] { "é" }, result.UnmappedCharacters.ToArray());
        Assert.IsFalse(result.RoundTrips);
    }

    [TestMethod]
    public void UnmappedCharacter_KeepIsDefault()
    {
        Assert.AreEqual("éɐ", Run("aé").Text);
    }

    [TestMethod]
    public void UnmappedCharacter_PlaceholderSubstitutes()
    {
        var result = Run("aé", new UpsideDownOptions(Unmapped: UpsideDownUnmapped.Placeholder));
        Assert.AreEqual("▯ɐ", result.Text);
    }

    [TestMethod]
    public void Compatible_FoldsUpperCaseAndReportsLossyRoundTrip()
    {
        var result = Run("G", new UpsideDownOptions(Variant: UpsideDownVariant.Compatible));
        Assert.AreEqual("ƃ", result.Text);
        Assert.IsFalse(result.RoundTrips);
    }

    [TestMethod]
    public void Full_RoundTripsLettersAndDigits()
    {
        Assert.IsTrue(Run("The Quick Brown Fox 0123456789.").RoundTrips);
    }

    [TestMethod]
    public void Unflip_CaseUpper_AppliesToDecodedText()
    {
        var result = Run("ǝɥɔɐɔoǝ⅁", new UpsideDownOptions(UpsideDownDirection.Unflip, Case: UpsideDownCase.Upper));
        Assert.AreEqual("GEOCACHE", result.Text);
    }

    [TestMethod]
    public void RemoveSpaces_DropsWhitespaceBeforeFlipping()
    {
        Assert.AreEqual("qɐ", Run("a b", new UpsideDownOptions(RemoveSpaces: true)).Text);
    }

    [TestMethod]
    public void GroupSize_InsertsSpacesEveryNCharacters()
    {
        Assert.AreEqual("ɔq ɐ", Run("abc", new UpsideDownOptions(GroupSize: 2)).Text);
    }

    [TestMethod]
    public void EmptyInput_ProducesEmptyResult()
    {
        var result = Run("");
        Assert.AreEqual("", result.Text);
        Assert.AreEqual(0, result.TotalCount);
        Assert.IsTrue(result.RoundTrips);
    }

    [TestMethod]
    public void Coverage_CountsMappedVersusTotalNonWhitespace()
    {
        var result = Run("a é");
        Assert.AreEqual(2, result.TotalCount);
        Assert.AreEqual(1, result.MappedCount);
    }
}
