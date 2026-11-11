using System.Globalization;

namespace GcToolkit.Core.Workbench;

/// <summary>What went wrong while reading or computing a formula.</summary>
public enum FormulaErrorKind
{
    /// <summary>The text is not a valid formula (bad character, missing bracket, ...).</summary>
    Syntax,

    /// <summary>One or more variables have no value yet.</summary>
    UnknownVariable,

    /// <summary>A function name that does not exist.</summary>
    UnknownFunction,

    /// <summary>A function was called with too few or too many arguments.</summary>
    WrongArgumentCount,

    /// <summary>Division or modulo by zero.</summary>
    DivisionByZero,

    /// <summary>A variable is not a number, or a result is out of range (overflow, square root of a negative number).</summary>
    InvalidValue,

    /// <summary>A coordinate template expression gave something that cannot be written as digits.</summary>
    NotWholeNumber,

    /// <summary>The substituted template is not a valid coordinate.</summary>
    InvalidCoordinate,
}

/// <summary>A formula problem. <see cref="Position"/> is a zero-based character index into the text that was parsed.</summary>
public sealed record FormulaError(FormulaErrorKind Kind, string Message, int Position);

/// <summary>The outcome of <see cref="FormulaEvaluator.Evaluate"/>.</summary>
/// <param name="Value">The computed value, or <see langword="null"/> when <paramref name="Error"/> is set.</param>
/// <param name="Error">The problem, or <see langword="null"/> on success.</param>
/// <param name="MissingVariables">Upper-case names of variables without a value, in order of first use.</param>
public sealed record FormulaResult(decimal? Value, FormulaError? Error, IReadOnlyList<string> MissingVariables)
{
    public bool IsSuccess => Error is null;
}

/// <summary>
/// Evaluates the arithmetic found in geocaching puzzles. Hand-written parser, no reflection, no dynamic code.
/// <para>
/// Numbers: integers and decimals with a dot (<c>12</c>, <c>3.5</c>, <c>.5</c>).
/// Operators: <c>+ - * / %</c> (modulo) and <c>^</c> or <c>**</c> (power, right to left), unary <c>-</c>/<c>+</c>,
/// brackets. <c>×</c>, <c>·</c>, <c>÷</c> and <c>−</c> are accepted as well. Power binds tighter than unary
/// minus, so <c>-2^2</c> is <c>-4</c>.
/// </para>
/// <para>
/// Variables: any name starting with a letter (<c>A</c>, <c>x2</c>, <c>church</c>), case-insensitive.
/// Neighbouring letters are one name (<c>AB</c> is not <c>A*B</c>); there is no implicit multiplication.
/// </para>
/// <para>
/// Functions (case-insensitive): <c>digitsum(x)</c> / <c>crosssum(x)</c> / <c>cs(x)</c> add up the digits;
/// <c>digitalroot(x)</c> / <c>iterateddigitsum(x)</c> / <c>ics(x)</c> repeat that until one digit is left;
/// <c>len(x)</c> counts the digits; <c>abs</c>, <c>sqrt</c>, <c>floor</c>, <c>ceil</c>, <c>round</c> (halves go away from zero);
/// <c>min(a,b,...)</c> and <c>max(a,b,...)</c>.
/// </para>
/// </summary>
public static class FormulaEvaluator
{
    /// <summary>Parses and computes <paramref name="expression"/>. Never throws for bad input; see <see cref="FormulaResult.Error"/>.</summary>
    /// <param name="variables">Variable name to numeric text. Names are case-insensitive; blank values count as missing.</param>
    public static FormulaResult Evaluate(string expression, IReadOnlyDictionary<string, string>? variables = null)
    {
        FormulaNode root;
        try
        {
            root = FormulaParser.Parse(expression);
        }
        catch (FormulaException ex)
        {
            return new FormulaResult(null, ex.Error, []);
        }

        var values = Normalize(variables);
        List<FormulaNode.VariableUse> uses = [];
        root.CollectVariables(uses);

        var missing = uses.Where(u => !values.ContainsKey(u.Name)).ToList();
        if (missing.Count > 0)
        {
            var names = missing.Select(m => m.Name).Distinct().ToList();
            FormulaError error = new(
                FormulaErrorKind.UnknownVariable,
                $"No value for {string.Join(", ", names)}.",
                missing[0].Position);
            return new FormulaResult(null, error, names);
        }

        Dictionary<string, decimal> numbers = new();
        foreach (var use in uses)
        {
            if (numbers.ContainsKey(use.Name))
            {
                continue;
            }

            if (!decimal.TryParse(values[use.Name], NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint,
                    CultureInfo.InvariantCulture, out var number))
            {
                FormulaError error = new(FormulaErrorKind.InvalidValue, $"{use.Name} is not a number.", use.Position);
                return new FormulaResult(null, error, []);
            }

            numbers[use.Name] = number;
        }

        try
        {
            return new FormulaResult(root.Evaluate(numbers), null, []);
        }
        catch (FormulaException ex)
        {
            return new FormulaResult(null, ex.Error, []);
        }
    }

    /// <summary>
    /// The distinct variable names (upper case) used by <paramref name="expression"/>, in order of first use.
    /// Empty when the expression has a syntax error.
    /// </summary>
    public static IReadOnlyList<string> GetVariables(string expression)
    {
        try
        {
            List<FormulaNode.VariableUse> uses = [];
            FormulaParser.Parse(expression).CollectVariables(uses);
            return uses.Select(u => u.Name).Distinct().ToList();
        }
        catch (FormulaException)
        {
            return [];
        }
    }

    internal static Dictionary<string, string> Normalize(IReadOnlyDictionary<string, string>? variables)
    {
        Dictionary<string, string> result = new();
        if (variables is null)
        {
            return result;
        }

        foreach (var (name, value) in variables)
        {
            if (!string.IsNullOrWhiteSpace(name) && !string.IsNullOrWhiteSpace(value))
            {
                result[name.Trim().ToUpperInvariant()] = value.Trim();
            }
        }

        return result;
    }
}
