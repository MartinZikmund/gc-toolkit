using GcToolkit.Core.Listings;

namespace GcToolkit.Core.Tests.Listings;

[TestClass]
public class HiddenContentInspectorTests
{
    private static IReadOnlyList<HiddenContentFinding> Find(string html, HiddenContentKind kind) =>
        HiddenContentInspector.Analyze(html).Findings.Where(f => f.Kind == kind).ToList();

    // ---- Comments ----

    [TestMethod]
    public void Analyze_HtmlComment_IsReported()
    {
        var found = Find("<p>Hi</p><!-- N 50 05.123 --><p>Bye</p>", HiddenContentKind.HtmlComment);

        Assert.HasCount(1, found);
        Assert.AreEqual("N 50 05.123", found[0].Value);
    }

    [TestMethod]
    public void Analyze_CommentBeforeRootElement_IsReported()
    {
        var found = Find("<!-- first --><div>x</div>", HiddenContentKind.HtmlComment);

        Assert.HasCount(1, found);
    }

    // ---- Hidden by styling ----

    [TestMethod]
    [DataRow("<span style=\"display:none\">secret</span>")]
    [DataRow("<span style=\"DISPLAY : NONE !important\">secret</span>")]
    [DataRow("<span style=\"visibility:hidden\">secret</span>")]
    [DataRow("<span style=\"font-size:0\">secret</span>")]
    [DataRow("<span style=\"font-size:1px\">secret</span>")]
    [DataRow("<span style=\"font-size:0.05em\">secret</span>")]
    [DataRow("<span style=\"opacity:0\">secret</span>")]
    [DataRow("<span style=\"color:transparent\">secret</span>")]
    [DataRow("<span hidden>secret</span>")]
    [DataRow("<span style=\"color:#fff\">secret</span>")]
    [DataRow("<span style=\"color:white\">secret</span>")]
    [DataRow("<span style=\"color:rgb(255,255,254)\">secret</span>")]
    [DataRow("<div style=\"background-color:#336699\"><span style=\"color:#336699\">secret</span></div>")]
    [DataRow("<div style=\"background:#000 url(x.png)\"><font color=\"#010101\">secret</font></div>")]
    [DataRow("<div style=\"display:none\"><p><b>secret</b></p></div>")]
    public void Analyze_HiddenText_IsReported(string html)
    {
        var found = Find(html, HiddenContentKind.HiddenText);

        Assert.HasCount(1, found);
        Assert.AreEqual("secret", found[0].Value);
        Assert.IsFalse(string.IsNullOrEmpty(found[0].Detail));
    }

    [TestMethod]
    [DataRow("<span style=\"display:block\">shown</span>")]
    [DataRow("<span style=\"font-size:12px\">shown</span>")]
    [DataRow("<span style=\"color:#f00\">shown</span>")]
    [DataRow("<div style=\"background:#000\"><span style=\"color:#fff\">shown</span></div>")]
    public void Analyze_NormalText_IsNotReportedAsHidden(string html)
    {
        var report = HiddenContentInspector.Analyze(html);

        Assert.IsFalse(report.Findings.Any(f => f.Kind == HiddenContentKind.HiddenText && f.Value == "shown"));
    }

    [TestMethod]
    public void Analyze_HiddenText_IsExcludedFromVisibleText()
    {
        var report = HiddenContentInspector.Analyze("<p>Hello <span style=\"display:none\">secret</span>world</p>");

        Assert.AreEqual("Hello world", report.VisibleText);
    }

    [TestMethod]
    public void Analyze_HiddenGroup_IsReportedOnceNotPerDescendant()
    {
        var found = Find("<div hidden>one <b>two</b></div>", HiddenContentKind.HiddenText);

        Assert.HasCount(1, found);
        Assert.AreEqual("one two", found[0].Value);
    }

    // ---- Attributes and URLs ----

    [TestMethod]
    public void Analyze_ImageAltAndTitle_AreReported()
    {
        const string html = "<img src=\"https://img.geocaching.com/a.jpg\" alt=\"look closer\" title=\"42\">";

        Assert.AreEqual("look closer", Find(html, HiddenContentKind.ImageAlt).Single().Value);
        Assert.AreEqual("42", Find(html, HiddenContentKind.ImageTitle).Single().Value);
        Assert.AreEqual("https://img.geocaching.com/a.jpg", Find(html, HiddenContentKind.ImageUrl).Single().Value);
    }

