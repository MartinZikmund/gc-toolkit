using GcToolkit.Core.Geocaches.Detail;

namespace GcToolkit.Core.Tests.Geocaches;

[TestClass]
public class ListingHtmlBuilderTests
{
    private static readonly ListingHtmlLabels _labels = new("Attributes", "Your note", "Images");

    private static string Build(string? longHtml, bool isDark = false, bool isHtml = true)
        => ListingHtmlBuilder.Build(new ListingHtmlModel { LongDescription = longHtml, LongDescriptionIsHtml = isHtml, IsDark = isDark, Labels = _labels }, nonce: "N0NCE");

    [TestMethod]
    [DataRow("<p>a</p><script>alert(1)</script>", "alert(1)")]
    [DataRow("<script src=\"https://evil/x.js\"></script>", "evil/x.js")]
    [DataRow("<img src=x onerror=\"alert(2)\">", "onerror")]
    [DataRow("<a href=\"javascript:alert(3)\">x</a>", "javascript:")]
    [DataRow("<iframe src=\"https://evil\"></iframe>", "<iframe")]
    public void Build_RemovesActiveContent(string input, string forbidden)
        => Assert.IsFalse(Build(input).Contains(forbidden, StringComparison.OrdinalIgnoreCase));

    [TestMethod]
    public void Build_AllowsOnlyOurScriptThroughTheCsp()
    {
        var html = Build("<p>x</p>");

        Assert.IsTrue(html.Contains("script-src 'nonce-N0NCE'", StringComparison.Ordinal));
        Assert.IsTrue(html.Contains("<script nonce=\"N0NCE\">", StringComparison.Ordinal));
    }

    [TestMethod]
    public void Build_PlainText_IsEncodedAndKeepsLineBreaks()
    {
        var html = Build("a < b\nsecond", isHtml: false);

        Assert.IsTrue(html.Contains("a &lt; b<br>second", StringComparison.Ordinal));
    }

    [TestMethod]
    public void Build_Dark_DropsInlineColorsSoTextStaysReadable()
    {
        var html = Build("<p style=\"color:#000000; font-weight:bold\"><font color=\"black\">hi</font></p>", isDark: true);

        Assert.IsFalse(html.Contains("#000000", StringComparison.Ordinal));
        Assert.IsFalse(html.Contains("color=\"black\"", StringComparison.Ordinal));
        Assert.IsTrue(html.Contains("font-weight:bold", StringComparison.Ordinal));
    }

    [TestMethod]
    public void Build_Light_KeepsInlineColors()
        => Assert.IsTrue(Build("<p style=\"color:#123456\">x</p>").Contains("#123456", StringComparison.Ordinal));

    [TestMethod]
    public void Build_Images_AreListedWithCaptions()
    {
        var html = ListingHtmlBuilder.Build(
            new ListingHtmlModel { Labels = _labels, Images = [new ListingImage("https://img/1.jpg", "Spoiler")] },
            nonce: "n");

        Assert.IsTrue(html.Contains("src=\"https://img/1.jpg\"", StringComparison.Ordinal));
        Assert.IsTrue(html.Contains("<figcaption>Spoiler</figcaption>", StringComparison.Ordinal));
    }

    [TestMethod]
    public void ToPlainText_StripsTagsAndDecodesEntities()
        => Assert.AreEqual("Hi & bye\nnext", HtmlText.ToPlainText("<p>Hi &amp; bye</p><p>next</p>"));
}
