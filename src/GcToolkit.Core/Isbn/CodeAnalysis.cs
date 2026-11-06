namespace GcToolkit.Core.Isbn;

public enum CodeStatus
{
    Empty,
    InvalidCharacter,

    /// <summary>An <c>X</c> anywhere but the check position of a mod-11 code.</summary>
    MisplacedX,
    WrongLength,
    TooManyWildcards,
    Valid,
    InvalidCheckDigit,

    /// <summary>The check digit was missing (or <c>?</c>) and has been filled in.</summary>
    CheckDigitComputed,

    /// <summary>Unknown body digits were enumerated; <see cref="CodeAnalysis.Solutions"/> may be empty.</summary>
    Solved,
}

/// <summary>Why a valid code has no (or an unusual) conversion.</summary>
public enum ConversionNote
{
    None,

    /// <summary>ISBN-10 only ever covered the 978 range.</summary>
    NoIsbn10For979,

    /// <summary>A valid EAN-13 that doesn't start 978/979, so it isn't actually an ISBN.</summary>
    NotBookland,
}

public enum CorrectionKind
{
    /// <summary>Two neighbouring characters swapped — the most common typing error.</summary>
    Transposition,
    Substitution,
}

/// <param name="Position">1-based; for a transposition, the first of the pair.</param>
public sealed record CodeCorrection(string Code, CorrectionKind Kind, int Position, char From, char To);

public sealed record CodeConversion(CodeFormat Format, string Code);

/// <param name="Position">1-based position in the text as typed.</param>
public sealed record InputError(char Character, int Position);

public sealed record BookLookupLink(string Name, Uri Uri);

public sealed record CodeAnalysis
{
    public static CodeAnalysis Empty { get; } = new() { Status = CodeStatus.Empty };

    public required CodeStatus Status { get; init; }

    public CodeFormat? Format { get; init; }

    /// <summary>The input reduced to digits, <c>X</c> and <c>?</c> wildcards.</summary>
    public string Entered { get; init; } = string.Empty;

    /// <summary>The complete, valid code — set for <see cref="CodeStatus.Valid"/> and <see cref="CodeStatus.CheckDigitComputed"/>.</summary>
    public string? Code { get; init; }

    public char? EnteredCheck { get; init; }

    public char? ExpectedCheck { get; init; }

    public CheckDigitWorking? Working { get; init; }

    public IReadOnlyList<CodeConversion> Conversions { get; init; } = [];

    public ConversionNote Note { get; init; }

    public IReadOnlyList<BookLookupLink> LookupLinks { get; init; } = [];

    public IReadOnlyList<string> Solutions { get; init; } = [];

    public long SolutionCount { get; init; }

    public bool SolutionsTruncated => SolutionCount > Solutions.Count;

    public IReadOnlyList<CodeCorrection> Corrections { get; init; } = [];

    public InputError? Error { get; init; }

    /// <summary>Unknown body digits (a missing check digit isn't counted).</summary>
    public int WildcardCount { get; init; }

    /// <summary>Full lengths the chosen standard accepts — set for <see cref="CodeStatus.WrongLength"/>.</summary>
    public IReadOnlyList<int> ExpectedLengths { get; init; } = [];
}
