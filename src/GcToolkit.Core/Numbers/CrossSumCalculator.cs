using System.Numerics;

namespace GcToolkit.Core.Numbers;

public enum CrossSumError
{
    None,
    NoDigits,
    InvalidDigit,
    InvalidBase,
    InvalidModulus,
    TooLong,
}

/// <summary>The cross-sum variants of one number.</summary>
/// <param name="Digits">The number's digits in <paramref name="DigitBase"/>, most significant first.</param>
/// <param name="CrossSum">Plain digit sum.</param>
/// <param name="AlternatingSum">Digits summed with alternating signs, rightmost digit positive.</param>
/// <param name="CrossSumModulo">The cross sum modulo <paramref name="Modulus"/>, when one was asked for.</param>
/// <param name="Reduction">The cross sum repeated until a single digit of the base is left, e.g. <c>[30, 15]</c>.</param>
public sealed record CrossSumResult(
    CrossSumError Error,
    char InvalidCharacter,
    bool IsNegative,
    int InputBase,
    int DigitBase,
    IReadOnlyList<int> Digits,
    long CrossSum,
    long AlternatingSum,
    long? Modulus,
    long? CrossSumModulo,
    IReadOnlyList<long> Reduction)
{
    public bool IsValid => Error == CrossSumError.None;

    public static CrossSumResult Failure(CrossSumError error, int inputBase, int digitBase, char invalid = '\0')
        => new(error, invalid, false, inputBase, digitBase, [], 0, 0, null, null, []);
}

/// <summary>
/// Cross-sum variants of a whole number: the plain and alternating digit sums, the sum modulo n,
/// and sums over the digits of another base (2–36, letters as digits above 9). The alternating sum
/// counts the rightmost digit positive, as in the divisibility-by-11 rule. The sign is ignored.
/// Pure and static.
/// </summary>
public static class CrossSumCalculator
{
    public const int MinBase = 2;

    public const int MaxBase = 36;

    /// <summary>Converting between bases is quadratic, so very long numbers are refused there. Same-base
    /// sums are a single pass and have no limit.</summary>
    public const int MaxConversionDigits = 10_000;

    public static CrossSumResult Calculate(string? input, int inputBase = 10, int digitBase = 10, long? modulus = null)
    {
        if (!IsValidBase(inputBase) || !IsValidBase(digitBase))
        {
            return CrossSumResult.Failure(CrossSumError.InvalidBase, inputBase, digitBase);
        }

        if (modulus is <= 0)
        {
            return CrossSumResult.Failure(CrossSumError.InvalidModulus, inputBase, digitBase);
        }

        if (!TryParseDigits(input, inputBase, out var negative, out var parsed, out var error, out var invalid))
        {
            return CrossSumResult.Failure(error, inputBase, digitBase, invalid);
        }

        var digits = parsed;
        if (inputBase != digitBase)
        {
            if (parsed.Length > MaxConversionDigits)
            {
                return CrossSumResult.Failure(CrossSumError.TooLong, inputBase, digitBase);
            }

            digits = Convert(parsed, inputBase, digitBase);
        }

        long sum = 0, alternating = 0;
        for (var i = 0; i < digits.Length; i++)
        {
            var digit = digits[digits.Length - 1 - i];
            sum += digit;
            alternating += i % 2 == 0 ? digit : -digit;
        }

        return new CrossSumResult(
            CrossSumError.None, '\0', negative, inputBase, digitBase, digits,
            sum, alternating, modulus, modulus is { } n ? sum % n : null,
            Reduce(sum, digitBase));
    }

    private static bool IsValidBase(int numberBase) => numberBase is >= MinBase and <= MaxBase;

    private static bool TryParseDigits(
        string? input, int numberBase, out bool negative, out int[] digits, out CrossSumError error, out char invalid)
    {
        negative = false;
        digits = [];
        error = CrossSumError.NoDigits;
        invalid = '\0';

        if (string.IsNullOrWhiteSpace(input))
        {
            return false;
        }

        List<int> values = new(input.Length);
        var signAllowed = true;
        foreach (var character in input)
        {
            if (char.IsWhiteSpace(character))
            {
                continue;
            }

            if (signAllowed && character is '-' or '+')
            {
                negative = character == '-';
                signAllowed = false;
                continue;
            }

            signAllowed = false;
            var value = DigitValue(character);
            if (value < 0 || value >= numberBase)
            {
                error = CrossSumError.InvalidDigit;
                invalid = character;
                return false;
            }

            values.Add(value);
        }

        if (values.Count == 0)
        {
            return false;
        }

        digits = [.. values];
        return true;
    }

    private static int DigitValue(char character) => character switch
    {
        >= '0' and <= '9' => character - '0',
        >= 'a' and <= 'z' => character - 'a' + 10,
        >= 'A' and <= 'Z' => character - 'A' + 10,
        _ => -1,
    };

    private static int[] Convert(int[] digits, int fromBase, int toBase)
    {
        BigInteger value = BigInteger.Zero;
        foreach (var digit in digits)
        {
            value = value * fromBase + digit;
        }

        if (value.IsZero)
        {
            return [0];
        }

        List<int> result = [];
        while (!value.IsZero)
        {
            value = BigInteger.DivRem(value, toBase, out var remainder);
            result.Add((int)remainder);
        }

        result.Reverse();
        return [.. result];
    }

    private static List<long> Reduce(long sum, int numberBase)
    {
        List<long> steps = [sum];
        var current = sum;
        while (current >= numberBase)
        {
            long next = 0;
            for (var rest = current; rest > 0; rest /= numberBase)
            {
                next += rest % numberBase;
            }

            steps.Add(next);
            current = next;
        }

        return steps;
    }
}
