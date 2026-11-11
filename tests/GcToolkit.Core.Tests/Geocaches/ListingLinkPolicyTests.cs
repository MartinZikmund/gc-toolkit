using GcToolkit.Core.Geocaches.Detail;

namespace GcToolkit.Core.Tests.Geocaches;

[TestClass]
public class ListingLinkPolicyTests
{
    [TestMethod]
    [DataRow("https://www.geocaching.com/geocache/GC8KX2Q")]
    [DataRow("http://example.com/a")]
    [DataRow("mailto:owner@example.com")]
    public void Decide_WebAndMailLinks_OpenInTheSystem(string uri)
        => Assert.AreEqual(ListingLinkAction.OpenExternally, ListingLinkPolicy.Decide(uri));

    [TestMethod]
    [DataRow("data:text/html;charset=utf-8,%3Chtml%3E")]
    [DataRow("about:blank")]
    public void Decide_TheListingDocumentItself_IsAllowed(string uri)
        => Assert.AreEqual(ListingLinkAction.Allow, ListingLinkPolicy.Decide(uri));

    [TestMethod]
    [DataRow("file:///C:/Windows/System32/calc.exe")]
    [DataRow("ms-settings:privacy")]
    [DataRow("javascript:alert(1)")]
    [DataRow("someapp://do-something")]
    [DataRow("not a uri")]
    [DataRow(null)]
    public void Decide_AnythingElse_IsBlocked(string? uri)
        => Assert.AreEqual(ListingLinkAction.Block, ListingLinkPolicy.Decide(uri));
}
