using System.Collections;

namespace Albeoris.Games.Core.NsCollections;

public static partial class ExtensionsForLinq
{
    public static IReadOnlyCollection<TResult> SelectCollection<TSource, TResult>(this IReadOnlyCollection<TSource> source, Func<TSource, TResult> selector)
    {
        return new Collection<TSource, TResult>(source, selector);
    }

    private sealed class Collection<TSource, TResult> : IReadOnlyCollection<TResult>
    {
        private readonly IReadOnlyCollection<TSource> _source;
        private readonly Func<TSource, TResult> _selector;

        public Collection(IReadOnlyCollection<TSource> source, Func<TSource, TResult> selector)
        {
            ArgumentNullException.ThrowIfNull(source);
            ArgumentNullException.ThrowIfNull(selector);

            _source = source;
            _selector = selector;
        }

        public Int32 Count => _source.Count;
        public IEnumerator<TResult> GetEnumerator() => _source.Select(_selector).GetEnumerator();
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }
}