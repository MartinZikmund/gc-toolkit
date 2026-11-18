using GcToolkit.Core.Workbench;

namespace GcToolkit.Core.Tests.Workbench;

[TestClass]
public class ToolSuggesterTests
{
    [TestMethod]
    [DataRow("N 50° 04.375 E 014° 26.120", "CoordinateConversion")]
    [DataRow("50.07292, 14.43533", "CoordinateConversion")]
    [DataRow("... --- ...", "MorseCode")]
    [DataRow(".-.. . --. .. --- -.", "MorseCode")]
    [DataRow("01001000 01101001", "BaseConverter")]
    [DataRow("MCMLXXXIV", "RomanNumerals")]
    [DataRow("SGVsbG8gd29ybGQ=", "Base64")]
    [DataRow("GC8KX2Q", "GcCodeId")]
    [DataRow("8 5 12 12 15", "NumbersToLetters")]
    [DataRow("WKUHH VHYHQ ILYH", "CaesarCipher")]
    public void Suggest_RecognisedText_PutsTheMatchingToolFirst(string text, string expected)
        => Assert.AreEqual(expected, ToolSuggester.Suggest(text)[0]);

    [TestMethod]
    public void Suggest_Letters_OffersTheLetterCiphers()
    {
        var suggestions = ToolSuggester.Suggest("WKUHH VHYHQ ILYH");

        CollectionAssert.IsSubsetOf(new[] { "CaesarCipher", "VigenereCipher", "AtbashCipher", "WordValue" }, suggestions.ToList());
    }

    [TestMethod]
    public void Suggest_AnyText_HasNoDuplicatesAndAtMostFour()
    {
        foreach (var text in new[] { "MCMLXXXIV", "8 5 12 12 15", "WKUHH VHYHQ ILYH", "? !" })
        {
            var suggestions = ToolSuggester.Suggest(text);

            Assert.IsTrue(suggestions.Count is > 0 and <= 4, text);
            Assert.AreEqual(suggestions.Count, suggestions.Distinct().Count(), text);
        }
    }

    [TestMethod]
    [DataRow("")]
    [DataRow("   ")]
    public void Suggest_Blank_SuggestsNothing(string text)
        => Assert.AreEqual(0, ToolSuggester.Suggest(text).Count);
}