    [TestMethod]
    public void Analyze_ElementTitle_IsReported()
    {
        var found = Find("<span title=\"hover me\">text</span>", HiddenContentKind.ElementTitle);

        Assert.AreEqual("hover me", found.Single().Value);
    }

    [TestMethod]
    public void Analyze_LinkTargets_AreReportedAndEmptyOnesSkipped()
    {
        const string html = "<a href=\"https://example.com/x\">a</a><a href=\"#\">b</a><a>c</a><a href=\"\">d</a>";

        var found = Find(html, HiddenContentKind.LinkTarget);

        Assert.HasCount(1, found);
        Assert.AreEqual("https://example.com/x", found[0].Value);
    }

    [TestMethod]
    public void Analyze_EmptyAltAndTitle_AreNotReported()
    {
        var report = HiddenContentInspector.Analyze("<img src=\"a.png\" alt=\"\" title=\" \">");

        Assert.IsFalse(report.Findings.Any(f => f.Kind is HiddenContentKind.ImageAlt or HiddenContentKind.ImageTitle));
    }

    // ---- Invisible characters and whitespace ----

    [TestMethod]
    public void Analyze_ZeroWidthRun_ReportsPositionAndCount()
    {
        var found = Find("<p>ab\u200B\u200C\u200Dcd</p>", HiddenContentKind.InvisibleCharacters);

        Assert.HasCount(1, found);
        Assert.AreEqual(3, found[0].Count);
        Assert.AreEqual(2, found[0].Position);
        StringAssert.Contains(found[0].Value, "U+200B");
        StringAssert.Contains(found[0].Value, "U+200D");
    }

    [TestMethod]
    public void Analyze_SeparateInvisibleRuns_AreSeparateFindings()
    {
        var found = Find("<p>a\u200Bb\uFEFFc</p>", HiddenContentKind.InvisibleCharacters);

        Assert.HasCount(2, found);
        Assert.AreEqual(1, found[0].Position);
        Assert.AreEqual(3, found[1].Position);
    }

    [TestMethod]
    public void Analyze_TrailingTabsAndSpaces_AreReportedAsOddWhitespace()
    {
        var found = Find("<pre>line one \t \t  \nline two\t  \t\nplain</pre>", HiddenContentKind.OddWhitespace);

        Assert.HasCount(1, found);
        Assert.AreEqual(2, found[0].Count);
    }

    [TestMethod]
    public void Analyze_OrdinarySingleTrailingSpace_IsNotOddWhitespace()
    {
        var report = HiddenContentInspector.Analyze("<pre>one \ntwo\n</pre>");

        Assert.IsFalse(report.Findings.Any(f => f.Kind == HiddenContentKind.OddWhitespace));
    }

    [TestMethod]
    public void Analyze_UnusualUnicodeSpaces_AreReportedAsOddWhitespace()
    {
        var found = Find("<p>a\u2003\u2002b</p>", HiddenContentKind.OddWhitespace);

        Assert.HasCount(1, found);
        Assert.AreEqual(2, found[0].Count);
    }

    // ---- Blobs ----

    [TestMethod]
    public void Analyze_Base64InText_IsReported()
    {
        // "The coordinates are north 50 east 14"
        var found = Find("<p>Note: VGhlIGNvb3JkaW5hdGVzIGFyZSBub3J0aCA1MCBlYXN0IDE0 end</p>", HiddenContentKind.Base64Blob);

        Assert.HasCount(1, found);
        Assert.AreEqual("VGhlIGNvb3JkaW5hdGVzIGFyZSBub3J0aCA1MCBlYXN0IDE0", found[0].Value);
    }

    [TestMethod]
    public void Analyze_Base64InAttribute_IsReported()
    {
        var found = Find("<div data-x=\"VGhlIGNvb3JkaW5hdGVzIGFyZSBub3J0aA==\">x</div>", HiddenContentKind.Base64Blob);

        Assert.HasCount(1, found);
    }

