using GcToolkit.Core.Workbench;

namespace GcToolkit.Core.Tests.Workbench;

[TestClass]
public class FormulaEvaluatorTests
{
    private static decimal Eval(string expression, params (string Name, string Value)[] variables)
    {
        var result = FormulaEvaluator.Evaluate(expression, variables.ToDictionary(v => v.Name, v => v.Value));
        Assert.IsTrue(result.IsSuccess, $"{expression}: {result.Error?.Message}");
        return result.Value!.Value;
    }

    private static FormulaError Fail(string expression, params (string Name, string Value)[] variables)
    {
        var result = FormulaEvaluator.Evaluate(expression, variables.ToDictionary(v => v.Name, v => v.Value));
        Assert.IsFalse(result.IsSuccess, expression);
        return result.Error!;
    }

    [TestMethod]
    [DataRow("1+2*3", 7)]
    [DataRow("(1+2)*3", 9)]
    [DataRow("10-4-3", 3)]
    [DataRow("100/10/5", 2)]
    [DataRow("2^3^2", 512)]
    [DataRow("2**3", 8)]
    [DataRow("-2^2", -4)]
    [DataRow("2^-1", 0.5)]
    [DataRow("-(3+4)", -7)]
    [DataRow("--5", 5)]
    [DataRow("+5", 5)]
    [DataRow("17%5", 2)]
    [DataRow("2+17%5*3", 8)]
    [DataRow("1.5+2.25", 3.75)]
    [DataRow(".5*4", 2)]
    [DataRow("0.1+0.2", 0.3)]
    [DataRow("  7 *  ( 2 + 1 ) ", 21)]
    [DataRow("6×7", 42)]
    [DataRow("9÷3", 3)]
    public void Evaluate_Arithmetic_FollowsPrecedence(string expression, double expected)
        => Assert.AreEqual((decimal)expected, Eval(expression));

    [TestMethod]
    public void Evaluate_Variables_AreCaseInsensitive()
    {
        Assert.AreEqual(12m, Eval("a+B", ("A", "5"), ("b", "7")));
        Assert.AreEqual(9m, Eval("Church*2", ("CHURCH", "4.5")));
    }

    [TestMethod]
    public void Evaluate_AdjacentLetters_AreOneNamedVariable()
        => Assert.AreEqual(3m, Eval("AB", ("AB", "3")));

    [TestMethod]
    [DataRow("digitsum(12345)", 15)]
    [DataRow("cs(999)", 27)]
    [DataRow("CS(A)", 10)]
    [DataRow("cs(A+B)*2", 8)]
    [DataRow("digitalroot(9875)", 2)]
    [DataRow("ics(999)", 9)]
    [DataRow("ics(0)", 0)]
    [DataRow("cs(-123)", 6)]
    [DataRow("cs(1.25)", 8)]
    [DataRow("abs(-4)", 4)]
    [DataRow("sqrt(144)", 12)]
    [DataRow("floor(2.7)", 2)]
    [DataRow("ceil(2.1)", 3)]
    [DataRow("round(2.5)", 3)]
    [DataRow("min(4,2,9)", 2)]
    [DataRow("max(4, 2, 9)", 9)]
    [DataRow("len(2024)", 4)]
    public void Evaluate_Functions_ReturnExpectedValue(string expression, double expected)
        => Assert.AreEqual((decimal)expected, Eval(expression, ("A", "19"), ("B", "3")));

    [TestMethod]
    public void Evaluate_DivisionByZero_ReportsErrorWithPosition()
    {
        var error = Fail("4/(2-2)");
        Assert.AreEqual(FormulaErrorKind.DivisionByZero, error.Kind);
        Assert.AreEqual(1, error.Position);
    }

    [TestMethod]
    public void Evaluate_ModuloByZero_ReportsError()
        => Assert.AreEqual(FormulaErrorKind.DivisionByZero, Fail("5%0").Kind);

    [TestMethod]
    [DataRow("")]
    [DataRow("   ")]
    [DataRow("1+")]
    [DataRow("*3")]
    [DataRow("(1+2")]
    [DataRow("1+2)")]
    [DataRow("1 2")]
    [DataRow("1..2")]
    [DataRow("2A")]
    [DataRow("cs(1,")]
    [DataRow("1 + $")]
    public void Evaluate_BadSyntax_ReportsSyntaxError(string expression)
        => Assert.AreEqual(FormulaErrorKind.Syntax, Fail(expression).Kind);

    [TestMethod]
    public void Evaluate_SyntaxError_ReportsPosition()
    {
        Assert.AreEqual(2, Fail("1+").Position);
        Assert.AreEqual(4, Fail("1 + $").Position);
        Assert.AreEqual(2, Fail("1 2").Position);
    }

    [TestMethod]
    public void Evaluate_UnknownVariable_ListsEveryMissingOne()
    {
        var result = FormulaEvaluator.Evaluate("A+B*c+a", new Dictionary<string, string> { ["B"] = "2" });
        Assert.IsFalse(result.IsSuccess);
        Assert.AreEqual(FormulaErrorKind.UnknownVariable, result.Error!.Kind);
        CollectionAssert.AreEqual(new[] { "A", "C" }, result.MissingVariables.ToArray());
    }

    [TestMethod]
    public void Evaluate_BlankVariable_CountsAsMissing()
        => Assert.AreEqual(FormulaErrorKind.UnknownVariable, Fail("A", ("A", "  ")).Kind);

    [TestMethod]
    public void Evaluate_NonNumericVariable_ReportsInvalidValue()
        => Assert.AreEqual(FormulaErrorKind.InvalidValue, Fail("A+1", ("A", "abc")).Kind);

    [TestMethod]
    public void Evaluate_UnknownFunction_ReportsError()
        => Assert.AreEqual(FormulaErrorKind.UnknownFunction, Fail("foo(1)").Kind);

    [TestMethod]
    [DataRow("cs()")]
    [DataRow("cs(1,2)")]
    [DataRow("min()")]
    public void Evaluate_WrongArgumentCount_ReportsError(string expression)
        => Assert.AreEqual(FormulaErrorKind.WrongArgumentCount, Fail(expression).Kind);

    [TestMethod]
    [DataRow("sqrt(-1)")]
    [DataRow("10^100")]
    [DataRow("79228162514264337593543950335*10")]
    public void Evaluate_OutOfRange_ReportsInvalidValue(string expression)
        => Assert.AreEqual(FormulaErrorKind.InvalidValue, Fail(expression).Kind);

    [TestMethod]
    public void GetVariables_ReturnsDistinctUpperCaseNamesInOrder()
    {
        var names = FormulaEvaluator.GetVariables("cs(b)+A*b+Church");
        CollectionAssert.AreEqual(new[] { "B", "A", "CHURCH" }, names.ToArray());
    }

    [TestMethod]
    public void GetVariables_FunctionNamesAreNotVariables()
        => CollectionAssert.AreEqual(new[] { "A" }, FormulaEvaluator.GetVariables("ics(A)").ToArray());

    [TestMethod]
    public void GetVariables_SyntaxError_ReturnsEmpty()
        => Assert.AreEqual(0, FormulaEvaluator.GetVariables("A+").Count);
}
