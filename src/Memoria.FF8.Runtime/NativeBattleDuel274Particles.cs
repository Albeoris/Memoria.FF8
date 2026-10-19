using System.Buffers.Binary;

namespace Memoria.FF8.Runtime;

public static class NativeBattleDuel274Particles
{
    public static NativeBattleDuel274Frame Emit68AEE0(NativeBattleDuel274Frame frame, short bone, int count, int mode, short sprite, Func<short, NativeBattleVector> readBone502170)
    {
        NativeBattleVector position = readBone502170(bone);
        int poolIndex = NativeBattleDuel274Banks.Index(frame, 0x2508498);
        NativeBattleEffectTaskPool pool = frame.Effect.Pools[poolIndex];
        if (pool.DescriptorAddress != 0x2508498 || pool.Stride != 36)
        {
            throw new InvalidDataException("Native Duel particles require their actual shared physical bank.");
        }
        NativeCrtRandomState random = frame.Random;
        while (pool.Slots.Any(task => (task.Flags & 1) == 0))
        {
            if ((uint)mode > 4)
            {
                throw new NotSupportedException("Native Duel particle birth requires its implemented velocity selector.");
            }
            (pool, int slot) = pool.Append(0x68B140);
            NativeBattleModulePoolTask task = pool.Slots[slot].WriteDword(12, unchecked((ushort)position.X) | unchecked((uint)(ushort)position.Y << 16)).WriteDword(16, unchecked((ushort)position.Z));
            switch (mode)
            {
                case 0:
                    task = task.WriteWord(20, unchecked((ushort)((byte)Next() - 128))).WriteWord(22, unchecked((ushort)((byte)Next() - 128))).WriteWord(24, unchecked((ushort)(-128 - (Next() & 0x7F))));
                    break;
                case 1:
                    task = task.WriteWord(20, unchecked((ushort)((byte)Next() - 128))).WriteWord(22, unchecked((ushort)(-128 - (Next() & 0x7F)))).WriteWord(24, unchecked((ushort)((byte)Next() - 128)));
                    break;
                case 2:
                {
                    int angle = Next();
                    int radius = unchecked((byte)Next()) + 256;
                    task = task.WriteWord(20, unchecked((ushort)(radius * NativeBattleCameraMath.Sine(angle) >> 12))).WriteWord(22, unchecked((ushort)(radius * NativeBattleCameraMath.Cosine(angle) >> 12))).WriteWord(24, unchecked((ushort)((Next() & 0x3F) - 32)));
                    break;
                }
                case 3:
                {
                    int angle = Next();
                    int radius = (Next() & 0x7F) + 128;
                    task = task.WriteWord(20, unchecked((ushort)(radius * NativeBattleCameraMath.Sine(angle) >> 12))).WriteWord(24, unchecked((ushort)(radius * NativeBattleCameraMath.Cosine(angle) >> 12))).WriteWord(22, unchecked((ushort)(-128 - (Next() & 0x7F)))).WriteWord(28, 0).WriteWord(30, 20).WriteWord(32, 0);
                    break;
                }
                case 4:
                {
                    int angle = Next();
                    int radius = Next() & 0x3FF;
                    task = task.WriteWord(12, unchecked((ushort)(position.X + (radius * NativeBattleCameraMath.Sine(angle) >> 11)))).WriteWord(16, unchecked((ushort)(position.Z + (radius * NativeBattleCameraMath.Cosine(angle) >> 12)))).WriteWord(14, unchecked((ushort)(position.Y - (Next() & 0x7FF)))).WriteWord(20, 0).WriteWord(22, 0).WriteWord(24, 0).WriteWord(28, 0).WriteWord(30, unchecked((ushort)-20)).WriteWord(32, 0);
                    break;
                }
            }
            if (mode < 3)
            {
                task = task.WriteWord(28, unchecked((ushort)(-Signed(task, 20) >> 3))).WriteWord(30, unchecked((ushort)(-Signed(task, 22) >> 3))).WriteWord(32, unchecked((ushort)(-Signed(task, 24) >> 3)));
            }
            task = task.WriteWord(26, unchecked((ushort)sprite));
            pool = pool with { Slots = pool.Slots.SetItem(slot, task) };
            count = unchecked(count - 1);
            if (count == 0)
            {
                break;
            }
        }
        return frame with { Random = random, Effect = frame.Effect with { Pools = frame.Effect.Pools.SetItem(poolIndex, pool) } };

        ushort Next()
        {
            random = random.Draw(out ushort value);
            return value;
        }
    }

    public static NativeBattleDuel274TaskStep Advance68B140(NativeBattleDuel274Frame frame, int slot, NativeBattleDuel274Resources resources, Func<NativeBattleDuel274Frame, NativeBattleModulePoolTask, int, short, short, short, NativeBattleDuel274Frame> draw571C80)
    {
        int poolIndex = NativeBattleDuel274Banks.Index(frame, 0x2508498);
        NativeBattleEffectTaskPool pool = frame.Effect.Pools[poolIndex];
        NativeBattleModulePoolTask task = pool.Slots[slot];
        if (pool.DescriptorAddress != 0x2508498 || pool.Stride != 36 || task.Callback != 0x68B140)
        {
            throw new InvalidDataException("Native Duel particle advancement requires its actual callback and bank.");
        }
        short sprite = Signed(task, 26);
        uint directory = unchecked(0x110E3B8u + (uint)(sprite * 8));
        short size = BinaryPrimitives.ReadInt16LittleEndian(resources.Read(unchecked(directory + 6), 2).AsSpan());
        short duration = BinaryPrimitives.ReadInt16LittleEndian(resources.Read(unchecked(directory + 4), 2).AsSpan());
        frame = draw571C80(frame, task, sprite, Signed(task, 18), size, unchecked((short)-(size >> 2)));
        pool = frame.Effect.Pools[poolIndex];
        task = pool.Slots[slot];
        short counter = unchecked((short)(Signed(task, 18) + 1));
        task = task.WriteWord(18, unchecked((ushort)counter));
        int result = 2;
        if (counter < duration)
        {
            task = task.WriteWord(12, unchecked((ushort)(Signed(task, 12) + Signed(task, 20)))).WriteWord(14, unchecked((ushort)(Signed(task, 14) + Signed(task, 22)))).WriteWord(16, unchecked((ushort)(Signed(task, 16) + Signed(task, 24))));
            task = task.WriteWord(20, unchecked((ushort)(Signed(task, 20) + Signed(task, 28)))).WriteWord(22, unchecked((ushort)(Signed(task, 22) + Signed(task, 30)))).WriteWord(24, unchecked((ushort)(Signed(task, 24) + Signed(task, 32))));
            result = 0;
        }
        pool = pool with { Slots = pool.Slots.SetItem(slot, task) };
        frame = frame with { Effect = frame.Effect with { Pools = frame.Effect.Pools.SetItem(poolIndex, pool) } };
        return new(frame, pool, result);
    }

    private static short Signed(NativeBattleModulePoolTask task, int offset) => unchecked((short)task.ReadWord(offset));
}
