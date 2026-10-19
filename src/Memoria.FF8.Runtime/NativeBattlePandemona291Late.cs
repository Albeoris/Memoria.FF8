using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public static class NativeBattlePandemona291Late
{
    public static (NativeBattlePandemona291Frame Frame, NativeBattleVector Position) Anchor6F0CC0(NativeBattlePandemona291Frame frame, uint address, ImmutableArray<byte> source)
    {
        if (source.IsDefault || source.Length != 8)
        {
            throw new InvalidDataException("Native Pandemona model anchor requires its original packed input and signed bone selector.");
        }
        NativeBattleEffectArena arena = frame.Memory.Arena ?? throw new NotSupportedException("Native Pandemona model anchor requires its current arena.");
        int model = NativeBattleWaterExecutionFields.ArenaOffset(unchecked(address + 0x154), 0x80);
        uint directory = BinaryPrimitives.ReadUInt32LittleEndian(arena.Read(model + 0x64, 4));
        uint skeleton = BinaryPrimitives.ReadUInt32LittleEndian(arena.Read(NativeBattleWaterExecutionFields.ArenaOffset(directory, 4), 4));
        short selected = BinaryPrimitives.ReadInt16LittleEndian(source.AsSpan().Slice(6));
        int bone = NativeBattleWaterExecutionFields.ArenaOffset(unchecked(skeleton + 0x20u + (uint)(selected * 48)), 32);
        NativeBattleActionMatrix matrix = NativeBattleGfMatrices.Compose56C2F0(NativeBattleArenaPose.ReadMatrix(arena, model + 0x40), NativeBattleArenaPose.ReadMatrix(arena, bone));
        NativeBattleVector input = new(BinaryPrimitives.ReadInt16LittleEndian(source.AsSpan()), BinaryPrimitives.ReadInt16LittleEndian(source.AsSpan().Slice(2)), BinaryPrimitives.ReadInt16LittleEndian(source.AsSpan().Slice(4)));
        NativeBattleFullMacVertex mac = NativeBattleGfMatrices.TransformMac460840(matrix, input);
        NativeBattleVector result = new(unchecked((short)Math.Clamp(mac.X, short.MinValue, short.MaxValue)), unchecked((short)Math.Clamp(mac.Y, short.MinValue, short.MaxValue)), unchecked((short)Math.Clamp(mac.Z, short.MinValue, short.MaxValue)));
        NativeBattleScriptedColorRegisters registers = frame.Memory.ScriptedColorRegisters ?? throw new NotSupportedException("Native Pandemona model anchor requires its canonical GTE input owner.");
        registers = registers with { NormalInput = source, NormalInputKnown = null, Mac = [mac.X, mac.Y, mac.Z], Ir = [result.X, result.Y, result.Z], Flags = mac.Flags };
        return (frame with { Memory = frame.Memory with { ScriptedColorRegisters = registers } }, result);
    }

    public static NativeBattleEffectArena Advance6F1BA0(NativeBattleEffectArena arena, uint address)
    {
        int start = NativeBattleWaterExecutionFields.ArenaOffset(address, 0x1030);
        for (int index = 0; index < 128; index++)
        {
            int record = start + 0x42C + index * 24;
            ushort life = Word(record + 6);
            if (life == 0)
            {
                continue;
            }
            life--;
            Store(record + 6, life);
            if (life == 0)
            {
                arena = arena.Write(start + 0x102C, BitConverter.GetBytes(unchecked(address + 0x42Cu + (uint)(index * 24))));
                continue;
            }
            Store(record + 0x10, Word(record + 0x10) + (unchecked((sbyte)arena.Read(record + 0x14, 1)[0]) << 2));
            Store(record + 0x12, Word(record + 0x12) + (unchecked((sbyte)arena.Read(record + 0x15, 1)[0]) << 2));
            Store(record, Word(record) + Word(record + 8));
            Store(record + 4, Word(record + 4) + Word(record + 0xC));
            Store(record + 2, Word(record + 2) + Word(record + 0xA));
        }
        return arena;

        ushort Word(int offset) => BinaryPrimitives.ReadUInt16LittleEndian(arena.Read(offset, 2));
        void Store(int offset, int value) => arena = arena.Write(offset, BitConverter.GetBytes(unchecked((ushort)value)));
    }

    public static NativeBattlePandemona291TrailAllocation Spawn184(NativeBattleEffectArena arena, NativeCrtRandomState random, uint address, NativeBattleVector origin, int elapsed, ImmutableArray<byte> trigonometry, int countShift = 3, int radiusShift = 1, int scale = 2048)
    {
        for (int remaining = elapsed >> countShift; remaining > 0; remaining--)
        {
            NativeBattlePandemona291TrailAllocation allocated = NativeBattlePandemona291Trails.Allocate6F1550(arena, random, address);
            arena = allocated.Arena;
            random = allocated.Random;
            if (allocated.Address is not uint selected)
            {
                break;
            }
            int record = NativeBattleWaterExecutionFields.ArenaOffset(selected, 24);
            random = random.Draw(out ushort angle);
            int offset = (angle & 0xFFF) * 4;
            short x = unchecked((short)(BinaryPrimitives.ReadInt16LittleEndian(trigonometry.AsSpan().Slice(offset)) >> radiusShift));
            short y = unchecked((short)(BinaryPrimitives.ReadInt16LittleEndian(trigonometry.AsSpan().Slice(offset + 2)) >> radiusShift));
            Store(8, x);
            Store(0xA, y);
            Store(0, origin.X + x);
            Store(2, origin.Y + y);
            Store(4, origin.Z - 4096);
            Store(0xC, 512);
            Store(8, -x >> 3);
            Store(0xA, -y >> 3);
            Store(6, 8);
            Store(0xE, scale);

            void Store(int field, int value) => arena = arena.Write(record + field, BitConverter.GetBytes(unchecked((ushort)value)));
        }
        return new(arena, random, null);
    }
    public static NativeBattlePandemona291TrailAllocation Spawn224(NativeBattleEffectArena arena, NativeCrtRandomState random, uint address, int ground)
    {
        for (int index = 0; index < 4; index++)
        {
            NativeBattlePandemona291TrailAllocation allocated = NativeBattlePandemona291Trails.Allocate6F1550(arena, random, address);
            arena = allocated.Arena;
            random = allocated.Random;
            if (allocated.Address is not uint selected)
            {
                break;
            }
            int record = NativeBattleWaterExecutionFields.ArenaOffset(selected, 24);
            random = random.Draw(out ushort x);
            Store(0, (x & 0xFFF) - 0x800);
            random = random.Draw(out ushort y);
            Store(2, -(y & 0x7FF));
            Store(4, ground - 4096);
            Store(6, 12);
            Store(8, 0);
            Store(0xA, 0);
            Store(0xC, 682);
            Store(0xE, 4096);

            void Store(int field, int value) => arena = arena.Write(record + field, BitConverter.GetBytes(unchecked((ushort)value)));
        }
        return new(arena, random, null);
    }

    public static NativeBattleEffectArena Convert284(NativeBattleEffectArena arena, uint address)
    {
        int start = NativeBattleWaterExecutionFields.ArenaOffset(address, 0x1030);
        for (int index = 0; index < 128; index++)
        {
            int record = start + 0x42C + index * 24;
            if (BinaryPrimitives.ReadUInt16LittleEndian(arena.Read(record + 6, 2)) == 0)
            {
                continue;
            }
            Store(8, BinaryPrimitives.ReadInt16LittleEndian(arena.Read(record + 8, 2)) >> 4);
            Store(0xC, BinaryPrimitives.ReadInt16LittleEndian(arena.Read(record + 0xC, 2)) >> 4);
            Store(6, 36);
            Store(0xA, 2048);

            void Store(int field, int value) => arena = arena.Write(record + field, BitConverter.GetBytes(unchecked((ushort)value)));
        }
        return arena;
    }

}
