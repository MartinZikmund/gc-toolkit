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
}

public static class CrossSumCalculator
{
    public const int MaxConversionDigits = 10_000;

    public static CrossSumResult Calculate(string? input, int inputBase = 10, int digitBase = 10, long? modulus = null)
        => throw new NotImplementedException();
}
