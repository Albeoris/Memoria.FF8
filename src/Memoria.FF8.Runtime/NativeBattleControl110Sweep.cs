using System.Buffers.Binary;

namespace Memoria.FF8.Runtime;

internal abstract partial record BattleExecutionReference
{
    internal sealed record Control110Sweep(int Current, int Phase) : BattleExecutionReference
    {
        internal int Next(int count)
        {
            return Math.Min(Current + 1, count - 1);
        }

        internal Control110Sweep Advance(int count, Action<int> beginTarget)
        {
            if (count < 4 || Current < 0 || Current >= count || Phase is < 0 or >= 54)
            {
                throw new InvalidDataException("Native control110 lost its expanded director position.");
            }

            int current = Current;
            long phase = (long)Phase + count;
            while (phase >= 54 && current < count - 1)
            {
                phase -= 54;
                current++;
                beginTarget(current);
            }
            return new(current, current == count - 1 ? 0 : checked((int)phase));
        }

        internal int Fraction(int count)
        {
            return count < 4 ? Phase : checked((int)((long)Phase * 4096 / 54));
        }
    }
}

internal static class NativeBattleControl110SweepStorage
{
    internal static void Write(NativeBattleModulePoolTask task, BattleExecutionReference.Control110Sweep sweep, int count)
    {
        if (count < 4)
        {
            byte[] cursor = new byte[4];
            BinaryPrimitives.WriteUInt16LittleEndian(cursor, checked((ushort)sweep.Current));
            BinaryPrimitives.WriteUInt16LittleEndian(cursor.AsSpan(2), checked((ushort)sweep.Next(count)));
            task.WriteExecutionReference(28, 4, sweep, [.. cursor]);
            task.WriteWord(32, checked((ushort)sweep.Phase));
        }
        else
        {
            task.WriteExecutionReference(28, 4, sweep);
        }
    }

    internal static NativeBattleEffectArena MirrorRetiredTargetPrefix(NativeBattleEffectArena arena, int offset, NativeBattleEffectTaskPool pool)
    {
        const int prefixLength = 2 * 44;
        if (pool.Head is not null || pool.Stride != 44 || !NativeBattlePresentationTaskStorage.IsHostedBank(pool.BankAddress, pool.Stride, pool.Slots.Length))
        {
            throw new InvalidDataException("Native control110 can only mirror its retired hosted target bank.");
        }
        NativeBattleTaskBankOwner owner = pool.RequireOwner();
        var storage = NativeBattleEffectArena.BorrowWorking(owner.Storage, owner.StorageOffset, prefixLength);
        (byte[] bytes, bool[] known) = storage.ReadSnapshot(0, prefixLength);
        return arena.WriteWithKnownness(offset, bytes, known);
    }
}
