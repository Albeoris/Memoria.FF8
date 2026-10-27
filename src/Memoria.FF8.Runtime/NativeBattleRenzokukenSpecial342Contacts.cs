using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public static partial class NativeBattleRenzokukenSpecialController
{
    public static NativeBattleRenzokuken160ChildStep Advance578A00(NativeBattleRenzokuken160Frame frame, NativeBattleEffectTaskPool pool, int slot, NativeBattleRenzokuken160Inputs inputs, NativeBattleRenzokuken160Resources resources)
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
        byte[] sparks = ReadState(frame).Core.Sparks.ToArray();
        byte[] debris = ReadState(frame).Core.Debris.ToArray();
        byte[] impacts = ReadState(frame).Core.ImpactStates.ToArray();
        for (int contact = 0; contact < inputs.AbSegments.Length - 1; contact++)
        {
            accumulated += inputs.AbSegments[contact];
            if (counter != accumulated - resources.ContactLeadFrames[contact])
            {
                continue;
            }
            (pool, int child) = pool.Append(0x578DF0);
            NativeBattleModulePoolTask created = pool.Slots[child].WriteWord(12, 0).WriteWord(14, (ushort)contact);
            ushort scale = (ushort)(Rand() % 640 + 1024);
            created = created.WriteWord(28, scale).WriteWord(30, scale);
            pool = pool.WriteSlot(child, created);
            NativeBattleRenzokuken160State state = ReadState(frame).Core;
            NativeBattleRenzokukenContactOrigin origin = ContactOrigin(state, contact);
            int impact = contact * 156;
            Put161(impacts, impact, origin.Position.X);
            Put161(impacts, impact + 2, origin.Position.Y);
            Put161(impacts, impact + 4, origin.Position.Z);
            WritePadding(impacts, impact + 6, origin.Padding, 0, contact);
            Dword342(impacts, impact + 8, state.Direction.X);
            Dword342(impacts, impact + 12, state.Direction.Y);
            Dword342(impacts, impact + 16, state.Direction.Z);
            Dword342(impacts, impact + 20, unchecked((int)state.DirectionPadding));
            Dword342(impacts, impact + 24, 16);
            for (int index = 0; index < 16; index++)
            {
                Put161(impacts, impact + 28 + 2 * index, Rand() % 4096);
                int extent = Rand() % 2048 + 2048;
                Put161(impacts, impact + 60 + 2 * index, extent);
                Put161(impacts, impact + 92 + 2 * index, unchecked(1000 * extent - 300) >> 12);
                Put161(impacts, impact + 124 + 2 * index, Rand() % 200 + 120);
            }
            for (int emission = 0; emission < 3; emission++)
            {
                int index = FreeContact342(sparks, 12);
                if (index == 40)
                {
                    break;
                }
                int offset = 12 * index;
                Put161(sparks, offset, 0);
                Put161(sparks, offset + 2, Rand() % 1536 + 2560);
                Put161(sparks, offset + 4, origin.Position.X);
                Put161(sparks, offset + 6, origin.Position.Y);
                Put161(sparks, offset + 8, origin.Position.Z);
                WritePadding(sparks, offset + 10, origin.Padding, 1, index);
                Put161(sparks, offset + 4, Get161(sparks, offset + 4) + Rand() % 350 - 175);
                Put161(sparks, offset + 6, Get161(sparks, offset + 6) + Rand() % 350 - 175);
                Put161(sparks, offset + 8, Get161(sparks, offset + 8) + Rand() % 350 - 175);
            }
            for (int emission = 0; emission < 12; emission++)
            {
                int index = FreeContact342(debris, 20);
                if (index == 40)
                {
                    break;
                }
                int offset = 20 * index;
                Put161(debris, offset, 0);
                Put161(debris, offset + 2, Rand() % 2048 + 3072);
                Put161(debris, offset + 4, origin.Position.X);
                Put161(debris, offset + 6, origin.Position.Y);
                Put161(debris, offset + 8, origin.Position.Z);
                WritePadding(debris, offset + 10, origin.Padding, 2, index);
                int velocity = Rand() % 200 + 140;
                Put161(debris, offset + 12, Rand() % 200 + (unchecked(velocity * state.Direction.X) >> 12) - 100);
                Put161(debris, offset + 14, Rand() % 200 + (unchecked(velocity * state.Direction.Y) >> 12) - 100);
                Put161(debris, offset + 16, Rand() % 200 + (unchecked(velocity * state.Direction.Z) >> 12) - 100);
                if (contact == 3)
                {
                    for (int axis = offset + 12; axis <= offset + 16; axis += 2)
                    {
                        Put161(debris, axis, -Get161(debris, axis));
                    }
                }
            }
            if (contact is > 0 and < 7)
            {
                (pool, int extra) = pool.Append(0x5790D0);
                pool = pool.WriteSlot(extra, pool.Slots[extra].WriteWord(12, 0).WriteWord(14, (ushort)contact));
            }
        }
        frame = WriteState(frame, ReadState(frame).Core with { Sparks = sparks.ToImmutableArray(), Debris = debris.ToImmutableArray(), ImpactStates = impacts.ToImmutableArray() });
        task = task.WriteWord(12, unchecked((ushort)(counter + 1)));
        return new(frame, pool.WriteSlot(slot, task), unchecked((short)task.ReadWord(12)) >= ReadState(frame).Core.Duration - 1 ? 2 : 0);

        NativeBattleRenzokukenContactOrigin ContactOrigin(NativeBattleRenzokuken160State state, int contact)
        {
            if (resources.ContactOffsets is not { } offsets || contact >= offsets.Length)
            {
                throw new NotSupportedException("Native578A00 requires its original eight contact offsets.");
            }
            NativeBattleActionMatrix body = inputs.ReadModel(state.Actor).GeometryWorldMatrix ?? throw new NotSupportedException("Native578A00 requires current actor matrix64.");
            NativeBattleVector delta = NativeBattleGfMatrices.RotateShort56C4F0(body, offsets[contact]);
            return new(new(unchecked((short)(state.Origin.X + delta.X)), unchecked((short)(state.Origin.Y + delta.Y)), unchecked((short)(state.Origin.Z + delta.Z))), null);
        }

        void WritePadding(byte[] bytes, int offset, ushort? value, int bank, int index)
        {
            if (value is ushort known)
            {
                Put161(bytes, offset, known);
            }
            else
            {
                frame = NativeBattleRenzokukenSpecialController.MarkContactPaddingUnknown(frame, bank, index);
            }
        }

        int Rand()
        {
            NativeCrtRandomState next = frame.Random.Draw(out ushort value);
            frame = frame with { Random = next };
            return value;
        }
    }

    private static int FreeContact342(byte[] bank, int stride)
    {
        int index = 0;
        while (index < 40 && Get161(bank, index * stride) >= 0)
        {
            index++;
        }
        return index;
    }

    private static void Dword342(byte[] bank, int offset, int value) => BinaryPrimitives.WriteInt32LittleEndian(bank.AsSpan(offset), value);
}
