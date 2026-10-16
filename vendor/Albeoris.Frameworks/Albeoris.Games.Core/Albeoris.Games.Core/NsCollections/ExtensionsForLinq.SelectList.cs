using System.Collections;

namespace Albeoris.Games.Core.NsCollections;

public static partial class ExtensionsForLinq
{
    public static IReadOnlyList<TResult> SelectList<TSource, TResult>(this IReadOnlyList<TSource> source, Func<TSource, TResult> selector)
    {
        return new List<TSource, TResult>(source, selector);
    }

    private sealed class List<TSource, TResult> : IReadOnlyList<TResult>
    {
        private readonly IReadOnlyList<TSource> _source;
        private readonly Func<TSource, TResult> _selector;

        public List(IReadOnlyList<TSource> source, Func<TSource, TResult> selector)
        {
            ArgumentNullException.ThrowIfNull(source);
            ArgumentNullException.ThrowIfNull(selector);

            _source = source;
            _selector = selector;
        }

        public Int32 Count => _source.Count;
        public TResult this[Int32 index] => _selector(_source[index]);
        public IEnumerator<TResult> GetEnumerator() => _source.Select(_selector).GetEnumerator();
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }
}