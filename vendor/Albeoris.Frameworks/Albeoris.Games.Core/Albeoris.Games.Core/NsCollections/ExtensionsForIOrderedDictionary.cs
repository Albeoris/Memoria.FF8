using Albeoris.Games.Core.Abstractions.NsCollections;

namespace Albeoris.Games.Core.NsCollections;

public static class ExtensionsForIOrderedDictionary
{
#if NET10_0_OR_GREATER
    public static Boolean TryRemove<TKey, TValue>(this System.Collections.Generic.OrderedDictionary<TKey, TValue> dictionary, TKey key, [System.Diagnostics.CodeAnalysis.MaybeNullWhen(false)] out TValue value) where TKey : notnull
    {
        return dictionary.Remove(key, out value);
    }
#endif

    public static void Add<TKey, TValue>(this IOrderedDictionary<TKey, TValue> dictionary, TKey key, TValue value) where TKey : notnull
    {
        if (!dictionary.TryAdd(key, value))
            throw new ArgumentException($"The key [{key}] is already in the dictionary.", nameof(key));
    }

    public static void Remove<TKey, TValue>(this IOrderedDictionary<TKey, TValue> dictionary, TKey key, out TValue removedValue) where TKey : notnull
    {
        if (!dictionary.TryRemove(key, out removedValue!))
            throw new ArgumentException($"The key [{key}] is not in the dictionary.", nameof(key));
    }
}
