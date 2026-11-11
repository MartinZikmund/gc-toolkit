using System.Globalization;

namespace GcToolkit.Core.Workbench;

internal sealed class FormulaException(FormulaError error) : Exception(error.Message)
{
    public FormulaError Error { get; } = error;

    public FormulaException(FormulaErrorKind kind, string message, int position)
        : this(new FormulaError(kind, message, position))
    {
    }
}

internal abstract class FormulaNode(int position)
{
    public readonly record struct VariableUse(string Name, int Position);

    public int Position { get; } = position;

    public abstract decimal Evaluate(IReadOnlyDictionary<string, decimal> variables);

    public abstract void CollectVariables(List<VariableUse> uses);
}

internal sealed class NumberNode(decimal value, int position) : FormulaNode(position)
{
    public override decimal Evaluate(IReadOnlyDictionary<string, decimal> variables) => value;

    public override void CollectVariables(List<VariableUse> uses)
    {
    }
}

internal sealed class VariableNode(string name, int position) : FormulaNode(position)
{
    public override decimal Evaluate(IReadOnlyDictionary<string, decimal> variables) => variables[name];

    public override void CollectVariables(List<VariableUse> uses) => uses.Add(new VariableUse(name, Position));
}

internal sealed class NegateNode(FormulaNode operand, int position) : FormulaNode(position)
{
    public override decimal Evaluate(IReadOnlyDictionary<string, decimal> variables) => -operand.Evaluate(variables);

    public override void CollectVariables(List<VariableUse> uses) => operand.CollectVariables(uses);
}

internal sealed class BinaryNode(char op, FormulaNode left, FormulaNode right, int position) : FormulaNode(position)
{
    public override decimal Evaluate(IReadOnlyDictionary<string, decimal> variables)
    {
        var l = left.Evaluate(variables);
        var r = right.Evaluate(variables);
        try
        {
            switch (op)
            {
                case '+':
                    return l + r;
                case '-':
                    return l - r;
                case '*':
                    return l * r;
                case '/':
                    return r == 0 ? throw DivisionByZero() : l / r;
                case '%':
                    return r == 0 ? throw DivisionByZero() : l % r;
                default:
                    return Power(l, r);
            }
        }
        catch (OverflowException)
        {
            throw OutOfRange();
        }
    }

    public override void CollectVariables(List<VariableUse> uses)
    {
        left.CollectVariables(uses);
        right.CollectVariables(uses);
    }

    private decimal Power(decimal b, decimal e)
    {
        if (e == decimal.Truncate(e) && Math.Abs(e) <= 400)
        {
            var count = (int)Math.Abs(e);
            decimal result = 1;
            for (var i = 0; i < count; i++)
            {
                result *= b;
            }

            if (e < 0)
            {
                return result == 0 ? throw DivisionByZero() : 1 / result;
            }

            return result;
        }

        var value = Math.Pow((double)b, (double)e);
        if (!double.IsFinite(value) || Math.Abs(value) > 7e28)
        {
            throw OutOfRange();
        }

        return (decimal)value;
    }

    private FormulaException DivisionByZero() => new(FormulaErrorKind.DivisionByZero, "Division by zero.", Position);

    private FormulaException OutOfRange() => new(FormulaErrorKind.InvalidValue, "The result is too large.", Position);
}

internal sealed class CallNode(string name, FormulaFunction function, List<FormulaNode> arguments, int position)
    : FormulaNode(position)
{
    public override decimal Evaluate(IReadOnlyDictionary<string, decimal> variables)
    {
        var values = arguments.Select(a => a.Evaluate(variables)).ToArray();
        try
        {
            return function.Invoke(values) ?? throw new FormulaException(
                FormulaErrorKind.InvalidValue, $"{name}() cannot be computed for these values.", Position);
        }
        catch (OverflowException)
        {
            throw new FormulaException(FormulaErrorKind.InvalidValue, "The result is too large.", Position);
        }
    }

    public override void CollectVariables(List<VariableUse> uses)
    {
        foreach (var argument in arguments)
        {
            argument.CollectVariables(uses);
        }
    }
}

