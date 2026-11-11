using System.Globalization;

namespace GcToolkit.Core.ViewModels;

/// <summary>
/// A tool's inputs as plain invariant strings, so any tool can be persisted through one
/// source-generated JSON contract (a string dictionary) without its own serializable type.
/// </summary>
public sealed class ToolInputState : IEquatable<ToolInputState>
{
    private readonly Dictionary<string, string> _values;

    public ToolInputState()
        => _values = new(StringComparer.Ordinal);

    public ToolInputState(IReadOnlyDictionary<string, string> values)
        => _values = new(values, StringComparer.Ordinal);

    public IReadOnlyDictionary<string, string> Values => _values;

    public void Set(string key, string value) => _values[key] = value;

    public void Set(string key, int value) => _values[key] = value.ToString(CultureInfo.InvariantCulture);

    public void Set(string key, double value) => _values[key] = value.ToString("R", CultureInfo.InvariantCulture);

    public void Set(string key, bool value) => _values[key] = value ? "true" : "false";

    public void Set<TEnum>(string key, TEnum value)
        where TEnum : struct, Enum
        => _values[key] = value.ToString();

    public string GetString(string key, string defaultValue = "")
        => _values.TryGetValue(key, out var value) ? value : defaultValue;

    public int GetInt(string key, int defaultValue = 0)
        => _values.TryGetValue(key, out var value) && int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed)
            ? parsed
            : defaultValue;

    public double GetDouble(string key, double defaultValue = 0)
        => _values.TryGetValue(key, out var value) && double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed)
            ? parsed
            : defaultValue;

    public bool GetBool(string key, bool defaultValue = false)
        => _values.TryGetValue(key, out var value) && bool.TryParse(value, out var parsed) ? parsed : defaultValue;

    public TEnum GetEnum<TEnum>(string key, TEnum defaultValue)
        where TEnum : struct, Enum
        => _values.TryGetValue(key, out var value) && Enum.TryParse<TEnum>(value, out var parsed) && Enum.IsDefined(parsed)
            ? parsed
            : defaultValue;

    public bool Contains(string key) => _values.ContainsKey(key);

    public bool Equals(ToolInputState? other)
        => other is not null
            && other._values.Count == _values.Count
            && _values.All(pair => other._values.TryGetValue(pair.Key, out var value) && value == pair.Value);

    public override bool Equals(object? obj) => Equals(obj as ToolInputState);

    public override int GetHashCode() => _values.Count;
}
