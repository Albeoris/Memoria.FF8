using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleShivaParticleBanksTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Existing_particles_draw_before_pause_and_preserve_unconsumed_unknown_suffixes(bool growing)
    {
        NativeBattleShivaEarlyTaskFrame frame = Fixture(growing, 31);
        int address = growing ? 0x2C000 : 0x2D000;
        NativeBattleEffectArena arena = Record(frame.Memory.Arena!, address, growing ? 0x200u : 0x100u, 0).MarkUnknown(address + 14, 2).MarkUnknown(address + 18, 2).MarkUnknown(address + 22, 2);
        frame = frame with { Memory = frame.Memory with { Arena = arena } };
        int draws = 0;
        NativeBattleShivaEarlyTaskStep paused = Advance(frame, growing, 0x201, (current, draw) =>
        {
            draws++;
            Assert.Equal((ushort)0, draw.Phase);
            Assert.Equal(-65, draw.Scale);
            Assert.Equal(5, draw.DepthOffset);
            Assert.Equal((ushort)8, draw.SpriteFlags);
            Assert.Equal(new NativeBattleModuleAnchor(10, 20, 30, 0), draw.Anchor);
            return current;
        });
        Assert.Equal(1, draws);
        Assert.Equal(NativeBattleTestSnapshots.Json(frame), NativeBattleTestSnapshots.Json(paused.Frame));
        NativeBattleShivaEarlyTaskStep moved = Advance(frame, growing, 0, (current, _) => current);
        NativeBattleEffectArena after = moved.Frame.Memory.Arena!;
        Assert.Equal((ushort)1, Uword(after, address + 4));
        Assert.Equal(growing ? unchecked((ushort)-67) : unchecked((ushort)-65), Uword(after, address + 6));
        Assert.Equal((ushort)5, Uword(after, address + 8));
        Assert.Equal((ushort)20, Uword(after, address + 10));
        Assert.Equal((ushort)39, Uword(after, address + 12));
        Assert.Equal(growing ? unchecked((ushort)-4) : unchecked((ushort)-3), Uword(after, address + 16));
        Assert.Equal(growing ? (ushort)8 : (ushort)7, Uword(after, address + 20));
        Assert.Throws<NotSupportedException>(() => after.Read(address + 14, 2));
        Assert.Throws<NotSupportedException>(() => after.Read(address + 18, 2));
        Assert.Throws<NotSupportedException>(() => after.Read(address + 22, 2));
        Assert.Equal(frame.Random, moved.Frame.Random);
        Assert.Equal(0, moved.Result);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Terminal_sprite_still_increments_phase_then_retires_before_motion(bool growing)
    {
        NativeBattleShivaEarlyTaskFrame frame = Fixture(growing, 31);
        int address = growing ? 0x2C000 : 0x2D000;
        frame = frame with { Memory = frame.Memory with { Arena = Record(frame.Memory.Arena!, address, growing ? 0x200u : 0x100u, 1) } };
        NativeBattleShivaEarlyTaskStep result = Advance(frame, growing, 0, (current, draw) =>
        {
            Assert.Equal((short)-1, draw.SpriteFrame!.NextOffset);
            return current;
        });
        NativeBattleEffectArena arena = result.Frame.Memory.Arena!;
        Assert.Equal(0u, Dword(arena, address));
        Assert.Equal((ushort)2, Uword(arena, address + 4));
        Assert.Equal((ushort)10, Uword(arena, address + 8));
        Assert.Equal(unchecked((ushort)-5), Uword(arena, address + 16));
        Assert.Equal(2, result.Result);
    }

    [Fact]
    public void First_bank_allocator_reaches150_despite100_scan_and_rotates_the_other_bank_in_place()
    {
        NativeBattleShivaEarlyTaskFrame frame = Fixture(false, 3);
        NativeBattleEffectArena arena = frame.Memory.Arena!;
        for (int index = 0; index < 150; index++)
        {
            arena = PutDword(arena, 0x2D000 + index * 24, index == 100 ? 0u : 1u);
        }
        int crossAddress = 0x2C000 + 100 * 24;
        arena = PutWord(PutWord(PutWord(arena, crossAddress + 8, 1), crossAddress + 10, 2), crossAddress + 12, 3);
        int spawned = 0x2D000 + 100 * 24;
        arena = arena.MarkUnknown(spawned + 14, 2).MarkUnknown(spawned + 18, 2).MarkUnknown(spawned + 22, 2);
        frame = frame with { Memory = frame.Memory with { Arena = arena } };
        NativeBattleActionMatrix world = new([0, 0, 4096, 0, 4096, 0, -4096, 0, 0], 100, 200, 300, 4096);
        NativeBattleShivaEarlyTaskFrame restored = NativeBattleTestSnapshots.RestoreJson<NativeBattleShivaEarlyTaskFrame>(NativeBattleTestSnapshots.Json(frame))!;
        NativeBattleShivaEarlyTaskStep result = NativeBattleShivaParticleBanks.Advance5C42A0(frame, 0, 0, world, null!, (_, _) => throw new InvalidOperationException("Newly spawned records cannot draw in their allocation callback."));
        NativeBattleEffectArena after = result.Frame.Memory.Arena!;
        Assert.Equal(0x100u, Dword(after, spawned));
        Assert.Equal((ushort)2089, Uword(after, spawned + 6));
        Assert.Equal(unchecked((ushort)-33), Uword(after, spawned + 8));
        Assert.Equal((ushort)166, Uword(after, spawned + 10));
        Assert.Equal((ushort)200, Uword(after, spawned + 12));
        Assert.Equal((ushort)3, Uword(after, crossAddress + 8));
        Assert.Equal((ushort)2, Uword(after, crossAddress + 10));
        Assert.Equal(ushort.MaxValue, Uword(after, crossAddress + 12));
        Assert.Equal(new NativeCrtRandomState(0x3D6C1037), result.Frame.Random);
        Assert.Throws<NotSupportedException>(() => after.Read(spawned + 18, 2));
        Assert.Equal(2, result.Result);
        NativeBattleShivaEarlyTaskStep independent = NativeBattleShivaParticleBanks.Advance5C42A0(restored, 0, 0, world, null!, null!);
        Assert.Equal(NativeBattleTestSnapshots.Json(result), NativeBattleTestSnapshots.Json(independent));
    }

    [Theory]
    [InlineData(false, 12, 0xDCFD7FA9u)]
    [InlineData(true, 6, 0x4DB793AFu)]
    public void Spawn_limits_consume_exact_random_counts_and_completion_uses_pre_spawn_survivors(bool growing, int count, uint seed)
    {
        NativeBattleShivaEarlyTaskFrame frame = Fixture(growing, 3);
        NativeBattleShivaEarlyTaskStep result = Advance(frame, growing, 0, (_, _) => throw new InvalidOperationException("Spawns must follow the draw scan."));
        int bank = growing ? 0x2C000 : 0x2D000;
        Assert.Equal(count, Enumerable.Range(0, 150).Count(index => Dword(result.Frame.Memory.Arena!, bank + index * 24) != 0));
        Assert.Equal(new NativeCrtRandomState(seed), result.Frame.Random);
        Assert.Equal(2, result.Result);
        Assert.Equal((ushort)4, result.Frame.Pool.Slots[0].ReadWord(0x0C));
    }

    private static NativeBattleShivaEarlyTaskStep Advance(NativeBattleShivaEarlyTaskFrame frame, bool growing, uint flags, Func<NativeBattleShivaEarlyTaskFrame, NativeBattleModuleDraw, NativeBattleShivaEarlyTaskFrame> draw)
    {
        return growing ? NativeBattleShivaParticleBanks.Advance5C4550(frame, 0, flags, NativeBattleCuragaMatrices.Identity8DD770(), Sprite(), draw) : NativeBattleShivaParticleBanks.Advance5C42A0(frame, 0, flags, NativeBattleCuragaMatrices.Identity8DD770(), Sprite(), draw);
    }

    private static NativeBattleShivaEarlyTaskFrame Fixture(bool growing, ushort counter)
    {
        NativeBattleEffectTaskPool pool = NativeBattleEffectTaskPool.Initialize(0x22BC198, 0x22BC1A8, 0x24, 100);
        (pool, int slot) = pool.Append(growing ? 0x5C4550u : 0x5C42A0u);
        pool = NativeBattleTaskFixture.EditPool(pool, data => data with { Slots = pool.Slots.SetItem(slot, pool.Slots[slot].WriteWord(0x0C, counter)) });
        return new(NativeBattleEffectMemory.FromColdProcess() with { Arena = NativeBattleEffectArena.Clear() }, pool, new(1), new(new(0, 0, 0, 0), new(0, 0, 0, 0)));
    }

    private static NativeBattleModuleResource Sprite()
    {
        byte[] bytes = new byte[20];
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(8), 2);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(10), 16);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(12), 16);
        BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(14), -1);
        return new(0, 0xD20AD0, 0x570CC0, 0x570BC0, bytes.ToImmutableArray());
    }

    private static NativeBattleEffectArena Record(NativeBattleEffectArena arena, int address, uint flags, ushort phase)
    {
        arena = PutDword(arena, address, flags);
        arena = PutWord(arena, address + 4, phase);
        arena = PutWord(arena, address + 6, -65);
        arena = PutWord(arena, address + 8, 10);
        arena = PutWord(arena, address + 10, 20);
        arena = PutWord(arena, address + 12, 30);
        arena = PutWord(arena, address + 16, -5);
        return PutWord(arena, address + 20, 9);
    }

    private static ushort Uword(NativeBattleEffectArena arena, int address) => BinaryPrimitives.ReadUInt16LittleEndian(arena.Read(address, 2));
    private static uint Dword(NativeBattleEffectArena arena, int address) => BinaryPrimitives.ReadUInt32LittleEndian(arena.Read(address, 4));

    private static NativeBattleEffectArena PutWord(NativeBattleEffectArena arena, int address, int value)
    {
        byte[] bytes = new byte[2];
        BinaryPrimitives.WriteUInt16LittleEndian(bytes, unchecked((ushort)value));
        return arena.Write(address, bytes);
    }

    private static NativeBattleEffectArena PutDword(NativeBattleEffectArena arena, int address, uint value)
    {
        byte[] bytes = new byte[4];
        BinaryPrimitives.WriteUInt32LittleEndian(bytes, value);
        return arena.Write(address, bytes);
    }
}
