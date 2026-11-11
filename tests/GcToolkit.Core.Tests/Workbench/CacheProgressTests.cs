using GcToolkit.Core.Geocaches;
using GcToolkit.Core.Workbench;

namespace GcToolkit.Core.Tests.Workbench;

[TestClass]
public class CacheProgressTests
{
    private static CacheProgress Make(int vars = 0, int lines = 0, int tools = 0, bool found = false, bool removed = false)
        => new(GeocacheCode.Parse("GC1AAA"), "Cache", vars, 5, lines, tools, DateTimeOffset.UnixEpoch, false, found, removed);

    [TestMethod]
    public void IsInProgress_NoWork_False() => Assert.IsFalse(Make().IsInProgress);

    [TestMethod]
    [DataRow(1, 0, 0)]
    [DataRow(0, 1, 0)]
    [DataRow(0, 0, 1)]
    public void IsInProgress_AnyWork_True(int vars, int lines, int tools)
        => Assert.IsTrue(Make(vars, lines, tools).IsInProgress);

    [TestMethod]
    public void IsInProgress_Found_False() => Assert.IsFalse(Make(vars: 2, found: true).IsInProgress);

    [TestMethod]
    public void IsInProgress_Removed_False() => Assert.IsFalse(Make(vars: 2, removed: true).IsInProgress);
}
