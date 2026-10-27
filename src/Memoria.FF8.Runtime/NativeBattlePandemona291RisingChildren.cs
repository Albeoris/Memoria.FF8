using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public static class NativeBattlePandemona291RisingChildren
{
    public static NativeBattlePandemona291Frame Spawn114(NativeBattlePandemona291Frame frame)
    {
        NativeBattleEffectTaskPool pool = frame.Effect.Pools.Single(current => current.DescriptorAddress == 0x2556288);
        NativeBattleEffectArena arena = frame.Memory.Arena ?? throw new NotSupportedException("Native Pandemona ribbons require their actual task arena.");
        for (int remaining = 2; remaining >= 0; remaining--)
        {
            (pool, int index) = pool.Append(0x6F1060);
            NativeBattleModulePoolTask task = pool.Slots[index];
            int angle = remaining * 1365;
            task = task.WriteWord(0xC, 0).WriteDword(0x20, 0).WriteWord(0x10, unchecked((ushort)((remaining << 11) - 6000))).WriteWord(0x1A, 4096).WriteWord(0x1E, 0).WriteWord(0x1C, 0).WriteWord(0x14, unchecked((ushort)((remaining & 1) != 0 ? -210 : 210))).WriteWord(0x18, unchecked((ushort)((remaining & 1) != 0 ? -64 : 64))).WriteWord(0x12, unchecked((ushort)angle)).WriteWord(0x16, unchecked((ushort)angle));
            int start = NativeBattleWaterExecutionFields.ArenaOffset(unchecked(pool.BankAddress + (uint)(index * pool.Stride)), pool.Stride);
            foreach (int field in new[] { 0xC, 0x10, 0x12, 0x14, 0x16, 0x18, 0x1A, 0x1C, 0x1E })
            {
                arena = arena.Write(start + field, BitConverter.GetBytes(task.ReadWord(field)));
            }
            arena = arena.Write(start + 0x20, BitConverter.GetBytes(0));
            pool = pool.WriteSlot(index, task);
        }
        return frame with { Memory = frame.Memory with { Arena = arena }, Effect = frame.Effect with { Pools = frame.Effect.Pools.Select(current => current.DescriptorAddress == pool.DescriptorAddress ? pool : current).ToImmutableArray() } };
    }

    public static NativeBattlePandemona291ChildStep Finish6F1060(NativeBattlePandemona291Frame frame, uint address, NativeBattleModulePoolTask task)
    {
        if ((frame.Memory.Pandemona291?.Pause2556258 ?? throw new NotSupportedException("Native Pandemona ribbons require their current pause DWORD.")) != 0)
        {
            return new(frame, task, 0);
        }
        NativeBattleEffectArena arena = frame.Memory.Arena!;
        int start = NativeBattleWaterExecutionFields.ArenaOffset(address, 0x45C);
        short age = unchecked((short)task.ReadWord(0xC));
        Word(0x16, task.ReadWord(0x16) + task.ReadWord(0x18));
        if (age >= 20)
        {
            if (age == 20)
            {
                Word(0x1A, 4096);
                Word(0x1C, 1024);
                Word(0x1E, -64);
            }
            Word(0x1A, task.ReadWord(0x1A) + task.ReadWord(0x1C));
            short velocity = unchecked((short)(task.ReadWord(0x1C) + task.ReadWord(0x1E)));
            Word(0x1C, velocity);
            if (velocity < 64)
            {
                Word(0x1C, 64);
            }
        }
        short next = unchecked((short)(age + 1));
        Word(0xC, next);
        int result = next > 8 && task.ReadDword(0x20) < 2 ? 2 : 0;
        return new(frame with { Memory = frame.Memory with { Arena = arena } }, task, result);

        void Word(int offset, int value)
        {
            ushort word = unchecked((ushort)value);
            task = task.WriteWord(offset, word);
            arena = arena.Write(start + offset, BitConverter.GetBytes(word));
        }
    }

    public static NativeBattlePandemona291TrailAllocation Transition124(NativeBattleEffectArena arena, NativeCrtRandomState random, uint address, int center)
    {
        int start = NativeBattleWaterExecutionFields.ArenaOffset(address, 0x1030);
        for (int index = 0; index < 128; index++)
        {
            int record = start + 0x42C + index * 24;
            ushort life = U16(record + 6);
            if (life == 0)
            {
                continue;
            }
            if (I16(record + 0xA) >= -1024)
            {
                Word(record + 6, 0);
                arena = arena.Write(start + 0x102C, BitConverter.GetBytes(unchecked(address + 0x42Cu + (uint)(index * 24))));
                continue;
            }
            Word(record + 6, life + 24);
            Word(record + 8, I16(record) / 256);
            Word(record + 0xC, unchecked(I16(record + 4) - center) / 256);
            random = random.Draw(out ushort velocity);
            Word(record + 0xA, (velocity & 127) + 64);
        }
        return new(arena, random, null);

        ushort U16(int offset) => BinaryPrimitives.ReadUInt16LittleEndian(arena.Read(offset, 2));
        short I16(int offset) => unchecked((short)U16(offset));
        void Word(int offset, int value) => arena = arena.Write(offset, BitConverter.GetBytes(unchecked((ushort)value)));
    }

    public static NativeBattleEffectArena Advance6F1B20(NativeBattleEffectArena arena, uint address)
    {
        int start = NativeBattleWaterExecutionFields.ArenaOffset(address, 0x1030);
        for (int index = 0; index < 128; index++)
        {
            int record = start + 0x42C + index * 24;
            ushort life = U16(record + 6);
            if (life == 0)
            {
                continue;
            }
            life--;
            Word(record + 6, life);
            if (life == 0)
            {
                arena = arena.Write(start + 0x102C, BitConverter.GetBytes(unchecked(address + 0x42Cu + (uint)(index * 24))));
                continue;
            }
            Word(record + 0x10, U16(record + 0x10) + (unchecked((sbyte)arena.Read(record + 0x14, 1)[0]) << 2));
            Word(record, U16(record) + U16(record + 8));
            Word(record + 2, U16(record + 2) + U16(record + 0xA));
            short velocity = unchecked((short)(U16(record + 0xA) + 20));
            Word(record + 0x12, U16(record + 0x12) + (unchecked((sbyte)arena.Read(record + 0x15, 1)[0]) << 2));
            Word(record + 4, U16(record + 4) + U16(record + 0xC));
            Word(record + 0xA, velocity);
            if (I16(record + 2) >= 0)
            {
                Word(record + 0xA, -(velocity / 2));
            }
        }
        return arena;

        ushort U16(int offset) => BinaryPrimitives.ReadUInt16LittleEndian(arena.Read(offset, 2));
        short I16(int offset) => unchecked((short)U16(offset));
        void Word(int offset, int value) => arena = arena.Write(offset, BitConverter.GetBytes(unchecked((ushort)value)));
    }
}
