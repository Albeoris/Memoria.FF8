using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public static partial class NativeBattleDuel112Births
{
    public static NativeBattleDuel112Frame Early6CBF90(NativeBattleDuel112Frame frame, int targetSlot, NativeBattleVector bone241, NativeBattleModelTransform actor)
    {
        NativeBattleModulePoolTask target = frame.Effect.Pools[1].Slots[targetSlot];
        int elapsed = unchecked((short)target.ReadWord(12)) - 10;
        NativeCrtRandomState random = frame.Random;
        if ((uint)elapsed < 2)
        {
            int angle = Next();
            for (int index = 0; index < 8; index++, angle += 512)
            {
                if (!Append(3, 0x6CD750, 24, out int slot, out NativeBattleModulePoolTask task))
                {
                    continue;
                }
                task = task.WriteDword(16, 0x12C5650).WriteWord(14, 12).WriteWord(34, 4096).WriteWord(26, 4096);
                task = Point(task, 20, new(actor.Position28.X, actor.Angle36, actor.Position28.Z));
                task = task.WriteWord(28, unchecked((ushort)(NativeBattleCameraMath.Cosine(angle) >> 3))).WriteWord(32, unchecked((ushort)(NativeBattleCameraMath.Sine(angle) >> 3)));
                int value = Next() & 0xFFF;
                int magnitude = 1024 - (value >> 2) + value;
                task = task.WriteWord(28, unchecked((ushort)(magnitude * Signed(task, 28) >> 14))).WriteWord(32, unchecked((ushort)(magnitude * Signed(task, 32) >> 14)));
                Store(3, slot, task);
            }
        }
        if (elapsed == 0)
        {
            for (int index = 0; index < 12; index++)
            {
                if (!Append(2, 0x6CD5C0, 16, out int slot, out NativeBattleModulePoolTask task))
                {
                    continue;
                }
                task = Point(task, 16, bone241);
                task = task.WriteWord(16, unchecked((ushort)(task.ReadWord(16) + (Next() & 0x1FF) - 256)));
                task = task.WriteWord(20, unchecked((ushort)(task.ReadWord(20) + (Next() & 0x1FF) - 256)));
                task = task.WriteWord(22, (ushort)((Next() & 0x7FF) + 4096)).WriteDword(24, 0x12C5650).WriteWord(14, 12);
                Store(2, slot, task);
            }
        }
        else if ((uint)(elapsed - 2) <= 5)
        {
            NativeBattleVector delta = new(unchecked((short)(bone241.X - Signed(target, 76))), unchecked((short)(bone241.Y - Signed(target, 78))), unchecked((short)(bone241.Z - Signed(target, 80))));
            for (int index = 0; index < 2; index++)
            {
                if (!Append(4, 0x6CD640, 36, out int slot, out NativeBattleModulePoolTask task))
                {
                    continue;
                }
                task = Point(task, 16, delta).MarkPayloadUnknown(22, 2);
                int x = (Next() & 0x1F) + 32;
                if ((Next() & 1) != 0)
                {
                    x = -x;
                }
                int z = (Next() & 0x1F) + 32;
                if ((Next() & 1) != 0)
                {
                    z = -z;
                }
                task = task.WriteWord(24, unchecked((ushort)x)).WriteWord(28, unchecked((ushort)z));
                task = task.WriteWord(32, Next()).WriteWord(34, Next()).WriteWord(36, Next());
                task = task.WriteWord(40, unchecked((ushort)((Next() & 0x7F) - 64))).WriteWord(42, unchecked((ushort)((Next() & 0x7F) - 64))).WriteWord(44, unchecked((ushort)((Next() & 0x7F) - 64)));
                Store(4, slot, task);
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
            if (pool.Stride - 12 != clearBytes || pool.DescriptorAddress != (poolIndex == 2 ? 0x2521940u : poolIndex == 3 ? 0x2521930u : 0x2521920u))
            {
                throw new InvalidDataException("Native Duel birth requires its actual task dimensions.");
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

    private static short Signed(NativeBattleModulePoolTask task, int offset) => unchecked((short)task.ReadWord(offset));
    private static NativeBattleModulePoolTask Point(NativeBattleModulePoolTask task, int offset, NativeBattleVector point) => task.WriteWord(offset, unchecked((ushort)point.X)).WriteWord(offset + 2, unchecked((ushort)point.Y)).WriteWord(offset + 4, unchecked((ushort)point.Z));
}
