using System.Collections.Immutable;
using System.Runtime.InteropServices;

namespace Memoria.FF8.Runtime;

/// <summary>A value span retained on an owned immutable packet page.</summary>
internal readonly struct NativeBattleImmutableSlice<T> where T : unmanaged
{
    private readonly ImmutableArray<T> page;
    private readonly int start;

    private NativeBattleImmutableSlice(ImmutableArray<T> page, int start, int length)
    {
        this.page = page;
        this.start = start;
        Length = length;
    }

    internal int Length { get; }
    internal bool IsDefault => page.IsDefault;
    internal bool IsEmpty => Length == 0;
    internal bool IsDefaultOrEmpty => IsEmpty;
    internal T this[int index] => AsSpan()[index];

    internal ReadOnlySpan<T> AsSpan()
    {
        return page.AsSpan(start, Length);
    }

    internal NativeBattleImmutableSlice<T> Slice(int offset, int length)
    {
        _ = AsSpan().Slice(offset, length);
        return new(page, start + offset, length);
    }

    internal ImmutableArray<T> ToImmutableArray()
    {
        return page.IsDefault || start == 0 && Length == page.Length ? page : ImmutableArray.Create(AsSpan());
    }

    public static implicit operator NativeBattleImmutableSlice<T>(ImmutableArray<T> values)
    {
        return new(values, 0, values.IsDefault ? 0 : values.Length);
    }

    /// <summary>Appends disjoint slices. A batch never publishes its slices before freezing all its builders.</summary>
    internal sealed class Builder
    {
        private const int PageLength = 512;
        private T[]? active;
        private int length;
        private bool frozen;

        internal NativeBattleImmutableSlice<T> Append(ReadOnlySpan<T> values)
        {
            if (frozen)
            {
                throw new InvalidOperationException("Published packet pages cannot be changed.");
            }
            if (values.IsEmpty)
            {
                return ImmutableArray<T>.Empty;
            }
            if (active is null || active.Length - length < values.Length)
            {
                active = new T[Math.Max(PageLength, values.Length)];
                length = 0;
            }
            values.CopyTo(active.AsSpan(length));
            NativeBattleImmutableSlice<T> result = new(ImmutableCollectionsMarshal.AsImmutableArray(active), length, values.Length);
            length += values.Length;
            return result;
        }

        internal void Freeze()
        {
            frozen = true;
            active = null;
        }
    }
}