internal sealed record FormulaFunction(int MinArguments, int MaxArguments, Func<decimal[], decimal?> Invoke)
{
    private static readonly Dictionary<string, FormulaFunction> _all = Build();

    public static FormulaFunction? Find(string name) => _all.GetValueOrDefault(name);

    private static Dictionary<string, FormulaFunction> Build()
    {
        FormulaFunction digitSum = new(1, 1, a => DigitSum(a[0]));
        FormulaFunction digitalRoot = new(1, 1, a => DigitalRoot(a[0]));

        return new(StringComparer.OrdinalIgnoreCase)
        {
            ["digitsum"] = digitSum,
            ["crosssum"] = digitSum,
            ["cs"] = digitSum,
            ["digitalroot"] = digitalRoot,
            ["iterateddigitsum"] = digitalRoot,
            ["ics"] = digitalRoot,
            ["len"] = new(1, 1, a => Digits(a[0]).Length),
            ["abs"] = new(1, 1, a => Math.Abs(a[0])),
            ["sqrt"] = new(1, 1, a => a[0] < 0 ? null : (decimal)Math.Sqrt((double)a[0])),
            ["floor"] = new(1, 1, a => Math.Floor(a[0])),
            ["ceil"] = new(1, 1, a => Math.Ceiling(a[0])),
            ["round"] = new(1, 1, a => Math.Round(a[0], MidpointRounding.AwayFromZero)),
            ["min"] = new(1, int.MaxValue, a => a.Min()),
            ["max"] = new(1, int.MaxValue, a => a.Max()),
        };
    }

    private static string Digits(decimal value)
        => new(Math.Abs(value).ToString(CultureInfo.InvariantCulture).Where(char.IsAsciiDigit).ToArray());

    private static decimal DigitSum(decimal value) => Digits(value).Sum(c => c - '0');

    private static decimal DigitalRoot(decimal value)
    {
        var sum = DigitSum(value);
        while (sum >= 10)
        {
            sum = DigitSum(sum);
        }

        return sum;
    }
}

/// <summary>Recursive descent parser: additive, multiplicative, unary, power, primary.</summary>
internal sealed class FormulaParser
{
    private readonly string _text;
    private int _pos;

    private FormulaParser(string text) => _text = text;

    public static FormulaNode Parse(string text)
    {
        FormulaParser parser = new(text ?? string.Empty);
        parser.SkipWhitespace();
        if (parser.AtEnd)
        {
            throw new FormulaException(FormulaErrorKind.Syntax, "The formula is empty.", 0);
        }

        var node = parser.ParseAdditive();
        parser.SkipWhitespace();
        if (!parser.AtEnd)
        {
            throw parser.Unexpected();
        }

        return node;
    }

    private bool AtEnd => _pos >= _text.Length;

    private char Current => _text[_pos];

    private FormulaNode ParseAdditive()
    {
        var left = ParseMultiplicative();
        while (true)
        {
            SkipWhitespace();
            if (AtEnd || Current is not ('+' or '-' or '−'))
            {
                return left;
            }

            var opPos = _pos;
            var op = Current == '+' ? '+' : '-';
            _pos++;
            left = new BinaryNode(op, left, ParseMultiplicative(), opPos);
        }
    }

    private FormulaNode ParseMultiplicative()
    {
        var left = ParseUnary();
        while (true)
        {
            SkipWhitespace();
            if (AtEnd)
            {
                return left;
            }

            char op;
            switch (Current)
            {
                case '*' when !(_pos + 1 < _text.Length && _text[_pos + 1] == '*'):
                case '×':
                case '·':
                    op = '*';
                    break;
                case '/':
                case '÷':
                    op = '/';
                    break;
                case '%':
                    op = '%';
                    break;
                default:
                    return left;
            }

            var opPos = _pos;
            _pos++;
            left = new BinaryNode(op, left, ParseUnary(), opPos);
        }
    }

