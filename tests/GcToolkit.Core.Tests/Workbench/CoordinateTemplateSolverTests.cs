using GcToolkit.Core.Coordinates;
using GcToolkit.Core.Workbench;

namespace GcToolkit.Core.Tests.Workbench;

[TestClass]
public class CoordinateTemplateSolverTests
{
    private static Dictionary<string, string> Vars(params (string Name, string Value)[] pairs)
        => pairs.ToDictionary(p => p.Name, p => p.Value);

    [TestMethod]
    public void Solve_BareLetters_AreReplacedByTheirDigits()
    {
        var solution = CoordinateTemplateSolver.Solve(
            "N 50° 0A.BCD E 014° 2E.FGH",
            Vars(("A", "1"), ("B", "2"), ("C", "3"), ("D", "4"), ("E", "5"), ("F", "6"), ("G", "7"), ("H", "8")));

        Assert.IsTrue(solution.IsComplete);
        Assert.AreEqual("N 50° 01.234 E 014° 25.678", solution.Text);
        Assert.AreEqual(50 + 1.234 / 60, solution.Coordinate!.Value.Latitude, 1e-9);
        Assert.AreEqual(14 + 25.678 / 60, solution.Coordinate.Value.Longitude, 1e-9);
    }

    [TestMethod]
    public void Solve_MultiDigitValue_FillsSeveralPositions()
    {
        var solution = CoordinateTemplateSolver.Solve("N 50° 12.AB E 014° 20.000", Vars(("A", "407"), ("B", "5")));

        Assert.AreEqual("N 50° 12.4075 E 014° 20.000", solution.Text);
    }

    [TestMethod]
    public void Solve_MultiDigitValue_KeepsLeadingZeros()
    {
        var solution = CoordinateTemplateSolver.Solve("N 50° 12.A E 014° 20.000", Vars(("A", "073")));
        Assert.AreEqual("N 50° 12.073 E 014° 20.000", solution.Text);
    }

    [TestMethod]
    public void Solve_ParenthesisedExpressions_AreEvaluatedAndInserted()
    {
        var solution = CoordinateTemplateSolver.Solve(
            "N 49° 1(A+B).C(D*2)E E 016° 3F.(G-1)HI",
            Vars(("A", "2"), ("B", "3"), ("C", "4"), ("D", "5"), ("E", "6"), ("F", "7"), ("G", "9"), ("H", "1"), ("I", "2")));

        Assert.IsTrue(solution.IsComplete, solution.Error?.Message);
        Assert.AreEqual("N 49° 15.4106 E 016° 37.812", solution.Text);
        Assert.IsNotNull(solution.Coordinate);
    }

    [TestMethod]
    public void Solve_ExpressionResultCanBeMultiDigit()
    {
        var solution = CoordinateTemplateSolver.Solve("N 50° (A*B).000 E 014° 20.000", Vars(("A", "5"), ("B", "9")));
        Assert.AreEqual("N 50° 45.000 E 014° 20.000", solution.Text);
    }

    [TestMethod]
    public void Solve_NamedVariablesInsideParentheses()
    {
        var solution = CoordinateTemplateSolver.Solve("N 50° (church+1).000 E 014° 20.000", Vars(("CHURCH", "7")));
        Assert.AreEqual("N 50° 8.000 E 014° 20.000", solution.Text);
    }

    [TestMethod]
    public void Solve_FunctionsInsideParentheses()
    {
        var solution = CoordinateTemplateSolver.Solve("N 50° 1(cs(A)).000 E 014° 20.000", Vars(("A", "1234")));
        Assert.AreEqual("N 50° 110.000 E 014° 20.000", solution.Text);
    }

    [TestMethod]
    public void Solve_MissingVariables_AreReportedAndNoCoordinateProduced()
    {
        var solution = CoordinateTemplateSolver.Solve("N 50° 0A.BCD E 014° 2E.(F+A)", Vars(("A", "1"), ("C", "3")));

        Assert.IsFalse(solution.IsComplete);
        Assert.IsNull(solution.Text);
        Assert.IsNull(solution.Coordinate);
        CollectionAssert.AreEqual(new[] { "B", "D", "E", "F" }, solution.MissingVariables.ToArray());
        CollectionAssert.AreEqual(new[] { "A", "B", "C", "D", "E", "F" }, solution.Variables.ToArray());
    }

    [TestMethod]
    public void Solve_HemisphereLettersAreNotVariables()
    {
        var solution = CoordinateTemplateSolver.Solve("N 50° 12.345 E 014° 20.000", Vars());
        Assert.AreEqual(0, solution.Variables.Count);
        Assert.IsTrue(solution.IsComplete);
    }

    [TestMethod]
    public void Solve_LowercaseTemplate_WorksLikeUppercase()
    {
        var solution = CoordinateTemplateSolver.Solve("n 50° 0a.bcd e 014° 20.000", Vars(("A", "1"), ("B", "2"), ("C", "3"), ("D", "4")));
        Assert.AreEqual("n 50° 01.234 e 014° 20.000", solution.Text);
    }

