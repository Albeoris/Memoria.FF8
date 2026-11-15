using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;

namespace Memoria.FF8.Runtime;

internal sealed partial class NativeBattleWorkingMemory
{
    private Dictionary<int, BattleExecutionReferenceEntry>? executionReferences;
    private List<int>? removedExecutionReferences;
    private int RemovedExecutionCount => removedExecutionReferences?.Count ?? 0;
    internal int ExecutionReferenceCount => Observe(executionReferences?.Count ?? 0);

    internal void WriteExecutionReference(int offset, int width, BattleExecutionReference reference, ImmutableArray<byte>? nativeBytes = null)
    {
        BattleExecutionReferenceEntry entry = new(width, reference);
        RequireRange(offset, width);
        ExecutionReferenceEncoding.Validate(width, nativeBytes);

        ReadOnlySpan<byte> bytes = nativeBytes is { } exact ? exact.AsSpan() : default;
        PrepareStorageReceipts();
        PrepareMutation(offset, width, bytes, nativeBytes is null, 1);
        var storage = executionReferences ?? new();
        storage.EnsureCapacity(checked(storage.Count - RemovedExecutionCount + 1));
        SaveBytes(offset, width, bytes, nativeBytes is null);
        InvalidatePointers();
        if (nativeBytes is not null)
        {
            bytes.CopyTo(data.AsSpan(offset));
            bytesWritten += width;
            NativeBattleTelemetry.Record(1005, NativeBattleTrafficKind.CpuCopyPayload, width);
        }
        Array.Fill(known, nativeBytes is not null, offset, width);
        if (checkpoint)
        {
            pointerUndo!.Add(new(offset, false, null, IsExecution: true));
        }
        storage[offset] = entry;
        executionReferences = storage;
        storedLength = Math.Max(storedLength, offset + width);
        RecordStorageReceipts(offset, width, nativeBytes is not null);
        RecordKnownness(offset, width, nativeBytes is not null);
        version++;
        NativeBattleTelemetry.Record(1005, NativeBattleTrafficKind.Fill, width);
    }

    internal BattleExecutionReferenceEntry? FindExecutionReference(int offset)
    {
        return executionReferences?.GetValueOrDefault(offset);
    }

    internal bool TryReadExecutionReference<T>(int offset, int width, [NotNullWhen(true)] out T? reference) where T : BattleExecutionReference
    {
        ExecutionReferenceEncoding.RequireWidth(width);
        RequireRange(offset, width);
        reference = FindExecutionReference(offset) is { } entry && entry.Width == width ? entry.Reference as T : null;
        return reference is not null;
    }

    internal T ReadExecutionReference<T>(int offset, int width) where T : BattleExecutionReference
    {
        return TryReadExecutionReference<T>(offset, width, out T? reference) ? reference : throw new InvalidDataException("No live execution reference of the requested kind and width exists.");
    }

    internal IEnumerable<KeyValuePair<int, BattleExecutionReferenceEntry>> EnumerateExecutionReferences()
    {
        RequireActive();
        return executionReferences is { } entries ? entries : Enumerable.Empty<KeyValuePair<int, BattleExecutionReferenceEntry>>();
    }

    private void CollectExecutionReferences(int offset, int length)
    {
        removedExecutionReferences?.Clear();
        if (executionReferences is null)
        {
            return;
        }
        foreach ((int start, BattleExecutionReferenceEntry entry) in executionReferences)
        {
            if (start < (long)offset + length && start + (long)entry.Width > offset)
            {
                (removedExecutionReferences ??= []).Add(start);
            }
        }
    }

    private void InvalidateExecutionReferences()
    {
        if (removedExecutionReferences is null)
        {
            return;
        }
        foreach (int start in removedExecutionReferences)
        {
            if (checkpoint)
            {
                pointerUndo!.Add(new(start, true, null, IsExecution: true, Execution: executionReferences![start]));
            }
            executionReferences!.Remove(start);
        }
    }

    internal void RemoveExecutionReference(int offset)
    {
        RequireRange(offset, 0);
        if (executionReferences is null || !executionReferences.TryGetValue(offset, out BattleExecutionReferenceEntry? entry))
        {
            return;
        }
        ReserveJournal(0, 1);
        if (checkpoint)
        {
            pointerUndo!.Add(new(offset, true, null, IsExecution: true, Execution: entry));
        }
        executionReferences.Remove(offset);
        version++;
    }

    private void RestoreExecutionUndo(PointerUndo undo)
    {
        if (undo.Present)
        {
            executionReferences![undo.Offset] = undo.Execution!;
        }
        else
        {
            executionReferences!.Remove(undo.Offset);
        }
    }

    private void RestoreExecutionReferences(ImmutableDictionary<int, BattleExecutionReferenceEntry> references)
    {
        foreach ((int offset, BattleExecutionReferenceEntry entry) in references.OrderBy(item => item.Key))
        {
            RequireRange(offset, entry.Width);
            if (offset > storedLength - entry.Width || pointers?.Keys.Any(pointer => pointer < offset + (long)entry.Width && pointer + 4L > offset) == true)
            {
                throw new InvalidDataException("Retained execution reference contradicts its backing extent.");
            }
            bool firstKnown = IsKnown(offset);
            for (int lane = 1; lane < entry.Width; lane++)
            {
                if (IsKnown(offset + lane) != firstKnown)
                {
                    throw new InvalidDataException("Retained execution reference has inconsistent backing-byte knownness.");
                }
            }
            CollectExecutionReferences(offset, entry.Width);
            if (RemovedExecutionCount != 0)
            {
                throw new InvalidDataException("Retained execution references overlap.");
            }
            (executionReferences ??= []).Add(offset, entry);
        }
    }
}
