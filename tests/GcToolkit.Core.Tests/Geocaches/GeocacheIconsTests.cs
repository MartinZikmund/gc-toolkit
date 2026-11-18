using System.Text.RegularExpressions;
using GcToolkit.Core.Geocaches;

namespace GcToolkit.Core.Tests.Geocaches;

[TestClass]
public sealed partial class GeocacheIconsTests
{
    public static IEnumerable<object[]> KnownTypes =>
        Enum.GetValues<GeocacheType>().Where(t => t != GeocacheType.Unknown).Select(t => new object[] { t });

    public static IEnumerable<object[]> KnownSizes =>
        Enum.GetValues<GeocacheSize>().Where(s => s != GeocacheSize.Unknown).Select(s => new object[] { s });

    public static IEnumerable<object[]> KnownAttributeIds =>
        Enumerable.Range(1, GeocacheAttributeCatalog.MaxKnownId)
            .Where(GeocacheAttributeCatalog.IsKnown)
            .Select(id => new object[] { id });

    [TestMethod]
    [DynamicData(nameof(KnownTypes))]
    public void Type_KnownType_HasOwnIcon(GeocacheType type)
    {
        var path = GeocacheIcons.Type(type);

        AssertValidPath(path);
        Assert.AreNotEqual(GeocacheIcons.Unknown, path);
    }

    [TestMethod]
    public void Type_Unknown_FallsBackToUnknownIcon()
        => Assert.AreEqual(GeocacheIcons.Unknown, GeocacheIcons.Type(GeocacheType.Unknown));

    [TestMethod]
    public void Type_EveryKnownType_IsDistinct()
    {
        var paths = KnownTypes.Select(t => GeocacheIcons.Type((GeocacheType)t[0])).ToList();

        Assert.AreEqual(paths.Count, paths.Distinct().Count());
    }

    [TestMethod]
    [DynamicData(nameof(KnownSizes))]
    public void Size_KnownSize_HasOwnIcon(GeocacheSize size)
    {
        var path = GeocacheIcons.Size(size);

        AssertValidPath(path);
        Assert.AreNotEqual(GeocacheIcons.Unknown, path);
    }

    [TestMethod]
    public void Size_EveryKnownSize_IsDistinct()
    {
        var paths = KnownSizes.Select(s => GeocacheIcons.Size((GeocacheSize)s[0])).ToList();

        Assert.AreEqual(paths.Count, paths.Distinct().Count());
    }

    [TestMethod]
    [DynamicData(nameof(KnownAttributeIds))]
    public void Attribute_KnownId_HasOwnIcon(int id)
    {
        var path = GeocacheIcons.Attribute(id);

        AssertValidPath(path);
        Assert.AreNotEqual(GeocacheIcons.Unknown, path);
    }

    [TestMethod]
    [DataRow(0)]
    [DataRow(16)]
    [DataRow(999)]
    public void Attribute_UnknownId_FallsBackToUnknownIcon(int id)
        => Assert.AreEqual(GeocacheIcons.Unknown, GeocacheIcons.Attribute(id));

    [TestMethod]
    public void Unknown_IsValidPath() => AssertValidPath(GeocacheIcons.Unknown);

    [TestMethod]
    public void ToSvg_Positive_UsesCurrentColorOnA24Box()
    {
        var svg = GeocacheIcons.ToSvg(GeocacheIcons.Unknown);

        StringAssert.Contains(svg, "viewBox=\"0 0 24 24\"");
        StringAssert.Contains(svg, "fill=\"currentColor\"");
        StringAssert.Contains(svg, $"d=\"{GeocacheIcons.Unknown}\"");
        Assert.IsFalse(svg.Contains("mask", StringComparison.Ordinal));
    }

    [TestMethod]
    public void ToSvg_Negated_StrikesTheIconThrough()
    {
        var svg = GeocacheIcons.ToSvg(GeocacheIcons.Unknown, isNegated: true);

        StringAssert.Contains(svg, "mask");
        StringAssert.Contains(svg, $"d=\"{GeocacheIcons.NegationStrike}\"");
    }

    private static void AssertValidPath(string path)
    {
        Assert.IsFalse(string.IsNullOrWhiteSpace(path));
        Assert.IsTrue(PathRegex().IsMatch(path), $"Not plain path data: {path[..Math.Min(40, path.Length)]}");
    }

    [GeneratedRegex(@"^\s*[Mm][\sMmLlHhVvCcSsQqTtAaZz0-9.,eE+-]*$")]
    private static partial Regex PathRegex();
}
