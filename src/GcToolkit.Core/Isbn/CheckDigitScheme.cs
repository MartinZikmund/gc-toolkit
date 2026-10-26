using System.Globalization;

namespace GcToolkit.Core.Isbn;

/// <summary>
/// A weighted-sum check digit: the body digits are multiplied by position weights, and the check
/// digit is whatever brings the total to a multiple of <see cref="Modulus"/>. Mod-11 schemes
/// (ISBN-10, ISSN) spell a check value of 10 as <c>X</c>.
/// </summary>
public sealed class CheckDigitScheme
{
    private readonly Func<int, int> _weight;

    private CheckDigitScheme(CodeFormat format, int length, int modulus, Func<int, int> weight)
    {
        Format = format;
        Length = length;
        Modulus = modulus;
        _weight = weight;
    }

    public static CheckDigitScheme Isbn10 { get; } = new(CodeFormat.Isbn10, 10, 11, i => 10 - i);

    public static CheckDigitScheme Isbn13 { get; } = new(CodeFormat.Isbn13, 13, 10, AlternatingOneThree);

    public static CheckDigitScheme Issn { get; } = new(CodeFormat.Issn, 8, 11, i => 8 - i);

    public static CheckDigitScheme Ean13 { get; } = new(CodeFormat.Ean13, 13, 10, AlternatingOneThree);

    /// <summary>UPC-A is EAN-13 with the leading 0 dropped, so its weights start at 3.</summary>
    public static CheckDigitScheme UpcA { get; } = new(CodeFormat.UpcA, 12, 10, i => i % 2 == 0 ? 3 : 1);

    public CodeFormat Format { get; }

    /// <summary>Total length, check digit included.</summary>
    public int Length { get; }

    public int Modulus { get; }

    public bool AllowsX => Modulus == 11;

    public static CheckDigitScheme For(CodeFormat format) => format switch
    {
        CodeFormat.Isbn10 => Isbn10,
        CodeFormat.Isbn13 => Isbn13,
        CodeFormat.Issn => Issn,
        CodeFormat.Ean13 => Ean13,
        CodeFormat.UpcA => UpcA,
        _ => throw new ArgumentOutOfRangeException(nameof(format), format, null),
    };

    public int WeightAt(int index) => _weight(index);

    /// <summary>The check value (0…10) for <paramref name="body"/>, without building the working.</summary>
    public int CheckValue(ReadOnlySpan<int> body)
    {
        var sum = 0;
        for (var i = 0; i < body.Length; i++)
        {
            sum += body[i] * _weight(i);
        }

        return (Modulus - (sum % Modulus)) % Modulus;
    }

    public CheckDigitWorking Compute(ReadOnlySpan<int> body)
    {
        List<WeightedDigit> terms = new(body.Length);
        var sum = 0;
        for (var i = 0; i < body.Length; i++)
        {
            var weight = _weight(i);
            terms.Add(new WeightedDigit(body[i], weight));
            sum += body[i] * weight;
        }

        var remainder = sum % Modulus;
        return new CheckDigitWorking(terms, sum, Modulus, remainder, (Modulus - remainder) % Modulus);
    }

    /// <summary>Builds the complete code from a digit-only body.</summary>
    public string Complete(string body)
    {
        var digits = body.Select(c => c - '0').ToArray();
        return body + ToSymbol(CheckValue(digits));
    }

    public static char ToSymbol(int value) => value == 10 ? 'X' : (char)('0' + value);

    private static int AlternatingOneThree(int index) => index % 2 == 0 ? 1 : 3;
}

public readonly record struct WeightedDigit(int Digit, int Weight);

/// <summary>The check-digit arithmetic, step by step, so a solver can verify it by hand.</summary>
public sealed record CheckDigitWorking(
    IReadOnlyList<WeightedDigit> Terms, int Sum, int Modulus, int Remainder, int CheckValue)
{
    public char CheckSymbol => CheckDigitScheme.ToSymbol(CheckValue);

    /// <summary><c>9×1 + 7×3 + … = 129</c></summary>
    public string TermsLine
        => $"{string.Join(" + ", Terms.Select(t => $"{t.Digit}×{t.Weight}"))} = {Format(Sum)}";

    /// <summary><c>129 mod 10 = 9</c></summary>
    public string RemainderLine => $"{Format(Sum)} mod {Modulus} = {Remainder}";

    /// <summary><c>(10 − 9) mod 10 = 1</c>, with <c>= X</c> appended for a mod-11 ten.</summary>
    public string CheckLine
        => $"({Modulus} − {Remainder}) mod {Modulus} = {CheckValue}" + (CheckValue == 10 ? " = X" : string.Empty);

    private static string Format(int value) => value.ToString(CultureInfo.InvariantCulture);
}
