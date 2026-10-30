using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public static partial class NativeBattleRenzokukenSpecialController
{
    public static NativeBattleRenzokuken160ChildStep Advance5FE440(NativeBattleRenzokuken160Frame frame, NativeBattleEffectTaskPool pool, int slot, NativeBattleRenzokuken160Inputs inputs, NativeBattleRenzokuken160Resources resources, uint impactCallback = 0x5FE790, Func<int, NativeBattleRenzokukenContactOrigin>? readOrigin = null)
    {
        if ((inputs.GraphicsFlags & 0x201) != 0)
        {
            return new(frame, pool, 0);
        }
        if (frame.Effect.Binding.Effect is 332 or 333 && readOrigin is null)
        {
            throw new NotSupportedException("Native seven/eight-contact Renzokuken requires the actual actor-matrix contact-offset transform.");
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
            (pool, int child) = pool.Append(impactCallback);
            NativeBattleModulePoolTask created = pool.Slots[child].WriteWord(12, 0).WriteWord(14, (ushort)contact);
            ushort scale = (ushort)(Rand() % 768 + 1280);
            created = created.WriteWord(28, scale).WriteWord(30, scale);
            pool = pool.WriteSlot(child, created);
            NativeBattleRenzokuken160State state = ReadState(frame).Core;
            NativeBattleRenzokukenContactOrigin origin = readOrigin?.Invoke(contact) ?? new(state.Origin, state.OriginPadding);
            int impact = contact * 156;
            Put161(impacts, impact, origin.Position.X);
            Put161(impacts, impact + 2, origin.Position.Y);
            Put161(impacts, impact + 4, origin.Position.Z);
            WritePadding(impacts, impact + 6, origin.Padding, 0, contact);
            Dword161(impacts, impact + 8, state.Direction.X);
            Dword161(impacts, impact + 12, state.Direction.Y);
            Dword161(impacts, impact + 16, state.Direction.Z);
            Dword161(impacts, impact + 20, unchecked((int)state.DirectionPadding));
            Dword161(impacts, impact + 24, 16);
            for (int index = 0; index < 16; index++)
            {
                Put161(impacts, impact + 28 + 2 * index, Rand() % 4096);
                int extent = Rand() % 3072 + 3072;
                Put161(impacts, impact + 60 + 2 * index, extent);
                Put161(impacts, impact + 92 + 2 * index, unchecked(1000 * extent) >> 12);
                Put161(impacts, impact + 124 + 2 * index, Rand() % 200 + 120);
            }
            for (int emission = 0; emission < 3; emission++)
            {
                int index = FreeContact161(sparks, 12);
                if (index == 40)
                {
                    break;
                }
                int offset = 12 * index;
                Put161(sparks, offset, 0);
                NativeBattleRenzokukenVisualLifetimes.Allocated(frame.Effect, 0, index);
                Put161(sparks, offset + 2, Rand() % 2048 + 3584);
                Put161(sparks, offset + 4, origin.Position.X);
                Put161(sparks, offset + 6, origin.Position.Y);
                Put161(sparks, offset + 8, origin.Position.Z);
                WritePadding(sparks, offset + 10, origin.Padding, 1, index);
                Put161(sparks, offset + 4, Get161(sparks, offset + 4) + Rand() % 700 - 350);
                Put161(sparks, offset + 6, Get161(sparks, offset + 6) + Rand() % 700 - 350);
                Put161(sparks, offset + 8, Get161(sparks, offset + 8) + Rand() % 700 - 350);
            }
            for (int emission = 0; emission < 12; emission++)
            {
                int index = FreeContact161(debris, 20);
                if (index == 40)
                {
                    break;
                }
                int offset = 20 * index;
                Put161(debris, offset, 0);
                NativeBattleRenzokukenVisualLifetimes.Allocated(frame.Effect, 1, index);
                Put161(debris, offset + 2, Rand() % 4096 + 6144);
                Put161(debris, offset + 4, origin.Position.X);
                Put161(debris, offset + 6, origin.Position.Y);
                Put161(debris, offset + 8, origin.Position.Z);
                WritePadding(debris, offset + 10, origin.Padding, 2, index);
                int velocity = Rand() % 200 + 140;
                Put161(debris, offset + 12, Rand() % 200 + (unchecked(velocity * state.Direction.X) >> 12) - 100);
                Put161(debris, offset + 14, Rand() % 200 + (unchecked(velocity * state.Direction.Y) >> 12) - 100);
                Put161(debris, offset + 16, Rand() % 200 + (unchecked(velocity * state.Direction.Z) >> 12) - 100);

            }
        }
        frame = WriteState(frame, ReadState(frame).Core with { Sparks = sparks.ToImmutableArray(), Debris = debris.ToImmutableArray(), ImpactStates = impacts.ToImmutableArray() });
        task = task.WriteWord(12, unchecked((ushort)(counter + 1)));
        return new(frame, pool.WriteSlot(slot, task), unchecked((short)task.ReadWord(12)) >= ReadState(frame).Core.Duration - 1 ? 2 : 0);

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

    private static int FreeContact161(byte[] bank, int stride)
    {
        int index = 0;
        while (index < 40 && Get161(bank, index * stride) >= 0)
        {
            index++;
        }
        return index;
    }

    private static void Dword161(byte[] bank, int offset, int value) => BinaryPrimitives.WriteInt32LittleEndian(bank.AsSpan(offset), value);
}
