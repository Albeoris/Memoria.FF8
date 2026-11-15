using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;

namespace Memoria.FF8.Runtime;

internal sealed partial class NativeBattleTaskBody
{
    internal void WriteExecutionReference(int offset, int width, BattleExecutionReference reference, ImmutableArray<byte>? nativeBytes)
    {
        ExecutionReferenceEncoding.Validate(width, nativeBytes);
        if (TryRoute(offset, width, out NativeBattleEffectArenaTransaction? transaction, out int relative))
        {
            transaction.WriteExecutionReference(relative, width, reference, nativeBytes);
        }
        else
        {
            Kernel.WriteExecutionReference(offset, width, reference, nativeBytes);
        }
    }

    internal bool TryReadExecutionReference<T>(int offset, int width, [NotNullWhen(true)] out T? reference) where T : BattleExecutionReference
    {
        ExecutionReferenceEncoding.RequireWidth(width);
        return TryRoute(offset, width, out NativeBattleEffectArenaTransaction? transaction, out int relative)
            ? transaction.TryReadExecutionReference(relative, width, out reference)
            : Kernel.TryReadExecutionReference(offset, width, out reference);
    }

    internal IEnumerable<KeyValuePair<int, BattleExecutionReferenceEntry>> EnumerateExecutionReferences()
    {
        if (TryRoute(owner.StorageOffset, owner.BankLength, out NativeBattleEffectArenaTransaction? transaction, out int relative))
        {
            foreach ((int offset, BattleExecutionReferenceEntry entry) in NativeBattleEffectArena.ProjectExecutionReferences(transaction.EnumerateExecutionReferences(), relative, owner.BankLength))
            {
                yield return new(checked(owner.StorageOffset + offset), entry);
            }
            yield break;
        }
        foreach ((int offset, BattleExecutionReferenceEntry entry) in NativeBattleEffectArena.ProjectExecutionReferences(Kernel.EnumerateExecutionReferences(), owner.StorageOffset, owner.BankLength))
        {
            yield return new(checked(owner.StorageOffset + offset), entry);
        }
    }
}

public sealed partial record NativeBattleModulePoolTask
{
    internal int ReadContactCount(int offset) => ReadExecutionReference<BattleExecutionReference.ContactCount>(offset, 2).Value;

    internal NativeBattleModulePoolTask WriteContactCount(int offset, int count)
    {
        BattleExecutionReference.ContactCount reference = new(count);
        WriteExecutionReference(offset, 2, reference, reference.EncodeOriginalWord());
        return this;
    }

    internal int ReadTraversalPosition(int offset) => ReadExecutionReference<BattleExecutionReference.TraversalPosition>(offset, 1).Value;

    internal NativeBattleModulePoolTask WriteTraversalPosition(int offset, int position)
    {
        BattleExecutionReference.TraversalPosition reference = new(position);
        WriteExecutionReference(offset, 1, reference, reference.EncodeOriginalByte());
        return this;
    }

    internal void WriteExecutionReference(int offset, int width, BattleExecutionReference reference, ImmutableArray<byte>? nativeBytes = null)
    {
        ExecutionReferenceEncoding.Validate(width, nativeBytes);
        RequirePayloadRange(offset, width);
        NativeBattleTaskBankOwner active = RequireOwner();
        active.Body.WriteExecutionReference(checked(active.StorageOffset + slot * active.Stride + offset), width, reference, nativeBytes);
    }

    internal T ReadExecutionReference<T>(int offset, int width) where T : BattleExecutionReference
    {
        return TryReadExecutionReference<T>(offset, width, out T? reference) ? reference : throw new InvalidDataException("No live execution reference of the requested kind and width exists.");
    }

    internal bool TryReadExecutionReference<T>(int offset, int width, [NotNullWhen(true)] out T? reference) where T : BattleExecutionReference
    {
        ExecutionReferenceEncoding.RequireWidth(width);
        RequirePayloadRange(offset, width);
        if (owner is { } active)
        {
            return active.Body.TryReadExecutionReference(checked(active.StorageOffset + slot * active.Stride + offset), width, out reference);
        }
        reference = RequireSnapshotData().ExecutionReferences.TryGetValue(offset, out BattleExecutionReferenceEntry? entry) && entry.Width == width ? entry.Reference as T : null;
        return reference is not null;
    }

    internal IEnumerable<KeyValuePair<int, BattleExecutionReferenceEntry>> EnumerateExecutionReferences()
    {
        return owner is { } active
            ? NativeBattleEffectArena.ProjectExecutionReferences(active.Body.EnumerateExecutionReferences(), checked(active.StorageOffset + slot * active.Stride), active.Stride).Where(pair => pair.Key >= 12)
            : RequireSnapshotData().ExecutionReferences;
    }
}
