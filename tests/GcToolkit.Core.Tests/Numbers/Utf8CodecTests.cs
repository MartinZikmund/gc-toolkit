using GcToolkit.Core.Numbers;

namespace GcToolkit.Core.Tests.Numbers;

[TestClass]
public class Utf8CodecTests
{
    private static Utf8Options Options(
        Utf8Representation rep = Utf8Representation.Hex,
        Utf8Separator sep = Utf8Separator.Space,
        bool upper = true,
        bool bom = false) => new(rep, sep, upper, bom);

    [TestMethod]
    public void Encode_Ascii_IsSingleBytes()
        => Assert.AreEqual("48 69", Utf8Codec.Encode("Hi", Options()).Output);

    [TestMethod]
    public void Encode_MultiByteAndEmoji_UsesUtf8Sequences()
        => Assert.AreEqual("C3 A9 E2 82 AC F0 9F 98 80", Utf8Codec.Encode("é€😀", Options()).Output);

    [TestMethod]
    public void Encode_LowerCaseNoSeparator_Formats()
        => Assert.AreEqual("c3a9", Utf8Codec.Encode("é", Options(sep: Utf8Separator.None, upper: false)).Output);

    [TestMethod]
    public void Encode_PrefixAndEscapeSeparators_Format()
    {
        Assert.AreEqual("0x48 0x69", Utf8Codec.Encode("Hi", Options(sep: Utf8Separator.ZeroX)).Output);
        Assert.AreEqual("\\x48\\x69", Utf8Codec.Encode("Hi", Options(sep: Utf8Separator.EscapeX)).Output);
        Assert.AreEqual("48,69", Utf8Codec.Encode("Hi", Options(sep: Utf8Separator.Comma)).Output);
    }

    [TestMethod]
    public void Encode_DecimalBinaryOctal_Formats()
    {
        Assert.AreEqual("195 169", Utf8Codec.Encode("é", Options(Utf8Representation.Decimal)).Output);
        Assert.AreEqual("11000011 10101001", Utf8Codec.Encode("é", Options(Utf8Representation.Binary)).Output);
        Assert.AreEqual("303 251", Utf8Codec.Encode("é", Options(Utf8Representation.Octal)).Output);
    }

    [TestMethod]
    public void Encode_Percent_IgnoresSeparator()
        => Assert.AreEqual("%C3%A9", Utf8Codec.Encode("é", Options(Utf8Representation.Percent)).Output);

    [TestMethod]
    public void Encode_CodePoints_UsesUPlus()
        => Assert.AreEqual("U+0041 U+1F600", Utf8Codec.Encode("A😀", Options(Utf8Representation.CodePoints)).Output);

    [TestMethod]
    public void Encode_Utf16AndUtf32_UseEndianness()
    {
        Assert.AreEqual("00 41", Utf8Codec.Encode("A", Options(Utf8Representation.Utf16BE)).Output);
        Assert.AreEqual("41 00", Utf8Codec.Encode("A", Options(Utf8Representation.Utf16LE)).Output);
        Assert.AreEqual("00 00 00 41", Utf8Codec.Encode("A", Options(Utf8Representation.Utf32BE)).Output);
        Assert.AreEqual("41 00 00 00", Utf8Codec.Encode("A", Options(Utf8Representation.Utf32LE)).Output);
    }

    [TestMethod]
    public void Encode_WithBom_PrependsBom()
        => Assert.AreEqual("EF BB BF 41", Utf8Codec.Encode("A", Options(bom: true)).Output);

    [TestMethod]
    public void Encode_LoneSurrogate_ReportsError()
        => Assert.AreEqual(Utf8Error.LoneSurrogate, Utf8Codec.Encode("a\uD800", Options()).Error);

    [TestMethod]
    public void Encode_Empty_IsEmptyWithoutError()
    {
        var result = Utf8Codec.Encode("", Options());
        Assert.AreEqual("", result.Output);
        Assert.AreEqual(Utf8Error.None, result.Error);
    }

    [TestMethod]
    [DataRow("48 65 6C 6C 6F", "Hello")]
    [DataRow("48656c6c6f", "Hello")]
    [DataRow("0x48, 0x69", "Hi")]
    [DataRow("\\x48\\x69", "Hi")]
    [DataRow("%C3%A9", "é")]
    [DataRow("F0 9F 98 80", "😀")]
    [DataRow("U+0048 u+1F600", "H😀")]
    public void Decode_ForgivingInput_ProducesText(string input, string expected)
    {
        var result = Utf8Codec.Decode(input);
        Assert.AreEqual(Utf8Error.None, result.Error);
        Assert.AreEqual(expected, result.Output);
    }

    [TestMethod]
    public void Decode_StripsBom()
        => Assert.AreEqual("A", Utf8Codec.Decode("EF BB BF 41").Output);

    [TestMethod]
    public void Decode_OddLength_IsError()
        => Assert.AreEqual(Utf8Error.OddLength, Utf8Codec.Decode("414").Error);

    [TestMethod]
    public void Decode_NonHex_IsError()
        => Assert.AreEqual(Utf8Error.InvalidHex, Utf8Codec.Decode("4G").Error);

    [TestMethod]
    [DataRow("C0 80")] // overlong NUL
    [DataRow("80")] // stray continuation
    [DataRow("ED A0 80")] // encoded surrogate
    [DataRow("E2 82")] // truncated
    public void Decode_InvalidSequences_AreErrors(string input)
        => Assert.AreEqual(Utf8Error.InvalidSequence, Utf8Codec.Decode(input).Error);

    [TestMethod]
    public void Decode_InvalidCodePoint_IsError()
        => Assert.AreEqual(Utf8Error.InvalidCodePoint, Utf8Codec.Decode("U+110000").Error);

    [TestMethod]
    public void RoundTrip_MixedText_Survives()
    {
        const string text = "Ahoj světe – 你好 😀";
        var encoded = Utf8Codec.Encode(text, Options()).Output;
        Assert.AreEqual(text, Utf8Codec.Decode(encoded).Output);
    }

    [TestMethod]
    public void Breakdown_ListsPerCharacterDetails()
    {
        var rows = Utf8Codec.Breakdown("A€😀");
        Assert.AreEqual(3, rows.Count);
        Assert.AreEqual("U+20AC", rows[1].CodePoint);
        Assert.AreEqual(3, rows[1].ByteCount);
        Assert.AreEqual("E2 82 AC", rows[1].Hex);
        Assert.AreEqual("U+1F600", rows[2].CodePoint);
        Assert.AreEqual(4, rows[2].ByteCount);
    }
}
