namespace Memoria.FF8.Runtime;

public static class NativeBattleDuel274MeteorParticles
{
    public static NativeBattleDuel274Frame Emit68CA60(NativeBattleDuel274Frame frame, NativeBattleVector position, short fourthWord, int count, bool grounded)
    {
        int index = NativeBattleDuel274Banks.Index(frame, 0x2508478);
        NativeBattleEffectTaskPool pool = frame.Effect.Pools[index];
        if (pool.Stride != 44)
        {
            throw new InvalidDataException("Native Meteor Strike debris requires its actual44-byte bank.");
        }
        NativeCrtRandomState random = frame.Random;
        for (int current = 0; current < count && pool.Slots.Any(task => (task.Flags & 1) == 0); current++)
        {
            (pool, int slot) = pool.Append(0x68D3B0);
            NativeBattleModulePoolTask task = pool.Slots[slot].WriteWord(12, unchecked((ushort)position.X)).WriteWord(14, unchecked((ushort)position.Y)).WriteWord(16, unchecked((ushort)position.Z)).WriteWord(18, unchecked((ushort)fourthWord));
            task = task.WriteWord(18, 0);
            if (grounded)
            {
                task = task.WriteWord(14, 0).WriteWord(20, unchecked((ushort)((Next() & 0x1FF) - 256))).WriteWord(24, unchecked((ushort)((Next() & 0x1FF) - 256))).WriteWord(22, unchecked((ushort)(-64 - (Next() & 0x7F))));
            }
            else
            {
                task = task.WriteWord(12, unchecked((ushort)(position.X + (Next() & 0x1FF) - 256))).WriteWord(16, unchecked((ushort)(position.Z + (Next() & 0x1FF) - 256))).WriteWord(14, unchecked((ushort)(position.Y + (byte)Next())));
                task = task.WriteWord(20, unchecked((ushort)((Next() & 0x7F) - 64))).WriteWord(24, unchecked((ushort)((Next() & 0x7F) - 64))).WriteWord(22, (ushort)(Next() & 0x1F));
            }
            task = task.WriteWord(28, Next()).WriteWord(30, Next()).WriteWord(32, Next());
            task = task.WriteWord(36, unchecked((ushort)((byte)Next() - 128))).WriteWord(38, unchecked((ushort)((byte)Next() - 128))).WriteWord(40, unchecked((ushort)((byte)Next() - 128)));
            pool = pool.WriteSlot(slot, task);
        }
        return frame with { Random = random, Effect = frame.Effect with { Pools = frame.Effect.Pools.SetItem(index, pool) } };

        ushort Next()
        {
            random = random.Draw(out ushort value);
            return value;
        }
    }

    public static NativeBattleDuel274TaskStep Advance68D3B0(NativeBattleDuel274Frame frame, int slot, NativeBattleActionMatrix view, Func<NativeBattleDuel274Frame, uint, NativeBattleActionMatrix, uint, int?, uint?, NativeBattleDuel274Frame> draw572200)
    {
        int index = NativeBattleDuel274Banks.Index(frame, 0x2508478);
        NativeBattleEffectTaskPool pool = frame.Effect.Pools[index];
        NativeBattleModulePoolTask task = pool.Slots[slot];
        if (pool.Stride != 44 || task.Callback != 0x68D3B0)
        {
            throw new InvalidDataException("Native Meteor Strike debris requires its actual callback and physical dimensions.");
        }
        NativeBattleActionMatrix local = NativeBattleGfMatrices.Euler56CD50(new(Signed(28), Signed(30), Signed(32))) with { X = Signed(12), Y = Signed(14), Z = Signed(16) };
        frame = draw572200(frame, 0x10E9360, NativeBattleGfMatrices.Compose56C2F0(view, local), 0, null, null);
        pool = frame.Effect.Pools[index];
        task = pool.Slots[slot];
        task = task.WriteWord(28, unchecked((ushort)(Signed(28) + Signed(36)))).WriteWord(32, unchecked((ushort)(Signed(32) + Signed(40)))).WriteWord(22, unchecked((ushort)(Signed(22) + 15))).WriteWord(30, unchecked((ushort)(Signed(30) + Signed(38))));
        task = task.WriteWord(12, unchecked((ushort)(Signed(12) + Signed(20)))).WriteWord(14, unchecked((ushort)(Signed(14) + Signed(22)))).WriteWord(16, unchecked((ushort)(Signed(16) + Signed(24))));
        if (Signed(14) > 0)
        {
            task = task.WriteWord(14, 0).WriteWord(22, unchecked((ushort)-Signed(22)));
        }
        short counter = unchecked((short)(Signed(18) + 1));
        task = task.WriteWord(18, unchecked((ushort)counter));
        pool = pool.WriteSlot(slot, task);
        frame = frame with { Effect = frame.Effect with { Pools = frame.Effect.Pools.SetItem(index, pool) } };
        return new(frame, pool, counter < 20 ? 0 : 2);

        short Signed(int offset) => unchecked((short)task.ReadWord(offset));
    }
}
