using GcToolkit.Core.Geocaches.Detail;

namespace GcToolkit.Core.Tests.Geocaches;

[TestClass]
public class ListingSelectionTests
{
    [TestMethod]
    public void TryParse_SelectionMessage_ReadsTextAndBounds()
    {
        var parsed = ListingSelection.TryParse("""{"type":"selection","text":"WKUHH","x":10,"y":20.5,"w":30,"h":12}""", out var selection);

        Assert.IsTrue(parsed);
        Assert.AreEqual(new ListingSelection("WKUHH", 10, 20.5, 30, 12), selection);
    }

    [TestMethod]
    public void TryParse_MessageSentAsAJsonString_IsUnwrapped()
    {
        var parsed = ListingSelection.TryParse("\"{\\\"type\\\":\\\"selection\\\",\\\"text\\\":\\\"abc\\\"}\"", out var selection);

        Assert.IsTrue(parsed);
        Assert.AreEqual("abc", selection.Text);
        Assert.AreEqual(0d, selection.Width);
    }

    [TestMethod]
    [DataRow("""{"type":"other","text":"x"}""")]
    [DataRow("not json")]
    [DataRow("")]
    [DataRow("null")]
    public void TryParse_AnythingElse_IsRejected(string json)
        => Assert.IsFalse(ListingSelection.TryParse(json, out _));
}
