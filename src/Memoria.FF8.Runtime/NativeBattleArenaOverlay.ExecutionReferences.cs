using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;

namespace Memoria.FF8.Runtime;

internal sealed partial class NativeBattleArenaOverlay
{
    private readonly Dictionary<int, BattleExecutionReferenceEntry?> executionChanges = [];
    private readonly List<(int Offset, bool Present, BattleExecutionReferenceEntry? Entry)> executionUndo = [];

    internal IEnumerable<KeyValuePair<int, BattleExecutionReferenceEntry>> EnumerateExecutionReferences()
    {
        NativeBattleEffectArena owner = Source;
        var inherited = privateParent is null ? owner.EnumerateCommittedExecutionReferences() : privateParent.EnumerateExecutionReferences();
        int origin = privateParent is null ? 0 : privateParentOffset;
        foreach ((int offset, BattleExecutionReferenceEntry entry) in inherited)
        {
            int local = offset - origin;
            if (local >= 0 && local <= owner.Bound - entry.Width && !executionChanges.ContainsKey(local) && !OverlapsPrivateBytes(local, entry.Width))
            {
                yield return new(local, entry);
            }
        }
        foreach ((int offset, BattleExecutionReferenceEntry? entry) in executionChanges)
        {
            if (entry is not null)
            {
                yield return new(offset, entry);
            }
        }
    }

    internal bool TryReadExecutionReference<T>(int offset, int width, [NotNullWhen(true)] out T? reference) where T : BattleExecutionReference
    {
        ExecutionReferenceEncoding.RequireWidth(width);
        Source.RequireRange(offset, width);
        reference = FindExecutionReference(offset) is { } entry && entry.Width == width ? entry.Reference as T : null;
        return reference is not null;
    }

    private BattleExecutionReferenceEntry? FindExecutionReference(int offset)
    {
        if (executionChanges.TryGetValue(offset, out BattleExecutionReferenceEntry? changed))
        {
            return changed;
        }

        NativeBattleEffectArena owner = Source;
        BattleExecutionReferenceEntry? inherited = privateParent is null
            ? owner.Working.FindExecutionReference(checked(owner.WorkingOffset + offset))
            : privateParent.FindExecutionReference(checked(privateParentOffset + offset));
        return inherited is not null && offset <= owner.Bound - inherited.Width && !OverlapsPrivateBytes(offset, inherited.Width) ? inherited : null;
    }

    internal void WriteExecutionReference(int offset, int width, BattleExecutionReference reference, ImmutableArray<byte>? nativeBytes)
    {
        BattleExecutionReferenceEntry entry = new(width, reference);
        ExecutionReferenceEncoding.Validate(width, nativeBytes);
        Source.RequireRange(offset, width);
        executionChanges.EnsureCapacity(checked(executionChanges.Count + 1));
        if (checkpoint)
        {
            executionUndo.EnsureCapacity(checked(executionUndo.Count + 1));
        }
        if (nativeBytes is { } bytes)
        {
            Write(offset, bytes.AsSpan());
        }
        else
        {
            MarkUnknown(offset, width);
        }
        RecordExecutionChange(offset, entry);
    }

    private long EstimateExecutionReferenceCount()
    {
        // Overrides, removals and references outside a private slice may overestimate lookup work.
        return executionChanges.Count + (privateParent is not null ? privateParent.EstimateExecutionReferenceCount() : (long)Source.Working.ExecutionReferenceCount);
    }

    private void InvalidateExecutionReferences(int offset, int length)
    {
        if (length == 0)
        {
            return;
        }
        long referenceCount = EstimateExecutionReferenceCount();
        if (referenceCount == 0)
        {
            return;
        }
        // References can start in the store or up to three bytes before it. Probe these offsets
        // when that costs less than walking the reference table, retaining the short scalar path.
        if (length <= 4 || (long)length + 3 <= referenceCount)
        {
            int end = checked(offset + length);
            for (int start = Math.Max(0, offset - 3); start < end; start++)
            {
                if (FindExecutionReference(start) is { } entry && (long)start + entry.Width > offset)
                {
                    executionChanges.EnsureCapacity(checked(executionChanges.Count + 1));
                    if (checkpoint)
                    {
                        executionUndo.EnsureCapacity(checked(executionUndo.Count + 1));
                    }
                    RecordExecutionChange(start, null);
                }
            }
            return;
        }

        InvalidateExecutionReferencesByEnumeration(offset, length);
    }

    private void InvalidateExecutionReferencesByEnumeration(int offset, int length)
    {
        int[] offsets = [.. EnumerateExecutionReferences().Where(pair => pair.Key < (long)offset + length && pair.Key + (long)pair.Value.Width > offset).Select(pair => pair.Key)];
        Remove(offsets);

        void Remove(ReadOnlySpan<int> offsets)
        {
            if (offsets.IsEmpty)
            {
                return;
            }
            executionChanges.EnsureCapacity(checked(executionChanges.Count + offsets.Length + 1));
            if (checkpoint)
            {
                executionUndo.EnsureCapacity(checked(executionUndo.Count + offsets.Length + 1));
            }
            foreach (int start in offsets)
            {
                RecordExecutionChange(start, null);
            }
        }
    }

    private void RecordExecutionChange(int offset, BattleExecutionReferenceEntry? entry)
    {
        if (checkpoint)
        {
            bool present = executionChanges.TryGetValue(offset, out BattleExecutionReferenceEntry? previous);
            executionUndo.Add((offset, present, previous));
        }
        executionChanges[offset] = entry;
    }

    private void RollbackExecutionChanges()
    {
        for (int index = executionUndo.Count - 1; index >= 0; index--)
        {
            var (offset, present, entry) = executionUndo[index];
            if (present)
            {
                executionChanges[offset] = entry;
            }
            else
            {
                executionChanges.Remove(offset);
            }
        }
    }

    private void ApplyExecutionChanges(NativeBattleWorkingMemory memory, int baseOffset)
    {
        Span<byte> bytes = stackalloc byte[4];
        Span<bool> known = stackalloc bool[4];
        foreach ((int offset, BattleExecutionReferenceEntry? entry) in executionChanges)
        {
            int destination = checked(baseOffset + offset);
            if (entry is null)
            {
                memory.RemoveExecutionReference(destination);
                continue;
            }
            memory.ReadWithKnownness(destination, bytes[..entry.Width], known[..entry.Width]);
            memory.WriteExecutionReference(destination, entry.Width, entry.Reference, ExecutionReferenceEncoding.Capture(bytes[..entry.Width], known[..entry.Width]));
        }
    }
}