    private FormulaNode ParseUnary()
    {
        SkipWhitespace();
        if (!AtEnd && Current is '-' or '−')
        {
            var pos = _pos++;
            return new NegateNode(ParseUnary(), pos);
        }

        if (!AtEnd && Current == '+')
        {
            _pos++;
            return ParseUnary();
        }

        return ParsePower();
    }

    private FormulaNode ParsePower()
    {
        var left = ParsePrimary();
        SkipWhitespace();
        if (AtEnd)
        {
            return left;
        }

        var opPos = _pos;
        if (Current == '^')
        {
            _pos++;
        }
        else if (Current == '*' && _pos + 1 < _text.Length && _text[_pos + 1] == '*')
        {
            _pos += 2;
        }
        else
        {
            return left;
        }

        return new BinaryNode('^', left, ParseUnary(), opPos);
    }

    private FormulaNode ParsePrimary()
    {
        SkipWhitespace();
        if (AtEnd)
        {
            throw new FormulaException(FormulaErrorKind.Syntax, "The formula ends too early.", _text.Length);
        }

        var start = _pos;
        var c = Current;

        if (char.IsAsciiDigit(c) || (c == '.' && _pos + 1 < _text.Length && char.IsAsciiDigit(_text[_pos + 1])))
        {
            return ParseNumber();
        }

        if (char.IsLetter(c) || c == '_')
        {
            while (!AtEnd && (char.IsLetterOrDigit(Current) || Current == '_'))
            {
                _pos++;
            }

            var name = _text[start.._pos];
            SkipWhitespace();
            if (!AtEnd && Current == '(')
            {
                return ParseCall(name, start);
            }

            return new VariableNode(name.ToUpperInvariant(), start);
        }

        if (c == '(')
        {
            _pos++;
            var inner = ParseAdditive();
            Expect(')');
            return inner;
        }

        throw Unexpected();
    }

    private FormulaNode ParseNumber()
    {
        var start = _pos;
        while (!AtEnd && char.IsAsciiDigit(Current))
        {
            _pos++;
        }

        if (!AtEnd && Current == '.' && _pos + 1 < _text.Length && char.IsAsciiDigit(_text[_pos + 1]))
        {
            _pos++;
            while (!AtEnd && char.IsAsciiDigit(Current))
            {
                _pos++;
            }
        }

        if (!decimal.TryParse(_text.AsSpan(start.._pos), NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var value))
        {
            throw new FormulaException(FormulaErrorKind.InvalidValue, "The number is too large.", start);
        }

        return new NumberNode(value, start);
    }

    private FormulaNode ParseCall(string name, int start)
    {
        var function = FormulaFunction.Find(name)
            ?? throw new FormulaException(FormulaErrorKind.UnknownFunction, $"Unknown function {name}.", start);

        _pos++;
        List<FormulaNode> arguments = [];
        SkipWhitespace();
        if (!AtEnd && Current == ')')
        {
            _pos++;
        }
        else
        {
            while (true)
            {
                arguments.Add(ParseAdditive());
                SkipWhitespace();
                if (!AtEnd && Current == ',')
                {
                    _pos++;
                    continue;
                }

                Expect(')');
                break;
            }
        }

        if (arguments.Count < function.MinArguments || arguments.Count > function.MaxArguments)
        {
            throw new FormulaException(
                FormulaErrorKind.WrongArgumentCount, $"{name}() was given {arguments.Count} argument(s).", start);
        }

        return new CallNode(name, function, arguments, start);
    }

    private void Expect(char expected)
    {
        SkipWhitespace();
        if (AtEnd)
        {
            throw new FormulaException(FormulaErrorKind.Syntax, $"Missing '{expected}'.", _text.Length);
        }

        if (Current != expected)
        {
            throw Unexpected();
        }

        _pos++;
    }

    private FormulaException Unexpected()
        => new(FormulaErrorKind.Syntax, $"Unexpected '{Current}'.", _pos);

    private void SkipWhitespace()
    {
        while (!AtEnd && char.IsWhiteSpace(Current))
        {
            _pos++;
        }
    }
}
