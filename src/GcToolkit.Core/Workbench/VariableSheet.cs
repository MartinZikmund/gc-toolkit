namespace GcToolkit.Core.Workbench;

/// <summary>One coordinate template of a cache, for example one stage of a multi-stage cache.</summary>
/// <param name="Name">What the user calls it ("Stage 2", "Final").</param>
/// <param name="Template">Raw template text, e.g. <c>N 50° 0A.BCD E 014° 2E.FGH</c>.</param>
public sealed record FormulaStage(string Name, string Template);

/// <summary>
/// The variables and coordinate templates of one cache. Immutable: the <c>With*</c> methods return a changed copy.
/// Variable names are stored upper case; values are kept as typed (leading zeros matter when a bare letter is
/// substituted into a template).
/// </summary>
public sealed record VariableSheet(IReadOnlyDictionary<string, string> Variables, IReadOnlyList<FormulaStage> Stages)
{
    public static VariableSheet Empty { get; } = new(
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase),
        []);

    /// <summary>Sets a variable. A blank value removes it.</summary>
    public VariableSheet WithVariable(string name, string value)
    {
        Dictionary<string, string> variables = new(Variables, StringComparer.OrdinalIgnoreCase);
        var key = name.Trim().ToUpperInvariant();
        if (string.IsNullOrWhiteSpace(value))
        {
            variables.Remove(key);
        }
        else
        {
            variables[key] = value.Trim();
        }

        return this with { Variables = variables };
    }

    public VariableSheet WithStage(FormulaStage stage) => this with { Stages = [.. Stages, stage] };
}
