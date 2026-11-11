using System.Collections;

namespace GcToolkit.Core.Geocaches;

/// <summary>
/// An immutable list that compares by content, so records holding lists keep value equality.
/// </summary>
public sealed class EquatableList<T> : IReadOnlyList<T>, IEquatable<EquatableList<T>>
{
    private readonly T[] _items;

    private EquatableList(T[] items) => _items = items;

    public static EquatableList<T> Empty { get; } = new([]);

    public static EquatableList<T> From(IEnumerable<T>? items) => items switch
    {
        null => Empty,
        EquatableList<T> list => list,
        _ => items.ToArray() is { Length: > 0 } array ? new(array) : Empty,
    };

    public T this[int index] => _items[index];

    public int Count => _items.Length;

    public bool Equals(EquatableList<T>? other) =>
        other is not null && _items.AsSpan().SequenceEqual(other._items);

    public override bool Equals(object? obj) => Equals(obj as EquatableList<T>);

    public override int GetHashCode()
    {
        HashCode hash = new();
        foreach (var item in _items)
        {
            hash.Add(item);
        }

        return hash.ToHashCode();
    }

    public IEnumerator<T> GetEnumerator() => ((IEnumerable<T>)_items).GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}
