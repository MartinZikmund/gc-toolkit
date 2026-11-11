using GcToolkit.Core.Coordinates;
using GcToolkit.Core.Geocaches;

namespace GcToolkit.Core.Tests.Geocaches;

[TestClass]
public class GeocacheTests
{
    private static readonly GeoCoordinate _posted = new(50.06666, 14.41666);
    private static readonly GeoCoordinate _corrected = new(50.06193, 14.42470);

    private static Geocache CreateCache(string name = "The Lost Legion", GeocacheUserData? userData = null) => new()
    {
        Code = GeocacheCode.Parse("GC8KX2Q"),
        Name = name,
        Type = GeocacheType.Mystery,
        Coordinate = _posted,
        UserData = userData ?? new(),
    };

    [TestMethod]
    public void EffectiveCoordinate_NoCorrection_ReturnsPosted()
        => Assert.AreEqual(_posted, CreateCache().EffectiveCoordinate);

    [TestMethod]
    public void EffectiveCoordinate_Corrected_ReturnsCorrected()
        => Assert.AreEqual(_corrected, CreateCache(userData: new() { CorrectedCoordinate = _corrected }).EffectiveCoordinate);

    [TestMethod]
    public void MergeFrom_SourceDataChanged_TakesIncomingSourceData()
    {
        var existing = CreateCache("Old name");
        var incoming = CreateCache("New name");

        var merged = existing.MergeFrom(incoming);

        Assert.AreEqual("New name", merged.Name);
    }

    [TestMethod]
    public void MergeFrom_IncomingHasNoUserData_KeepsExistingUserData()
    {
        GeocacheUserData userData = new() { CorrectedCoordinate = _corrected, Note = "A = 3", IsSolved = true, IsFound = true };
        var existing = CreateCache(userData: userData);
        var incoming = CreateCache();

        var merged = existing.MergeFrom(incoming);

        Assert.AreEqual(userData, merged.UserData);
    }

    [TestMethod]
    public void MergeFrom_ExistingHasNoUserData_FillsFromIncoming()
    {
        var existing = CreateCache();
        var incoming = CreateCache(userData: new() { CorrectedCoordinate = _corrected, Note = "from server", IsFound = true });

        var merged = existing.MergeFrom(incoming);

        Assert.AreEqual(_corrected, merged.UserData.CorrectedCoordinate);
        Assert.AreEqual("from server", merged.UserData.Note);
        Assert.IsTrue(merged.UserData.IsFound);
    }

    [TestMethod]
    public void MergeFrom_BothHaveUserData_ExistingWins()
    {
        GeoCoordinate other = new(50.0, 14.0);
        var existing = CreateCache(userData: new() { CorrectedCoordinate = _corrected, Note = "mine" });
        var incoming = CreateCache(userData: new() { CorrectedCoordinate = other, Note = "theirs" });

        var merged = existing.MergeFrom(incoming);

        Assert.AreEqual(_corrected, merged.UserData.CorrectedCoordinate);
        Assert.AreEqual("mine", merged.UserData.Note);
    }

    [TestMethod]
    public void MergeFrom_DifferentCode_Throws()
    {
        var existing = CreateCache();
        var incoming = CreateCache() with { Code = GeocacheCode.Parse("GC12345") };

        Assert.ThrowsExactly<ArgumentException>(() => existing.MergeFrom(incoming));
    }
}
