using System.Runtime.CompilerServices;

namespace Memoria.FF8.Runtime;

internal sealed partial class NativeBattleEffectArenaTransaction
{
    private sealed class ActiveRoute
    {
        internal NativeBattleEffectArenaTransaction? Transaction;
    }

    private static readonly ConditionalWeakTable<NativeBattleWorkingMemory, ActiveRoute> ActiveRoutes = new();
    private ActiveRoute? registeredRoute;

    internal int AbsoluteOffset => Source.WorkingOffset;

    private void RegisterRoute()
    {
        ActiveRoute entry = ActiveRoutes.GetValue(Source.Working, static _ => new());
        if (Interlocked.CompareExchange(ref entry.Transaction, this, null) is not null)
        {
            throw new InvalidOperationException("A physical native bank already has an active public arena transaction.");
        }
        registeredRoute = entry;
    }

    private void UnregisterRoute()
    {
        if (registeredRoute is { } entry)
        {
            Interlocked.CompareExchange(ref entry.Transaction, null, this);
            registeredRoute = null;
        }
    }

    internal static bool TryRoute(NativeBattleWorkingMemory kernel, int absoluteOffset, int length, out NativeBattleEffectArenaTransaction? transaction, out int relativeOffset)
    {
        if (absoluteOffset < 0 || length < 0 || absoluteOffset > kernel.LogicalLength - length)
        {
            throw new InvalidDataException("Native physical routing leaves its kernel bounds.");
        }
        transaction = null;
        relativeOffset = 0;
        if (!ActiveRoutes.TryGetValue(kernel, out ActiveRoute? entry) || entry.Transaction is not { } active)
        {
            return false;
        }
        active.RequireActive();
        int first = active.AbsoluteOffset;
        int end = checked(first + active.Source.Bound);
        if (absoluteOffset >= first && absoluteOffset <= end - length)
        {
            transaction = active;
            relativeOffset = absoluteOffset - first;
            return true;
        }
        if (absoluteOffset < end && (long)absoluteOffset + length > first)
        {
            throw new NotSupportedException("Native physical access partially overlaps an active arena transaction window.");
        }
        return false;
    }

    internal static bool HasActive(NativeBattleWorkingMemory kernel) => ActiveRoutes.TryGetValue(kernel, out ActiveRoute? entry) && entry.Transaction is not null;

    internal static void RequireCommittedForCapture(NativeBattleWorkingMemory kernel)
    {
        if (ActiveRoutes.TryGetValue(kernel, out ActiveRoute? entry) && entry.Transaction is { } active && active.writer.OwnedOverlay.HasPendingChanges)
        {
            throw new InvalidOperationException("Native physical capture requires its active arena transaction to publish its pending changes and close its callback checkpoint.");
        }
    }

    internal void WriteWithKnownness(int offset, ReadOnlySpan<byte> bytes, ReadOnlySpan<bool> known)
    {
        RequireActive();
        writer.WriteWithKnownness(offset, bytes, known);
    }

    internal void WriteRetainedBytes(int offset, ReadOnlySpan<byte> bytes)
    {
        RequireActive();
        writer.WriteRetainedBytes(offset, bytes);
    }

}

public partial record NativeBattleEffectArena
{
    internal bool TryEffectiveRoute(int offset, int length, out NativeBattleEffectArenaTransaction? transaction, out int relativeOffset)
    {
        RequireRange(offset, length);
        transaction = Transaction;
        relativeOffset = offset;
        if (transaction is not null)
        {
            transaction.RequireActive();
            return true;
        }
        return borrowed && !snapshot && NativeBattleEffectArenaTransaction.TryRoute(Working, workingOffset + offset, length, out transaction, out relativeOffset);
    }

    internal void ReadCommittedWithKnownness(int offset, Span<byte> bytes, Span<bool> known)
    {
        RequireRange(offset, bytes.Length);
        Working.ReadWithKnownness(workingOffset + offset, bytes, known);
    }

    internal void ReadCommittedInto(int offset, Span<byte> bytes)
    {
        RequireRange(offset, bytes.Length);
        Working.ReadInto(workingOffset + offset, bytes);
    }

    internal bool TryGetCommittedResourcePointer(int offset, out NativeBattleResourcePointer? pointer)
    {
        RequireRange(offset, 4);
        return Working.TryGetResourcePointer(workingOffset + offset, out pointer);
    }
}
