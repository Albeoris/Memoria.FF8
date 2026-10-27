using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleShivaModelParticlesTests
{
    [Fact]
    public void Model_sprites_read_only_flag_BYTE_draw_before_pause_and_shrink_without_moving()
    {
        NativeBattleShivaEarlyTaskFrame frame = Fixture(0x5C5950, 42);
        NativeBattleEffectArena arena = Record(frame.Memory.Arena!, 1, 0, 1024).MarkUnknown(0x2C001, 3).MarkUnknown(0x2C010, 8);
        frame = frame with { Memory = frame.Memory with { Arena = arena } };
        int draws = 0;
        NativeBattleShivaEarlyTaskStep paused = NativeBattleShivaModelParticles.Advance5C5950(frame, 0, 0x201, null!, 0, Sprite(), null!, (current, draw) =>
        {
            draws++;
            Assert.Equal(new NativeBattleModuleAnchor(10, 20, 30, 0), draw.Anchor);
            Assert.Equal(1024, draw.Scale);
            Assert.Equal(-64, draw.DepthOffset);
            Assert.Equal((ushort)0, draw.SpriteFlags);
            return current;
        });
        Assert.Equal(1, draws);
        Assert.Equal(NativeBattleTestSnapshots.Json(frame), NativeBattleTestSnapshots.Json(paused.Frame));
        NativeBattleShivaEarlyTaskStep moved = NativeBattleShivaModelParticles.Advance5C5950(frame, 0, 0, null!, 0, Sprite(), null!, (current, _) => current);
        Assert.Equal((ushort)992, Word(moved.Frame.Memory.Arena!, 0x2C006));
        Assert.Equal((ushort)1, Word(moved.Frame.Memory.Arena!, 0x2C004));
        Assert.Equal((ushort)10, Word(moved.Frame.Memory.Arena!, 0x2C008));
        Assert.Throws<NotSupportedException>(() => moved.Frame.Memory.Arena!.Read(0x2C010, 2));
        NativeBattleShivaEarlyTaskStep expired = NativeBattleShivaModelParticles.Advance5C5950(moved.Frame, 0, 0, null!, 0, Sprite(), null!, (current, _) => current);
        Assert.Equal(0u, Dword(expired.Frame.Memory.Arena!, 0x2C000));
        Assert.Equal((ushort)2, Word(expired.Frame.Memory.Arena!, 0x2C004));
        Assert.Equal(2, expired.Result);
    }

    [Fact]
    public void Model_spawn_samples_the_real_bone_after_three_draws_then_rotates_and_offsets_with_three_more()
    {
        NativeBattleShivaEarlyTaskFrame frame = Fixture(0x5C5950, 3);
        NativeBattleEffectArena arena = frame.Memory.Arena!;
        for (int index = 1; index < 150; index++)
        {
            arena = PutDword(arena, 0x2C000 + index * 24, 2);
        }
        arena = arena.MarkUnknown(0x2C00E, 10);
        frame = frame with { Memory = frame.Memory with { Arena = arena } };
        NativeBattleActionMatrix world = new([0, 0, 4096, 0, 4096, 0, -4096, 0, 0], 100, 200, 300, 4096);
        int samples = 0;
        NativeBattleShivaEarlyTaskStep result = NativeBattleShivaModelParticles.Advance5C5950(frame, 0, 0, world, 7, null!, (current, bone, fraction) =>
        {
            samples++;
            Assert.Equal(6, bone);
            Assert.Equal(2083, fraction);
            Assert.Equal(new NativeCrtRandomState(0x18BE873A), current.Random);
            return new(1, 2, 3);
        }, (_, _) => throw new InvalidOperationException("Fresh particles cannot draw in their spawn callback."));
        Assert.Equal(1, samples);
        Assert.Equal(new NativeCrtRandomState(0x3D6C1037), result.Frame.Random);
        Assert.Equal((ushort)425, Word(result.Frame.Memory.Arena!, 0x2C006));
        Assert.Equal((ushort)953, Word(result.Frame.Memory.Arena!, 0x2C008));
        Assert.Equal(unchecked((ushort)-579), Word(result.Frame.Memory.Arena!, 0x2C00A));
        Assert.Equal(unchecked((ushort)-127), Word(result.Frame.Memory.Arena!, 0x2C00C));
        Assert.Throws<NotSupportedException>(() => result.Frame.Memory.Arena!.Read(0x2C010, 2));
        Assert.Equal(2, result.Result);
    }

    [Fact]
    public void Anchor_growth_updates_two_actual_bones_even_paused_and_preserves_both_anchor_W_words()
    {
        NativeBattleShivaEarlyTaskFrame frame = NativeBattleShivaModelParticles.Initialize5C5BB0(Fixture(0, 0));
        Assert.Equal((ushort)0xA5A5, frame.Pool.Slots[0].ReadWord(0x0E));
        NativeBattleActionMatrix world = new([0, 0, 4096, 0, 4096, 0, -4096, 0, 0], 100, 200, 300, 4096);
        List<int> bones = [];
        NativeBattleShivaEarlyTaskStep result = NativeBattleShivaModelParticles.Advance5C5BE0(frame, 0, 0x201, world, new(0, 0xD31A74, [0]), (_, bone, fraction) =>
        {
            bones.Add(bone);
            Assert.Equal(6144, fraction);
            return bone == 26 ? new(-1, -3, 5) : new(-2, -4, 6);
        }, (current, draw) =>
        {
            Assert.Equal(new NativeBattleModuleAnchor(105, 197, 301, 222), draw.Anchor);
            Assert.Equal(35, draw.Scale);
            Assert.Equal(-4, draw.DepthOffset);
            Assert.Null(draw.Context.Fade);
            return current;
        });
        Assert.Equal(new[] { 26, 32 }, bones);
        Assert.Equal(new NativeBattleShivaRetainedVector(-1, -3, 5, 111), result.Frame.Memory.Shiva!.LocalAnchor22BCFC8);
        Assert.Equal(new NativeBattleShivaRetainedVector(105, 197, 301, 222), result.Frame.Memory.Shiva.WorldAnchor22BCFC0);
        Assert.Equal((ushort)0, result.Frame.Pool.Slots[0].ReadWord(0x0C));
        Assert.Equal((ushort)35, result.Frame.Pool.Slots[0].ReadWord(0x1C));
        NativeBattleModulePoolTask late = result.Frame.Pool.Slots[0].WriteWord(0x0C, 55).WriteWord(0x1C, 100).WriteWord(0x22, unchecked((ushort)-9));
        NativeBattleShivaEarlyTaskFrame lateFrame = result.Frame with { Pool = NativeBattleTaskFixture.EditPool(result.Frame.Pool, data => data with { Slots = result.Frame.Pool.Slots.SetItem(0, late) }) };
        NativeBattleShivaEarlyTaskStep completed = NativeBattleShivaModelParticles.Advance5C5BE0(lateFrame, 0, 0, null!, new(0, 0xD31A74, [0]), null!, (current, draw) =>
        {
            Assert.Equal(3410, draw.Context.Fade);
            return current;
        });
        Assert.Equal((ushort)91, completed.Frame.Pool.Slots[0].ReadWord(0x1C));
        Assert.Equal(unchecked((ushort)-8), completed.Frame.Pool.Slots[0].ReadWord(0x22));
        Assert.Equal(2, completed.Result);
        Assert.Equal(result.Frame.Memory.Shiva.LocalAnchor22BCFC8, completed.Frame.Memory.Shiva!.LocalAnchor22BCFC8);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Ring_banks_use_distinct_world_and_local_anchor_matrix_paths_before_pause(bool forward)
    {
        NativeBattleShivaEarlyTaskFrame frame = Fixture(forward ? 0x5C6040u : 0x5C5D90u, forward ? (ushort)17 : (ushort)24);
        frame = frame with { Memory = frame.Memory with { Arena = Record(frame.Memory.Arena!, forward ? 4u : 2u, 6, 4096) } };
        NativeBattleActionMatrix view = new([0, -4096, 0, 4096, 0, 0, 0, 0, 4096], 1000, 2000, 3000, 4096);
        int draws = 0;
        NativeBattleShivaEarlyTaskStep result = Ring(frame, forward, 0x201, view, (current, draw) =>
        {
            draws++;
            Assert.Equal(forward ? 800 : 980, draw.Matrix.X);
            Assert.Equal(forward ? 2100 : 2010, draw.Matrix.Y);
            Assert.Equal(forward ? 3300 : 3030, draw.Matrix.Z);
            Assert.Equal(forward ? 0x33u : 0xF3u, draw.Context.Flags);
            if (forward)
            {
                Assert.Null(draw.Context.Fade);
                Assert.Equal(view.Rotation.ToArray(), draw.Matrix.Rotation.ToArray());
            }
            else
            {
                Assert.Equal(0, draw.Context.Fade);
                Assert.Equal(new short[] { 4096, 0, 0, 0, 1280, 0, 0, 0, 0 }, draw.Matrix.Rotation.ToArray());
            }
            return current;
        });
        Assert.Equal(1, draws);
        Assert.Equal(NativeBattleTestSnapshots.Json(frame), NativeBattleTestSnapshots.Json(result.Frame));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Ring_updates_use_signed_decay_and_terminal_draws_do_not_update_size_or_angles(bool forward)
    {
        NativeBattleShivaEarlyTaskFrame frame = Fixture(forward ? 0x5C6040u : 0x5C5D90u, forward ? (ushort)17 : (ushort)24);
        NativeBattleEffectArena arena = Record(frame.Memory.Arena!, forward ? 4u : 2u, forward ? (ushort)8 : (ushort)10, 1000);
        arena = PutWord(PutWord(arena, 0x2C014, 100), 0x2C016, -7);
        frame = frame with { Memory = frame.Memory with { Arena = arena } };
        NativeBattleShivaEarlyTaskStep first = Ring(frame, forward, 0, NativeBattleCuragaMatrices.Identity8DD770(), (current, _) => current);
        Assert.Equal(forward ? (ushort)1007 : (ushort)1000, Word(first.Frame.Memory.Arena!, 0x2C006));
        Assert.Equal(forward ? unchecked((ushort)-6) : unchecked((ushort)-7), Word(first.Frame.Memory.Arena!, 0x2C016));
        Assert.Equal(forward ? (ushort)100 : (ushort)93, Word(first.Frame.Memory.Arena!, 0x2C014));
        NativeBattleShivaEarlyTaskStep second = Ring(first.Frame, forward, 0, NativeBattleCuragaMatrices.Identity8DD770(), (current, _) => current);
        Assert.Equal(0u, Dword(second.Frame.Memory.Arena!, 0x2C000));
        Assert.Equal(Word(first.Frame.Memory.Arena!, 0x2C006), Word(second.Frame.Memory.Arena!, 0x2C006));
        Assert.Equal(2, second.Result);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Fresh_ring_spawn_uses_three_samples_and_no_draw_then_continues_from_independent_JSON(bool forward)
    {
        NativeBattleShivaEarlyTaskFrame frame = Fixture(forward ? 0x5C6040u : 0x5C5D90u, forward ? (ushort)13 : (ushort)11);
        NativeBattleShivaEarlyTaskStep result = Ring(frame, forward, 0, NativeBattleCuragaMatrices.Identity8DD770(), (_, _) => throw new InvalidOperationException("Fresh ring records are drawn only on the next callback."));
        Assert.Equal(new NativeCrtRandomState(0x18BE873A), result.Frame.Random);
        Assert.Equal(forward ? 4u : 2u, Dword(result.Frame.Memory.Arena!, 0x2C000));
        Assert.Equal(forward ? (ushort)6185 : (ushort)361, Word(result.Frame.Memory.Arena!, 0x2C006));
        Assert.Equal(forward ? (ushort)1237 : unchecked((ushort)-29), Word(result.Frame.Memory.Arena!, 0x2C016));
        Assert.Equal(2, result.Result);
        NativeBattleShivaEarlyTaskFrame restored = NativeBattleTestSnapshots.RestoreJson<NativeBattleShivaEarlyTaskFrame>(NativeBattleTestSnapshots.Json(result.Frame))!;
        NativeBattleShivaEarlyTaskStep first = Ring(result.Frame, forward, 0, NativeBattleCuragaMatrices.Identity8DD770(), (current, _) => current);
        NativeBattleShivaEarlyTaskStep second = Ring(restored, forward, 0, NativeBattleCuragaMatrices.Identity8DD770(), (current, _) => current);
        Assert.Equal(NativeBattleTestSnapshots.Json(first), NativeBattleTestSnapshots.Json(second));
    }

    private static NativeBattleShivaEarlyTaskStep Ring(NativeBattleShivaEarlyTaskFrame frame, bool forward, uint flags, NativeBattleActionMatrix view, Func<NativeBattleShivaEarlyTaskFrame, NativeBattleShivaModelMeshDraw, NativeBattleShivaEarlyTaskFrame> draw)
    {
        return forward ? NativeBattleShivaModelParticles.Advance5C6040(frame, 0, flags, view, new(0, 0xD309A4, [0]), draw) : NativeBattleShivaModelParticles.Advance5C5D90(frame, 0, flags, view, new(0, 0xD3048C, [0]), draw);
    }

    private static NativeBattleShivaEarlyTaskFrame Fixture(uint callback, ushort counter)
    {
        NativeBattleEffectTaskPool pool = NativeBattleEffectTaskPool.Initialize(0x22BC198, 0x22BC1A8, 0x24, 100);
        pool = NativeBattleTaskFixture.EditPool(pool, data => data with { Slots = pool.Slots.Select(task => NativeBattleTaskFixture.EditTask(task, taskData => taskData with { Payload = Enumerable.Repeat((byte)0xA5, 24).ToImmutableArray() })).ToImmutableArray() });
        if (callback != 0)
        {
            (pool, int slot) = pool.Append(callback);
            pool = NativeBattleTaskFixture.EditPool(pool, data => data with { Slots = pool.Slots.SetItem(slot, pool.Slots[slot].WriteWord(0x0C, counter)) });
        }
        NativeBattleShivaProcess process = NativeBattleShivaProcess.FromColdProcess() with { LocalAnchor22BCFC8 = new(100, 200, 300, 111), WorldAnchor22BCFC0 = new(10, 20, 30, 222) };
        return new(NativeBattleEffectMemory.FromColdProcess() with { Arena = NativeBattleEffectArena.Clear(), Shiva = process }, pool, new(1), new(new(0, 0, 0, 0), new(0, 0, 0, 0)));
    }

    private static NativeBattleModuleResource Sprite()
    {
        byte[] bytes = new byte[20];
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(8), 2);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(10), 16);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(12), 16);
        BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(14), -1);
        return new(0, 0xD208B0, 0x570CC0, 0x570BC0, bytes.ToImmutableArray());
    }

    private static NativeBattleEffectArena Record(NativeBattleEffectArena arena, uint flags, ushort phase, ushort scale)
    {
        arena = PutDword(arena, 0x2C000, flags);
        arena = PutWord(arena, 0x2C004, phase);
        arena = PutWord(arena, 0x2C006, scale);
        arena = PutWord(arena, 0x2C008, 10);
        arena = PutWord(arena, 0x2C00A, 20);
        arena = PutWord(arena, 0x2C00C, 30);
        return arena;
    }

    private static ushort Word(NativeBattleEffectArena arena, int address) => BinaryPrimitives.ReadUInt16LittleEndian(arena.Read(address, 2));
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
