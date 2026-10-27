using System.Buffers.Binary;

namespace Memoria.FF8.Runtime;

public static partial class NativeBattleDuel112Births
{
    public static NativeBattleDuel112Frame Late6CBF90(NativeBattleDuel112Frame frame, int targetSlot, short actorYaw)
    {
        NativeBattleModulePoolTask target = frame.Effect.Pools[1].Slots[targetSlot];
        int elapsed = unchecked((short)target.ReadWord(12)) - 69;
        NativeCrtRandomState random = frame.Random;
        if (elapsed == 2)
        {
            for (int index = 0; index < 16; index++)
            {
                if (!Append(4, 0x6CD640, 36, out int slot, out NativeBattleModulePoolTask task))
                {
                    continue;
                }
                int angle = Next();
                angle = unchecked(angle - Next());
                angle = unchecked(angle - Next());
                angle = unchecked(angle + Next()) >> 1;
                int magnitude = Math.Abs(angle);
                int x = -NativeBattleCameraMath.Sine(actorYaw + angle);
                int z = -NativeBattleCameraMath.Cosine(actorYaw + angle);
                task = task.WriteWord(16, unchecked((ushort)(Signed(target, 100) + (unchecked((short)x) >> 4)))).WriteWord(18, 0).WriteWord(20, unchecked((ushort)(Signed(target, 104) + (unchecked((short)z) >> 4))));
                int factor = 40000 - magnitude;
                int velocityX = unchecked((short)((uint)unchecked(factor * unchecked((short)x)) >> 16)) >> 4;
                int velocityZ = unchecked((short)((uint)unchecked(factor * unchecked((short)z)) >> 16)) >> 4;
                int velocityY = unchecked(-((factor >> 2) * Next())) >> 20;
                task = task.WriteWord(24, unchecked((ushort)velocityX)).WriteWord(26, unchecked((ushort)velocityY)).WriteWord(28, unchecked((ushort)velocityZ));
                task = task.WriteWord(32, Next()).WriteWord(34, Next()).WriteWord(36, Next());
                task = task.WriteWord(40, unchecked((ushort)((Next() & 0x7F) - 64))).WriteWord(42, unchecked((ushort)((Next() & 0x7F) - 64))).WriteWord(44, unchecked((ushort)((Next() & 0x7F) - 64)));
                Store(4, slot, task);
            }
        }
        else if ((uint)(elapsed - 41) < 4)
        {
            NativeBattleDuel112Resources resources = frame.Memory.Duel112?.Resources ?? throw new NotSupportedException("Native Duel final sprite birth requires its actual executable vector owner.");
            NativeBattleActionMatrix matrix = NativeBattleGfMatrices.Euler56CD50(new(unchecked((short)Next()), unchecked((short)Next()), unchecked((short)Next()))) with { X = 0, Y = 0, Z = 0 };
            for (uint vectorAddress = 0x12DB748; ; vectorAddress -= 8)
            {
                if (Append(3, 0x6CD750, 24, out int slot, out NativeBattleModulePoolTask task))
                {
                    ReadOnlySpan<byte> bytes = resources.Read(vectorAddress, 6).AsSpan();
                    NativeBattleVector source = new(BinaryPrimitives.ReadInt16LittleEndian(bytes), BinaryPrimitives.ReadInt16LittleEndian(bytes.Slice(2)), BinaryPrimitives.ReadInt16LittleEndian(bytes.Slice(4)));
                    NativeBattleVector point = NativeBattleGfMatrices.Transform460840(matrix, source);
                    task = task.WriteDword(16, 0x12C5794).WriteWord(14, 15);
                    task = task.WriteWord(28, unchecked((ushort)(point.X >> 7))).WriteWord(30, unchecked((ushort)(point.Y >> 7))).WriteWord(32, unchecked((ushort)(point.Z >> 7)));
                    task = Point(task, 20, new(unchecked((short)(Signed(target, 92) + (point.X >> 3))), unchecked((short)(Signed(target, 94) + (point.Y >> 3))), unchecked((short)(Signed(target, 96) + (point.Z >> 3)))));
                    task = task.WriteWord(26, 4096).WriteWord(34, unchecked((ushort)(target.ReadWord(172) + 4096)));
                    Store(3, slot, task);
                }
                if (vectorAddress == 0x12DB730)
                {
                    break;
                }
            }
        }
        return frame with { Random = random };

        ushort Next()
        {
            random = random.Draw(out ushort value);
            return value;
        }
        bool Append(int poolIndex, uint callback, int clearBytes, out int slot, out NativeBattleModulePoolTask task)
        {
            NativeBattleEffectTaskPool pool = frame.Effect.Pools[poolIndex];
            if (pool.Stride - 12 != clearBytes || pool.DescriptorAddress != (poolIndex == 3 ? 0x2521930u : 0x2521920u))
            {
                throw new InvalidDataException("Native Duel final birth requires its actual physical task bank.");
            }
            slot = -1;
            task = null!;
            if (pool.Slots.All(candidate => (candidate.Flags & 1) != 0))
            {
                return false;
            }
            (pool, slot) = pool.Append(callback);
            task = pool.Slots[slot].WriteBytes(12, new byte[clearBytes]);
            frame = frame with { Effect = frame.Effect with { Pools = frame.Effect.Pools.SetItem(poolIndex, pool) } };
            return true;
        }
        void Store(int poolIndex, int slot, NativeBattleModulePoolTask task)
        {
            NativeBattleEffectTaskPool pool = frame.Effect.Pools[poolIndex];
            frame = frame with { Effect = frame.Effect with { Pools = frame.Effect.Pools.SetItem(poolIndex, pool.WriteSlot(slot, task)) } };
        }
    }
}
