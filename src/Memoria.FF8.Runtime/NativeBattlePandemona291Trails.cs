using System.Buffers.Binary;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattlePandemona291TrailAllocation(NativeBattleEffectArena Arena, NativeCrtRandomState Random, uint? Address);

public static class NativeBattlePandemona291Trails
{
    public static NativeBattlePandemona291TrailAllocation Allocate6F1550(NativeBattleEffectArena arena, NativeCrtRandomState random, uint taskAddress)
    {
        int task = Offset(taskAddress);
        uint selected = BinaryPrimitives.ReadUInt32LittleEndian(arena.Read(task + 0x102C, 4));
        if (Word(arena, Offset(selected) + 6) != 0)
        {
            selected = unchecked(taskAddress + 0x42C);
            int remaining = 128;
            while (Word(arena, Offset(selected) + 6) != 0)
            {
                selected = unchecked(selected + 24);
                if (--remaining == 0)
                {
                    return new(arena, random, null);
                }
            }
        }
        uint next = selected >= unchecked(taskAddress + 0x1014) ? unchecked(taskAddress + 0x42C) : unchecked(selected + 24);
        arena = arena.Write(task + 0x102C, BitConverter.GetBytes(next));
        int target = Offset(selected);
        random = random.Draw(out ushort selector);
        arena = arena.Write(target + 0x16, [(byte)(selector & 3)]);
        random = random.Draw(out ushort angleX);
        arena = Store(arena, target + 0x10, angleX);
        random = random.Draw(out ushort angleZ);
        arena = Store(arena, target + 0x12, angleZ);
        random = random.Draw(out ushort spinX);
        arena = arena.Write(target + 0x14, [(byte)spinX]);
        random = random.Draw(out ushort spinZ);
        arena = arena.Write(target + 0x15, [(byte)spinZ]);
        NativeBattlePilotVisualLifetimes.Allocated(arena, unchecked(taskAddress + 0x42C), checked((int)((selected - taskAddress - 0x42C) / 24)));
        return new(arena, random, selected);
    }

    public static NativeBattlePandemona291TrailAllocation SpawnOpening(NativeBattleEffectArena arena, NativeCrtRandomState random, uint taskAddress, int center)
    {
        for (int index = 0; index < 4; index++)
        {
            NativeBattlePandemona291TrailAllocation allocated = Allocate6F1550(arena, random, taskAddress);
            arena = allocated.Arena;
            random = allocated.Random;
            if (allocated.Address is not uint address)
            {
                break;
            }
            int target = Offset(address);
            random = random.Draw(out ushort position);
            arena = Store(arena, target, unchecked((ushort)((position & 0xFFF) - 0x800)));
            arena = Store(arena, target + 2, 1000);
            arena = Store(arena, target + 4, unchecked((ushort)(center + 1000)));
            arena = Store(arena, target + 6, 20);
            arena = Store(arena, target + 8, 0);
            arena = Store(arena, target + 0xA, unchecked((ushort)-100));
            arena = Store(arena, target + 0xC, unchecked((ushort)-400));
            arena = Store(arena, target + 0xE, 4096);
        }
        return new(arena, random, null);
    }

    public static NativeBattleEffectArena Advance6F19C0(NativeBattleEffectArena arena, uint taskAddress)
    {
        int task = Offset(taskAddress);
        for (int index = 0; index < 128; index++)
        {
            int target = checked(task + 0x42C + index * 24);
            ushort life = Word(arena, target + 6);
            if (life == 0)
            {
                continue;
            }
            life = unchecked((ushort)(life - 1));
            arena = Store(arena, target + 6, life);
            if (life == 0)
            {
                arena = arena.Write(task + 0x102C, BitConverter.GetBytes(unchecked(NativeBattleEffectNodeMemory.ArenaAddress + (uint)target)));
                continue;
            }
            arena = Store(arena, target + 0x10, unchecked((ushort)(Word(arena, target + 0x10) + (unchecked((sbyte)arena.Read(target + 0x14, 1)[0]) << 2))));
            arena = Store(arena, target + 0x12, unchecked((ushort)(Word(arena, target + 0x12) + (unchecked((sbyte)arena.Read(target + 0x15, 1)[0]) << 2))));
            arena = Store(arena, target, unchecked((ushort)(Word(arena, target) + Word(arena, target + 8))));
            arena = Store(arena, target + 4, unchecked((ushort)(Word(arena, target + 4) + Word(arena, target + 0xC))));
            arena = Store(arena, target + 2, unchecked((ushort)(Word(arena, target + 2) + Word(arena, target + 0xA))));
            arena = Store(arena, target + 0xA, unchecked((ushort)(Word(arena, target + 0xA) - 10)));
        }
        return arena;
    }

    private static int Offset(uint address) => checked((int)(address - NativeBattleEffectNodeMemory.ArenaAddress));
    private static ushort Word(NativeBattleEffectArena arena, int offset) => BinaryPrimitives.ReadUInt16LittleEndian(arena.Read(offset, 2));
    private static NativeBattleEffectArena Store(NativeBattleEffectArena arena, int offset, ushort value) => arena.Write(offset, BitConverter.GetBytes(value));
}
