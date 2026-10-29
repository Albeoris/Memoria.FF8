using System.Collections.Immutable;
using System.Runtime.CompilerServices;

namespace Memoria.FF8.Runtime;

/// <summary>Transient presentation lifecycle evidence. It is neither gameplay state nor part of save equality.</summary>
internal static class NativeWorldUiOccurrences
{
    private sealed class Occurrences(ImmutableDictionary<int, long> windows)
    {
        public ImmutableDictionary<int, long> Windows { get; } = windows;
    }

    private static readonly ConditionalWeakTable<NativeWorldEventUi, Occurrences> owners = new();
    private static long sequence;

    internal static void Copy(NativeWorldEventUi source, NativeWorldEventUi target)
    {
        if (owners.TryGetValue(source, out Occurrences? evidence))
        {
            owners.Add(target, evidence);
        }
    }

    internal static NativeWorldEventUi Open(NativeWorldEventUi target, int windowId)
    {
        ImmutableDictionary<int, long> previous = owners.TryGetValue(target, out Occurrences? evidence) ? evidence.Windows : ImmutableDictionary<int, long>.Empty;
        owners.Remove(target);
        owners.Add(target, new(previous.SetItem(windowId, Interlocked.Increment(ref sequence))));
        return target;
    }

    internal static long Read(NativeWorldEventUi owner, int windowId)
    {
        return owners.TryGetValue(owner, out Occurrences? evidence) ? evidence.Windows.GetValueOrDefault(windowId) : 0;
    }
}
