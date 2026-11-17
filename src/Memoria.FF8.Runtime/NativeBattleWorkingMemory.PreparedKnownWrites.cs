namespace Memoria.FF8.Runtime;

internal sealed partial class NativeBattleWorkingMemory
{
    internal NativeBattlePreparedKnownWrites PrepareKnownWrites(int offset, int length, int writeRuns)
    {
        RequireRange(offset, length);
        if (!CanPrepareKnownWrites)
        {
            return default;
        }
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(length);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(writeRuns);
        PrepareStorageReceipts(writeRuns, writeRuns);
        ReserveJournal(Math.Min(length, RemainingJournalEntries), 0);
        EnsureCapacity(offset + length);
        return new(this, offset, offset + length, writeRuns);
    }

    private int RemainingJournalEntries => maxJournalEntries - byteUndo!.Count - pointerUndo!.Count;

    private bool CanPrepareKnownWrites => checkpoint && nestedCheckpoints!.Count == 0 && RetainedAuthority is null &&
        pointers is null && executionReferences is null && readWindows is null && textureFiles is null;

    internal ref struct NativeBattlePreparedKnownWrites
    {
        private readonly NativeBattleWorkingMemory? owner;
        private readonly long checkpoint;
        private readonly int end;
        private int nextOffset;
        private int remainingRuns;
        private long expectedVersion;

        internal NativeBattlePreparedKnownWrites(NativeBattleWorkingMemory owner, int offset, int end, int writeRuns)
        {
            this.owner = owner;
            checkpoint = owner.checkpointGeneration;
            this.end = end;
            nextOffset = offset;
            remainingRuns = writeRuns;
            expectedVersion = owner.version;
        }

        internal bool IsPrepared => owner is not null;

        internal void Write(int offset, scoped ReadOnlySpan<byte> source)
        {
            NativeBattleWorkingMemory memory = owner ?? throw new InvalidOperationException("Native working memory known writes require their prepared interval.");
            memory.RequireActive();
            if (checkpoint != memory.checkpointGeneration || !memory.CanPrepareKnownWrites || expectedVersion != memory.version)
            {
                throw new InvalidOperationException("Native working memory known writes require their original checkpoint without other mutations.");
            }
            if (remainingRuns == 0 || source.IsEmpty || offset < nextOffset || source.Length > end - offset)
            {
                throw new InvalidOperationException("Native working memory known writes must be disjoint and remain within their prepared interval.");
            }
            if (source.Length > memory.RemainingJournalEntries)
            {
                // An upper bound is only a reservation. Identical bytes can still fit a tighter journal.
                memory.PrepareMutation(offset, source.Length, source, false, 0);
            }
            memory.WriteKnownCore(offset, source);
            expectedVersion = memory.version;
            nextOffset = offset + source.Length;
            remainingRuns--;
        }
    }
}
