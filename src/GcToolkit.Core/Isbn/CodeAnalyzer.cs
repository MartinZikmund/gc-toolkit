using System.Text;
using System.Text.RegularExpressions;

namespace GcToolkit.Core.Isbn;

/// <summary>
/// Validates, completes, converts and solves ISBN-family codes. Input is forgiving — hyphens,
/// spaces and an "ISBN-13:" label are ignored — and <c>?</c>, <c>_</c> or <c>*</c> mark an unknown
/// digit: an unknown check digit is computed, unknown body digits are enumerated.
/// </summary>
public static partial class CodeAnalyzer
{
    /// <summary>10^6 fills is still instant; more would only bury the answer.</summary>
    public const int MaxWildcards = 6;

    public const int MaxSolutions = 200;

    private const char Wildcard = '?';

    private static readonly CheckDigitScheme[] _isbnSchemes = [CheckDigitScheme.Isbn10, CheckDigitScheme.Isbn13];

    public static CodeAnalysis Analyze(string? input, CodeStandard standard)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            return CodeAnalysis.Empty;
        }

        var (entered, error) = Normalize(input);
        if (error is not null)
        {
            return new() { Status = CodeStatus.InvalidCharacter, Error = error };
        }

        if (entered.Length == 0)
        {
            return CodeAnalysis.Empty;
        }

        var schemes = SchemesFor(standard);
        // One short of a full length means "compute the check digit for me".
        var scheme = schemes.FirstOrDefault(s => entered.Length == s.Length || entered.Length == s.Length - 1);
        if (scheme is null)
        {
            return new()
            {
                Status = CodeStatus.WrongLength,
                Entered = entered,
                ExpectedLengths = [.. schemes.Select(s => s.Length)],
            };
        }

        var code = entered.Length == scheme.Length ? entered : entered + Wildcard;
        if (HasMisplacedX(scheme, code))
        {
            return new() { Status = CodeStatus.MisplacedX, Format = scheme.Format, Entered = entered };
        }

        var body = code[..^1];
        var check = code[^1];
        var wildcards = body.Count(c => c == Wildcard);
        if (wildcards > MaxWildcards)
        {
            return new()
            {
                Status = CodeStatus.TooManyWildcards,
                Format = scheme.Format,
                Entered = entered,
                WildcardCount = wildcards,
            };
        }

        if (wildcards > 0)
        {
            return Solve(scheme, entered, body, check, wildcards);
        }

        var working = scheme.Compute(ToDigits(body));
        var expected = working.CheckSymbol;
        if (check != Wildcard && check != expected)
        {
            return new()
            {
                Status = CodeStatus.InvalidCheckDigit,
                Format = scheme.Format,
                Entered = entered,
                EnteredCheck = check,
                ExpectedCheck = expected,
                Working = working,
                Corrections = FindCorrections(scheme, code),
            };
        }

        var complete = body + expected;
        var (conversions, note) = Convert(scheme.Format, complete);
        var isbn13 = Isbn13For(scheme.Format, complete, conversions);
        return new()
        {
            Status = check == Wildcard ? CodeStatus.CheckDigitComputed : CodeStatus.Valid,
            Format = scheme.Format,
            Entered = entered,
            Code = complete,
            EnteredCheck = check == Wildcard ? null : check,
            ExpectedCheck = expected,
            Working = working,
            Conversions = conversions,
            Note = note,
            LookupLinks = isbn13 is null ? [] : BookLookup.LinksFor(isbn13),
        };
    }

    private static IReadOnlyList<CheckDigitScheme> SchemesFor(CodeStandard standard) => standard switch
    {
        CodeStandard.Isbn => _isbnSchemes,
        CodeStandard.Issn => [CheckDigitScheme.Issn],
        CodeStandard.Ean13 => [CheckDigitScheme.Ean13],
        CodeStandard.UpcA => [CheckDigitScheme.UpcA],
        _ => throw new ArgumentOutOfRangeException(nameof(standard), standard, null),
    };

    /// <summary>Reduces the input to digits, <c>X</c> and <see cref="Wildcard"/>, or reports the first stray character.</summary>
    private static (string Symbols, InputError? Error) Normalize(string input)
    {
        var label = LabelPrefix().Match(input);
        StringBuilder symbols = new(input.Length);
        for (var i = label.Success ? label.Length : 0; i < input.Length; i++)
        {
            var c = input[i];
            switch (c)
            {
                case >= '0' and <= '9':
                    symbols.Append(c);
                    break;
                case 'X' or 'x':
                    symbols.Append('X');
                    break;
                case '?' or '_' or '*':
                    symbols.Append(Wildcard);
                    break;
                case '-' or '‐' or '‑' or '‒' or '–' or '—' or '−':
                    break;
                default:
                    if (!char.IsWhiteSpace(c))
                    {
                        return (string.Empty, new InputError(c, i + 1));
                    }

                    break;
            }
        }

        return (symbols.ToString(), null);
    }

    private static bool HasMisplacedX(CheckDigitScheme scheme, string code)
    {
        for (var i = 0; i < code.Length; i++)
        {
            if (code[i] == 'X' && !(scheme.AllowsX && i == code.Length - 1))
            {
                return true;
            }
        }

        return false;
    }

    private static CodeAnalysis Solve(CheckDigitScheme scheme, string entered, string body, char check, int wildcards)
    {
        var digits = ToDigits(body);
        var unknowns = Enumerable.Range(0, body.Length).Where(i => body[i] == Wildcard).ToArray();
        var fills = (int)Math.Pow(10, wildcards);
        List<string> solutions = [];
        long count = 0;

        for (var fill = 0; fill < fills; fill++)
        {
            var rest = fill;
            for (var u = unknowns.Length - 1; u >= 0; u--)
            {
                digits[unknowns[u]] = rest % 10;
                rest /= 10;
            }

            var symbol = CheckDigitScheme.ToSymbol(scheme.CheckValue(digits));
            if (check != Wildcard && check != symbol)
            {
                continue;
            }

            count++;
            if (solutions.Count < MaxSolutions)
            {
                solutions.Add(string.Concat(digits.Select(d => (char)('0' + d))) + symbol);
            }
        }

        return new()
        {
            Status = CodeStatus.Solved,
            Format = scheme.Format,
            Entered = entered,
            WildcardCount = wildcards,
            Solutions = solutions,
            SolutionCount = count,
        };
    }

    /// <summary>Every code one adjacent swap or one changed character away that validates — swaps first.</summary>
    private static IReadOnlyList<CodeCorrection> FindCorrections(CheckDigitScheme scheme, string code)
    {
        List<CodeCorrection> corrections = [];
        var chars = code.ToCharArray();

        for (var i = 0; i < chars.Length - 1; i++)
        {
            if (chars[i] == chars[i + 1])
            {
                continue;
            }

            (chars[i], chars[i + 1]) = (chars[i + 1], chars[i]);
            if (IsValid(scheme, chars))
            {
                corrections.Add(new(new string(chars), CorrectionKind.Transposition, i + 1, chars[i + 1], chars[i]));
            }

            (chars[i], chars[i + 1]) = (chars[i + 1], chars[i]);
        }

        for (var i = 0; i < chars.Length; i++)
        {
            var original = chars[i];
            var candidates = scheme.AllowsX && i == chars.Length - 1 ? "0123456789X" : "0123456789";
            foreach (var candidate in candidates)
            {
                if (candidate == original)
                {
                    continue;
                }

                chars[i] = candidate;
                if (IsValid(scheme, chars))
                {
                    corrections.Add(new(new string(chars), CorrectionKind.Substitution, i + 1, original, candidate));
                }
            }

            chars[i] = original;
        }

        return corrections;
    }

    private static bool IsValid(CheckDigitScheme scheme, char[] code)
    {
        Span<int> body = stackalloc int[code.Length - 1];
        for (var i = 0; i < body.Length; i++)
        {
            if (code[i] is < '0' or > '9')
            {
                return false;
            }

            body[i] = code[i] - '0';
        }

        return CheckDigitScheme.ToSymbol(scheme.CheckValue(body)) == code[^1];
    }

    private static (IReadOnlyList<CodeConversion> Conversions, ConversionNote Note) Convert(CodeFormat format, string code)
        => format switch
        {
            CodeFormat.Isbn10 => ([new(CodeFormat.Isbn13, CheckDigitScheme.Isbn13.Complete("978" + code[..9]))], ConversionNote.None),
            CodeFormat.Isbn13 when code.StartsWith("978", StringComparison.Ordinal) => ([ToIsbn10(code)], ConversionNote.None),
            CodeFormat.Isbn13 when code.StartsWith("979", StringComparison.Ordinal) => ([], ConversionNote.NoIsbn10For979),
            CodeFormat.Isbn13 => ([], ConversionNote.NotBookland),
            CodeFormat.Issn => ([new(CodeFormat.Ean13, CheckDigitScheme.Ean13.Complete("977" + code[..7] + "00"))], ConversionNote.None),
            CodeFormat.UpcA => ([new(CodeFormat.Ean13, "0" + code)], ConversionNote.None),
            CodeFormat.Ean13 when code.StartsWith("978", StringComparison.Ordinal) => ([ToIsbn10(code)], ConversionNote.None),
            CodeFormat.Ean13 when code.StartsWith("977", StringComparison.Ordinal)
                => ([new(CodeFormat.Issn, CheckDigitScheme.Issn.Complete(code[3..10]))], ConversionNote.None),
            CodeFormat.Ean13 when code[0] == '0' => ([new(CodeFormat.UpcA, code[1..])], ConversionNote.None),
            _ => ([], ConversionNote.None),
        };

    private static CodeConversion ToIsbn10(string ean) => new(CodeFormat.Isbn10, CheckDigitScheme.Isbn10.Complete(ean[3..12]));

    /// <summary>The ISBN-13 a book catalogue can look up, if the code is a book number at all.</summary>
    private static string? Isbn13For(CodeFormat format, string code, IReadOnlyList<CodeConversion> conversions)
        => format switch
        {
            CodeFormat.Isbn10 => conversions[0].Code,
            CodeFormat.Isbn13 or CodeFormat.Ean13 when IsBookland(code) => code,
            _ => null,
        };

    private static bool IsBookland(string code)
        => code.StartsWith("978", StringComparison.Ordinal) || code.StartsWith("979", StringComparison.Ordinal);

    private static int[] ToDigits(string body) => [.. body.Select(c => c == Wildcard ? 0 : c - '0')];

    // "ISBN-13:", "ISSN", "UPC-A" … — the number suffix only when no digit follows, so "ISBN 1302…" keeps its digits.
    [GeneratedRegex(@"^\s*(?:ISBN|ISSN|ISMN|EAN|UPC)(?:[-\s]?(?:10|13)(?!\d)|-?A(?![0-9A-Z]))?\s*:?", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex LabelPrefix();
}
