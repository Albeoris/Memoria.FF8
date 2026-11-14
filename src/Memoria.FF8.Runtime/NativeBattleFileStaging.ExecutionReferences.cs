using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;

namespace Memoria.FF8.Runtime;

public sealed partial record NativeBattleFileStaging
{
    private ImmutableDictionary<uint, BattleExecutionReferenceEntry> retainedExecutionReferences = ImmutableDictionary<uint, BattleExecutionReferenceEntry>.Empty;

    internal NativeBattleFileStaging WriteExecutionReference(uint address, int width, BattleExecutionReference reference, ImmutableArray<byte>? nativeBytes = null)
    {
        BattleExecutionReferenceEntry entry = new(width, reference);
        ExecutionReferenceEncoding.Validate(width, nativeBytes);
        RequireWritableRange(address, width);
        if (Transaction is { } transaction)
        {
            transaction.WriteExecutionReference(address, entry, nativeBytes);
        }
        else
        {
            RequireOwner().WriteExecutionReference(address, entry, nativeBytes);
        }
        return this;
    }

    internal T ReadExecutionReference<T>(uint address, int width) where T : BattleExecutionReference
    {
        return TryReadExecutionReference<T>(address, width, out T? reference) ? reference : throw new InvalidDataException("No live staging reference of the requested kind and width exists.");
    }

    internal bool TryReadExecutionReference<T>(uint address, int width, [NotNullWhen(true)] out T? reference) where T : BattleExecutionReference
    {
        ExecutionReferenceEncoding.RequireWidth(width);
        RequireOffsetRange(address, width);
        BattleExecutionReferenceEntry? entry = Transaction is { } transaction
            ? transaction.ReadExecutionReference(address)
            : snapshot ? retainedExecutionReferences.GetValueOrDefault(address) : RequireOwner().ReadExecutionReference(address);
        reference = entry?.Width == width ? entry.Reference as T : null;
        return reference is not null;
    }

    internal static bool ExecutionReferenceOverlaps(uint start, int width, uint address, int length)
    {
        return start < (ulong)address + (uint)length && (ulong)start + (uint)width > address;
    }
}

internal sealed partial class NativeBattleFileStagingWorkingOwner
{
    private readonly Dictionary<uint, BattleExecutionReferenceEntry> executionReferences = [];
    private readonly List<(uint Address, BattleExecutionReferenceEntry? Previous)> executionUndo = [];
    private readonly List<uint> removedExecutionReferences = [];

    internal int ExecutionReferenceCount => executionReferences.Count;

    internal BattleExecutionReferenceEntry? ReadExecutionReference(uint address) => executionReferences.GetValueOrDefault(address);

    internal IEnumerable<KeyValuePair<uint, BattleExecutionReferenceEntry>> EnumerateExecutionReferences() => executionReferences;

    internal void WriteExecutionReference(uint address, BattleExecutionReferenceEntry entry, ImmutableArray<byte>? nativeBytes)
    {
        int depth = BeginPublicationCheckpoint();
        try
        {
            if (address < BaseAddress)
            {
                Rebase(address);
            }
            if (nativeBytes is { } bytes)
            {
                Write(address, bytes.AsSpan());
            }
            else
            {
                MarkUnknown(address, entry.Width);
            }
            SetExecutionReference(address, entry);
            CommitPublicationCheckpoint(depth);
        }
        catch
        {
            if (CheckpointDepth == depth)
            {
                RollbackPublicationCheckpoint(depth);
            }
            throw;
        }
    }

    internal void SetExecutionReference(uint address, BattleExecutionReferenceEntry? entry)
    {
        BattleExecutionReferenceEntry? previous = ReadExecutionReference(address);
        if (entry == previous)
        {
            return;
        }
        if (checkpoint)
        {
            executionUndo.Add((address, previous));
        }
        if (entry is null)
        {
            executionReferences.Remove(address);
        }
        else
        {
            executionReferences[address] = entry;
        }
        version++;
    }

    private void InvalidateExecutionReferences(uint address, int length)
    {
        if (executionReferences.Count == 0)
        {
            return;
        }
        removedExecutionReferences.Clear();
        foreach ((uint start, BattleExecutionReferenceEntry entry) in executionReferences)
        {
            if (NativeBattleFileStaging.ExecutionReferenceOverlaps(start, entry.Width, address, length))
            {
                removedExecutionReferences.Add(start);
            }
        }
        if (checkpoint)
        {
            executionUndo.EnsureCapacity(checked(executionUndo.Count + removedExecutionReferences.Count));
        }
        foreach (uint start in removedExecutionReferences)
        {
            SetExecutionReference(start, null);
        }
    }

    private void RollbackExecutionReferences(int first)
    {
        for (int index = executionUndo.Count - 1; index >= first; index--)
        {
            var (address, previous) = executionUndo[index];
            if (previous is null)
            {
                executionReferences.Remove(address);
            }
            else
            {
                executionReferences[address] = previous;
            }
        }
        executionUndo.RemoveRange(first, executionUndo.Count - first);
    }

