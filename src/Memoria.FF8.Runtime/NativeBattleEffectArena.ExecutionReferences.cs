using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;

namespace Memoria.FF8.Runtime;

public partial record NativeBattleEffectArena
{
    internal NativeBattleEffectArena WriteExecutionReference(int offset, int width, BattleExecutionReference reference, ImmutableArray<byte>? nativeBytes = null)
    {
        RequireWritable();
        ExecutionReferenceEncoding.Validate(width, nativeBytes);
        RequireRange(offset, width);
        if (TryEffectiveRoute(offset, width, out NativeBattleEffectArenaTransaction? transaction, out int translated))
        {
            transaction!.WriteExecutionReference(translated, width, reference, nativeBytes);
        }
        else
        {
            Working.WriteExecutionReference(checked(workingOffset + offset), width, reference, nativeBytes);
        }
        return this;
    }

    internal T ReadExecutionReference<T>(int offset, int width) where T : BattleExecutionReference
    {
        return TryReadExecutionReference<T>(offset, width, out T? reference) ? reference : throw new InvalidDataException("No live execution reference of the requested kind and width exists.");
    }

    internal bool TryReadExecutionReference<T>(int offset, int width, [NotNullWhen(true)] out T? reference) where T : BattleExecutionReference
    {
        ExecutionReferenceEncoding.RequireWidth(width);
        RequireRange(offset, width);
        if (TryEffectiveRoute(offset, width, out NativeBattleEffectArenaTransaction? transaction, out int translated))
        {
            return transaction!.TryReadExecutionReference(translated, width, out reference);
        }
        if (!IsSnapshot)
        {
            return Working.TryReadExecutionReference(checked(workingOffset + offset), width, out reference);
        }
        reference = null;
        int absolute = checked(capturedOffset + offset);
        if (capturedAuthority?.ExecutionReferences.TryGetValue(absolute, out BattleExecutionReferenceEntry? entry) == true && entry.Width == width)
        {
            reference = entry.Reference as T;
        }
        return reference is not null;
    }

    internal IEnumerable<KeyValuePair<int, BattleExecutionReferenceEntry>> EnumerateExecutionReferences()
    {
        if (TryEffectiveRoute(0, bound, out NativeBattleEffectArenaTransaction? transaction, out int translated))
        {
            return ProjectExecutionReferences(transaction!.EnumerateExecutionReferences(), translated, bound);
        }
        return EnumerateCommittedExecutionReferences();
    }

    internal IEnumerable<KeyValuePair<int, BattleExecutionReferenceEntry>> EnumerateCommittedExecutionReferences()
    {
        if (!IsSnapshot)
        {
            return ProjectExecutionReferences(Working.EnumerateExecutionReferences(), workingOffset, bound);
        }
        return ProjectExecutionReferences(capturedAuthority?.ExecutionReferences ?? ImmutableDictionary<int, BattleExecutionReferenceEntry>.Empty, capturedOffset, bound);
    }

    internal static IEnumerable<KeyValuePair<int, BattleExecutionReferenceEntry>> ProjectExecutionReferences(IEnumerable<KeyValuePair<int, BattleExecutionReferenceEntry>> source, int start, int length)
    {
        foreach ((int offset, BattleExecutionReferenceEntry entry) in source)
        {
            int relative = offset - start;
            if (relative >= 0 && relative <= length - entry.Width)
            {
                yield return new(relative, entry);
            }
        }
    }
}

internal sealed partial class NativeBattleEffectArenaWriter
{
    internal void WriteExecutionReference(int offset, int width, BattleExecutionReference reference, ImmutableArray<byte>? nativeBytes)
    {
        RequireActive();
        ExecutionReferenceEncoding.Validate(width, nativeBytes);
        source.RequireRange(offset, width);
        if (transaction is null)
        {
            overlay!.WriteExecutionReference(offset, width, reference, nativeBytes);
        }
        else
        {
            transaction.WriteExecutionReference(checked(transactionOffset + offset), width, reference, nativeBytes);
        }
    }

    internal bool TryReadExecutionReference<T>(int offset, int width, [NotNullWhen(true)] out T? reference) where T : BattleExecutionReference
    {
        RequireActive();
        ExecutionReferenceEncoding.RequireWidth(width);
        source.RequireRange(offset, width);
        return transaction is null ? overlay!.TryReadExecutionReference(offset, width, out reference) : transaction.TryReadExecutionReference(checked(transactionOffset + offset), width, out reference);
    }

    internal IEnumerable<KeyValuePair<int, BattleExecutionReferenceEntry>> EnumerateExecutionReferences()
    {
        RequireActive();
        return transaction is null ? overlay!.EnumerateExecutionReferences() : source.EnumerateExecutionReferences();
    }
}

internal sealed partial class NativeBattleEffectArenaTransaction
{
    internal void WriteExecutionReference(int offset, int width, BattleExecutionReference reference, ImmutableArray<byte>? nativeBytes)
    {
        RequireActive();
        writer.WriteExecutionReference(offset, width, reference, nativeBytes);
    }

    internal bool TryReadExecutionReference<T>(int offset, int width, [NotNullWhen(true)] out T? reference) where T : BattleExecutionReference
    {
        RequireActive();
        return writer.TryReadExecutionReference(offset, width, out reference);
    }

    internal IEnumerable<KeyValuePair<int, BattleExecutionReferenceEntry>> EnumerateExecutionReferences()
    {
        using var entries = writer.EnumerateExecutionReferences().GetEnumerator();
        while (true)
        {
            RequireActive();
            if (!entries.MoveNext())
            {
                yield break;
            }
            yield return entries.Current;
        }
    }
}
