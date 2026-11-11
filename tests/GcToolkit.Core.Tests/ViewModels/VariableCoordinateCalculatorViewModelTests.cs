using GcToolkit.Core.Coordinates;
using GcToolkit.Core.Tests.Fakes;
using GcToolkit.Core.ViewModels.Tools;
using MZikmund.Toolkit.WinUI.Services;

namespace GcToolkit.Core.Tests.ViewModels;

[TestClass]
public class VariableCoordinateCalculatorViewModelTests
{
    private const string Template = "N 50° 0A.BCD E 014° 2E.FGH";

    private readonly FakeClipboardService _clipboard = new();
    private readonly FakeShareService _share = new();

    private VariableCoordinateCalculatorViewModel Create(IPreferences? preferences = null, FakeStringLocalizer? localizer = null) => new(
        new StubCatalogService("VariableCoordinateCalculator"),
        new FakeRecentsService(),
        new FakeFavoriteToolsService(),
        localizer ?? new FakeStringLocalizer(),
        _clipboard,
        _share,
        preferences ?? new InMemoryPreferences());

    private static void Fill(VariableCoordinateCalculatorViewModel vm, string letters, string digits)
    {
        for (var i = 0; i < letters.Length; i++)
        {
            vm.Variables.Single(v => v.Name == letters[i].ToString()).Value = digits[i].ToString();
        }
    }

    [TestMethod]
    public void Template_WithVariables_ListsThemInOrderOfFirstUse()
    {
        var vm = Create();

        vm.Stages[0].Template = Template;

        CollectionAssert.AreEqual(
            new[] { "A", "B", "C", "D", "E", "F", "G", "H" },
            vm.Variables.Select(v => v.Name).ToArray());
        Assert.IsTrue(vm.Variables.All(v => v.IsMissing));
    }

    [TestMethod]
    public void Template_HemisphereLetters_AreNotVariables()
    {
        var vm = Create();

        vm.Stages[0].Template = "N 50° 12.345 E 014° 25.678";

        Assert.AreEqual(0, vm.Variables.Count);
        Assert.IsTrue(vm.Stages[0].IsComplete);
    }

    [TestMethod]
    public void Variables_WhileSomeAreMissing_ShowNoResultAndNoError()
    {
        var vm = Create();
        vm.Stages[0].Template = Template;

        Fill(vm, "ABC", "123");

        var stage = vm.Stages[0];
        Assert.IsFalse(stage.IsComplete);
        Assert.IsFalse(stage.HasError);
        Assert.IsTrue(stage.HasMissing);
        Assert.IsFalse(vm.Variables.Single(v => v.Name == "A").IsMissing);
        Assert.IsTrue(vm.Variables.Single(v => v.Name == "D").IsMissing);
    }

    [TestMethod]
    public void Variables_AllFilled_ProduceTheResultInTheChosenFormat()
    {
        var vm = Create();
        vm.Stages[0].Template = Template;

        Fill(vm, "ABCDEFGH", "12345678");

        Assert.IsTrue(vm.Stages[0].IsComplete);
        Assert.AreEqual("N 50° 01.234' E 014° 25.678'", vm.Stages[0].ResultText);

        vm.FormatIndex = (int)CoordinateFormat.DecimalDegrees;

        StringAssert.StartsWith(vm.Stages[0].ResultText, "N 50.0205");
    }

    [TestMethod]
    [DataRow("N 50° 0(1/0).123 E 014° 25.678", "FormulaError_DivisionByZero", "Division by zero")]
    [DataRow("N 50° 0(1+).123 E 014° 25.678", "FormulaError_Syntax", "Not a valid formula")]
    [DataRow("N 99° 01.123 E 014° 25.678", "FormulaError_InvalidCoordinate", "Not a valid coordinate")]
    public void Template_Error_IsShownInTheUserLanguage(string template, string key, string message)
    {
        var vm = Create(localizer: new FakeStringLocalizer(new Dictionary<string, string>
        {
            ["VariableCoordinateErrorAt"] = "At {0}: {1}",
            [key] = message,
        }));

        vm.Stages[0].Template = template;

        StringAssert.StartsWith(vm.Stages[0].ErrorText, "At ");
        StringAssert.EndsWith(vm.Stages[0].ErrorText, ": " + message);
    }

    [TestMethod]
    public void Template_ThatCannotBeSolved_ShowsTheErrorWithItsPosition()
    {
        var vm = Create();

        vm.Stages[0].Template = "N 50° 0(1/0).123 E 014° 25.678";

        var stage = vm.Stages[0];
        Assert.IsTrue(stage.HasError);
        Assert.IsFalse(stage.IsComplete);
        Assert.IsTrue(stage.ErrorPosition >= 0);
        Assert.IsFalse(vm.HasAnyResult);
    }

    [TestMethod]
    public void Variable_ChangingTheTemplate_KeepsTheValuesYouAlreadyTyped()
    {
        var vm = Create();
        vm.Stages[0].Template = "N 50° 0A.BCD E 014° 25.678";
        vm.Variables.Single(v => v.Name == "A").Value = "7";

        vm.Stages[0].Template = "N 50° 0.BC E 014° 25.678";
        vm.Stages[0].Template = "N 50° 0A.BCD E 014° 25.678";

        Assert.AreEqual("7", vm.Variables.Single(v => v.Name == "A").Value);
    }