    [TestMethod]
    public void Solve_SouthWestTemplate()
    {
        var solution = CoordinateTemplateSolver.Solve("S 33° 5A.000 W 070° 4B.000", Vars(("A", "1"), ("B", "2")));
        Assert.IsTrue(solution.IsComplete);
        Assert.IsTrue(solution.Coordinate!.Value.Latitude < 0);
        Assert.IsTrue(solution.Coordinate.Value.Longitude < 0);
    }

    [TestMethod]
    public void Solve_ExpressionError_IsReportedWithTemplatePosition()
    {
        var solution = CoordinateTemplateSolver.Solve("N 50° 12.(A/0) E 014° 20.000", Vars(("A", "1")));

        Assert.IsFalse(solution.IsComplete);
        Assert.AreEqual(FormulaErrorKind.DivisionByZero, solution.Error!.Kind);
        Assert.AreEqual("N 50° 12.(A/0) E 014° 20.000".IndexOf('/'), solution.Error.Position);
    }

    [TestMethod]
    public void Solve_UnbalancedParenthesis_IsASyntaxError()
    {
        var solution = CoordinateTemplateSolver.Solve("N 50° 12.(A+1 E 014° 20.000", Vars(("A", "1")));
        Assert.AreEqual(FormulaErrorKind.Syntax, solution.Error!.Kind);
    }

    [TestMethod]
    public void Solve_FractionalExpressionResult_IsRejected()
    {
        var solution = CoordinateTemplateSolver.Solve("N 50° 12.(A/2)00 E 014° 20.000", Vars(("A", "3")));
        Assert.AreEqual(FormulaErrorKind.NotWholeNumber, solution.Error!.Kind);
    }

    [TestMethod]
    public void Solve_NegativeExpressionResult_IsRejected()
    {
        var solution = CoordinateTemplateSolver.Solve("N 50° 12.(A-5)00 E 014° 20.000", Vars(("A", "3")));
        Assert.AreEqual(FormulaErrorKind.NotWholeNumber, solution.Error!.Kind);
    }

    [TestMethod]
    public void Solve_ResultThatIsNotACoordinate_IsReported()
    {
        var solution = CoordinateTemplateSolver.Solve("N 95° 12.A E 014° 20.000", Vars(("A", "5")));

        Assert.AreEqual("N 95° 12.5 E 014° 20.000", solution.Text);
        Assert.IsNull(solution.Coordinate);
        Assert.AreEqual(FormulaErrorKind.InvalidCoordinate, solution.Error!.Kind);
    }

    [TestMethod]
    public void Solve_EmptyTemplate_IsAnError()
        => Assert.AreEqual(FormulaErrorKind.InvalidCoordinate, CoordinateTemplateSolver.Solve("  ", Vars()).Error!.Kind);

    [TestMethod]
    public void Solve_WithPostedCoordinates_ReportsDistance()
    {
        GeoCoordinate posted = new(50.0, 14.0);
        var solution = CoordinateTemplateSolver.Solve("N 50° 00.000 E 014° 01.000", Vars(), posted);

        var expected = Geodesy.DistanceMeters(posted, solution.Coordinate!.Value);
        Assert.AreEqual(expected, solution.DistanceMeters!.Value, 1e-6);
        Assert.IsTrue(solution.IsWithinTwoMiles);
    }

    [TestMethod]
    public void Solve_FarFromPosted_IsOutsideTwoMiles()
    {
        GeoCoordinate posted = new(50.0, 14.0);
        var solution = CoordinateTemplateSolver.Solve("N 50° 30.000 E 014° 00.000", Vars(), posted);

        Assert.IsFalse(solution.IsWithinTwoMiles);
        Assert.AreEqual(55_597, solution.DistanceMeters!.Value, 100);
    }

    [TestMethod]
    public void Solve_WithoutPosted_HasNoDistance()
    {
        var solution = CoordinateTemplateSolver.Solve("N 50° 30.000 E 014° 00.000", Vars());
        Assert.IsNull(solution.DistanceMeters);
        Assert.IsFalse(solution.IsWithinTwoMiles);
    }

    [TestMethod]
    public void Solve_DecimalDegreesTemplate()
    {
        var solution = CoordinateTemplateSolver.Solve("N 50.A E 014.B", Vars(("A", "5"), ("B", "25")));
        Assert.AreEqual(50.5, solution.Coordinate!.Value.Latitude, 1e-9);
        Assert.AreEqual(14.25, solution.Coordinate.Value.Longitude, 1e-9);
    }

    [TestMethod]
    public void Solve_Sheet_UsesVariablesAndStage()
    {
        VariableSheet sheet = VariableSheet.Empty
            .WithVariable("a", "1")
            .WithStage(new FormulaStage("Final", "N 50° 0A.000 E 014° 20.000"));

        var solutions = CoordinateTemplateSolver.SolveAll(sheet);

        Assert.AreEqual(1, solutions.Count);
        Assert.AreEqual("N 50° 01.000 E 014° 20.000", solutions[0].Text);
    }
}
