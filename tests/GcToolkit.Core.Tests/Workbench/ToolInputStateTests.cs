using System.Globalization;
using GcToolkit.Core.ViewModels;

namespace GcToolkit.Core.Tests.Workbench;

[TestClass]
public class ToolInputStateTests
{
    private enum Fruit
    {
        Apple,
        Pear,
    }

    [TestMethod]
    public void Set_TypedValues_ReadBackTheSame()
    {
        ToolInputState state = new();

        state.Set("text", "Hello");
        state.Set("count", 13);
        state.Set("ratio", 1.5);
        state.Set("flag", true);
        state.Set("fruit", Fruit.Pear);

        Assert.AreEqual("Hello", state.GetString("text"));
        Assert.AreEqual(13, state.GetInt("count"));
        Assert.AreEqual(1.5, state.GetDouble("ratio"));
        Assert.IsTrue(state.GetBool("flag"));
        Assert.AreEqual(Fruit.Pear, state.GetEnum("fruit", Fruit.Apple));
    }

    [TestMethod]
    public void Get_MissingKey_ReturnsTheDefault()
    {
        ToolInputState state = new();

        Assert.AreEqual("x", state.GetString("text", "x"));
        Assert.AreEqual(7, state.GetInt("count", 7));
        Assert.IsTrue(state.GetBool("flag", true));
        Assert.AreEqual(Fruit.Apple, state.GetEnum("fruit", Fruit.Apple));
    }

    [TestMethod]
    public void Get_UnparsableValue_ReturnsTheDefault()
    {
        ToolInputState state = new(new Dictionary<string, string> { ["count"] = "lots", ["fruit"] = "Banana" });

        Assert.AreEqual(3, state.GetInt("count", 3));
        Assert.AreEqual(Fruit.Apple, state.GetEnum("fruit", Fruit.Apple));
    }

    [TestMethod]
    public void Set_Double_IsCultureInvariant()
    {
        var previous = CultureInfo.CurrentCulture;
        CultureInfo.CurrentCulture = new CultureInfo("cs-CZ");
        try
        {
            ToolInputState state = new();
            state.Set("ratio", 1.5);

            Assert.AreEqual("1.5", state.Values["ratio"]);
            Assert.AreEqual(1.5, state.GetDouble("ratio"));
        }
        finally
        {
            CultureInfo.CurrentCulture = previous;
        }
    }

    [TestMethod]
    public void Equals_SameValues_AreEqual()
    {
        ToolInputState a = new();
        a.Set("text", "Hi");
        ToolInputState b = new();
        b.Set("text", "Hi");
        ToolInputState c = new();
        c.Set("text", "Ho");

        Assert.AreEqual(a, b);
        Assert.AreNotEqual(a, c);
    }
}
