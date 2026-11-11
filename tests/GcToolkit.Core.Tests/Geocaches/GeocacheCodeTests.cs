using GcToolkit.Core.Geocaches;

namespace GcToolkit.Core.Tests.Geocaches;

[TestClass]
public class GeocacheCodeTests
{
    [TestMethod]
    [DataRow("GC8KX2Q", "GC8KX2Q")]
    [DataRow("gc8kx2q", "GC8KX2Q")]
    [DataRow("  GC12  ", "GC12")]
    [DataRow("oc1a2b", "OC1A2B")]
    public void TryParse_ValidCode_NormalizesToUpperTrimmed(string input, string expected)
    {
        Assert.IsTrue(GeocacheCode.TryParse(input, out var code));
        Assert.AreEqual(expected, code.Value);
    }

    [TestMethod]
    [DataRow(null)]
    [DataRow("")]
    [DataRow("   ")]
    [DataRow("G")]
    [DataRow("GC 12")]
    [DataRow("GC-12")]
    public void TryParse_InvalidCode_ReturnsFalse(string? input)
        => Assert.IsFalse(GeocacheCode.TryParse(input, out _));

    [TestMethod]
    public void Equality_DifferentCasing_IsEqual()
        => Assert.AreEqual(GeocacheCode.Parse("gc8kx2q"), GeocacheCode.Parse("GC8KX2Q"));

    [TestMethod]
    public void Parse_InvalidCode_Throws()
        => Assert.ThrowsExactly<FormatException>(() => GeocacheCode.Parse("not a code"));

    [TestMethod]
    [DataRow("GC8KX2Q", true)]
    [DataRow("OC1A2B", false)]
    public void IsGeocachingCom_ByPrefix(string input, bool expected)
        => Assert.AreEqual(expected, GeocacheCode.Parse(input).IsGeocachingCom);
}
