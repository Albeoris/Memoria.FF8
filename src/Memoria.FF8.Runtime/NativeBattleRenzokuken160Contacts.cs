using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public static partial class NativeBattleRenzokuken160Controller
{
    public static NativeBattleRenzokuken160ChildStep Advance600110(NativeBattleRenzokuken160Frame frame, NativeBattleEffectTaskPool pool, int slot, NativeBattleRenzokuken160Inputs inputs, NativeBattleRenzokuken160Resources resources, uint impactCallback = 0x6004A0)
    {
        if ((inputs.GraphicsFlags & 0x201) != 0)
        {
            return new(frame, pool, 0);
        }
        if (inputs.AbSegments.IsDefaultOrEmpty || resources.ContactLeadFrames.Length < inputs.AbSegments.Length - 1)
        {
            throw new NotSupportedException("Native600110 requires the reached AB contact bytes and original lead-time DWORDs.");
        }
        NativeBattleModulePoolTask task = pool.Slots[slot];
        short counter = unchecked((short)task.ReadWord(12));
        int accumulated = 0;
        byte[] sparks = State(frame).Sparks.ToArray();
        byte[] debris = State(frame).Debris.ToArray();
        byte[] impacts = State(frame).ImpactStates.ToArray();
        for (int contact = 0; contact < inputs.AbSegments.Length - 1; contact++)
        {
            accumulated += inputs.AbSegments[contact];
            if (counter != accumulated - resources.ContactLeadFrames[contact])
            {
                continue;
            }
            (pool, int child) = pool.Append(impactCallback);
            NativeBattleModulePoolTask created = pool.Slots[child].WriteWord(12, 0).WriteWord(14, (ushort)contact);
            ushort scale = (ushort)(Rand() % 640 + 1024);
            created = created.WriteWord(28, scale).WriteWord(30, scale);
            pool = pool with { Slots = pool.Slots.SetItem(child, created) };
            NativeBattleRenzokuken160State state = State(frame);
            int impact = contact * 156;
            Write(impacts, impact, state.Origin.X);
            Write(impacts, impact + 2, state.Origin.Y);
            Write(impacts, impact + 4, state.Origin.Z);
            Write(impacts, impact + 6, state.OriginPadding);
            Dword(impacts, impact + 8, state.Direction.X);
            Dword(impacts, impact + 12, state.Direction.Y);
            Dword(impacts, impact + 16, state.Direction.Z);
            Dword(impacts, impact + 20, unchecked((int)state.DirectionPadding));
            Dword(impacts, impact + 24, 16);
            for (int index = 0; index < 16; index++)
            {
                Write(impacts, impact + 28 + 2 * index, Rand() % 4096);
                int extent = Rand() % 2048 + 2048;
                Write(impacts, impact + 60 + 2 * index, extent);
                Write(impacts, impact + 92 + 2 * index, unchecked(1000 * extent - 300) >> 12);
                Write(impacts, impact + 124 + 2 * index, Rand() % 200 + 120);
            }
            for (int emission = 0; emission < 3; emission++)
            {
                int index = Free(sparks, 12);
                if (index == 40)
                {
                    break;
                }
                int offset = 12 * index;
                Write(sparks, offset, 0);
                Write(sparks, offset + 2, Rand() % 1536 + 2560);
                Write(sparks, offset + 4, state.Origin.X);
                Write(sparks, offset + 6, state.Origin.Y);
                Write(sparks, offset + 8, state.Origin.Z);
                Write(sparks, offset + 10, state.OriginPadding);
                Write(sparks, offset + 4, Read(sparks, offset + 4) + Rand() % 350 - 175);
                Write(sparks, offset + 6, Read(sparks, offset + 6) + Rand() % 350 - 175);
                Write(sparks, offset + 8, Read(sparks, offset + 8) + Rand() % 350 - 175);
            }
            for (int emission = 0; emission < 12; emission++)
            {
                int index = Free(debris, 20);
                if (index == 40)
                {
                    break;
                }
                int offset = 20 * index;
                Write(debris, offset, 0);
                Write(debris, offset + 2, Rand() % 2048 + 3072);
                Write(debris, offset + 4, state.Origin.X);
                Write(debris, offset + 6, state.Origin.Y);
                Write(debris, offset + 8, state.Origin.Z);
                Write(debris, offset + 10, state.OriginPadding);
                int velocity = Rand() % 200 + 140;
                Write(debris, offset + 12, Rand() % 200 + (unchecked(velocity * state.Direction.X) >> 12) - 100);
                Write(debris, offset + 14, Rand() % 200 + (unchecked(velocity * state.Direction.Y) >> 12) - 100);
                Write(debris, offset + 16, Rand() % 200 + (unchecked(velocity * state.Direction.Z) >> 12) - 100);
                if (contact == 3)
                {
                    for (int axis = offset + 12; axis <= offset + 16; axis += 2)
                    {
                        Write(debris, axis, -Read(debris, axis));
                    }
                }
            }
        }
        frame = WriteState(frame, State(frame) with { Sparks = sparks.ToImmutableArray(), Debris = debris.ToImmutableArray(), ImpactStates = impacts.ToImmutableArray() });
        task = task.WriteWord(12, unchecked((ushort)(counter + 1)));
        return new(frame, pool with { Slots = pool.Slots.SetItem(slot, pool.Slots[slot] with { Payload = task.Payload }) }, unchecked((short)task.ReadWord(12)) >= State(frame).Duration - 1 ? 2 : 0);

        int Rand()
        {
            NativeCrtRandomState next = frame.Random.Draw(out ushort value);
            frame = frame with { Random = next };
            return value;
        }
    }

    private static int Free(byte[] bank, int stride)
    {
        int index = 0;
        while (index < 40 && Read(bank, index * stride) >= 0)
        {
            index++;
        }
        return index;
    }

    private static void Dword(byte[] bank, int offset, int value) => BinaryPrimitives.WriteInt32LittleEndian(bank.AsSpan(offset), value);
}
