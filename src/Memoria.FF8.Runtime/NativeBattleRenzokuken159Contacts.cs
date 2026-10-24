using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public static partial class NativeBattleRenzokuken159Controller
{
    public static NativeBattleRenzokuken159ChildStep Advance601FF0(NativeBattleRenzokuken159Frame frame, NativeBattleEffectTaskPool pool, int slot, NativeBattleRenzokuken159Inputs inputs, NativeBattleRenzokuken159Resources resources, uint impactCallback = 0x602390, Func<int, NativeBattleRenzokukenContactOrigin>? readOrigin = null)
    {
        if ((inputs.GraphicsFlags & 0x201) != 0)
        {
            return new(frame, pool, 0);
        }
        if (inputs.AbSegments.IsDefaultOrEmpty || resources.ContactLeadFrames.Length < inputs.AbSegments.Length - 1)
        {
            throw new NotSupportedException("Native601FF0 requires the reached AB contact bytes and original lead-time DWORDs.");
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
            ushort scale = (ushort)(Rand() % 768 + 1280);
            created = created.WriteWord(28, scale).WriteWord(30, scale);
            pool = pool with { Slots = pool.Slots.SetItem(child, created) };
            NativeBattleRenzokuken159State state = State(frame);
            NativeBattleRenzokukenContactOrigin origin = readOrigin?.Invoke(contact) ?? new(state.Origin, state.OriginPadding);
            int impact = contact * 156;
            Write(impacts, impact, origin.Position.X);
            Write(impacts, impact + 2, origin.Position.Y);
            Write(impacts, impact + 4, origin.Position.Z);
            WritePadding(impacts, impact + 6, origin.Padding, 0, contact);
            Dword(impacts, impact + 8, state.Direction.X);
            Dword(impacts, impact + 12, state.Direction.Y);
            Dword(impacts, impact + 16, state.Direction.Z);
            Dword(impacts, impact + 20, unchecked((int)state.DirectionPadding));
            Dword(impacts, impact + 24, 16);
            for (int index = 0; index < 16; index++)
            {
                Write(impacts, impact + 28 + 2 * index, Rand() % 4096);
                int extent = Rand() % 3072 + 3072;
                Write(impacts, impact + 60 + 2 * index, extent);
                Write(impacts, impact + 92 + 2 * index, unchecked(1000 * extent) >> 12);
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
                Write(sparks, offset + 2, Rand() % 1536 + 2816);
                Write(sparks, offset + 4, origin.Position.X);
                Write(sparks, offset + 6, origin.Position.Y);
                Write(sparks, offset + 8, origin.Position.Z);
                WritePadding(sparks, offset + 10, origin.Padding, 1, index);
                Write(sparks, offset + 4, Read(sparks, offset + 4) + Rand() % 550 - 275);
                Write(sparks, offset + 6, Read(sparks, offset + 6) + Rand() % 550 - 275);
                Write(sparks, offset + 8, Read(sparks, offset + 8) + Rand() % 550 - 275);
            }
            for (int emission = 0; emission < 12; emission++)
            {
                int index = Free(debris, 20);
                if (index == 80)
                {
                    break;
                }
                int offset = 20 * index;
                Write(debris, offset, 0);
                Write(debris, offset + 2, Rand() % 3328 + 4608);
                Write(debris, offset + 4, origin.Position.X);
                Write(debris, offset + 6, origin.Position.Y);
                Write(debris, offset + 8, origin.Position.Z);
                WritePadding(debris, offset + 10, origin.Padding, 2, index);
                int velocity = Rand() % 200 + 140;
                Write(debris, offset + 12, Rand() % (2 * resources.ContactSpread[contact]) + (unchecked(velocity * state.Direction.X) >> 12) - resources.ContactSpread[contact]);
                Write(debris, offset + 14, Rand() % (2 * resources.ContactSpread[contact]) + (unchecked(velocity * state.Direction.Y) >> 12) - resources.ContactSpread[contact]);
                Write(debris, offset + 16, Rand() % (2 * resources.ContactSpread[contact]) + (unchecked(velocity * state.Direction.Z) >> 12) - resources.ContactSpread[contact]);
                if (contact == 0)
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

        void WritePadding(byte[] bytes, int offset, ushort? value, int bank, int index)
        {
            if (value is ushort known)
            {
                Write(bytes, offset, known);
            }
            else
            {
                throw new NotSupportedException("Native159 requires retained source padding for its physical DWORD copy.");
            }
        }

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
        while (index < bank.Length / stride && Read(bank, index * stride) >= 0)
        {
            index++;
        }
        return index;
    }

    private static void Dword(byte[] bank, int offset, int value) => BinaryPrimitives.WriteInt32LittleEndian(bank.AsSpan(offset), value);
}