    private void RestoreExecutionReferences(ImmutableDictionary<uint, BattleExecutionReferenceEntry> references)
    {
        Span<byte> bytes = stackalloc byte[4];
        Span<bool> known = stackalloc bool[4];
        foreach ((uint address, BattleExecutionReferenceEntry entry) in references)
        {
            NativeBattleFileStaging.RequirePhysicalRange(address, entry.Width);
            if (address < BaseAddress || (ulong)address - BaseAddress + (uint)entry.Width > int.MaxValue ||
                pointers.Keys.Any(start => NativeBattleFileStaging.ExecutionReferenceOverlaps(start, 4, address, entry.Width)) ||
                executionReferences.Any(pair => NativeBattleFileStaging.ExecutionReferenceOverlaps(pair.Key, pair.Value.Width, address, entry.Width)))
            {
                throw new InvalidDataException("A retained staging reference conflicts with its backing storage.");
            }
            ReadWithKnownness(address, bytes[..entry.Width], known[..entry.Width]);
            _ = ExecutionReferenceEncoding.Capture(bytes[..entry.Width], known[..entry.Width]);
            executionReferences.Add(address, entry);
        }
    }
}

internal sealed partial class NativeBattleFileStagingOverlay
{
    private readonly NativeBattleStagingDeltaMap<BattleExecutionReferenceEntry?> executionChanges = new();
    private readonly List<(uint Address, bool Present, BattleExecutionReferenceEntry? Entry)> executionUndo = [];
    private readonly List<uint> invalidatedExecution = [];

    internal BattleExecutionReferenceEntry? ReadExecutionReference(uint address)
    {
        if (executionChanges.TryGetValue(address, out BattleExecutionReferenceEntry? entry))
        {
            return entry;
        }
        return parent is null ? source.ReadExecutionReference(address) : parent.ReadExecutionReference(address);
    }

    private IEnumerable<KeyValuePair<uint, BattleExecutionReferenceEntry>> EnumerateExecutionReferences()
    {
        var inherited = parent is null ? source.EnumerateExecutionReferences() : parent.EnumerateExecutionReferences();
        foreach (var pair in inherited)
        {
            if (!executionChanges.TryGetValue(pair.Key, out _))
            {
                yield return pair;
            }
        }
        foreach ((uint address, BattleExecutionReferenceEntry? entry) in executionChanges)
        {
            if (entry is not null)
            {
                yield return new(address, entry);
            }
        }
    }

    internal void WriteExecutionReference(uint address, BattleExecutionReferenceEntry entry, ImmutableArray<byte>? nativeBytes)
    {
        int depth = BeginNestedCheckpoint();
        try
        {
            if (address < BaseAddress)
            {
                Rebase(address);
            }
            if (nativeBytes is { } bytes)
            {
                Write(address, bytes.AsSpan());
            }
            else
            {
                MarkUnknown(address, entry.Width);
            }
            StoreExecutionReference(address, entry);
            CommitNestedCheckpoint(depth);
        }
        catch
        {
            RollbackNestedCheckpoint(depth);
            throw;
        }
    }

    private void StoreExecutionReference(uint address, BattleExecutionReferenceEntry? entry)
    {
        if (checkpoint)
        {
            bool present = executionChanges.TryGetValue(address, out BattleExecutionReferenceEntry? previous);
            executionUndo.Add((address, present, previous));
        }
        executionChanges[address] = entry;
    }

    private bool MayHaveExecutionReferences()
    {
        return executionChanges.Count != 0 || (parent is null ? source.ExecutionReferenceCount != 0 : parent.MayHaveExecutionReferences());
    }

    private void InvalidateExecutionReferences(uint address, int length)
    {
        if (!MayHaveExecutionReferences())
        {
            return;
        }
        invalidatedExecution.Clear();
        foreach ((uint start, BattleExecutionReferenceEntry entry) in EnumerateExecutionReferences())
        {
            if (NativeBattleFileStaging.ExecutionReferenceOverlaps(start, entry.Width, address, length))
            {
                invalidatedExecution.Add(start);
            }
        }
        executionChanges.EnsureCapacity(checked(executionChanges.Count + invalidatedExecution.Count));
        if (checkpoint)
        {
            executionUndo.EnsureCapacity(checked(executionUndo.Count + invalidatedExecution.Count));
        }
        foreach (uint start in invalidatedExecution)
        {
            StoreExecutionReference(start, null);
        }
    }

    private void RollbackExecutionReferences(int first)
    {
        for (int index = executionUndo.Count - 1; index >= first; index--)
        {
            var (address, present, entry) = executionUndo[index];
            if (present)
            {
                executionChanges[address] = entry;
            }
            else
            {
                executionChanges.Remove(address);
            }
        }
        executionUndo.RemoveRange(first, executionUndo.Count - first);
    }
}
