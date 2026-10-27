namespace Memoria.FF8.Runtime;

public sealed record NativeBattleLionHeart165Shake(short Offset) : NativeBattleLionHeart165Operation;

public static partial class NativeBattleLionHeart165Controller
{
    public static NativeBattleLionHeart165ChildStep Advance5E9D50(NativeBattleLionHeart165Frame frame, NativeBattleEffectTaskPool pool, int slot, NativeBattleLionHeart165Inputs inputs)
    {
        NativeBattleModulePoolTask task = pool.Slots[slot];
        short duration = Word(task, 14);
        if (duration == 0)
        {
            throw new NotSupportedException("Native Lion Heart stage fade requires its actual nonzero signed duration.");
        }
        short counter = Word(task, 12);
        frame = inputs.Execute(frame, new NativeBattleLionHeart165Stage(counter * (Word(task, 24) / duration) + 2048));
        if ((inputs.GraphicsFlags & 0x201) != 0)
        {
            return new(frame, pool, 0);
        }
        task = task.WriteWord(12, unchecked((ushort)(counter + 1)));
        bool released = Word(task, 12) >= duration;
        if (released && task.ReadWord(26) != 0)
        {
            frame = inputs.Execute(frame, new NativeBattleLionHeart165StageFlags(false));
        }
        return new(frame, pool.WriteSlot(slot, task), released ? 2 : 0);
    }

    public static NativeBattleLionHeart165ChildStep Advance5F0130(NativeBattleLionHeart165Frame frame, NativeBattleEffectTaskPool pool, int slot, NativeBattleLionHeart165Inputs inputs)
    {
        NativeBattleModulePoolTask task = pool.Slots[slot];
        short counter = Word(task, 12);
        int value = (200 * NativeBattleCameraMath.Sine((counter << 10) / 12 + 1024)) >> 12;
        frame = inputs.Execute(frame, new NativeBattleLionHeart165Shake(unchecked((short)((counter & 1) == 0 ? -value : value))));
        if ((inputs.GraphicsFlags & 0x201) != 0)
        {
            return new(frame, pool, 0);
        }
        task = task.WriteWord(12, unchecked((ushort)(counter + 1)));
        return new(frame, pool.WriteSlot(slot, task), Word(task, 12) >= 12 ? 2 : 0);
    }

    public static NativeBattleLionHeart165ChildStep Advance5F01B0(NativeBattleLionHeart165Frame frame, NativeBattleEffectTaskPool pool, int slot, Func<NativeBattleLionHeart165Frame, uint, NativeBattleLionHeart165Frame> beginTextureLoad)
    {
        NativeBattleModulePoolTask task = pool.Slots[slot];
        if (task.ReadWord(12) == 1)
        {
            uint completion = checked(pool.BankAddress + (uint)(slot * pool.Stride) + 14);
            frame = frame with { Effect = frame.Effect with { Pools = frame.Effect.Pools.SetItem(1, pool) } };
            frame = beginTextureLoad(frame, completion);
            pool = frame.Effect.Pools[1];
            task = pool.Slots[slot];
        }
        bool released = task.ReadWord(14) != 0;
        task = task.WriteWord(12, unchecked((ushort)(task.ReadWord(12) + 1)));
        return new(frame, pool.WriteSlot(slot, task), released ? 2 : 0);
    }
}
