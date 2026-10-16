using System.Collections;
using System.Diagnostics.CodeAnalysis;
using Albeoris.Games.Core.Abstractions.NsCollections;

namespace Albeoris.Games.Core.NsCollections;

public sealed class OrderedDictionary<TKey, TValue> : IOrderedDictionary<TKey, TValue> where TKey : notnull
{
    private readonly Dictionary<TKey, (Int32 index, TValue value)> _set;
    private readonly List<(TKey key, TValue value)> _list;

    public OrderedDictionary()
        : this(capacity: 0, EqualityComparer<TKey>.Default)
    {
    }

    public OrderedDictionary(IEqualityComparer<TKey> comparer)
        : this(capacity: 0, comparer)
    {
    }

    public OrderedDictionary(Int32 capacity, IEqualityComparer<TKey> comparer)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(capacity);
        ArgumentNullException.ThrowIfNull(comparer);

        _set = new Dictionary<TKey, (Int32 index, TValue value)>(capacity, comparer);
        _list = new List<(TKey key, TValue value)>(capacity);
    }

    public Int32 Count => _set.Count;
    public TValue this[TKey key] => _set.TryGetValue(key, out var value) ? value.value : throw new KeyNotFoundException(key.ToString());
    public IReadOnlyList<TKey> Keys => _list.SelectList(p => p.key);
    public IReadOnlyList<TValue> Values => _list.SelectList(p => p.value);

    public void EnsureCapacity(Int32 capacity)
    {
        _set.EnsureCapacity(capacity);
        _list.EnsureCapacity(capacity);
    }

    public void AddOrUpdate(TKey key, TValue value)
    {
        if (TryAdd(key, value)) return;
        if (TryReplace(key, value, out _)) return;
        throw new InvalidOperationException("Collection is out of sync.");
    }

    public Boolean TryAdd(TKey key, TValue value)
    {
        if (_set.ContainsKey(key))
            return false;

        _set.Add(key, (_list.Count, value));
        _list.Add((key, value));
        return true;
    }

    public Boolean TryReplace(TKey key, TValue value, [MaybeNullWhen(false)] out TValue previousValue)
    {
        if (!_set.TryGetValue(key, out var pair))
        {
            previousValue = default;
            return false;
        }

        previousValue = pair.value;
        _set[key] = (pair.index, value);
        _list[pair.index] = (key, value);
        return true;
    }

    public Boolean TryRemove(TKey key, [MaybeNullWhen(false)] out TValue value)
    {
        if (!_set.Remove(key, out var pair))
        {
            value = default;
            return false;
        }

        _list.RemoveAt(pair.index);
        value = pair.value;
        return true;
    }

    public Boolean ContainsKey(TKey key)
    {
        return _set.ContainsKey(key);
    }

    public Boolean TryGetValue(TKey key, [MaybeNullWhen(false)] out TValue value)
    {
        if (_set.TryGetValue(key, out var node))
        {
            value = node.value;
            return true;
        }

        value = default;
        return false;
    }

    public void Clear()
    {
        _set.Clear();
        _list.Clear();
    }

    public IEnumerator<(TKey key, TValue value)> GetEnumerator()
    {
        return _list.GetEnumerator();
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
        return GetEnumerator();
    }
}