    [TestMethod]
    public void Analyze_Base64InComment_IsReported()
    {
        var found = Find("<!-- VGhlIGNvb3JkaW5hdGVzIGFyZSBub3J0aA== -->", HiddenContentKind.Base64Blob);

        Assert.HasCount(1, found);
    }

    [TestMethod]
    public void Analyze_HexInText_IsReportedAsHexNotBase64()
    {
        var report = HiddenContentInspector.Analyze("<p>4e203530203035202e313233</p>");

        Assert.HasCount(1, report.Findings.Where(f => f.Kind == HiddenContentKind.HexBlob).ToList());
        Assert.IsFalse(report.Findings.Any(f => f.Kind == HiddenContentKind.Base64Blob));
    }

    [TestMethod]
    public void Analyze_SpacedHexBytes_AreReported()
    {
        var found = Find("<p>48 65 6c 6c 6f 20 47 43 21</p>", HiddenContentKind.HexBlob);

        Assert.HasCount(1, found);
    }

    [TestMethod]
    [DataRow("<p>Congratulations on finding this extraordinarily long-winded word</p>")]
    [DataRow("<p>deadbeef</p>")]
    [DataRow("<p>12345678901234567890</p>")]
    [DataRow("<p>Nonsense_with_underscores_and_a_lot_of_letters</p>")]
    [DataRow("<a href=\"https://www.geocaching.com/geocache/GC12345_a-very-long-cache-name-here\">x</a>")]
    public void Analyze_OrdinaryContent_ProducesNoBlobs(string html)
    {
        var report = HiddenContentInspector.Analyze(html);

        Assert.IsFalse(report.Findings.Any(f => f.Kind is HiddenContentKind.Base64Blob or HiddenContentKind.HexBlob));
    }

    // ---- Visible text ----

    [TestMethod]
    public void Analyze_VisibleText_KeepsParagraphsAndLineBreaks()
    {
        var report = HiddenContentInspector.Analyze(
            "<html><head><title>T</title><style>p{}</style></head><body><p>One   two</p><p>Three<br>four</p><!--c--><script>x()</script></body></html>");

        Assert.AreEqual("One two\nThree\nfour", report.VisibleText);
    }

    [TestMethod]
    public void Analyze_PreformattedText_KeepsItsLines()
    {
        var report = HiddenContentInspector.Analyze("<pre>a  b\nc</pre>");

        Assert.AreEqual("a  b\nc", report.VisibleText);
    }

    [TestMethod]
    public void Analyze_EmptyOrWhitespaceInput_ReturnsEmptyReport()
    {
        var report = HiddenContentInspector.Analyze("   ");

        Assert.IsEmpty(report.Findings);
        Assert.AreEqual(string.Empty, report.VisibleText);
    }

    [TestMethod]
    public void Analyze_NullInput_Throws() =>
        Assert.ThrowsExactly<ArgumentNullException>(() => HiddenContentInspector.Analyze(null!));

    // ---- A realistic listing ----

    [TestMethod]
    public void Analyze_TypicalMysteryListing_FindsTheHiddenClues()
    {
        const string html = """
            <!-- stage 2: look at the ROT13 -->
            <div style="background-color:#ffffff">
              <p>Welcome to my <b>mystery</b>.</p>
              <p style="color:#FFFFFF">The final is at N 50 05.123</p>
              <img src="https://img.geocaching.com/cache/x.jpg" alt="check the EXIF" title="Photo">
              <span style="font-size:1px">E 014 25.456</span>
              <a href="https://example.com/puzzle?k=1">Puzzle</a>
            </div>
            """;

        var report = HiddenContentInspector.Analyze(html);

        var hidden = report.Findings.Where(f => f.Kind == HiddenContentKind.HiddenText).Select(f => f.Value).ToList();
        CollectionAssert.AreEquivalent(new[] { "The final is at N 50 05.123", "E 014 25.456" }, hidden.ToArray());
        Assert.AreEqual("stage 2: look at the ROT13", Find(html, HiddenContentKind.HtmlComment).Single().Value);
        Assert.AreEqual("Welcome to my mystery.\nPuzzle", report.VisibleText.Replace("\n\n", "\n"));
    }
}
