namespace Memoria.FF8.Runtime;

/// <summary>Owned fixed pages shared by the ordering traversal and published command deliveries.</summary>
internal readonly record struct NativeBattleCommandPages<T> where T : struct
{
    private const int PageSize = 256;
    private readonly T[][]? pages;

    private NativeBattleCommandPages(T[][] pages, int length)
    {
        this.pages = pages;
        Length = length;
    }

    internal static NativeBattleCommandPages<T> Empty { get; } = new([], 0);
    internal bool IsDefault => pages is null;
    internal int Length { get; }

    internal ref readonly T this[int index]
    {
        get
        {
            ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual((uint)index, (uint)Length, nameof(index));
            return ref pages![index / PageSize][index % PageSize];
        }
    }

    internal static NativeBattleCommandPages<T> Create(ReadOnlySpan<T> values)
    {
        if (values.IsEmpty)
        {
            return Empty;
        }
        T[][] pages = new T[(values.Length - 1) / PageSize + 1][];
        for (int page = 0; page < pages.Length; page++)
        {
            int offset = page * PageSize;
            T[] entries = new T[PageSize];
            values.Slice(offset, Math.Min(PageSize, values.Length - offset)).CopyTo(entries);
            pages[page] = entries;
        }
        return new(pages, values.Length);
    }

    public Enumerator GetEnumerator() => new(this);

    public struct Enumerator
    {
        private readonly NativeBattleCommandPages<T> commands;
        private int index;

        internal Enumerator(NativeBattleCommandPages<T> commands)
        {
            this.commands = commands;
            index = -1;
        }

        public readonly ref readonly T Current => ref commands[index];

        public bool MoveNext()
        {
            if (index < commands.Length)
            {
                index++;
            }
            return index < commands.Length;
        }

        internal void Reset() => index = -1;
    }

    internal sealed class Builder
    {
        private List<T[]>? pages;
        private int length;
        private bool frozen;

        internal void Add(in T command)
        {
            RequireOpen();
            int offset = length % PageSize;
            if (offset == 0)
            {
                (pages ??= []).Add(new T[PageSize]);
            }
            pages![^1][offset] = command;
            length++;
        }

        internal NativeBattleCommandPages<T> Freeze()
        {
            RequireOpen();
            frozen = true;
            NativeBattleCommandPages<T> result = pages is null ? Empty : new(pages.ToArray(), length);
            pages = null;
            return result;
        }

        private void RequireOpen()
        {
            if (frozen)
            {
                throw new InvalidOperationException("The native command page builder is already frozen.");
            }
        }
    }

    internal NativeBattleCommandPages<T> Map(Func<T, T> transform, Func<T, T, bool> sameOwners)
    {
        if (Length == 0)
        {
            return this;
        }
        T[][]? mappedPages = null;
        for (int page = 0; page < pages!.Length; page++)
        {
            T[] original = pages[page];
            T[]? mapped = null;
            int used = Math.Min(PageSize, Length - page * PageSize);
            for (int index = 0; index < used; index++)
            {
                ref readonly T item = ref original[index];
                T replacement = transform(item);
                if (!sameOwners(item, replacement))
                {
                    mapped ??= (T[])original.Clone();
                    mapped[index] = replacement;
                }
            }
            if (mapped is not null)
            {
                mappedPages ??= (T[][])pages.Clone();
                mappedPages[page] = mapped;
            }
        }
        return mappedPages is null ? this : new(mappedPages, Length);
    }
}
