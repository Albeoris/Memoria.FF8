using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json.Serialization;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleShivaEarlyTaskFrame([property: JsonRequired] NativeBattleEffectMemory Memory, [property: JsonRequired] NativeBattleEffectTaskPool Pool, [property: JsonRequired] NativeCrtRandomState Random, [property: JsonRequired] NativeBattleShivaGeometryState Geometry);
public sealed record NativeBattleShivaEarlyTaskStep(NativeBattleShivaEarlyTaskFrame Frame, int Result);

public static class NativeBattleShivaEarlyTasks
{
    public static NativeBattleShivaEarlyTaskStep Advance5C1390(NativeBattleShivaEarlyTaskFrame frame, int slot, uint graphicsFlags, Func<NativeBattleShivaEarlyTaskFrame, int, NativeBattleShivaEarlyTaskFrame> regenerateGeometry5C1470)
    {
        ValidatePool(frame.Pool);
        if ((uint)slot >= frame.Pool.Slots.Length || frame.Pool.Slots[slot] is not { Callback: 0x5C1390 } task || (task.Flags & 1) == 0)
        {
            throw new InvalidDataException("Native Shiva first visual requires its allocated5C1390 task.");
        }
        ArgumentNullException.ThrowIfNull(regenerateGeometry5C1470);
        frame = regenerateGeometry5C1470(frame, Math.Min((int)unchecked((short)task.ReadWord(0x0E)), 36));
        ValidatePool(frame.Pool);
        if (frame.Pool.Slots[slot].ReadWord(0x0C) == 0)
        {
            NativeCrtRandomState random = frame.Random.Draw(out ushort sample);
            NativeBattleEffectTaskPool pool = frame.Pool;
            for (int index = 0; index < 4; index++)
            {
                int childSlot;
                (pool, childSlot) = pool.Append(0x5C16B0);
                NativeBattleModulePoolTask child = pool.Slots[childSlot].WriteWord(0x0C, 0).WriteWord(0x0E, (ushort)(1 << index)).WriteWord(0x10, 0).WriteWord(0x12, 0).WriteWord(0x14, 0).WriteWord(0x18, unchecked((ushort)(sample % 4096 + index * 1024))).WriteWord(0x1A, 60).WriteWord(0x1C, 4096).WriteWord(0x20, 6144).WriteWord(0x22, 0);
                pool = pool with { Slots = pool.Slots.SetItem(childSlot, child) };
            }
            frame = frame with { Pool = pool, Random = random, Geometry = frame.Geometry with { Current = frame.Geometry.Current with { Y = unchecked((short)0xD8F1) } } };
        }
        if ((graphicsFlags & 0x201) != 0)
        {
            return new(frame, 0);
        }
        task = frame.Pool.Slots[slot];
        task = task.WriteWord(0x0E, unchecked((ushort)(task.ReadWord(0x0E) + 2))).WriteWord(0x0C, unchecked((ushort)(task.ReadWord(0x0C) + 1)));
        frame = frame with { Pool = frame.Pool with { Slots = frame.Pool.Slots.SetItem(slot, task) } };
        return new(frame, unchecked((short)task.ReadWord(0x0C)) >= 60 ? 2 : 0);
    }

    public static NativeBattleShivaEarlyTaskFrame Spawn5C4820(NativeBattleShivaEarlyTaskFrame frame, ImmutableArray<byte> rows)
    {
        ValidatePool(frame.Pool);
        if (rows.IsDefault || rows.Length != 64)
        {
            throw new InvalidDataException("Native Shiva burst requires its four original eight-WORD parameter rows.");
        }
        NativeBattleEffectTaskPool pool = frame.Pool;
        NativeCrtRandomState random = frame.Random;
        for (int index = 0; index < 4; index++)
        {
            int childSlot;
            (pool, childSlot) = pool.Append(0x5C4900);
            random = random.Draw(out ushort first);
            random = random.Draw(out ushort second);
            random = random.Draw(out ushort third);
            random = random.Draw(out ushort fourth);
            random = random.Draw(out ushort fifth);
            random = random.Draw(out ushort sixth);
            int scale = (fifth % Word(2) + Word(0)) / Word(8);
            NativeBattleModulePoolTask child = pool.Slots[childSlot].WriteWord(0x0C, 0).WriteWord(0x0E, (ushort)(first % 4 + index * 2)).WriteWord(0x10, 0).WriteWord(0x12, unchecked((ushort)-(second % Word(0x0E) + Word(0x0C)))).WriteWord(0x14, 0).WriteWord(0x18, (ushort)(third % 4096)).WriteWord(0x1A, (ushort)(fourth % 120 + 90)).WriteWord(0x1C, unchecked((ushort)scale)).WriteWord(0x1E, unchecked((ushort)scale)).WriteWord(0x20, unchecked((ushort)(sixth % Word(6) + Word(4)))).WriteWord(0x22, unchecked((ushort)Word(0x0A)));
            pool = pool with { Slots = pool.Slots.SetItem(childSlot, child) };

            short Word(int offset) => BinaryPrimitives.ReadInt16LittleEndian(rows.AsSpan().Slice(index * 16 + offset));
        }
        return frame with { Pool = pool, Random = random };
    }

    private static void ValidatePool(NativeBattleEffectTaskPool pool)
    {
        pool.Validate();
        if (pool.DescriptorAddress != 0x22BC198 || pool.BankAddress != 0x22BC1A8 || pool.Stride != 0x24 || pool.Slots.Length != 100)
        {
            throw new InvalidDataException("Native Shiva visual tasks require the original22BC198 task bank.");
        }
    }
}
