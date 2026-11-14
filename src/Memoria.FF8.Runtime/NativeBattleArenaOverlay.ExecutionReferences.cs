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
        foreach ((int start, BattleExecutionReferenceEntry entry) in EnumerateExecutionReferences())
        {
            if (start == offset && entry.Width == width)
            {
                reference = entry.Reference as T;
                return reference is not null;
            }
        }
        reference = null;
        return false;
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

    private bool MayHaveExecutionReferences()
    {
        return executionChanges.Count != 0 ||
            (privateParent is not null ? privateParent.MayHaveExecutionReferences() : Source.Working.ExecutionReferenceCount != 0);
    }

    private void InvalidateExecutionReferences(int offset, int length)
    {
        if (length == 0 || !MayHaveExecutionReferences())
        {
            return;
        }
        int[] offsets = [.. EnumerateExecutionReferences().Where(pair => pair.Key < (long)offset + length && pair.Key + (long)pair.Value.Width > offset).Select(pair => pair.Key)];
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
