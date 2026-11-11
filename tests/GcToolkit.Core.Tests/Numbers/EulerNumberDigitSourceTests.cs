using GcToolkit.Core.Numbers;

namespace GcToolkit.Core.Tests.Numbers;

/// <summary>
/// Validates the embedded 1,000,000-decimal expansion of e. Reference values were cross-checked
/// against two independent authoritative sources: NASA's 2-million-digit file
/// (apod.nasa.gov/htmltest/gifcity/e.2mil) and the OEIS A001113 b-file (first 49,999 decimals).
/// </summary>
[TestClass]
public class EulerNumberDigitSourceTests
{
    private const string First50 = "71828182845904523536028747135266249775724709369995";

    [TestMethod]
    public async Task GetDecimalsAsync_Loaded_HasOneMillionDecimals()
    {
        var decimals = await EulerNumberDigitSource.GetDecimalsAsync();

        Assert.AreEqual(1_000_000, decimals.Length);
        Assert.AreEqual(EulerNumberDigitSource.AvailableDecimals, decimals.Length);
    }

    [TestMethod]
    public async Task GetDecimalsAsync_Loaded_StartsWithKnownFirst50()
    {
        var decimals = await EulerNumberDigitSource.GetDecimalsAsync();

        Assert.IsTrue(decimals.StartsWith(First50, StringComparison.Ordinal));
    }

    [TestMethod]
    public async Task GetDecimalsAsync_Loaded_ContainsOnlyDigits()
    {
        var decimals = await EulerNumberDigitSource.GetDecimalsAsync();

        Assert.IsTrue(decimals.All(char.IsAsciiDigit));
    }

    [TestMethod]
    [DataRow(100, '4')]
    [DataRow(1_000, '4')]
    [DataRow(1_000_000, '8')]
    public async Task GetDecimalsAsync_DeepPosition_MatchesReferenceDigit(int position, char expected)
    {
        var decimals = await EulerNumberDigitSource.GetDecimalsAsync();

        Assert.AreEqual(expected, new EulerNumberDigits(decimals).DigitAt(position));
    }

    [TestMethod]
    [DataRow(99_991, 100_000, "1004271658")]
    [DataRow(499_991, 500_000, "1486135897")]
    [DataRow(999_991, 1_000_000, "7694228188")]
    public async Task GetDecimalsAsync_DeepRange_MatchesReferenceDigits(int from, int to, string expected)
    {
        var decimals = await EulerNumberDigitSource.GetDecimalsAsync();

        Assert.AreEqual(expected, new EulerNumberDigits(decimals).Range(from, to));
    }

    [TestMethod]
    public async Task GetDecimalsAsync_CalledTwice_ReturnsSameCachedInstance()
    {
        var first = await EulerNumberDigitSource.GetDecimalsAsync();
        var second = await EulerNumberDigitSource.GetDecimalsAsync();

        Assert.AreSame(first, second);
    }

    [TestMethod]
    public async Task GetDecimalsAsync_ManyConcurrentCallers_ShareOneLoad()
    {
        var tasks = await Task.WhenAll(Enumerable.Range(0, 32).Select(_ => Task.Run(EulerNumberDigitSource.GetDecimalsAsync)));

        Assert.IsTrue(tasks.All(t => ReferenceEquals(t, tasks[0])));
    }
}
