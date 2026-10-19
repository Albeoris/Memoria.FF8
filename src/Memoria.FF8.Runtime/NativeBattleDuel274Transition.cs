namespace Memoria.FF8.Runtime;

public sealed record NativeBattleDuel274TransitionStep(NativeBattleDuel274Frame Frame, bool ContinueSelected);

public static class NativeBattleDuel274Transition
{
    public static NativeBattleDuel274TransitionStep Advance689AC0(NativeBattleDuel274Frame frame, int slot, NativeBattleDuel274ActorServices actors, Action<int> stage5713E0, Func<NativeBattleDuel274Frame, byte, NativeBattleDuel274Frame> draw68D4B0)
    {
        NativeBattleModulePoolTask task = frame.Effect.Pools[1].Slots[slot];
        byte phase = task.ReadByte(18);
        if (phase == 0)
        {
            return new(frame, true);
        }
        if (phase >= 4)
        {
            if (frame.Staging.Read(0x25084D8, 1)[0] != 0)
            {
                byte amount = unchecked((byte)(task.ReadByte(19) - 16));
                task = task.WriteByte(19, amount);
                Store();
                if (amount > 128)
                {
                    SetModelFlag(false);
                    stage5713E0(0);
                    task = task.WriteByte(19, 0).WriteByte(18, 0);
                    Store();
                    return new(frame, true);
                }
            }
            else
            {
                SetModelFlag(true);
                byte amount = unchecked((byte)(task.ReadByte(19) + 16));
                task = task.WriteByte(19, amount > 64 ? (byte)64 : amount);
                Store();
            }
            stage5713E0(2048);
            frame = draw68D4B0(frame, task.ReadByte(19));
        }
        task = frame.Effect.Pools[1].Slots[slot];
        phase = unchecked((byte)(task.ReadByte(18) + 1));
        task = task.WriteByte(18, phase > 4 ? (byte)4 : phase);
        Store();
        return new(frame, false);

        void Store()
        {
            NativeBattleEffectTaskPool pool = frame.Effect.Pools[1];
            frame = frame with { Effect = frame.Effect with { Pools = frame.Effect.Pools.SetItem(1, pool with { Slots = pool.Slots.SetItem(slot, task) }) } };
        }
        void SetModelFlag(bool frozen)
        {
            for (int index = 0; index < 7; index++)
            {
                ushort flags = actors.ReadFlags(index);
                if ((flags & 2) != 0)
                {
                    actors.WriteFlags(index, frozen ? (ushort)(flags | 8) : (ushort)(flags & 0xFFF7));
                }
            }
        }
    }
}
