using System.Globalization;
using System.Text;
using GcToolkit.Core.Coordinates;

namespace GcToolkit.Core.Workbench;

/// <summary>The outcome of solving one coordinate template.</summary>
/// <param name="Template">The template that was solved.</param>
/// <param name="Text">The template with all variables replaced, or <see langword="null"/> while something is missing or wrong.</param>
/// <param name="Coordinate">The parsed final coordinate, or <see langword="null"/> when it cannot be produced yet.</param>
/// <param name="Variables">Every variable the template uses (upper case, in order of first use).</param>
/// <param name="MissingVariables">The used variables that have no value yet (for dimming in the UI).</param>
/// <param name="Error">The first real problem (syntax, division by zero, not a coordinate, ...). Missing variables alone are not an error.</param>
/// <param name="DistanceMeters">Distance from the posted coordinates, when both are known.</param>
public sealed record TemplateSolution(
    string Template,
    string? Text,
    GeoCoordinate? Coordinate,
    IReadOnlyList<string> Variables,
    IReadOnlyList<string> MissingVariables,
    FormulaError? Error,
    double? DistanceMeters)
{
    /// <summary>The usual limit for how far a mystery's final may be from its posted coordinates (2 miles).</summary>
    public const double TwoMilesMeters = 3218.688;

    /// <summary><see langword="true"/> when a final coordinate was produced.</summary>
    public bool IsComplete => Coordinate is not null;

    /// <summary><see langword="true"/> when the distance is known and within <see cref="TwoMilesMeters"/>.</summary>
    public bool IsWithinTwoMiles => DistanceMeters is <= TwoMilesMeters;
}

/// <summary>
/// Fills in coordinate templates such as <c>N 50° 0A.BCD E 014° 2E.FGH</c> or
/// <c>N 49° 1(A+B).C(D*2)E E 016° 3F.(G-1)HI</c>.
/// <para>
/// A bare letter becomes the digits of that variable (text as typed, so <c>07</c> stays <c>07</c>).
/// A bracketed part is a <see cref="FormulaEvaluator"/> expression whose whole-number result is written as digits.
/// The hemisphere letters N/S/E/W are left alone: a letter only counts as a hemisphere when it stands at the
/// start or after whitespace, not next to a letter, and is the first of its kind (one N/S, one E/W).
/// </para>
/// </summary>
public static class CoordinateTemplateSolver
{
    /// <summary>Solves <paramref name="template"/> with the given variable values.</summary>
    /// <param name="posted">The cache's posted coordinates, for the distance check.</param>
    public static TemplateSolution Solve(
        string template,
        IReadOnlyDictionary<string, string>? variables,
        GeoCoordinate? posted = null)
    {
        template ??= string.Empty;
        if (string.IsNullOrWhiteSpace(template))
        {
            return Failure(template, [], [], new FormulaError(FormulaErrorKind.InvalidCoordinate, "The template is empty.", 0));
        }

        var values = FormulaEvaluator.Normalize(variables);
        List<string> used = [];
        List<string> missing = [];
        FormulaError? error = null;
        StringBuilder text = new();

        void Use(string name)
        {
            if (!used.Contains(name))
            {
                used.Add(name);
                if (!values.ContainsKey(name))
                {
                    missing.Add(name);
                }
            }
        }

        bool latitudeLetterSeen = false, longitudeLetterSeen = false;
        for (var i = 0; i < template.Length; i++)
        {
            var c = template[i];
            if (c == '(')
            {
                var close = FindClosingBracket(template, i);
                if (close < 0)
                {
                    error ??= new FormulaError(FormulaErrorKind.Syntax, "Missing ')'.", i);
                    break;
                }

                var expression = template[(i + 1)..close];
                foreach (var name in FormulaEvaluator.GetVariables(expression))
                {
                    Use(name);
                }

                var result = FormulaEvaluator.Evaluate(expression, values);
                if (result.Error is { } e)
                {
                    if (e.Kind != FormulaErrorKind.UnknownVariable)
                    {
                        error ??= e with { Position = e.Position + i + 1 };
                    }
                }
                else if (result.Value is { } value)
                {
                    if (value < 0 || value != decimal.Truncate(value))
                    {
                        error ??= new FormulaError(
                            FormulaErrorKind.NotWholeNumber,
                            $"({expression}) gives {value.ToString(CultureInfo.InvariantCulture)}, which cannot be used as digits.",
                            i);
                    }
                    else
                    {
                        text.Append(decimal.Truncate(value).ToString("0", CultureInfo.InvariantCulture));
                    }
                }

                i = close;
            }
            else if (c == ')')
            {
                error ??= new FormulaError(FormulaErrorKind.Syntax, "Unexpected ')'.", i);
            }
            else if (char.IsLetter(c))
            {
                if (IsHemisphere(template, i, ref latitudeLetterSeen, ref longitudeLetterSeen))
                {
                    text.Append(c);
                    continue;
                }

                var name = char.ToUpperInvariant(c).ToString();
                Use(name);
                if (values.TryGetValue(name, out var digits))
                {
                    text.Append(digits);
                }
            }
            else
            {
                text.Append(c);
            }
        }

        if (error is not null || missing.Count > 0)
        {
            return Failure(template, used, missing, error);
        }

        var final = text.ToString();
        if (!CoordinateParser.TryParse(final, out var coordinate, out _) || !coordinate.IsValid)
        {
            return new TemplateSolution(
                template, final, null, used, missing,
                new FormulaError(FormulaErrorKind.InvalidCoordinate, $"\"{final}\" is not a valid coordinate.", 0),
                null);
        }

        double? distance = posted is { } p ? Geodesy.DistanceMeters(p, coordinate) : null;
        return new TemplateSolution(template, final, coordinate, used, missing, null, distance);
    }

    /// <summary>Solves every stage of <paramref name="sheet"/> with the sheet's variables.</summary>
    public static IReadOnlyList<TemplateSolution> SolveAll(VariableSheet sheet, GeoCoordinate? posted = null)
        => sheet.Stages.Select(s => Solve(s.Template, sheet.Variables, posted)).ToList();

    private static TemplateSolution Failure(
        string template, List<string> used, List<string> missing, FormulaError? error)
        => new(template, null, null, used, missing, error, null);

    private static int FindClosingBracket(string text, int open)
    {
        var depth = 0;
        for (var i = open; i < text.Length; i++)
        {
            if (text[i] == '(')
            {
                depth++;
            }
            else if (text[i] == ')' && --depth == 0)
            {
                return i;
            }
        }

        return -1;
    }

    private static bool IsHemisphere(string text, int index, ref bool latitudeSeen, ref bool longitudeSeen)
    {
        var letter = char.ToUpperInvariant(text[index]);
        if (letter is not ('N' or 'S' or 'E' or 'W'))
        {
            return false;
        }

        var standsAlone = (index == 0 || char.IsWhiteSpace(text[index - 1])) &&
                          (index + 1 >= text.Length || !char.IsLetter(text[index + 1]));
        if (!standsAlone)
        {
            return false;
        }

        if (letter is 'N' or 'S')
        {
            if (latitudeSeen)
            {
                return false;
            }

            latitudeSeen = true;
            return true;
        }

        if (longitudeSeen)
        {
            return false;
        }

        longitudeSeen = true;
        return true;
    }
}
