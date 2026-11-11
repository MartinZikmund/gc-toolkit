using GcToolkit.Core.Workbench;

namespace GcToolkit.Core.Tests.Workbench;

[TestClass]
public class VariableSheetTests
{
    [TestMethod]
    public void WithVariable_StoresValueCaseInsensitively_AndLeavesOriginalUntouched()
    {
        var empty = VariableSheet.Empty;
        var sheet = empty.WithVariable("a", "5");

        Assert.AreEqual(0, empty.Variables.Count);
        Assert.AreEqual("5", sheet.Variables["A"]);
        Assert.AreEqual("5", sheet.Variables["a"]);
    }

    [TestMethod]
    public void WithVariable_BlankValue_RemovesVariable()
    {
        var sheet = VariableSheet.Empty.WithVariable("A", "5").WithVariable("A", " ");
        Assert.IsFalse(sheet.Variables.ContainsKey("A"));
    }

    [TestMethod]
    public void WithVariable_OverwritesExisting()
        => Assert.AreEqual("9", VariableSheet.Empty.WithVariable("A", "5").WithVariable("a", "9").Variables["A"]);

    [TestMethod]
    public void WithStage_AppendsStages()
    {
        var sheet = VariableSheet.Empty
            .WithStage(new FormulaStage("Stage 1", "N 50° 0A.000 E 014° 20.000"))
            .WithStage(new FormulaStage("Final", "N 50° 1B.000 E 014° 21.000"));

        Assert.AreEqual(2, sheet.Stages.Count);
        Assert.AreEqual("Final", sheet.Stages[1].Name);
    }
}