    [TestMethod]
    public void Posted_WithinTwoMiles_ShowsTheDistanceAndPassesTheCheck()
    {
        var vm = Create();
        vm.Stages[0].Template = Template;
        Fill(vm, "ABCDEFGH", "12345678");

        vm.PostedCoordinate = "N 50° 01.000 E 014° 25.000";

        var stage = vm.Stages[0];
        Assert.IsTrue(stage.HasDistance);
        Assert.IsTrue(stage.IsWithinTwoMiles);
        Assert.IsFalse(vm.HasPostedError);
    }

    [TestMethod]
    public void Posted_FarAway_FailsTheTwoMileCheck()
    {
        var vm = Create();
        vm.Stages[0].Template = Template;
        Fill(vm, "ABCDEFGH", "12345678");

        vm.PostedCoordinate = "N 51° 01.000 E 014° 25.000";

        Assert.IsTrue(vm.Stages[0].HasDistance);
        Assert.IsFalse(vm.Stages[0].IsWithinTwoMiles);
    }

    [TestMethod]
    public void Posted_Garbage_FlagsAnErrorAndSkipsTheDistance()
    {
        var vm = Create();
        vm.Stages[0].Template = Template;
        Fill(vm, "ABCDEFGH", "12345678");

        vm.PostedCoordinate = "somewhere nice";

        Assert.IsTrue(vm.HasPostedError);
        Assert.IsFalse(vm.Stages[0].HasDistance);
        Assert.IsTrue(vm.Stages[0].IsComplete);
    }

    [TestMethod]
    public void AddStage_AddsAnEmptyStageThatSharesTheVariables()
    {
        var vm = Create();
        vm.Stages[0].Template = Template;
        Fill(vm, "ABCDEFGH", "12345678");

        vm.AddStageCommand.Execute(null);
        vm.Stages[1].Template = Template;

        Assert.AreEqual(2, vm.Stages.Count);
        Assert.IsTrue(vm.Stages[1].IsComplete);
        Assert.AreEqual(8, vm.Variables.Count);
    }

    [TestMethod]
    public void RemoveStage_NeverLeavesZeroStages()
    {
        var vm = Create();

        vm.RemoveStageCommand.Execute(vm.Stages[0]);

        Assert.AreEqual(1, vm.Stages.Count);
    }

    [TestMethod]
    public void CopyAll_JoinsEveryFinishedStage()
    {
        var vm = Create();
        vm.Stages[0].Template = "N 50° 12.345 E 014° 25.678";
        vm.AddStageCommand.Execute(null);
        vm.Stages[1].Template = "N 49° 11.111 E 015° 10.000";

        vm.CopyAllCommand.Execute(null);

        Assert.AreEqual(
            "N 50° 12.345' E 014° 25.678'" + Environment.NewLine + "N 49° 11.111' E 015° 010.000'",
            _clipboard.LastText);
    }

    [TestMethod]
    public void CopyAll_WithNothingSolved_CannotRun()
    {
        var vm = Create();

        Assert.IsFalse(vm.CopyAllCommand.CanExecute(null));
    }

    [TestMethod]
    public async Task ShareAll_SendsTheResults()
    {
        var vm = Create();
        vm.Stages[0].Template = "N 50° 12.345 E 014° 25.678";

        await vm.ShareAllCommand.ExecuteAsync(null);

        Assert.AreEqual("N 50° 12.345' E 014° 25.678'", _share.LastText);
    }

    [TestMethod]
    public void LoadExample_FillsEverythingAndSolves()
    {
        var vm = Create();

        vm.LoadExampleCommand.Execute(null);

        Assert.IsTrue(vm.Stages[0].IsComplete);
        Assert.IsTrue(vm.Stages[0].IsWithinTwoMiles);
    }

    [TestMethod]
    public void Clear_ResetsTheSheet()
    {
        var vm = Create();
        vm.LoadExampleCommand.Execute(null);

        vm.ClearCommand.Execute(null);

        Assert.AreEqual(1, vm.Stages.Count);
        Assert.AreEqual(string.Empty, vm.Stages[0].Template);
        Assert.AreEqual(0, vm.Variables.Count);
        Assert.AreEqual(string.Empty, vm.PostedCoordinate);
    }

    [TestMethod]
    public void Sheet_IsRememberedOnATrimmedHead()
    {
        var preferences = InMemoryPreferences.TrimmedHead();
        var first = Create(preferences);
        first.LoadExampleCommand.Execute(null);
        first.AddStageCommand.Execute(null);
        first.Stages[1].Name = "Final";
        first.Stages[1].Template = Template;
        first.FormatIndex = (int)CoordinateFormat.DegreesMinutesSeconds;

        var second = Create(preferences);

        Assert.AreEqual(2, second.Stages.Count);
        Assert.AreEqual("Final", second.Stages[1].Name);
        Assert.AreEqual(first.Stages[0].Template, second.Stages[0].Template);
        Assert.AreEqual(first.PostedCoordinate, second.PostedCoordinate);
        Assert.AreEqual((int)CoordinateFormat.DegreesMinutesSeconds, second.FormatIndex);
        Assert.AreEqual(
            first.Variables.Single(v => v.Name == "A").Value,
            second.Variables.Single(v => v.Name == "A").Value);
        Assert.IsTrue(second.Stages[0].IsComplete);
    }

    [TestMethod]
    public void Sheet_WithCorruptStoredData_StartsEmpty()
    {
        var preferences = new InMemoryPreferences();
        preferences.SetRawComplex("variable-coordinate-sheet", "{ nope");

        var vm = Create(preferences);

        Assert.AreEqual(1, vm.Stages.Count);
        Assert.AreEqual(string.Empty, vm.Stages[0].Template);
    }
}
