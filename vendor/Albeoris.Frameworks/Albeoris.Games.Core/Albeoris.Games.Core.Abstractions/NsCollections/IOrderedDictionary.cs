using System.Diagnostics.CodeAnalysis;

namespace Albeoris.Games.Core.Abstractions.NsCollections;

public interface IOrderedDictionary<TKey, TValue> : IEnumerable<(TKey key, TValue value)> where TKey : notnull
{
    public Int32 Count { get; }
    TValue this[TKey key] { get; }
    IReadOnlyList<TKey> Keys { get; }
    IReadOnlyList<TValue> Values { get; }

    void EnsureCapacity(Int32 capacity);
    void AddOrUpdate(TKey key, TValue value);
    Boolean TryAdd(TKey key, TValue value);
    Boolean TryReplace(TKey key, TValue value, [MaybeNullWhen(false)] out TValue previousValue);
    Boolean TryRemove(TKey key, [MaybeNullWhen(false)] out TValue value);
    Boolean ContainsKey(TKey key);
    Boolean TryGetValue(TKey key, [MaybeNullWhen(false)] out TValue value);
    void Clear();
}