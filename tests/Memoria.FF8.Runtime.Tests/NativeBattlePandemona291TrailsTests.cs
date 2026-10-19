using System.Buffers.Binary;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattlePandemona291TrailsTests
{
    private const uint TaskAddress = 0x20DFAB8;

    [Fact]
    public void Four_allocations_consume_twenty_four_CRT_draws_before_same_callback_motion()
    {
        NativeBattleEffectArena arena = NativeBattleEffectArena.Clear().Write(0x102C, BitConverter.GetBytes(TaskAddress + 0x42C));
        NativeBattlePandemona291TrailAllocation spawned = NativeBattlePandemona291Trails.SpawnOpening(arena, new(1), TaskAddress, 4000);
        Assert.Equal(0x42Cu + 4 * 24, Dword(spawned.Arena, 0x102C) - TaskAddress);
        Assert.Equal(0x00994439u, spawned.Random.Seed);
        Assert.Equal(1, spawned.Arena.Read(0x442, 1)[0]);
        Assert.Equal(18467, Word(spawned.Arena, 0x43C));
        Assert.Equal(6334, Word(spawned.Arena, 0x43E));
        NativeBattleEffectArena result = NativeBattlePandemona291Trails.Advance6F19C0(spawned.Arena, TaskAddress);
        Assert.Equal(19, Word(result, 0x432));
        Assert.Equal(1388, Word(result, 0x42C));
        Assert.Equal(900, Word(result, 0x42E));
        Assert.Equal(4600, Word(result, 0x430));
        Assert.Equal(unchecked((ushort)-110), Word(result, 0x436));
        Assert.Equal(17971, Word(result, 0x43C));
        Assert.Equal(6210, Word(result, 0x43E));
        NativeBattleEffectArena restored = JsonSerializer.Deserialize<NativeBattleEffectArena>(JsonSerializer.Serialize(spawned.Arena))!;
        Assert.Equal(JsonSerializer.Serialize(result), JsonSerializer.Serialize(NativeBattlePandemona291Trails.Advance6F19C0(restored, TaskAddress)));
    }

    [Fact]
    public void Occupied_bank_returns_without_random_draws_or_unrelated_record_reads()
    {
        NativeBattleEffectArena arena = NativeBattleEffectArena.Clear().Write(0x102C, BitConverter.GetBytes(TaskAddress + 0x42C));
        for (int index = 0; index < 128; index++)
        {
            arena = arena.Write(0x432 + index * 24, BitConverter.GetBytes((ushort)1)).MarkUnknown(0x42C + index * 24, 6);
        }
        NativeBattlePandemona291TrailAllocation result = NativeBattlePandemona291Trails.Allocate6F1550(arena, new(123), TaskAddress);
        Assert.Null(result.Address);
        Assert.Equal(123u, result.Random.Seed);
        Assert.Same(arena, result.Arena);
    }

    [Fact]
    public void Expiring_record_publishes_free_pointer_without_reading_motion_and_last_allocation_wraps()
    {
        NativeBattleEffectArena arena = NativeBattleEffectArena.Clear().Write(0x102C, BitConverter.GetBytes(TaskAddress + 0x1014));
        NativeBattlePandemona291TrailAllocation allocated = NativeBattlePandemona291Trails.Allocate6F1550(arena, new(1), TaskAddress);
        Assert.Equal(TaskAddress + 0x1014, allocated.Address);
        Assert.Equal(TaskAddress + 0x42C, Dword(allocated.Arena, 0x102C));
        arena = allocated.Arena.Write(0x101A, BitConverter.GetBytes((ushort)1)).MarkUnknown(0x1014, 6).MarkUnknown(0x101C, 16);
        NativeBattleEffectArena result = NativeBattlePandemona291Trails.Advance6F19C0(arena, TaskAddress);
        Assert.Equal(0, Word(result, 0x101A));
        Assert.Equal(TaskAddress + 0x1014, Dword(result, 0x102C));
        Assert.Throws<NotSupportedException>(() => result.Read(0x101C, 2));
    }

    private static ushort Word(NativeBattleEffectArena arena, int offset) => BinaryPrimitives.ReadUInt16LittleEndian(arena.Read(offset, 2));
    private static uint Dword(NativeBattleEffectArena arena, int offset) => BinaryPrimitives.ReadUInt32LittleEndian(arena.Read(offset, 4));
}
